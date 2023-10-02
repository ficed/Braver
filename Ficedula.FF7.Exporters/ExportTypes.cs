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

namespace Ficedula.FF7.Exporters {

    public abstract class DataSource {
        public abstract bool Exists(string name);
        public abstract Stream Open(string name);
        public abstract IEnumerable<string> AllFiles { get; }

        public virtual Stream TryOpen(string name) {
            if (Exists(name))
                return Open(name);
            else
                return null;
        }

        private class FileDataSource : DataSource {
            private string _root;

            public override IEnumerable<string> AllFiles => Directory.GetFiles(_root)
                .Select(fn => fn.Substring(_root.Length).TrimStart(Path.DirectorySeparatorChar));

            public FileDataSource(string root) {
                _root = root;
            }

            public override bool Exists(string name) => File.Exists(Path.Combine(_root, name));
            public override Stream Open(string name) => File.OpenRead(Path.Combine(_root, name));
        }

        private class LGPDataSource : DataSource, IDisposable {
            private LGPFile _lgp;

            public override IEnumerable<string> AllFiles => _lgp.Filenames;

            public LGPDataSource(string file) {
                _lgp = new LGPFile(File.OpenRead(file));
            }

            public void Dispose() {
                _lgp.Dispose();
            }

            public override bool Exists(string name) => _lgp.Exists(name);
            public override Stream Open(string name) => _lgp.Open(name);
        }

        public static DataSource Create(string source) {
            if (Directory.Exists(source))
                return new FileDataSource(source);
            else
                return new LGPDataSource(source);
        }
    }

}
