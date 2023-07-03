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
                using (var game = new Game1())
                    game.Run();
            } catch (Exception ex) {
                System.Diagnostics.Trace.Fail(ex.ToString());
            }
        }
    }
}