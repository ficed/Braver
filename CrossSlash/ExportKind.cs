// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7;
using Ficedula.FF7.Exporters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace CrossSlash {

    public abstract class ExportKind {
        public abstract string Help { get; }
        public abstract object Config { get; }
        public abstract bool HasGui { get; }
        public abstract string Name { get; }

        public abstract void Execute(DataSource source, string dest, IEnumerable<string> parameters);
        public abstract Window ExecuteGui(DataSource source);

        public static readonly Dictionary<string, ExportKind> Exporters = new Dictionary<string, ExportKind>(StringComparer.InvariantCultureIgnoreCase) {
            ["FieldModel"] = new CrossSlash.FieldExport(),
            ["BattleModel"] = new CrossSlash.BattleExport(),
            ["LGP"] = new CrossSlash.LGP(),
            ["Tex"] = new CrossSlash.Tex(),
            ["Audio"] = new CrossSlash.Audio(),
        };

    }
}
