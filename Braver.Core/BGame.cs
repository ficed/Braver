// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Braver {

    public abstract class DataSource {
        public abstract Stream TryOpen(string file);
        public abstract IEnumerable<string> Scan();
    }

    public abstract class BGame {

        protected class PackDataSource : DataSource {
            private Pack _pack;
            private string _path;

            private HashSet<string> _files;

            public PackDataSource(Pack pack, string path) {
                _pack = pack;
                _path = path;

                _files = new HashSet<string>(
                    pack.Filenames
                    .Where(s => s.StartsWith(path + "\\", StringComparison.InvariantCultureIgnoreCase)),
                    StringComparer.InvariantCultureIgnoreCase
                );
            }

            public override IEnumerable<string> Scan() {
                return _files.Select(s => Path.GetFileName(s));
            }

            public override Stream TryOpen(string file) {
                string fn = _path + "\\" + file;
                if (_files.Contains(fn)) {
                    return _pack.Read(fn);
                } else
                    return null;
            }
        }

        protected class LGPDataSource : DataSource {
            private Ficedula.FF7.LGPFile _lgp;

            public LGPDataSource(Ficedula.FF7.LGPFile lgp) {
                _lgp = lgp;
            }

            public override IEnumerable<string> Scan() => _lgp.Filenames;
            public override Stream TryOpen(string file) => _lgp.TryOpen(file);
        }

        protected class FileDataSource : DataSource {
            private string _root;

            public FileDataSource(string root) {
                _root = root;
            }

            public override IEnumerable<string> Scan() {
                //TODO subdirectories
                return Directory.GetFiles(_root).Select(s => Path.GetFileName(s));
            }

            public override Stream TryOpen(string file) {
                string fn = Path.Combine(_root, file);
                if (File.Exists(fn))
                    return new FileStream(fn, FileMode.Open, FileAccess.Read);
                return null;
            }
        }
        public VMM Memory { get; } = new();
        public SaveMap SaveMap { get; }

        public SaveData SaveData { get; protected set; }
        protected Dictionary<string, List<DataSource>> _data = new Dictionary<string, List<DataSource>>(StringComparer.InvariantCultureIgnoreCase);
        private Dictionary<Type, object> _singletons = new();

        public BGame() {
            SaveMap = new SaveMap(Memory);
        }

        public int GameTimeSeconds {
            get => SaveMap.GameTimeSeconds + 60 * SaveMap.GameTimeMinutes + 60 * 60 * SaveMap.GameTimeHours;
            set {
                int v = value;
                SaveMap.GameTimeSeconds = (byte)(v % 60);
                v /= 60;
                SaveMap.GameTimeMinutes = (byte)(v % 60);
                v /= 60;
                SaveMap.GameTimeHours = (byte)(v % 60);
            }
        }
        public int CounterSeconds {
            get => SaveMap.CounterSeconds + 60 * SaveMap.CounterMinutes + 60 * 60 * SaveMap.CounterHours;
            set {
                int v = value;
                SaveMap.CounterSeconds = (byte)(v % 60);
                v /= 60;
                SaveMap.CounterMinutes = (byte)(v % 60);
                v /= 60;
                SaveMap.CounterHours = (byte)(v % 60);
            }
        }

        protected void FrameIncrement() {
            if (++SaveMap.GameTimeFrames >= 30) {
                SaveMap.GameTimeFrames = 0;
                GameTimeSeconds++;
            }
            if (CounterSeconds > 0) {
                if (++SaveMap.CounterFrames >= 30) {
                    SaveMap.CounterFrames = 0;
                    CounterSeconds--;
                }
            }
        }

        public void Save(string path, bool packed) {
            Directory.CreateDirectory(Path.GetDirectoryName(path + ".sav"));

            if (packed) {
                var sav = new MemoryStream();
                Serialisation.Serialise(SaveData, sav);
                var mem = new MemoryStream();
                Memory.Save(mem);
                using (var fs = File.Create(path + ".sav"))
                    Pack.Create(fs,
                        ("SaveData", sav.ToArray()),
                        ("Memory", mem.ToArray())
                    );
            } else {
                using (var fs = File.Create(path + ".sav"))
                    Serialisation.Serialise(SaveData, fs);
                using (var fs = File.Create(path + ".mem"))
                    Memory.Save(fs);
            }
        }

        public virtual void Load(string path) {
            using (var fs = File.OpenRead(path + ".sav")) {
                if (Pack.IsPack(fs)) {
                    var pack = new Pack(fs);
                    using (var data = pack.Read("SaveData"))
                        SaveData = Serialisation.Deserialise<SaveData>(data);
                    using (var data = pack.Read("Memory"))
                        Memory.Load(data);
                } else {
                    SaveData = Serialisation.Deserialise<SaveData>(fs);
                    using (var memfs = File.OpenRead(path + ".mem"))
                        Memory.Load(memfs);
                }
            }
            SaveData.CleanUp();
        }

        public T Singleton<T>() where T : Cacheable, new() {
            return Singleton<T>(() => {
                T t = new T();
                t.Init(this);
                return t;
            });
        }

        public T Singleton<T>(Func<T> create) {
            if (_singletons.TryGetValue(typeof(T), out object obj))
                return (T)obj;
            else {
                T t = create();
                _singletons[typeof(T)] = t;
                return t;
            }
        }


        public void NewGame() {
            SaveData = new SaveData();
            //using (var s = Open("save", "newgame.xml"))
            //    SaveData = Serialisation.Deserialise<SaveData>(s);
            Memory.ResetAll();
            Braver.NewGame.Init(this);
            SaveData.CleanUp();
        }

        public IEnumerable<string> ScanData(string category) {
            if (_data.TryGetValue(category, out var sources))
                return sources.SelectMany(s => s.Scan());
            else
                return Enumerable.Empty<string>();
        }

        public Stream TryOpen(string category, string file) {
            foreach (var source in _data[category]) {
                var s = source.TryOpen(file);
                if (s != null)
                    return s;
            }
            return null;
        }

        public Stream Open(string category, string file) {
            var s = TryOpen(category, file);
            if (s == null)
                throw new F7Exception($"Could not open {category}/{file}");
            else
                return s;
        }
        public string OpenString(string category, string file) {
            using (var s = Open(category, file)) {
                using (var sr = new StreamReader(s))
                    return sr.ReadToEnd();
            }
        }

    }

    public abstract class Cacheable {
        public abstract void Init(BGame g);
    }

}
