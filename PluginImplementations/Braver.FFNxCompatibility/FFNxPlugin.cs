// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Braver.Plugins.Field;
using Braver.Plugins.UI;
using Microsoft.Xna.Framework;
using Tommy;

namespace Braver.FFNxCompatibility {

    public class FFNxConfig {
        [ConfigProperty("Enable Field Ambient Sounds")]
        public bool FieldAmbientSounds { get; set; } = true;
    }

    public class FFNxPlugin : Plugin {
        public override string Name => "FFNx Compatibility";
        public override Version Version => new Version(0, 0, 1);
        public override object ConfigObject => _config;

        private FFNxConfig _config = new();
        private BGame _game;

        private List<TomlTable> _ambients;

        private List<TomlTable> GetAmbients() {
            return _game.TryOpenAll("Ambient", "config.toml", s => TOML.Parse(new StreamReader(s)))
                ?.ToList()
                ?? new List<TomlTable>();
        }

        public override IEnumerable<IPluginInstance> Get(string context, Type t) {
            _ambients ??= GetAmbients();
            yield return new FFNxFieldAmbience(_game, _ambients);
        }

        public override IEnumerable<Type> GetPluginInstances() {
            yield return typeof(IFieldLocation);
        }

        public override void Init(BGame game) {
            _game = game;
        }
    }

    public class FFNxFieldAmbience : IFieldLocation, IDisposable {
        private List<TomlTable> _ambients;
        private List<IAudioItem> _audio;
        private int _playing = -1;
        private BGame _game;

        public FFNxFieldAmbience(BGame game, List<TomlTable> ambients) {
            _ambients = ambients;
            _game = game;
        }

        public void Dispose() {
            if (_playing >= 0)
                _audio[_playing].Stop();
            foreach (var item in _audio)
                item.Dispose();
        }

        public void EntityMoved(IFieldEntity entity, bool isRunning, Vector3 from, Vector3 to) {
            //
        }

        public void FocusChanged() {
            //
        }

        public void Init(int fieldID, string fieldFile) {
            IEnumerable<string> files = null;
            foreach(var table in _ambients) {
                var item = table[$"field_{fieldID}"]["sequential"];
                if (item != null)
                    files = item.AsArray.Children.Select(n => n.AsString.Value);
            }

            if (files != null) {
                _audio = files
                    .Select(fn => _game.Audio.LoadStream("Ambient", fn + ".ogg"))
                    .ToList();
            } else
                _audio = new List<IAudioItem>();
        }

        public void Step(IField field) {
            if (!_audio.Any()) return;

            if ((_playing < 0) || !_audio[_playing].IsPlaying) {
                _playing = (_playing + 1) % _audio.Count;
                _audio[_playing].Play(1f, 0f, false, 1f);
            } 
        }

        public void Suspended() {
            if (_playing >= 0)
                _audio[_playing].Stop();
        }
    }
}