// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;

namespace Braver {
    public static class Program {

        [STAThread]
        static void Main() {
            try {
                System.Globalization.CultureInfo.CurrentCulture =
                    System.Globalization.CultureInfo.CurrentUICulture =
                    System.Globalization.CultureInfo.DefaultThreadCurrentCulture =
                    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

                using (var game = new Game1())
                    game.Run();
            } catch (Exception ex) {
                System.Diagnostics.Trace.Fail(ex.ToString());
                System.Windows.Forms.MessageBox.Show(
                    ex.Message, "Error",
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error
                );
            }
        }
    }
}