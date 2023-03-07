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
            lNoFieldScripts.Color = Game.DebugOptions.NoFieldScripts ? Color.White : Color.Gray;
            lNoRandomBattles.Color = Game.DebugOptions.NoRandomBattles ? Color.White : Color.Gray;
            lSkipBattleMenu.Color = Game.DebugOptions.SkipBattleMenu ? Color.White : Color.Gray;
            lAutoSaveOnFieldEntry.Color = Game.DebugOptions.AutoSaveOnFieldEntry ? Color.White : Color.Gray;
            lSeparateSaveFiles.Color = Game.DebugOptions.SeparateSaveFiles ? Color.White : Color.Gray;
        }

        public void LabelClick(Label L) {
            if (L == lNoFieldScripts)
                Game.DebugOptions.NoFieldScripts = !Game.DebugOptions.NoFieldScripts;
            else if (L == lNoRandomBattles)
                Game.DebugOptions.NoRandomBattles = !Game.DebugOptions.NoRandomBattles;
            else if (L == lSkipBattleMenu)
                Game.DebugOptions.SkipBattleMenu = !Game.DebugOptions.SkipBattleMenu;
            else if (L == lAutoSaveOnFieldEntry)
                Game.DebugOptions.AutoSaveOnFieldEntry = !Game.DebugOptions.AutoSaveOnFieldEntry;
            else if (L == lSeparateSaveFiles)
                Game.DebugOptions.SeparateSaveFiles = !Game.DebugOptions.SeparateSaveFiles;

            Update();
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
