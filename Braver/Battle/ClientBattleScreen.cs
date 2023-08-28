// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Net;
using Braver.Plugins.UI;
using Braver.Plugins;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpDX.X3DAudio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Braver.UI;
using Ficedula.FF7;

namespace Braver.Battle {
    public class ClientBattleScreen : Screen, Net.IListen<AddBattleModelMessage>, 
        Net.IListen<SetBattleCameraMessage>, Net.IListen<CharacterReadyMessage>,
        Net.IListen<TargetOptionsMessage> {
        public override Color ClearColor => Color.Black;
        public override string Description => "";

        private BattleRenderer<int> _renderer;
        private int _formationID;

        private UI.UIBatch _menuUI;
        private Menu<CharacterReadyMessage> _activeMenu;
        private PluginInstances<IBattleUI> _plugins;

        private TargetOptionsMessage _targets;
        private TargetOption _currentTargets;
        private ICharacterAction _targetsFor;

        public ClientBattleScreen(int formationID) {
            _formationID = formationID;
        }

        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);

            var scene = g.Singleton(() => new BattleSceneCache(g)).Scenes[_formationID];

            var ui = new UI.Layout.ClientScreen {
                UpdateInBackground = true,
            };
            ui.Init(g, graphics);
            _renderer = new BattleRenderer<int>(g, graphics, ui);
            _renderer.LoadBackground(scene.LocationID);
            g.Net.Listen<Net.AddBattleModelMessage>(this);
            g.Net.Listen<Net.SetBattleCameraMessage>(this);
            g.Net.Listen<Net.CharacterReadyMessage>(this);
            g.Net.Listen<Net.TargetOptionsMessage>(this);

            _plugins = GetPlugins<IBattleUI>(_formationID.ToString());

