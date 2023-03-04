// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {
    public class Pack {

        private const int SIGNATURE = 0x03f1ce04;

        private class Entry {
            public long Offset; 
            public long Size;
            public int Flags;
            public string Name;

            public void Save(Stream s) {
                s.WriteI64(Offset);
                s.WriteI64(Size);
                s.WriteI32(Flags);
                s.WriteS(Name);
            }
        }

        private Stream _source;
        private List<Entry> _entries = new();
        private Dictionary<string, Entry> _entriesByName;

        public IEnumerable<string> Filenames => _entriesByName.Keys;

        public Pack(Stream source) {
            _source = source;
            Trace.Assert(source.ReadI32() == SIGNATURE);

            int count = source.ReadI32();
            foreach(int _ in Enumerable.Range(0, count)) {
                _entries.Add(new Entry {
                    Offset = source.ReadI64(),
                    Size = source.ReadI64(),
                    Flags = source.ReadI32(),
                    Name = source.ReadS(),
                });
            }
            _entriesByName = _entries
                .ToDictionary(e => e.Name, e => e, StringComparer.InvariantCultureIgnoreCase);
        }

        public Stream Read(string file) {
            var entry = _entriesByName[file];
            lock (_source) {
                _source.Position = entry.Offset;
                byte[] bytes = new byte[entry.Size];
                _source.Read(bytes, 0, bytes.Length);
                var output = new MemoryStream();
                using var decompressor = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
                decompressor.CopyTo(output);
                output.Position = 0;
                return output;
            }
        }

        public static bool IsPack(Stream s) {
            s.Position = 0;
            bool result = s.ReadI32() == SIGNATURE;
            s.Position = 0;
            return result;
        }

        public static void Create(Stream dest, params (string name, byte[] data)[] files) {
            List<Entry> entries = files.Select(f => new Entry {
                    Name = f.name,
                })
                .ToList();

            dest.WriteI32(SIGNATURE);
            dest.WriteI32(files.Length);
            foreach (var entry in entries)
                entry.Save(dest);

            foreach(int i in Enumerable.Range(0, entries.Count)) {
                entries[i].Offset = dest.Position;
                var output = new MemoryStream();
                using var compressor = new GZipStream(output, CompressionMode.Compress);
                new MemoryStream(files[i].data).CopyTo(compressor);
                compressor.Flush();
                entries[i].Size = output.Length;
                output.Position = 0;
                output.CopyTo(dest);
            }

            dest.Position = 8;
            foreach (var entry in entries)
                entry.Save(dest);

        }
    }
}
