using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Braver {
    public class Audio {

        private string _ff7Dir;
        private Channel<string> _channel;
        private Ficedula.FF7.Audio _sfxSource;

        public Audio(string ff7dir) {
            _ff7Dir = ff7dir;
            _channel = Channel.CreateBounded<string>(8);
            Task.Run(RunMusic);
            _sfxSource = new Ficedula.FF7.Audio(
                System.IO.Path.Combine(ff7dir, "sound", "audio.dat"),
                System.IO.Path.Combine(ff7dir, "sound", "audio.fmt")
            );
        }

        private async Task RunMusic() {
            NAudio.Vorbis.VorbisWaveReader vorbis = null;
            NAudio.Wave.WaveOut waveOut = null;

            void DoStop() {
                if (vorbis != null) {
                    waveOut.Dispose();
                    vorbis.Dispose();
                    waveOut = null;
                    vorbis = null;
                }
            }

            string current = string.Empty;
            while (true) {
                string file = await _channel.Reader.ReadAsync();
                if (current != file) {
                    switch (file) {
                        case null:
                            return;
                        case "":
                            DoStop();
                            break;
                        default:
                            DoStop();
                            vorbis = new NAudio.Vorbis.VorbisWaveReader(System.IO.Path.Combine(_ff7Dir, "music_ogg", file + ".ogg"));
                            waveOut = new NAudio.Wave.WaveOut();
                            waveOut.Init(vorbis);
                            waveOut.Play();
                            break;
                    }
                }
                current = file;
            }
        }

        public void PlayMusic(string name) {
            _channel.Writer.TryWrite(name);
        }
        public void StopMusic() {
            _channel.Writer.TryWrite(string.Empty);
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
        DeEquip = 446,
    }
}
