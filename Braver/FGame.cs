// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Braver.Plugins.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Braver {

    public class FGame : BGame {

        private Stack<Screen> _screens = new();
        private GraphicsDevice _graphics;
        private PluginInstances<ISystem> _systemPlugins;
        private Overlay _overlay;

        public Net.Net Net { get; set; }

        public Screen Screen => _screens.Peek();
        public PluginManager PluginManager { get; }

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

            if (!settings.ContainsKey("braver")) 
                throw new F7Exception($"Not configured - please run BraverLauncher first to configure the game");

            GameOptions = new GameOptions(settings);

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
                    foreach(string candidate in parts.Skip(1)) {
                        string path = Expand(candidate);
                        try {
                            Trace.Listeners.Add(new TraceFile(path));
                        } catch {
                            Debug.WriteLine($"Failed to trace to {path}, moving on");
                            continue;
                        }
                        break;
                    }
                } else
                    throw new NotSupportedException($"Unrecognised data spec {spec}");
            }

            Trace.WriteLine("Braver: Data initialised");
            foreach (var setting in settings)
                Trace.WriteLine($"  Config: {setting.Key} = {setting.Value}");

            PluginManager = new PluginManager();
            if (_paths.ContainsKey("PLUGINS") && !string.IsNullOrWhiteSpace(_paths["PLUGINS"])) {

                List<Plugin> plugins = new();
                foreach(var folder in Directory.GetDirectories(_paths["PLUGINS"])) {
                    var foundInstances = Directory.GetFiles(folder, "*.dll")
                        .Select(fn => System.Reflection.Assembly.LoadFrom(fn))
                        .SelectMany(asm => asm.GetTypes())
                        .Where(t => t.IsAssignableTo(typeof(Plugin)))
                        .Select(t => Activator.CreateInstance(t))
                        .OfType<Plugin>();
                    foreach(var instance in foundInstances) {
                        plugins.Add(instance);
                        _data[instance.GetType().FullName] = new List<DataSource> {
                            new FileDataSource(folder)
                        };
                    }
                    if (Directory.Exists(Path.Combine(folder, "BraverData"))) {
                        plugins.Add(new DataOnlyPlugin(folder));
                    }
                }

                if (plugins.Any()) {
                    PluginConfigs config;

                    string pluginConfig = Path.Combine(_paths["PLUGINS"], "config.xml");
                    if (File.Exists(pluginConfig)) {
                        using (var fs = new FileStream(pluginConfig, FileMode.Open, FileAccess.Read))
                            config = Serialisation.Deserialise<PluginConfigs>(fs);
                    } else
                        config = new PluginConfigs();

                    PluginManager.Init(this, plugins, config);
                }
            }

            _systemPlugins = PluginManager.GetInstances<ISystem>("");

            Audio = new Audio(this, _paths["SFX"], PluginManager.GetInstances<ISfxSource>(""));

            Audio.Precache(Sfx.Cursor, true);
            Audio.Precache(Sfx.Cancel, true);
            Audio.Precache(Sfx.Invalid, true);

            _overlay = new Overlay(graphics, this);
        }

        private class TraceFile : TraceListener {

            private StreamWriter _writer;

            public TraceFile(string file) {
                _writer = new StreamWriter(file, false);
                new System.Threading.Thread(BackgroundFlush) {
                    IsBackground = true,
                    Name = "BackgroundLogFlusher"
                }.Start();
            }

            private void BackgroundFlush() {
                while (true) {
                    System.Threading.Thread.Sleep(10000);
                    lock (_writer)
                        _writer.Flush();
                }
            }

            public override void Fail(string message) {
                lock (_writer) {
                    _writer.WriteLine(message);
                    _writer.Flush();
                }
            }

            public override void Fail(string message, string detailMessage) {
                lock (_writer) {
                    _writer.WriteLine(message + ": " + detailMessage);
                    _writer.Flush();
                }
            }

            public override void Write(string message) {
                lock (_writer) {
                    _writer.Write(message);
                }
            }

            public override void WriteLine(string message) {
                lock (_writer) {
                    _writer.WriteLine(message);
                }
            }
        }

        public void AutoSave() {
            string path = GetPath("save");
            foreach (string file1 in Directory.GetFiles(path, "auto1.*"))
                File.Move(file1, Path.Combine(path, "auto2" + Path.GetExtension(file1)), true);
            foreach (string file0 in Directory.GetFiles(path, "auto.*"))
                File.Move(file0, Path.Combine(path, "auto1" + Path.GetExtension(file0)), true);
            Save(Path.Combine(path, "auto"), !GameOptions.SeparateSaveFiles);
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
            if (_screens.Any())
                Screen.Suspended();
            _screens.Push(s);
            s.Init(this, _graphics);
            _systemPlugins.Call(sys => sys.ActiveScreenChanged(s));
        }

        public void PopScreen(Screen current) {
            Trace.Assert(_screens.Pop() == current);
            Net.Unlisten(current);
            current.Dispose();
            Net.Send(new Net.PopScreenMessage());
            Screen.Reactivated();
            _systemPlugins.Call(sys => sys.ActiveScreenChanged(Screen));
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

        public void InvokeOnMainThread(Action a, int frameDelay = 0) {
            _invoke.Add(new PendingInvoke {  Action = a, FrameDelay = frameDelay });
        }

        private class PendingInvoke {
            public Action Action;
            public int FrameDelay;
        }
        private List<PendingInvoke> _invoke = new();
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


            int index = 0;
            while(index < _invoke.Count) {
                if (_invoke[index].FrameDelay-- <= 0) {
                    _invoke[index].Action();
                    _invoke.RemoveAt(index);
                } else {
                    index++;
                }
            }

            Screen last = null;
            while (Screen != last) {
                last = Screen;
                last.Step(gameTime);
            }

            Net.Update();
            Audio.Update();
        }

        public void Draw() {
            if (Screen.ShouldClear)
                _graphics.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Screen.ClearColor, 1f, 0);
            Screen.Render();
            _overlay.Render();
        }

    }
}
