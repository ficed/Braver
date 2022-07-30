using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace F7 {

    public abstract class DataSource {
        public abstract Stream TryOpen(string file);
        public abstract IEnumerable<string> Scan();
    }

    public class FGame {

        private class LGPDataSource : DataSource {
            private Ficedula.FF7.LGPFile _lgp;

            public LGPDataSource(Ficedula.FF7.LGPFile lgp) {
                _lgp = lgp;
            }

            public override IEnumerable<string> Scan() => _lgp.Filenames;
            public override Stream TryOpen(string file) => _lgp.TryOpen(file);
        }

        private class FileDataSource : DataSource {
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

        private Stack<Screen> _screens = new();

        public VMM Memory { get; } = new();

        public Audio Audio { get; }
        public Screen Screen => _screens.Peek();

        public SaveData SaveData { get; private set; }
        private Dictionary<string, List<DataSource>> _data = new Dictionary<string, List<DataSource>>(StringComparer.InvariantCultureIgnoreCase);
        private Dictionary<Type, object> _singletons = new Dictionary<Type, object>();

        public FGame(string data, string bdata) {
            _data["field"] = new List<DataSource> {
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "field", "flevel.lgp"))),
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "field", "char.lgp"))),
            };
            _data["menu"] = new List<DataSource> {
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "menu", "menu_us.lgp"))),
            };
            _data["wm"] = new List<DataSource> {
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "wm", "world_us.lgp"))),
                new FileDataSource(Path.Combine(data, "wm"))
            };
            foreach (string dir in Directory.GetDirectories(bdata)) {
                string category = Path.GetFileName(dir);
                if (!_data.TryGetValue(category, out var L))
                    L = _data[category] = new();
                L.Add(new FileDataSource(dir));
            }

            Audio = new Audio(data);

            Audio.Precache(Sfx.Cursor, true);
            Audio.Precache(Sfx.Cancel, true);
            Audio.Precache(Sfx.Invalid, true);
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
            using (var s = Open("save", "newgame.xml"))
                SaveData = Serialisation.Deserialise<SaveData>(s);
            SaveData.Loaded();
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
            using(var s = Open(category, file)) {
                using (var sr = new StreamReader(s))
                    return sr.ReadToEnd();
            }
        }

        public IEnumerable<string> ScanData(string category) {
            if (_data.TryGetValue(category, out var sources))
                return sources.SelectMany(s => s.Scan());
            else
                return Enumerable.Empty<string>();
        }

        public void PushScreen(Screen s) {
            _screens.Push(s);
        }

        public void PopScreen(Screen current) {
            Debug.Assert(_screens.Pop() == current);
            current.Dispose();
            Screen.Reactivated();
        }

        public void ChangeScreen(Screen from, Screen to) {
            _screens.TryPeek(out var current);
            Debug.Assert(from == current);
            if (current != null) {
                _screens.Pop();
                current.Dispose();
            }
            _screens.Push(to);
        }

        public void InvokeOnMainThread(Action a) {
            _invoke.Add(a);
        }

        private List<Action> _invoke = new();
        private int _lastSeconds;
        public void Step(GameTime gameTime, InputState input) {
            if (Screen.InputEnabled)
                Screen.ProcessInput(input);

            if ((int)gameTime.TotalGameTime.TotalSeconds != _lastSeconds) {
                _lastSeconds = (int)gameTime.TotalGameTime.TotalSeconds;
                SaveData.GameTimeSeconds++;
            }

            var actions = _invoke.ToArray();
            _invoke.Clear();
            foreach (var action in actions)
                action();

            Screen.Step(gameTime);

        }

    }
}
