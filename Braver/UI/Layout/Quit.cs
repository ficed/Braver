// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {
    internal class Quit : LayoutModel {

        public Box Root;
        public Label bYes, bNo;

        protected override void OnInit() {
            base.OnInit();
            PushFocus(Root, bYes);
        }

        public void Yes_Click() {
            InputEnabled = false;
            _screen.FadeOut(() => Environment.Exit(0));
        }

        public void No_Click() {
            _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
            _game.PopScreen(_screen);
        }

        public override bool ProcessInput(InputState input) {
            if (input.IsJustDown(InputKey.Cancel)) {
                No_Click();
                return true;
            }

            return base.ProcessInput(input);
        }
    }
}
