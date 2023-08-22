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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI {
    public class Splash : Screen {

        private UIBatch _ui;
        private int _menu = 0;

        private string _host, _key;
        private int _port;
        private PluginInstances<IUI> _plugins;

        public override string Description => "Braver";

        public Splash() {  //Server
        }
        public Splash(string host, int port, string key) {
            _host = host;
            _port = port;
            _key = key;
        }

        private void Announce() {
            _plugins.Call(ui => ui.Menu(_items, _menu, this));
        }

        public override void Init(FGame g, GraphicsDevice graphics) {
            if (_host != null) {
                g.Net = new Net.Client(g, _host, _port, _key);
            } else {
                g.Net = new Net.Server();
            }
            base.Init(g, graphics);
            _plugins = GetPlugins<IUI>("_Splash");
            _ui = new UIBatch(graphics, g);
            FadeIn(null);
            Layout.LayoutScreen.BeginBackgroundLoad(g, "MainMenu");
            _readyToRender = true;
            g.InvokeOnMainThread(Announce, 2);
            Trace.TraceWarning($"Braver {_version} started");
        }

        public override Color ClearColor => Color.Black;

        public override void Reactivated() {
            base.Reactivated();
            InputEnabled = true;
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);

            if (Game.Net is Net.Client) return;

            if (input.IsJustDown(InputKey.Down)) {
                _menu = (_menu + 1) % 5;
                Game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                Announce();
            } else if (input.IsJustDown(InputKey.Up)) {
                _menu = (_menu + 4) % 5;
                Game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                Announce();
            } else if (input.IsJustDown(InputKey.OK)) {                
                Game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                InputEnabled = false;
                FadeOut(() => {
                    switch (_menu) {
                        case 0:
                            Game.NewGame();
                            Game.ChangeScreen(this, new Field.FieldScreen(
                                ///*
                                new Ficedula.FF7.Field.FieldDestination {
                                    Triangle = ushort.MaxValue, //don't position player for a new game!
                                    DestinationFieldID = 116,
                                }
                                //*/
                            /*
                            new Ficedula.FF7.Field.FieldDestination {
                                X = 7, Y = -107, Triangle = 21,
                                Orientation = 132,
                                DestinationFieldID = 180,
                            }
                            */
                            /*
                                                            new Ficedula.FF7.Field.FieldDestination {
                                                                X = -225, Y = -830, Triangle = 152,
                                                                Orientation = 116,
                                                                DestinationFieldID = 195,
                                                            }
                            */
                            ));
                            break;

                        case 1:
                            string autoPath = System.IO.Path.Combine(Game.GetPath("save"), "auto");
                            if (System.IO.File.Exists(autoPath + ".sav"))
                                Game.Load(autoPath);
                            else
                                Game.Audio.PlaySfx(Sfx.Invalid, 1f, 0f);
                            break;

                        case 2:
                            Game.PushScreen(new UI.Layout.LayoutScreen("SaveMenu", parm: false));
                            break;

                        case 3:
                            Game.NewGame();
                            //Game.PushScreen(new TestScreen());
                            //Game.ChangeScreen(this, new TestScreen());
                            //Game.ChangeScreen(this, new WorldMap.WMScreen(139348, 126329));
                            Battle.BattleScreen.Launch(Game, 324, Battle.BattleFlags.None);
                            break;

                        case 4:
                            Environment.Exit(0);
                            break;
                    }
                });
            }
        }

        protected override void DoRender() {
            _ui.Render();
        }

        private string _version = Assembly.GetExecutingAssembly()
            .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), true)
            .OfType<AssemblyInformationalVersionAttribute>()
            .First()
            .InformationalVersion;

        private string[] _items = new[] {
            "New Game",
            "Continue",
            "Load Game",
            "Test",
            "Quit",
        };

        protected override void DoStep(GameTime elapsed) {
            _ui.Reset();

            _ui.DrawImage("logo_buster", 640, 100, 0.1f, Alignment.Center);

            if (Game.Net is Net.Client) {
                _ui.DrawText("main", Game.Net.Status, 640, 300, 0.2f, Color.White, Alignment.Center);
            } else {
                _ui.DrawText("main", _items[0], 600, 300, 0.2f, Color.White);
                _ui.DrawText("main", _items[1], 600, 335, 0.2f, Color.White);
                _ui.DrawText("main", _items[2], 600, 370, 0.2f, Color.White);
                _ui.DrawText("main", _items[3], 600, 405, 0.2f, Color.White);

                _ui.DrawText("main", _items[4], 600, 440, 0.2f, Color.White);

                _ui.DrawImage("pointer", 595, 300 + 35 * _menu, 0.3f, Alignment.Right);
            }

            _ui.DrawText("main", "v" + _version, 1275, 705, 0.2f, Color.White, Alignment.Right, size: 0.5f);

        }
    }
}