            _menuUI = new UI.UIBatch(Graphics, Game);
        }

        protected override void DoRender() {
            _renderer.Render();
            _menuUI.Render();
        }

        protected override void DoStep(GameTime elapsed) {
            _renderer.Step(elapsed);

            _menuUI.Reset();
            _activeMenu?.Step();

            if (_targetsFor != _activeMenu?.SelectedAction) {
                if (_targetsFor == null) { //we've just selected an action - request targets
                    _targetsFor = _activeMenu.SelectedAction;
                    Game.Net.Send(new GetTargetOptionsMessage {
                        Ability = _activeMenu.SelectedAction.Ability.Value, //TODO - OK?
                        TargettingFlags = _activeMenu.SelectedAction.TargetFlags,
                        SourceCharIndex = _activeMenu.Combatant.CharIndex,
                    });
                } else {
                    //we've cancelled our selection of an ability
                    _targetsFor = null;
                    _targets = null;
                    _currentTargets = null;
                }
            }

            if (_currentTargets != null) {
                //TODO this is mostly copied from BattleScreen - find a way to consolidate it
                IEnumerable<int> targets;
                if (_currentTargets.SingleTarget != null)
                    targets = Enumerable.Repeat(_currentTargets.SingleTarget.Value, 1);
                else if (_activeMenu.SelectedAction.TargetFlags.HasFlag(TargettingFlags.RandomTarget)) {
                    long index = ((long)elapsed.TotalGameTime.TotalMilliseconds / 100) % _currentTargets.TargetIDs.Count;
                    targets = Enumerable.Repeat(_currentTargets.TargetIDs[(int)index], 1);
                } else
                    targets = _currentTargets.TargetIDs;

                foreach (var target in targets) {
                    var screenPos = GetModelScreenPos(target);
                    //TODO clamp to screen, presumably
                    _menuUI.DrawImage("pointer", (int)screenPos.X, (int)screenPos.Y, 0.99f, Alignment.Right);
                }
            }

        }

        private Vector2 GetModelScreenPos(int modelID) {
            var model = _renderer.Models[modelID];
            var middle = (model.MaxBounds + model.MinBounds) * 0.5f;
            var screenPos = _renderer.View3D.ProjectTo2D(model.Translation + middle);
            return screenPos.XY();
        }

        public void Received(AddBattleModelMessage message) {
            var model = Model.LoadBattleModel(Graphics, Game, message.Code);
            model.Translation = message.Position;
            model.Scale = 1;
            if (message.Position.Z < 0)
                model.Rotation = new Vector3(0, 180, 0);
            _renderer.Models.Add(message.ID, model);
        }

        public void Received(SetBattleCameraMessage message) {
            _renderer.SetCamera(message.Camera);
        }

        public void Received(CharacterReadyMessage message) {
            if (_activeMenu == null) {
                _activeMenu = new Menu<CharacterReadyMessage>(
                    Game, _menuUI, message, _plugins
                );
            }
        }

        private void TargetsChanged() {
            //TODO - plugins - we don't have ICombatants on the client
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);

            if (input.IsJustDown(InputKey.Menu)) {
                Game.Net.Send(new CycleBattleMenuMessage { CurrentCharIndex = _activeMenu?.Combatant?.CharIndex ?? -1 });
            } else if (_currentTargets != null) {
                //TODO this is mostly copied from BattleScreen - find a way to consolidate it
                bool blip = false;
                if (!_currentTargets.MustTargetWholeGroup) {
                    if (input.IsRepeating(InputKey.Up)) {
                        _currentTargets.SingleTarget = _currentTargets.TargetIDs[(_currentTargets.TargetIDs.IndexOf(_currentTargets.SingleTarget.Value) + _currentTargets.TargetIDs.Count - 1) % _currentTargets.TargetIDs.Count];
                        blip = true;
                        TargetsChanged();
                    } else if (input.IsRepeating(InputKey.Down)) {
                        _currentTargets.SingleTarget = _currentTargets.TargetIDs[(_currentTargets.TargetIDs.IndexOf(_currentTargets.SingleTarget.Value) + 1) % _currentTargets.TargetIDs.Count];
                        blip = true;
                        TargetsChanged();
                    }
                }

                if (_activeMenu.SelectedAction.TargetFlags.HasFlag(TargettingFlags.ToggleMultiSingleTarget) && input.IsJustDown(InputKey.Select)) {
                    _currentTargets.MustTargetWholeGroup = !_currentTargets.MustTargetWholeGroup;
                    blip = true;
                    if (_currentTargets.MustTargetWholeGroup)
                        _currentTargets.SingleTarget = null;
                    else
                        _currentTargets.SingleTarget = _currentTargets.DefaultSingleTarget;
                    TargetsChanged();
                }

                int groupShift = 0;
                if (input.IsRepeating(InputKey.Left))
                    groupShift = -1;
                else if (input.IsRepeating(InputKey.Right))
                    groupShift = 1;

                if (groupShift != 0) {
                    int current = _targets.Options.IndexOf(_currentTargets);
                    int newIndex = current + groupShift;
                    if ((newIndex >= 0) && (newIndex < _targets.Options.Count)) {
                        _currentTargets = _targets.Options[newIndex];
                        System.Diagnostics.Trace.WriteLine($"Now targetting {_currentTargets}");
                        blip = true;
                    }
                }

                if (input.IsJustDown(InputKey.OK)) {
                    Game.Net.Send(new QueueActionMessage {
                        SourceCharIndex = _activeMenu.Combatant.CharIndex,
                        Ability = _activeMenu.SelectedAction.Ability.Value,
                        Name = _activeMenu.SelectedAction.Name,
                        TargetIDs = _currentTargets.TargetIDs
                    });
                    _targets = null;
                    _currentTargets = null;
                    _targetsFor = null;
                    _activeMenu = null;
                    Game.Net.Send(new CycleBattleMenuMessage { CurrentCharIndex = -1 });
                } else if (input.IsJustDown(InputKey.Cancel))
                    _activeMenu.ProcessInput(input);

                if (blip)
                    Game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
            } else {                
                _activeMenu?.ProcessInput(input);
            }
        }

        public void Received(TargetOptionsMessage message) {
            if (message.Ability.Equals(_activeMenu?.SelectedAction?.Ability)) {
                _targets = message;
                _currentTargets = _targets.Options.Single(opt => opt.IsDefault);
            }
        }
    }
}
