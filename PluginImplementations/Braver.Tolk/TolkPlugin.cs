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

namespace Braver.Tolk {

    public class TolkConfig {
        [ConfigProperty("Enable SAPI support")]
        public bool EnableSAPI { get; set; } = true;
        [ConfigProperty("Enable Footstep Sounds")]
        public bool EnableFootsteps { get; set; } = true;
        [ConfigProperty("Enable Focus sounds")]
        public bool EnableFocusTracking { get; set; } = true;
        [ConfigProperty("Voice dialogue")]
        public bool VoiceDialogue { get; set; } = true;
        [ConfigProperty("Movie AD")]
        public bool MovieAD { get; set; } = true;
    }

    public class TolkPlugin : Plugin {
        private TolkConfig _config = new();
        private BGame _game;

        public override string Name => "Tolk Text To Speech Plugin";
        public override Version Version => new Version(0, 0, 1);
        public override object ConfigObject => _config;

        private TolkInstance _tolk;
        public override IEnumerable<IPluginInstance> Get(string context, Type t) {
            if ((t == typeof(IUI)) || (t == typeof(ISystem)) || (t == typeof(IDialog)) || (t == typeof(IBattleUI))) {
                _tolk ??= new TolkInstance(_config);
                yield return _tolk;
            } else if (t == typeof(IFieldLocation))
                yield return new FootstepFocusPlugin(_game, _config.EnableFootsteps, _config.EnableFocusTracking);
            else if (t == typeof(IMovie))
                yield return new MovieAD(_game);
            else
                throw new NotSupportedException();
        }

        public override IEnumerable<Type> GetPluginInstances() {
            yield return typeof(ISystem);
            yield return typeof(IDialog);
            yield return typeof(IUI);
            yield return typeof(IBattleUI);
            if (_config.EnableFootsteps || _config.EnableFocusTracking)
                yield return typeof(IFieldLocation);
            if (_config.MovieAD)
                yield return typeof(IMovie);
        }

        public override void Init(BGame game) {
            _game = game;

            //TODO - this should probably go once MovieAD is split out
            string root = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "data");
            if (Directory.Exists(root)) {
                foreach(string folder in Directory.GetDirectories(root)) {
                    game.AddDataSource(Path.GetFileName(folder), new FileDataSource(folder));
                }
            }
        }
    }

    public class MovieAD : IMovie, IDisposable {
        private readonly BGame _game;
        private IAudioItem? _ad;

        public MovieAD(BGame game) {
            _game = game;
        }

        public void Dispose() {
            Stopped();
        }

        public void Loaded(string movie) {
            _ad?.Dispose();
            _ad = _game.Audio.TryLoadStream("MovieAD", Path.ChangeExtension(movie, ".ogg"));
        }

        public void Playing(int frame) {
            if ((frame == 0) && (_ad != null))
                _ad.Play(1f, 0f, false, 1f);
        }

        public void Stopped() {
            if (_ad != null) {
                _ad.Stop();
                _ad.Dispose();
                _ad = null;
            }
        }
    }

    public class TolkInstance : ISystem, IDialog, IUI, IBattleUI {

        private bool _dialog;

        public TolkInstance(TolkConfig config) {
            DavyKager.Tolk.TrySAPI(config.EnableSAPI);
            DavyKager.Tolk.Load();
            _dialog = config.VoiceDialogue;   
        }

        public void ActiveScreenChanged(IScreen screen) {
            DavyKager.Tolk.Speak(screen.Description, true);
        }

        public void ChoiceSelected(IEnumerable<string> choices, int selected) {
            DavyKager.Tolk.Speak(
                $"Choice {choices.ElementAtOrDefault(selected)}, {selected + 1} of {choices.Count()}",
                false
            );
        }

        public void Dialog(int tag, int index, string dialog) {
            if (_dialog)
                DavyKager.Tolk.Speak(dialog, false);
        }

        private object _lastMenuContainer = null;
        public void Menu(IEnumerable<string> items, int selected, object container) {
            DavyKager.Tolk.Speak(
                $"{items.ElementAtOrDefault(selected)}, {selected + 1} of {items.Count()}",
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

        /*
        public void BattleActionResult(IInProgress result) {
            string s = result.Description;
            if (!string.IsNullOrEmpty(s))
                DavyKager.Tolk.Speak(s, false);
        }
        */

        public void Showing(int window, int tag, IEnumerable<string> text) {
            //
        }

        public void Asking(int window, int tag, IEnumerable<string> text, IEnumerable<int> choiceLines) {
            //
        }

        public void ChoiceMade(int window, int choice) {
            //
        }

        public void Init(ILayoutScreen screen) {
            //
        }

        public void Reloaded() {
            //
        }

        public bool PreInput(InputState input) {
            return false;
        }
    }

    public class FootstepFocusPlugin : IFieldLocation, IDisposable {

        private IAudioItem _footsteps, _focusSound;
        private bool _playing = false;
        private Queue<Vector3> _positions = new();
        private Vector3 _lastPosition;
        private string _lastFocusName;
        private IField _field;

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

        public void Init(IField field) {
            _field = field;
        }

        public void EntityMoved(IFieldEntity entity, bool isRunning, Vector3 from, Vector3 to) {
            if (entity.IsPlayer) {
                _lastPosition = to;
                System.Diagnostics.Debug.WriteLine($"Player moved {from}->{to} len {(from - to).Length()}");
            }
        }

        private const int FOCUS_EVERY_X_FRAMES = 60;
        private int _focusCountdown = FOCUS_EVERY_X_FRAMES;
        public void Step() {

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
                var focusState = _field.GetFocusState();
                if (focusState != null) {
                    System.Diagnostics.Debug.WriteLine($"Focus at walkmesh distance {focusState.WalkmeshDistance}");
                    if (_field.Options.HasFlag(FieldOptions.PlayerControls)) {
                        float pan = 0f;
                        if (focusState.WalkmeshTriPoints.Count > 1) {
                            var prev = _field.Walkmesh[focusState.WalkmeshTriPoints[focusState.WalkmeshTriPoints.Count - 2]].GetMiddlePoint();
                            //var last = _field.Walkmesh[focusState.WalkmeshTriPoints[focusState.WalkmeshTriPoints.Count - 1]].GetMiddlePoint();
                            var last = _field.PlayerPosition;
                            var direction = _field.Transform(prev) - _field.Transform(last);
                            
                            System.Diagnostics.Debug.WriteLine(string.Join(", ", focusState.WalkmeshTriPoints));
                            System.Diagnostics.Debug.WriteLine(_field.Transform(prev));
                            System.Diagnostics.Debug.WriteLine(_field.Transform(last));

                            float sx = direction.X / 640f, sy = direction.Y / 360f;
                            pan = Math.Clamp(sx / Math.Abs(sy), -1f, +1f);
                        }
                        _focusSound.Play(1f, pan, false, 0.5f + (float)Math.Pow(0.9, focusState.WalkmeshDistance));
                    }
                    if (_lastFocusName != focusState.TargetName) {
                        _lastFocusName = focusState.TargetName;
                        DavyKager.Tolk.Output("Focus " + _lastFocusName);
                    }
                }
                _focusCountdown = FOCUS_EVERY_X_FRAMES;
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