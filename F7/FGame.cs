using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace F7 {

    public abstract class DataSource {
        public abstract Stream TryOpen(string file);
    }

    public class FGame {

        private class LGPDataSource : DataSource {
            private Ficedula.FF7.LGPFile _lgp;

            public LGPDataSource(Ficedula.FF7.LGPFile lgp) {
                _lgp = lgp;
            }
            public override Stream TryOpen(string file) => _lgp.TryOpen(file);
        }

        private class FileDataSource : DataSource {
            private string _root;

            public FileDataSource(string root) {
                _root = root;
            }
            public override Stream TryOpen(string file) {
                string fn = Path.Combine(_root, file);
                if (File.Exists(fn))
                    return new FileStream(fn, FileMode.Open, FileAccess.Read);
                return null;
            }
        }

        public VMM Memory { get; } = new();
        public SaveData SaveData { get; private set; }

        private Dictionary<string, List<DataSource>> _data = new Dictionary<string, List<DataSource>>(StringComparer.InvariantCultureIgnoreCase);

        public FGame(string data, string bdata) {
            _data["field"] = new List<DataSource> {
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "field", "flevel.lgp"))),
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "field", "char.lgp"))),
            };
            foreach(string dir in Directory.GetDirectories(bdata)) {
                string category = Path.GetFileName(dir);
                if (!_data.TryGetValue(category, out var L))
                    L = _data[category] = new();
                L.Add(new FileDataSource(dir));
            }
        }

        public void NewGame() {
            using (var s = Open("save", "newgame.xml"))
                SaveData = Serialisation.Deserialise<SaveData>(s);
            SaveData.Loaded();
        }

        public Stream Open(string category, string file) {
            foreach(var source in _data[category]) {
                var s = source.TryOpen(file);
                if (s != null)
                    return s;
            }
            throw new F7Exception($"Could not open {category}/{file}");
        }
    }
}
