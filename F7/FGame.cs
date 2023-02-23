using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Braver {

    public class LocalPref {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string Value{ get; set; }
    }
    public class LocalPrefs {
        [XmlElement("Pref")]
        public List<LocalPref> Prefs { get; set; } = new();
    }

    public class DebugOptions {
        public bool NoFieldScripts { get; set; }
        public bool NoRandomBattles { get; set; }
        public bool SkipBattleMenu { get; set; }
        public bool AutoSaveOnFieldEntry { get; set; }

        public DebugOptions(Dictionary<string, string> settings) {
            if (settings.TryGetValue("Debug", out string d) && bool.Parse(d)) {
                foreach(var prop in typeof(DebugOptions).GetProperties()) {
                    if (settings.TryGetValue($"Debug.{prop.Name}", out string value)) {
                        if (prop.PropertyType == typeof(bool))
                            prop.SetValue(this, bool.Parse(value));
                        else
                            throw new NotImplementedException();
                    }
                }
            }
        }
    }


    public class FGame : BGame {

        private Stack<Screen> _screens = new();
        private GraphicsDevice _graphics;

        public Net.Net Net { get; set; }

        public Audio Audio { get; }
        public Screen Screen => _screens.Peek();
        public DebugOptions DebugOptions { get; }

        private Dictionary<string, string> _prefs;
        private Dictionary<string, string> _paths = new(StringComparer.InvariantCultureIgnoreCase);

        public FGame(GraphicsDevice graphics) {
            _graphics = graphics;

            Dictionary<string, string> settings = Environment.GetCommandLineArgs()
                .Select(s => s.Split(new[] { '=' }, 2))
                .Where(sa => sa.Length == 2)
                .ToDictionary(sa => sa[0], sa => sa[1], StringComparer.InvariantCultureIgnoreCase);

            DebugOptions = new DebugOptions(settings);

            string dataFile = Path.Combine(Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]), "data.txt");
            if (settings.ContainsKey("Data"))
                dataFile = settings["Data"];

            string[] data = File.ReadAllLines(dataFile);

            string Expand(string s) {
                foreach (string setting in settings.Keys)
                    s = s.Replace($"%{setting}%", settings[setting]);
                return s;
            }

            foreach(string spec in data.Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith("#"))) {
                string[] parts = spec.Split((char[])null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts[0] == "DATA") {
                    if (!_data.TryGetValue(parts[2], out var list))
                        list = _data[parts[2]] = new List<DataSource>();

                    string path = Expand(parts[3]);
                    if (parts[1] == "LGP")
                        list.Add(new LGPDataSource(new Ficedula.FF7.LGPFile(path)));
                    else if (parts[1] == "FILE")
                        list.Add(new FileDataSource(path));
                    else
                        throw new NotSupportedException($"Unrecognised data source {spec}");

                } else if (parts[0] == "PATH") {
                    string path = Expand(parts[2]);
                    _paths[parts[1]] = path;
                } else
                    throw new NotSupportedException($"Unrecognised data spec {spec}");
            }

            Audio = new Audio(this, _paths["MUSIC"], _paths["SFX"]);

            Audio.Precache(Sfx.Cursor, true);
            Audio.Precache(Sfx.Cancel, true);
            Audio.Precache(Sfx.Invalid, true);

            string prefs = GetPrefsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(prefs));
            if (File.Exists(prefs)) {
                using (var fs = File.OpenRead(prefs)) {
                    var lp = Serialisation.Deserialise<LocalPrefs>(fs);
                    _prefs = lp.Prefs
                        .ToDictionary(p => p.Name, p => p.Value, StringComparer.InvariantCultureIgnoreCase);
                }
            } else
                _prefs = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        }

        public string GetPath(string name) => _paths[name];

        public static string GetSavePath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Braver", "save");
        private static string GetPrefsPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Braver", "prefs.xml");

        public string GetPref(string name, string def = "") {
            _prefs.TryGetValue(name, out string v);
            return v ?? def;
        }
        public void SetPref(string name, string value) {
            _prefs[name] = value;
            using (var fs = File.OpenWrite(GetPrefsPath())) {
                var lp = new LocalPrefs {
                    Prefs = _prefs.Select(kv => new LocalPref { Name = kv.Key, Value = kv.Value }).ToList()
                };
                Serialisation.Serialise(lp, fs);
            }
        }


        public void AutoSave() {
            string path = GetSavePath();
            foreach (string file1 in Directory.GetFiles(path, "auto1.*"))
                File.Move(file1, Path.Combine(path, "auto2" + Path.GetExtension(file1)), true);
            foreach (string file0 in Directory.GetFiles(path, "auto.*"))
                File.Move(file0, Path.Combine(path, "auto1" + Path.GetExtension(file0)), true);
            Save(Path.Combine(path, "auto"));
        }

        public Stream WriteDebugBData(string category, string file) {
            return new FileStream(Path.Combine(_paths["DEBUG"], category, file), FileMode.Create);
        }

        public override void Load(string path) {
            base.Load(path);
            switch (SaveData.Module) {
                case Module.WorldMap:
                    PushScreen(new WorldMap.WMScreen(SaveData.WorldMapX, SaveData.WorldMapY));
                    break;

                case Module.Field:
                    PushScreen(new Field.FieldScreen(SaveData.FieldDestination));
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        public void PushScreen(Screen s) {
            _screens.Push(s);
            s.Init(this, _graphics);
        }

        public void PopScreen(Screen current) {
            Debug.Assert(_screens.Pop() == current);
            Net.Unlisten(current);
            current.Dispose();
            Net.Send(new Net.PopScreenMessage());
            Screen.Reactivated();
        }

        public void ChangeScreen(Screen from, Screen to) {
            _screens.TryPeek(out var current);
            Debug.Assert(from == current);
            if (current != null) {
                _screens.Pop();
                Net.Unlisten(current);
                current.Dispose();
                Net.Send(new Net.PopScreenMessage());
            }
            PushScreen(to);
        }

        public void InvokeOnMainThread(Action a) {
            _invoke.Add(a);
        }

        private List<Action> _invoke = new();
        private bool _frameStep;

        private bool _isPaused; //TODO - pause visuals

        public void Step(GameTime gameTime, InputState input) {

            if (input.IsJustDown(InputKey.Pause))
                _isPaused = !_isPaused;

            if (_isPaused) {
                return;
            }

            if (Screen.InputEnabled)
                Screen.ProcessInput(input);

            _frameStep = !_frameStep;
            if (_frameStep)
                FrameIncrement();

            var actions = _invoke.ToArray();
            _invoke.Clear();
            foreach (var action in actions)
                action();

            Screen last = null;
            while (Screen != last) {
                last = Screen;
                last.Step(gameTime);
            }

            Net.Update();
            Audio.Update();
        }

    }
}
