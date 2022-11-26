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
        public VMM Memory { get; } = new();
        public SaveMap SaveMap { get; }

        public SaveData SaveData { get; private set; }
        protected Dictionary<string, List<DataSource>> _data = new Dictionary<string, List<DataSource>>(StringComparer.InvariantCultureIgnoreCase);
        private Dictionary<Type, object> _singletons = new();
        private Dictionary<Type, Dictionary<int, CacheItem>> _cacheItems = new();

        public BGame() {
            SaveMap = new SaveMap(Memory);
        }


        public void Save(string path) {
            Directory.CreateDirectory(Path.GetDirectoryName(path + ".sav"));
            using (var fs = File.OpenWrite(path + ".sav"))
                Serialisation.Serialise(SaveData, fs);
            using (var fs = File.OpenWrite(path + ".mem"))
                Memory.Save(fs);
        }

        public void Load(string path) {
            using (var fs = File.OpenRead(path + ".mem"))
                Memory.Load(fs);
            using (var fs = File.OpenRead(path + ".sav"))
                SaveData = Serialisation.Deserialise<SaveData>(fs);
            SaveData.Loaded();
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

        public T CacheItem<T>(int id) where T : CacheItem, new() {
            if (!_cacheItems.TryGetValue(typeof(T), out var dict))
                dict = _cacheItems[typeof(T)] = new Dictionary<int, CacheItem>();
            if (!dict.TryGetValue(id, out var item)) {
                T t = new T();
                t.Init(this, id);
                item = dict[id] = t;
            }
            return (T)item;
        }


        public void NewGame() {
            using (var s = Open("save", "newgame.xml"))
                SaveData = Serialisation.Deserialise<SaveData>(s);
            Memory.ResetAll();
            Braver.NewGame.Init(this);
            SaveData.Loaded();
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

    public abstract class CacheItem {
        public abstract void Init(BGame g, int index);
    }

    public class GameText {
        private List<Ficedula.FF7.KernelText> _texts = new();
        public GameText(BGame g) {
            var kernel = new Ficedula.FF7.Kernel(g.Open("kernel", "kernel.bin"));
            _texts.AddRange(Enumerable.Repeat<Ficedula.FF7.KernelText>(null, 9));
            _texts.AddRange(
                kernel.Sections
                .Skip(9)
                .Select(section => new Ficedula.FF7.KernelText(section))
            );
        }

        public string Get(int section, int item) => _texts[section].Get(item);
    }
}
