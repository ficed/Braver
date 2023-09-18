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
using Terminal.Gui;

namespace CrossSlash {
    public class SplashWindow : Window {
        public SplashWindow() {

            Title = "CrossSlash FF7 exporter";

            Button btnField = new Button {
                Width = Dim.Fill(2),
                Text = "Field Model Export",
                Y = Pos.At(4),
            };
            btnField.Clicked += () => {
                Application.RequestStop();
                Application.Run<FieldExportGuiWindow>();
            };

            Button btnBattle = new Button {
                Width = Dim.Fill(2),
                Text = "Battle Model Export",
                Y = Pos.Bottom(btnField) + 2,
            };
            btnBattle.Clicked += () => {
                Application.RequestStop();
                Application.Run<BattleExportGuiWindow>();
            };

            Add(btnField, btnBattle);
        }

    }
}
