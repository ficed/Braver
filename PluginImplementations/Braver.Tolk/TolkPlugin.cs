// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Battle;
using Braver.Field;
using Braver.Plugins;
using Braver.Plugins.Field;
using Braver.Plugins.UI;
using Microsoft.Xna.Framework;
using System;
using System.Windows.Forms;

namespace Braver.Tolk {

    public class TolkConfig {
        public bool EnableSAPI { get; set; } = true;
        public bool EnableFootsteps { get; set; } = true;
        public bool EnableFocusTracking { get; set; } = true;
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
                return new FootstepFocusPlugin(_game, _config.EnableFootsteps, _config.EnableFocusTracking);
            else
                throw new NotSupportedException();
        }

        public override IEnumerable<Type> GetPluginInstances() {
            yield return typeof(UISystem);
            if (_config.EnableFootsteps || _config.EnableFocusTracking)
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

        private object _lastMenuContainer = null;
        public void Menu(IEnumerable<string> items, int selected, object container) {
            DavyKager.Tolk.Speak(
                $"Menu {items.ElementAtOrDefault(selected)}, {selected + 1} of {items.Count()}",
                _lastMenuContainer == container
            );
            _lastMenuContainer = container;
        }

        public void BattleCharacterReady(ICombatant character) {
            DavyKager.Tolk.Speak($"{character.Name} ready", false);
        }

        public void BattleTargetHighlighted(IEnumerable<ICombatant> targets) {
            if (targets != null)
                DavyKager.Tolk.Speak($"Targetting {string.Join(", ", targets.Select(c => c.Name))}", true);
        }

        public void BattleActionStarted(string action) {
            DavyKager.Tolk.Speak(action, false);
        }

        public void BattleActionResult(IInProgress result) {
            string s = result.Description;
            if (!string.IsNullOrEmpty(s))
                DavyKager.Tolk.Speak(s, false);
        }
    }

    public class FootstepFocusPlugin : IFieldLocation, IDisposable {

        private IAudioItem _footsteps, _focusSound;
        private bool _playing = false;
        private Queue<Vector3> _positions = new();
        private Vector3 _lastPosition;
        private string _lastFocusName;

        public FootstepFocusPlugin(BGame game, bool footsteps, bool focus) {
            if (footsteps) {
                _footsteps = game.Audio.LoadStream(typeof(TolkPlugin).FullName, "footsteps.ogg");
                _footsteps.Play(1f, 0f, true, 1f);
                _footsteps.Pause();
            }
            if (focus) {
                _focusSound = game.Audio.LoadStream(typeof(TolkPlugin).FullName, "focus.ogg");
            }
        }

        public void EntityMoved(IFieldEntity entity, bool isRunning, Vector3 from, Vector3 to) {
            if (entity.IsPlayer) {
                _lastPosition = to;
                System.Diagnostics.Debug.WriteLine($"Player moved {from}->{to} len {(from - to).Length()}");
            }
        }

        private int _focusCountdown=180;
        public void Step(IField field) {

            if (_footsteps != null) {
                while (_positions.Count > 5)
                    _positions.Dequeue();
                _positions.Enqueue(_lastPosition);
                bool shouldPlay = (_positions.First() - _positions.Last()).Length() > 13f;
                if (shouldPlay != _playing) {
                    //System.Diagnostics.Debug.WriteLine($"Tolk change!");
                    if (shouldPlay)
                        _footsteps.Resume();
                    else
                        _footsteps.Pause();
                    _playing = shouldPlay;
                }
                //System.Diagnostics.Debug.WriteLine($"Tolk step! {string.Join(",", _positions)}");
            }

            if ((_focusSound != null) && (--_focusCountdown == 0)) {
                var focusState = field.GetFocusState();
                if (focusState != null) {
                    System.Diagnostics.Debug.WriteLine($"Focus at walkmesh distance {focusState.WalkmeshDistance}");
                    if (field.Options.HasFlag(FieldOptions.PlayerControls))
                        _focusSound.Play(1f, 0f, false, (float)Math.Pow(0.9, focusState.WalkmeshDistance));

                    if (_lastFocusName != focusState.TargetName) {
                        _lastFocusName = focusState.TargetName;
                        DavyKager.Tolk.Output("Focus " + _lastFocusName);
                    }
                }
                _focusCountdown = 180;
            }
        }

        public void Dispose() {
            _footsteps?.Dispose();
        }

        public void FocusChanged() {
            _focusCountdown = 1;
        }

        public void Suspended() {
            if (_playing) {
                _footsteps.Pause(); 
                _playing = false;
            }
        }
    }
}