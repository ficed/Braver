// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Braver.Plugins.Field;
using Braver.Plugins.UI;
using Microsoft.Xna.Framework;
using System.Diagnostics;
using Tommy;

namespace Braver.FFNxCompatibility {

    public class FFNxConfig {
        [ConfigProperty("Enable Field Ambient Sounds")]
        public bool FieldAmbientSounds { get; set; } = true;
        [ConfigProperty("Allow replacing sound effects")]
        public bool ReplaceSfx { get; set; } = true;
        [ConfigProperty("Allow voicing field dialog")]
        public bool FieldDialogVoices { get; set; } = true;
    }

    public class FFNxPlugin : Plugin {
        public override string Name => "FFNx Compatibility";
        public override Version Version => new Version(0, 0, 1);
        public override object ConfigObject => _config;

        private FFNxConfig _config = new();
        private BGame _game;

        private List<TomlTable> _ambients, _sfx;

        private List<TomlTable> GetToml(string category) {
            return _game.TryOpenAll(category, "config.toml", s => TOML.Parse(new StreamReader(s)))
                ?.ToList()
                ?? new List<TomlTable>();
        }

        public override IEnumerable<IPluginInstance> Get(string context, Type t) {
            if (t == typeof(IFieldLocation)) {
                _ambients ??= GetToml("Ambient");
                yield return new FFNxFieldAmbience(_game, _ambients);
            }
            if (t == typeof(ISfxSource)) {
                _sfx ??= GetToml("Sfx");
                yield return new FFNxSfx(_game, _sfx);
            }
            if (t == typeof(IDialog)) {
                yield return new FFNxFieldVoices(_game, context);
            }
        }

        public override IEnumerable<Type> GetPluginInstances() {
            if (_config.FieldAmbientSounds)
                yield return typeof(IFieldLocation);
            if (_config.ReplaceSfx)
                yield return typeof(ISfxSource);
            if (_config.FieldDialogVoices)
                yield return typeof(IDialog);
        }

        public override void Init(BGame game) {
            _game = game;
        }
    }

    public abstract class FFNxAudioPlugin {
        private Random _r = new();
        private Dictionary<string, int> _sequential = new Dictionary<string, int>();

        protected string GetAudioEntry(IEnumerable<TomlTable> tables, string heading) {
            foreach (var table in tables) {
                var item = table[heading]["sequential"];
                if (item != null && item.IsArray) {
                    var files = item.AsArray.Children.Select(n => n.ToString()).ToArray();
                    if (files.Any())
                        return files[_r.Next(files.Length)];
                }
                item = table[heading]["shuffle"];
                if (item != null && item.IsArray) {
                    var files = item.AsArray.Children.Select(n => n.ToString()).ToArray();
                    if (files.Any()) {
                        _sequential.TryGetValue(heading, out int s);
                        _sequential[heading] = s + 1;
                        return files[s % files.Length];
                    }
                }
            }
            return null;
        }
    }

    public class FFNxSfx : FFNxAudioPlugin, ISfxSource {

        private List<TomlTable> _sfx;
        private BGame _game;

        public FFNxSfx(BGame game, List<TomlTable> sfx) {
            _sfx = sfx;
            _game = game;
        }

        private SfxData LoadIndividual(string name) {
            using(var s = _game.Open("sfx", $"{name}.ogg")) {
                _game.Audio.DecodeStream(s, out byte[] raw, out int channels, out int frequency);
                return new SfxData {
                    RawData = raw,
                    Channels = channels,
                    Frequency = frequency
                };
            }
        }

        public SfxData Load(int sfxID) {
            var file = GetAudioEntry(_sfx, $"{sfxID + 1}");

            if (file != null) {
                Trace.WriteLine($"FFNxSfx: Remapping sfx {sfxID}->{file}");
                return LoadIndividual(file);
            } else
                return null;
        }
    }

    public class FFNxFieldVoices : IDialog, IDisposable {

        private IAudioItem _playing;
        private BGame _game;
        private string _field;

        public FFNxFieldVoices(BGame game, string field) {
            _game = game;
            _field = field;
        }

        public void Asking(int window, int tag, IEnumerable<string> text, IEnumerable<int> choiceLines) {
            //
        }

        public void ChoiceMade(int window, int choice) {
            //
        }

        public void ChoiceSelected(IEnumerable<string> choices, int selected) {
            //
        }

        public void Dialog(int tag, int index, string dialog) {
            if (_playing != null) {
                _playing.Stop();
                _playing.Dispose();
                _playing = null;
            }
            _playing = _game.Audio.TryLoadStream("Voice", $"{_field}\\{tag}.ogg")
                ?? _game.Audio.TryLoadStream("Voice", $"{_field}\\{tag}{(char)('a' + index)}.ogg");
            _playing?.Play(1f, 0f, false, 1f);
        }

        public void Dispose() {
            _playing?.Dispose();
        }

        public void Showing(int window, int tag, IEnumerable<string> text) {
            //
        }
    }

    public class FFNxFieldAmbience : FFNxAudioPlugin, IFieldLocation, IDisposable {
        private List<TomlTable> _ambients;
        private IAudioItem _audio;
        private int _playing = -1;
        private BGame _game;

        public FFNxFieldAmbience(BGame game, List<TomlTable> ambients) {
            _ambients = ambients;
            _game = game;
        }

        public void Dispose() {
            if (_audio != null) {
                _audio.Stop();
                _audio.Dispose();
            }
        }

        public void EntityMoved(IFieldEntity entity, bool isRunning, Vector3 from, Vector3 to) {
            //
        }

        public void FocusChanged() {
            //
        }

        public void Init(IField field) {
            var file = GetAudioEntry(_ambients, $"field_{field.FieldID}");

            if (file != null) {
                _audio = _game.Audio.LoadStream("Ambient", file + ".ogg");
            } 
        }

        public void Step() {
            if (_audio == null) return;

            if (!_audio.IsPlaying) {
                _audio.Play(1f, 0f, false, 1f);
            } 
        }

        public void Suspended() {
            if (_playing >= 0)
                _audio.Stop();
        }
    }
}