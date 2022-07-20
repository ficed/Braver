using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7 {
    public class LGPFile : IDisposable {

        private class Entry {
            public int Offset { get; init; }
            public string Name { get; init; }
            public bool ExtendedName { get; init; }
            public string? Path { get; set; }

            public string FullPath {
                get {
                    if (Path == null)
                        return Name;
                    else
                        return Path + "/" + Name;
                }
            }
        }

        private Stream _source;
        private Dictionary<string, Entry> _entries;

        public IEnumerable<string> Filenames => _entries.Select(e => e.Value.FullPath);

        public LGPFile(string filename) : this(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
        }

        public LGPFile(Stream source) {
            _source = source;
            _source.Position = 0;
            if (_source.ReadI16() != 0)
                throw new FFException("Invalid LGP file: bad header(0)");
            if (!_source.ReadAscii(10).Equals("SQUARESOFT"))
                throw new FFException("Invalid LGP file: bad header(1)");

            int numFiles = _source.ReadI32();
            List<Entry> tempEntries = new List<Entry>();
            foreach (int i in Enumerable.Range(0, numFiles)) {
                string name = _source.ReadAscii(20).Trim('\0', ' ');
                int offset = _source.ReadI32();
                _source.ReadByte();
                bool extended = _source.ReadI16() != 0;
                tempEntries.Add(new Entry {
                    Name = name,
                    Offset = offset,
                    ExtendedName = extended,
                });
            }
            if (tempEntries.Any(e => e.ExtendedName)) {
                _source.Seek(3600, System.IO.SeekOrigin.Current);
                foreach (int _ in Enumerable.Range(0, _source.ReadI16())) {
                    foreach (int __ in Enumerable.Range(0, _source.ReadI16())) {
                        string path = _source.ReadAscii(128);
                        int entry = _source.ReadI16();
                        tempEntries[entry].Path = path;
                    }
                }
            }
            _entries = tempEntries
                .ToDictionary(e => e.FullPath, e => e, StringComparer.InvariantCultureIgnoreCase);
        }

        public Stream? TryOpen(string name) {
            if (_entries.TryGetValue(name, out Entry? e)) {
                _source.Position = e.Offset + 20;
                int length = _source.ReadI32();
                //TODO: don't always load into memory, support passthrough reading from source?
                byte[] buffer = new byte[length];
                _source.Read(buffer, 0, length);
                var mem = new MemoryStream(buffer);
                return mem;
            } else
                return null;
        }
        public Stream Open(string name) {
            var s = TryOpen(name);
            if (s == null)
                throw new FFException($"LGP file {name} not found");
            return s;
        }

        public void Dispose() {
            _source.Dispose();
        }
    }
}
