// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {
    public static class Settings {

        public readonly static Dictionary<string, string> Values = new(StringComparer.InvariantCultureIgnoreCase);

        static Settings() {
            List<string> settingValues = new();
            string root = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            string configFile = Path.Combine(root, "braver.cfg");
            if (File.Exists(configFile))
                settingValues.AddRange(File.ReadAllLines(configFile));

            settingValues.AddRange(Environment.GetCommandLineArgs());

            foreach (var setting in settingValues
                .Select(s => s.Split(new[] { '=' }, 2))
                .Where(sa => sa.Length == 2)) {
                if (setting[1] == ".")
                    Values[setting[0]] = root;
                else
                    Values[setting[0]] = setting[1];
            }
        }
    }
}
