// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Plugins {
    public class DataOnlyPlugin : AutoEnabledPlugin {

        private string _root;

        public override string Name => $"Data Plugin: {_root}";
        public override Version Version => new Version(1, 0, 0);
        public override object ConfigObject => null;

        public DataOnlyPlugin(string root) { 
            _root = root;
        }

        public override IEnumerable<IPluginInstance> Get(string context, Type t) {
            throw new NotImplementedException();
        }

        public override IEnumerable<Type> GetPluginInstances() {
            return Enumerable.Empty<Type>();
        }

        public override void Init(BGame game) {
            string data = Path.Combine(_root, "BraverData");
            if (Directory.Exists(data)) {
                foreach(string folder in Directory.GetDirectories(data)) {
                    game.AddDataSource(Path.GetFileName(folder), new FileDataSource(folder));
                }
            }
        }
    }
}
