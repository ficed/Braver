// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Braver {
    public class BuiltIn : AutoEnabledPlugin {
        public override string Name => "Built in Braver functionality";
        public override Version Version => new Version(1, 0,  0);
        public override object ConfigObject => null;

        public override IEnumerable<IPluginInstance> Get(string context, Type t) {
            if (t == typeof(Plugins.Field.IModelLoader)) {
                yield return new HRCModelLoader();
            }
        }

        public override IEnumerable<Type> GetPluginInstances() {
            yield return typeof(Plugins.Field.IModelLoader);
        }

        public override void Init(BGame game) {
            //
        }
    }

    public class HRCModelLoader : Plugins.Field.IModelLoader {
        public Plugins.Field.FieldModelRenderer Load(BGame game, string category, string hrc) {
            return new Field.HRCFieldModel();
        }
    }
}
