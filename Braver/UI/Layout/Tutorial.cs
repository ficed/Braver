// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Braver.UI.Layout {
    internal class Tutorial : LayoutModel {

        public override bool ProcessInput(InputState input) {
            if (input.IsJustDown(InputKey.Cancel)) {
                _screen.FadeOut(() => _game.PopScreen(_screen));
            }
            return base.ProcessInput(input);
        }
    }
}
