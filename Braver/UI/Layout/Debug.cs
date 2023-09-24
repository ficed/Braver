// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Battle;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.UI.Layout {
    public class Debug : LayoutModel {

        public Label lNoFieldScripts, lNoRandomBattles, lSkipBattleMenu, lAutoSaveOnFieldEntry,
            lSeparateSaveFiles;
        public Box Root;

        protected override void OnInit() {
            base.OnInit();
            Update();
            PushFocus(Root, lNoFieldScripts);
        }

        private void Update() {

            void DoLabel(Label L, bool option) {
                L.Color = option ? Color.White : Color.Gray;
                L.FocusDescription = L.Text + " " + (option ? "On" : "Off");
            }

            DoLabel(lNoFieldScripts, Game.GameOptions.NoFieldScripts);
            DoLabel(lNoRandomBattles, Game.GameOptions.NoRandomBattles);
            DoLabel(lSkipBattleMenu, Game.GameOptions.SkipBattleMenu);
            DoLabel(lSeparateSaveFiles, Game.GameOptions.SeparateSaveFiles);

            lAutoSaveOnFieldEntry.Text = $"Auto Save on Field Entry: {Game.GameOptions.AutoSaveOnFieldEntry}";
        }

        public void LabelClick(Label L) {
            if (L == lNoFieldScripts)
                Game.GameOptions.NoFieldScripts = !Game.GameOptions.NoFieldScripts;
            else if (L == lNoRandomBattles)
                Game.GameOptions.NoRandomBattles = !Game.GameOptions.NoRandomBattles;
            else if (L == lSkipBattleMenu)
                Game.GameOptions.SkipBattleMenu = !Game.GameOptions.SkipBattleMenu;
            else if (L == lAutoSaveOnFieldEntry) {
                int maxValue = Enum.GetValues<FieldAutoSaveType>()
                    .Select(e => (int)e)
                    .Max();
                Game.GameOptions.AutoSaveOnFieldEntry = (FieldAutoSaveType)(((int)Game.GameOptions.AutoSaveOnFieldEntry + 1) % (maxValue + 1));
            } else if (L == lSeparateSaveFiles)
                Game.GameOptions.SeparateSaveFiles = !Game.GameOptions.SeparateSaveFiles;

            Update();
            ChangeFocus(Focus); //to re-announce new state
        }

        public override bool ProcessInput(InputState input) {
            
            if (input.IsJustDown(InputKey.Cancel)) {
                Game.PopScreen(_screen);
                return true;
            }

            return false;
        }
    }
}
