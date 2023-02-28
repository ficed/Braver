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
    internal class GameOver : LayoutModel {

        protected override void OnInit() {
            base.OnInit();
            Game.Audio.PlayMusic("over2");
            _screen.FadeIn(null, 90);
        }
    }
}
