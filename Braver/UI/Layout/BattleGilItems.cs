// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Battle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {

    public class BattleGilItems : LayoutModel {
        //TODO - animate numbers, display messages, ...

        public override bool IsRazorModel => true;

        public int GainedGil { get; set; }
        public List<InventoryItem> GainedItems { get; private set; }

        public override void Created(FGame g, LayoutScreen screen) {
            base.Created(g, screen);
            var results = (BattleResults)screen.Param;
            GainedGil = results.Gil;
            GainedItems = results.Items;
        }

        public override bool ProcessInput(InputState input) {

            if (input.IsJustDown(InputKey.OK)) {
                Game.PopScreen(_screen);
                Game.Audio.StopMusic(true);
                return true;
            }

            return false;
        }

    }
}
