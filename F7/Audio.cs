using Braver.UI.Layout;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Braver {
    public class Audio {

        private string _ff7Dir;
        private Channel<MusicCommand> _channel;
        private Ficedula.FF7.Audio _sfxSource;
        private FGame _game;

        public Audio(FGame game, string ff7dir) {
            _game = game;
            _ff7Dir = ff7dir;
            _channel = Channel.CreateBounded<MusicCommand>(8);
            Task.Run(RunMusic);
            _sfxSource = new Ficedula.FF7.Audio(
                System.IO.Path.Combine(ff7dir, "sound", "audio.dat"),
                System.IO.Path.Combine(ff7dir, "sound", "audio.fmt")
            );
        }

        private enum CommandType {
            Play,
            Stop,
            Push,
            Pop,
            SetVolume,
        }
        private class MusicCommand {
            public CommandType Command { get; set; }
            public string Track { get; set; }
            public byte Param { get; set; }
        }

        private class MusicContext {
            public NAudio.Vorbis.VorbisWaveReader Vorbis { get; set; }
            public NAudio.Wave.WaveOut WaveOut { get; set; }
            public string Track { get; set; }
        }

        private class LoopProvider : ISampleProvider {

            private NAudio.Vorbis.VorbisWaveReader _source;
            private int _loopStart, _loopEnd;

            public WaveFormat WaveFormat => _source.WaveFormat;

            private long _samplesRead;
            private long _seekBeforeLoopStart, _samplesBeforeLoopStart;

            public LoopProvider(NAudio.Vorbis.VorbisWaveReader source, int loopStart, int loopEnd) {
                _source = source;
                _loopStart = loopStart * source.WaveFormat.Channels;
                _loopEnd = loopEnd * source.WaveFormat.Channels;
            }

            private float[] _discardBuffer = new float[4096];

            public int Read(float[] buffer, int offset, int count) {

                int read = _source.Read(buffer, offset, count);
                _samplesRead += read;
                if ((_samplesRead <= _loopStart) && (_samplesRead > _seekBeforeLoopStart)) {
                    _samplesBeforeLoopStart = _samplesRead;
                    _seekBeforeLoopStart = _source.Position;
                } else if (_samplesRead >= _loopEnd) {
                    read -= (int)(_samplesRead - _loopEnd);
                    _samplesRead = _samplesBeforeLoopStart;
                    _source.Position = _seekBeforeLoopStart;
                    int toDiscard = (int)(_loopStart - _samplesBeforeLoopStart);
                    while (toDiscard > 0) {
                        int discard = _source.Read(_discardBuffer, 0, Math.Min(_discardBuffer.Length, toDiscard));
                        toDiscard -= discard;
                        _samplesRead += discard;
                    }
                    if (read == 0) { //Don't want to seem like end of stream, so must return some data now we've looped back!
                        read = _source.Read(buffer, offset, count);
                    }
                }

                if (read == 0)
                    System.Diagnostics.Debugger.Break();
                return read;
            }
        }

        public void StopLoopingSfx(bool includeChannels) {
            foreach (var loop in _activeLoops) {                
                loop.Stop();
                loop.Dispose();
            }

            if (includeChannels) {
                foreach (var chInstance in _channels.Values) {
                    chInstance.Stop();
                    chInstance.Dispose(); //TODO - reasonable or not?!
                }
            }

            _activeLoops.Clear();
            _channels.Clear();
        }

        public void Update() {
            var needsRestart = _activeLoops.Where(instance => instance.State != SoundState.Playing);
            foreach (var loop in needsRestart)
                loop.Play();
        }

        private async Task RunMusic() {
            var contexts = new Stack<MusicContext>();
            contexts.Push(new MusicContext());
            byte volume = 127;

            void DoStop() {
                var context = contexts.Peek();
                if (context.Vorbis != null) {
                    context.WaveOut.Dispose();
                    context.Vorbis.Dispose();
                    context.WaveOut = null;
                    context.Vorbis = null;
                }
            }
            void DoPlay(string track) {
                var current = contexts.Peek();
                DoStop();
                string file = System.IO.Path.Combine(_ff7Dir, "music_ogg", track + ".ogg");
                int loopStart, loopEnd;
                using (var reader = new NVorbis.VorbisReader(file)) {
                    loopStart = int.Parse(reader.Tags.GetTagSingle("LOOPSTART"));
                    loopEnd = int.Parse(reader.Tags.GetTagSingle("LOOPEND"));
                }                    
                current.Vorbis = new NAudio.Vorbis.VorbisWaveReader(file);
                current.WaveOut = new NAudio.Wave.WaveOut();
                //current.WaveOut.Init(current.Vorbis);
                current.WaveOut.Init(new LoopProvider(current.Vorbis, loopStart, loopEnd).ToWaveProvider());
                current.WaveOut.Volume = volume / 127f;
                current.WaveOut.Play();
                current.Track = track;
            }

            while (true) {
                var command = await _channel.Reader.ReadAsync();
                if (command == null) break;

                switch (command.Command) {
                    case CommandType.SetVolume:
                        volume = command.Param;
                        if (contexts.Peek().WaveOut != null)
                            contexts.Peek().WaveOut.Volume = volume / 127f;
                        break;

                    case CommandType.Play:
                        if (command.Track != contexts.Peek().Track)
                            DoPlay(command.Track);
                        break;

                    case CommandType.Stop:
                        DoStop();
                        break;

                    case CommandType.Pop:
                        DoStop();
                        contexts.Pop();
                        if (contexts.Peek().Vorbis != null)
                            contexts.Peek().WaveOut.Resume();
                        break;

                    case CommandType.Push:
                        if (contexts.Peek().Vorbis != null)
                            contexts.Peek().WaveOut.Pause();
                        contexts.Push(new MusicContext());
                        DoPlay(command.Track);
                        break;
                }

            }
        }

        public void SetMusicVolume(byte volume) {
            _channel.Writer.TryWrite(new MusicCommand {
                Command = CommandType.SetVolume,
                Param = volume,
            });
            //TODO net
        }
        public void PlayMusic(string name, bool pushContext = false) {
            _channel.Writer.TryWrite(new MusicCommand {
                Track = name,
                Command = pushContext ? CommandType.Push : CommandType.Play,
            });
            _game.Net.Send(new Net.MusicMessage { Track = name, IsPush = pushContext });
        }
        public void StopMusic(bool popContext = false) {
            _channel.Writer.TryWrite(new MusicCommand {
                Command = popContext ? CommandType.Pop : CommandType.Stop
            });
            _game.Net.Send(new Net.MusicMessage { Track = string.Empty, IsPop = popContext });
        }

        private class LoadedEffect {
            public SoundEffect Effect { get; set; }
            public bool ShouldLoop { get; set; }
        }

        private Dictionary<int, WeakReference<LoadedEffect>> _sfx = new();
        private HashSet<LoadedEffect> _recent0 = new(), _pinned = new(), _recent1;
        private HashSet<SoundEffectInstance> _activeLoops = new();
        private DateTime _lastPromote = DateTime.MinValue;
        private Dictionary<int, SoundEffectInstance> _channels = new();

        private LoadedEffect GetEffect(int which) {
            byte[] raw = _sfxSource.ExportPCM(which, out int freq, out int channels);
            var fx = new SoundEffect(raw, freq, channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
            return new LoadedEffect {
                Effect = fx,
                ShouldLoop = _sfxSource.GetExtraData(which)[0] != 0, //TODO - seems like it *might* be right?!
            };
        }

        public void Precache(Sfx which, bool pin) {
            var effect = GetEffect((int)which);
            _sfx[(int)which] = new WeakReference<LoadedEffect>(effect);

            if (pin)
                _pinned.Add(effect);
        }

        public void StopChannel(int channel) {
            if (_channels.TryGetValue(channel, out var instance)) {
                instance.Stop();
                instance.Dispose();
                _channels.Remove(channel);
            }
        }

        public void PlaySfx(Sfx which, float volume, float pan, int? channel = null) => PlaySfx((int)which, volume, pan, channel);
        public void PlaySfx(int which, float volume, float pan, int? channel = null) {
            LoadedEffect effect;

            if (_sfx.TryGetValue(which, out var wr) && wr.TryGetTarget(out effect)) {
                //
            } else {
                effect = GetEffect(which);
                _sfx[which] = new WeakReference<LoadedEffect>(effect);
            }

            if (effect.ShouldLoop) {
                var instance = effect.Effect.CreateInstance();
                instance.Pan = pan;
                instance.Volume = volume;
                instance.Play();
                _activeLoops.Add(instance);
                if (channel != null)
                    _channels[channel.Value] = instance;
            } else if (channel != null) {
                var instance = effect.Effect.CreateInstance();
                instance.Pan = pan;
                instance.Volume = volume;
                instance.Play();
                _channels[channel.Value] = instance;
            } else {
                effect.Effect.Play(volume, 0, pan);
            }

            if (_lastPromote < DateTime.Now.AddMinutes(-1)) {
                _recent1 = _recent0;
                _recent0 = new();
            }
            _recent0.Add(effect);
            _game.Net.Send(new Net.SfxMessage { Which = which, Volume = volume, Pan = pan });
        }

        public void Quit() {
            _channel.Writer.TryWrite(null);
        }
    }

    public enum Sfx {
        Cursor = 0,
        SaveReady = 1,
        Invalid = 2,
        Cancel = 3,
        EnemyDeath = 21,
        BattleSwirl = 42,
        DeEquip = 446,
    }
}
