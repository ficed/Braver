using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public bool AutoSaveOnFieldEntry { get; set; } = true;
    }


    public class FGame : BGame {

        private Stack<Screen> _screens = new();
        private GraphicsDevice _graphics;

        public Net.Net Net { get; set; }

        public Audio Audio { get; }
        public Screen Screen => _screens.Peek();
        public DebugOptions DebugOptions { get; } = new();

        public string FFMpegPath { get; private set; } //TODO - better place to put/calculate this?

        private Dictionary<string, string> _prefs;
        private string _bdata;

        public FGame(GraphicsDevice graphics, string root, string bdata) {
            _graphics = graphics;
            _bdata = bdata;
            string data = Path.Combine(root, "data");
            _data["field"] = new List<DataSource> {
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "field", "flevel.lgp"))),
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "field", "char.lgp"))),
            };
            _data["menu"] = new List<DataSource> {
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "menu", "menu_us.lgp"))),
            };
            _data["cd"] = new List<DataSource> {
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "cd", "disc_us.lgp"))),
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "cd", "cr_us.lgp"))),
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "cd", "moviecam.lgp"))),
            };
            _data["wm"] = new List<DataSource> {
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "wm", "world_us.lgp"))),
                new FileDataSource(Path.Combine(data, "wm"))
            };
            _data["battle"] = new List<DataSource> {
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "battle", "battle.lgp"))),
                new FileDataSource(Path.Combine(data, "battle"))
            };
            _data["kernel"] = new List<DataSource> {
                new FileDataSource(Path.Combine(data, "kernel"))
            };
            _data["root"] = new List<DataSource> {
                new FileDataSource(root),
            };
            foreach (string dir in Directory.GetDirectories(bdata)) {
                string category = Path.GetFileName(dir);
                if (!_data.TryGetValue(category, out var L))
                    L = _data[category] = new();
                L.Add(new FileDataSource(dir));
            }
            FFMpegPath = Path.Combine(bdata, "ffmpeg.exe");

            Audio = new Audio(this, data);

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
            return new FileStream(Path.Combine(_bdata, category, file), FileMode.Create);
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
