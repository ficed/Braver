// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver {
    public class ExeData : DataSource, IDisposable {

        //TODO: This doesn't really deal with different exe versions usefully
        //Ideally it needs to be able to shift offsets by searching for content or something

        private System.Reflection.PortableExecutable.PEReader _peReader;
        private Dictionary<string, (int address, int size)> _files = new(StringComparer.InvariantCultureIgnoreCase);

        public ExeData(string sourceFile, string sectionsFile, BGame game) {
            _peReader = new System.Reflection.PortableExecutable.PEReader(File.OpenRead(sourceFile));
            foreach(string line in game.OpenString("braver", sectionsFile).Split('\r', '\n')) {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    continue;
                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                _files[parts[0]] = (int.Parse(parts[1], System.Globalization.NumberStyles.HexNumber), int.Parse(parts[2], System.Globalization.NumberStyles.HexNumber));
            }
        }

        public void Dispose() {
            _peReader.Dispose();
        }

        public override IEnumerable<string> Scan() {
            return _files.Keys;
        }

        public override Stream TryOpen(string file) {
            if (_files.TryGetValue(file, out var which)) {
                var data = _peReader.GetSectionData(which.address - (int)_peReader.PEHeaders.PEHeader.ImageBase);
                var content = data.GetContent();
                return new MemoryStream(content.Take(Math.Min(which.size, content.Length)).ToArray());
            } else
                return null;
        }
    }
}
