// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Braver.Plugins.Field;
using Braver.Plugins.UI;
using Microsoft.Xna.Framework;
using System.Windows.Forms;

namespace Braver.Tolk {

    public class TolkConfig {
        public bool EnableSAPI { get; set; } = true;
        public bool EnableFootsteps { get; set; } = true;
    }

    public class TolkPlugin : Plugin {
        private TolkConfig _config = new();
        private BGame _game;

        public override string Name => "Tolk Text To Speech Plugin";
        public override Version Version => new Version(0, 0, 1);
        public override object ConfigObject => _config;

        public override IPluginInstance Get(string context, Type t) {
            if (t == typeof(UISystem))
                return new TolkInstance(_config);
            else if (t == typeof(IFieldLocation))
                return new FootstepPlugin(_game);
            else
                throw new NotSupportedException();
        }

        public override IEnumerable<Type> GetPluginInstances() {
            yield return typeof(UISystem);
            if (_config.EnableFootsteps)
                yield return typeof(IFieldLocation);
        }

        public override void Init(BGame game) {
            _game = game;
        }
    }

    public class TolkInstance : UISystem {

        public TolkInstance(TolkConfig config) {
            DavyKager.Tolk.TrySAPI(config.EnableSAPI);
            DavyKager.Tolk.Load();
        }

        public void ActiveScreenChanged(IScreen screen) {
            DavyKager.Tolk.Speak(screen.Description, true);
        }

        public void Choices(IEnumerable<string> choices, int selected) {
            DavyKager.Tolk.Speak(
                $"Choice {choices.ElementAtOrDefault(selected)}, {selected + 1} of {choices.Count()}",
                false
            );
        }

        public void Dialog(string dialog) {
            DavyKager.Tolk.Speak(dialog, false);
        }

        public void Menu(IEnumerable<string> items, int selected) {
            DavyKager.Tolk.Speak(
                $"Menu {items.ElementAtOrDefault(selected)}, {selected + 1} of {items.Count()}",
                false
            );
        }
    }

    public class FootstepPlugin : IFieldLocation, IDisposable {

        private IAudioItem _footsteps;
        private bool _playing = false;
        private Queue<Vector3> _positions = new();
        private Vector3 _lastPosition;

        public FootstepPlugin(BGame game) {
            _footsteps = game.Audio.LoadStream(typeof(TolkPlugin).FullName, "footsteps.ogg");
            _footsteps.Play(1f, 0f, true);
            _footsteps.Pause();
        }

        public void EntityMoved(IFieldEntity entity, bool isRunning, Vector3 from, Vector3 to) {
            if (entity.IsPlayer) {
                _lastPosition = to;
                System.Diagnostics.Debug.WriteLine($"Player moved {from}->{to} len {(from - to).Length()}");
            }
        }

        public void Step() {
            while (_positions.Count > 5)
                _positions.Dequeue();
            _positions.Enqueue(_lastPosition);
            bool shouldPlay = (_positions.First() - _positions.Last()).Length() > 13f;
            if (shouldPlay != _playing) {
                System.Diagnostics.Debug.WriteLine($"Tolk change!");
                if (shouldPlay)
                    _footsteps.Resume();
                else
                    _footsteps.Pause();
                _playing = shouldPlay;
            }
            System.Diagnostics.Debug.WriteLine($"Tolk step! {string.Join(",", _positions)}");
        }

        public void Dispose() {
            _footsteps.Dispose();
        }
    }
}