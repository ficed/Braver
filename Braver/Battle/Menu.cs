// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Braver.Plugins.UI;
using Braver.UI;
using Microsoft.Xna.Framework;
using SharpDX.MediaFoundation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Battle {
    public class Menu {

        private UIBatch _ui;
        private int _item, _subItem, _column, _subTop;
        private CharacterAction _subMenu;
        private FGame _game;
        private PluginInstances _plugins;

        public ICharacterAction SelectedAction { get; private set; }
        public CharacterCombatant Combatant { get; private set; }

        public Menu(FGame game, UIBatch ui, CharacterCombatant combatant, PluginInstances plugins) {
            _game = game;
            _ui = ui;
            Combatant = combatant;
            _game.Audio.PlaySfx(Sfx.SaveReady, 1f, 0f);
            _plugins = plugins;
            AnnounceMain();
        }

        public void Step() {
            int y = 700 - Combatant.Actions.Count * 30;
            _ui.DrawBox(new Rectangle(200, y, 150, Combatant.Actions.Count * 30 + 20), 0.6f);
            y += 10;
            if (_subMenu == null) {
                _ui.DrawImage("pointer", 210, y + 30 * _item, 0.67f, Alignment.Right);
            }
            foreach (var action in Combatant.Actions) {
                _ui.DrawText("main", action.Name, 220, y, 0.65f, Color.White);
                y += 30;
            }

            if (_subMenu != null) {
                _ui.DrawBox(new Rectangle(150, 570, 300, 150), 0.7f);
                if (_subItem < _subTop)
                    _subTop = _subItem;
                else if (_subTop < (_subItem - 3))
                    _subTop = _subItem - 3;

                y = 590;
                _ui.DrawImage("pointer", 170, y + 30 * (_subItem - _subTop), 0.77f, Alignment.Right);
                foreach (var action in _subMenu.SubMenu.Skip(_subTop).Take(4)) {
                    _ui.DrawText("main", action.Name, 170, y, 0.75f, Color.White);
                    _ui.DrawText("main", action.Annotation().ToString(), 430, y, 0.75f, Color.White, Alignment.Right);
                    y += 30;
                }
            }
        }

        private void AnnounceMain() {
            _plugins.Call<UISystem>(ui => ui.Menu(
                Combatant.Actions.Select(a => a.Name),
                _item,
                this
            ));
        }

        public bool ProcessInput(InputState input) {
            void AnnounceSub() {
                _plugins.Call<UISystem>(ui => ui.Menu(
                    _subMenu.SubMenu.Select(a => a.Name),
                    _subItem,
                    _subMenu
                ));
            }

            bool blip = true;
            if (SelectedAction != null) {
                if (input.IsJustDown(InputKey.Cancel)) {
                    SelectedAction = null;
                    _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
                    return true;
                } else
                    blip = false;
            } else if (_subMenu == null) {
                if (input.IsRepeating(InputKey.Up)) {
                    _item = Math.Max(0, _item - 1);
                    AnnounceMain();
                } else if (input.IsRepeating(InputKey.Down)) {
                    _item = Math.Min(Combatant.Actions.Count - 1, _item + 1);
                    AnnounceMain();
                } else if (input.IsRepeating(InputKey.Left)) {
                    _column = Math.Max(-1, _column - 1);
                    AnnounceMain();
                } else if (input.IsRepeating(InputKey.Right)) {
                    _column = Math.Min(1, _column + 1);
                    AnnounceMain();
                } else if (input.IsJustDown(InputKey.OK)) {
                    if (Combatant.Actions[_item].SubMenu != null) {
                        _subMenu = Combatant.Actions[_item];
                        _subTop = _subItem = 0;
                        AnnounceSub();
                    } else
                        SelectedAction = Combatant.Actions[_item];
                } else
                    blip = false;
            } else {
                if (input.IsRepeating(InputKey.Up)) {
                    _subItem = Math.Max(0, _subItem - 1);
                    AnnounceSub();
                } else if (input.IsRepeating(InputKey.Down)) {
                    _subItem = Math.Min(_subMenu.SubMenu.Count - 1, _subItem + 1);
                    AnnounceSub();
                } else if (input.IsJustDown(InputKey.OK)) {
                    SelectedAction = _subMenu.SubMenu[_subItem];
                } else if (input.IsJustDown(InputKey.Cancel)) {
                    _subMenu = null;
                    _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
                    AnnounceMain();
                    blip = false;
                } else
                    blip = false;
            }

            if (blip) {
                _game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                return true;
            } else
                return false;
        }
    }
}
