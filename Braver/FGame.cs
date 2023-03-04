// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Braver {

    public class DebugOptions {
        public bool NoFieldScripts { get; set; }
        public bool NoRandomBattles { get; set; }
        public bool SkipBattleMenu { get; set; }
        public bool AutoSaveOnFieldEntry { get; set; }
        public bool SeparateSaveFiles { get; set; }

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

        private Dictionary<string, string> _paths = new(StringComparer.InvariantCultureIgnoreCase);

        public FGame(GraphicsDevice graphics) {
            _graphics = graphics;

            Dictionary<string, string> settings = new(StringComparer.InvariantCultureIgnoreCase);
            List<string> settingValues = new();
            string root = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            string configFile = Path.Combine(root, "braver.cfg");
            if (File.Exists(configFile))
                settingValues.AddRange(File.ReadAllLines(configFile));

            settingValues.AddRange(Environment.GetCommandLineArgs());

            foreach(var setting in settingValues
                .Select(s => s.Split(new[] { '=' }, 2))
                .Where(sa => sa.Length == 2)) {
                if (setting[1] == ".")
                    settings[setting[0]] = root;
                else
                    settings[setting[0]] = setting[1];
            }

            DebugOptions = new DebugOptions(settings);

            string dataFile = Path.Combine(Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]), "data.txt");
            if (settings.ContainsKey("Data"))
                dataFile = settings["Data"];

            string[] data;

            if (File.Exists(dataFile))
                data = File.ReadAllLines(dataFile);
            else {
                using(var src = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Braver.data.txt")) { 
                    using(var sr = new StreamReader(src)) {
                        data = sr.ReadToEnd()
                            .Split('\r', '\n')
                            .ToArray();
                    }
                }
            }

            string Expand(string s) {
                foreach (string setting in settings.Keys)
                    s = s.Replace($"%{setting}%", settings[setting], StringComparison.InvariantCultureIgnoreCase);
                return s;
            }

            Dictionary<string, Pack> packs = new(StringComparer.InvariantCultureIgnoreCase);

            foreach(string spec in data.Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith("#"))) {
                string[] parts = spec.Split((char[])null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts[0] == "DATA") {
                    if (!_data.TryGetValue(parts[2], out var list))
                        list = _data[parts[2]] = new List<DataSource>();

                    string kind = parts[1].TrimEnd('?');
                    string path = Expand(parts[3]);
                    if (!File.Exists(path) && !Directory.Exists(path)) {
                        if (parts[1].EndsWith('?'))
                            continue;
                        else
                            throw new NotSupportedException($"Could not find data source '{path}'");
                    }
                    if (kind == "LGP")
                        list.Add(new LGPDataSource(new Ficedula.FF7.LGPFile(path)));
                    else if (kind == "FILE")
                        list.Add(new FileDataSource(path));
                    else if (kind == "PACK") {
                        if (!packs.TryGetValue(path, out var pack))
                            packs[path] = pack = new Pack(new FileStream(path, FileMode.Open, FileAccess.Read));
                        list.Add(new PackDataSource(pack, parts[2]));
                    } else
                        throw new NotSupportedException($"Unrecognised data source {spec}");

                } else if (parts[0] == "PATH") {
                    string path = Expand(parts[2]);
                    _paths[parts[1]] = path;
                } else if (parts[0] == "LOG") {
                    string path = Expand(parts[1]);
                    Trace.Listeners.Add(new TraceFile(path));
                } else
                    throw new NotSupportedException($"Unrecognised data spec {spec}");
            }

            Audio = new Audio(this, _paths["MUSIC"], _paths["SFX"]);

            Audio.Precache(Sfx.Cursor, true);
            Audio.Precache(Sfx.Cancel, true);
            Audio.Precache(Sfx.Invalid, true);

        }

        private class TraceFile : TraceListener {

            private StreamWriter _writer;
            private DateTime _lastFlush = DateTime.Now;

            public TraceFile(string file) {
                _writer = new StreamWriter(file, false);
            }

            public override void Write(string message) {
                lock (_writer) {
                    _writer.Write(message);
                    if (_lastFlush < DateTime.Now.AddSeconds(-10)) {
                        _writer.Flush();
                        _lastFlush = DateTime.Now;
                    }
                }
            }

            public override void WriteLine(string message) {
                lock (_writer) {
                    _writer.WriteLine(message);
                    if (_lastFlush < DateTime.Now.AddSeconds(-10)) {
                        _writer.Flush();
                        _lastFlush = DateTime.Now;
                    }
                }
            }
        }

        public string GetPath(string name) => _paths[name];

        public void AutoSave() {
            string path = GetPath("save");
            foreach (string file1 in Directory.GetFiles(path, "auto1.*"))
                File.Move(file1, Path.Combine(path, "auto2" + Path.GetExtension(file1)), true);
            foreach (string file0 in Directory.GetFiles(path, "auto.*"))
                File.Move(file0, Path.Combine(path, "auto1" + Path.GetExtension(file0)), true);
            Save(Path.Combine(path, "auto"), !DebugOptions.SeparateSaveFiles);
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
            Trace.Assert(_screens.Pop() == current);
            Net.Unlisten(current);
            current.Dispose();
            Net.Send(new Net.PopScreenMessage());
            Screen.Reactivated();
        }

        public void ChangeScreen(Screen from, Screen to) {
            _screens.TryPeek(out var current);
            Trace.Assert(from == current);
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
