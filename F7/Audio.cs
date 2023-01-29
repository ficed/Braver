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
        }
        private class MusicCommand {
            public CommandType Command { get; set; }
            public string Track { get; set; }
        }

        private class MusicContext {
            public NAudio.Vorbis.VorbisWaveReader Vorbis { get; set; }
            public NAudio.Wave.WaveOut WaveOut { get; set; }
            public string Track { get; set; }
        }

        private async Task RunMusic() {
            var contexts = new Stack<MusicContext>();
            contexts.Push(new MusicContext());

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
                current.Vorbis = new NAudio.Vorbis.VorbisWaveReader(System.IO.Path.Combine(_ff7Dir, "music_ogg", track + ".ogg"));
                current.WaveOut = new NAudio.Wave.WaveOut();
                current.WaveOut.Init(current.Vorbis);
                current.WaveOut.Play();
                current.Track = track;
            }

            while (true) {
                var command = await _channel.Reader.ReadAsync();
                if (command == null) break;

                switch (command.Command) {
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
                        DoPlay(command.Track);
                        break;
                }

            }
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

        private Dictionary<int, WeakReference<SoundEffect>> _sfx = new();
        private HashSet<SoundEffect> _recent0 = new(), _pinned = new(), _recent1;
        private DateTime _lastPromote = DateTime.MinValue;

        public void Precache(Sfx which, bool pin) {
            byte[] raw = _sfxSource.ExportPCM((int)which, out int freq, out int channels);
            var fx = new SoundEffect(raw, freq, channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
            _sfx[(int)which] = new WeakReference<SoundEffect>(fx);

            if (pin)
                _pinned.Add(fx);
        }

        public void PlaySfx(Sfx which, float volume, float pan) => PlaySfx((int)which, volume, pan);
        public void PlaySfx(int which, float volume, float pan) {
            SoundEffect fx;

            if (_sfx.TryGetValue(which, out var wr) && wr.TryGetTarget(out fx)) {
                //
            } else {
                byte[] raw = _sfxSource.ExportPCM(which, out int freq, out int channels);
                fx = new SoundEffect(raw, freq, channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
                _sfx[which] = new WeakReference<SoundEffect>(fx);
            }

            fx.Play(volume, 0, pan);
            if (_lastPromote < DateTime.Now.AddMinutes(-1)) {
                _recent1 = _recent0;
                _recent0 = new();
            }
            _recent0.Add(fx);
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
