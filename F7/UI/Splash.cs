using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI {
    public class Splash : Screen {

        private UIBatch _ui;
        private int _menu = 0;

        public Splash(FGame g, GraphicsDevice graphics) : base(g, graphics) {
            _ui = new UIBatch(graphics, g);
            FadeIn(null);
            Layout.LayoutScreen.BeginBackgroundLoad(g, "MainMenu");
        }

        public override Color ClearColor => Color.Black;

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);
            if (input.IsJustDown(InputKey.Down)) {
                _menu = (_menu + 1) % 4;
                Game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
            } else if (input.IsJustDown(InputKey.Up)) {
                _menu = (_menu + 3) % 4;
                Game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
            } else if (input.IsJustDown(InputKey.OK)) {                
                Game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                InputEnabled = false;
                FadeOut(() => {
                    switch (_menu) {
                        case 0:
                            Game.NewGame();
                            Game.ChangeScreen(this, new Field.FieldScreen(
                                new Ficedula.FF7.Field.FieldDestination {
                                    X = -225, Y = -830, Triangle = 152,
                                    Orientation = 116,
                                    DestinationFieldID = 195,
                                },
                                Game, Graphics
                            ));
                            break;

                        case 1:
                            Game.NewGame();
                            Game.ChangeScreen(this, new WorldMap.WMScreen(Game, Graphics, 139348, 126329));
                            break;

                        case 2:
                            Game.NewGame();
                            Game.ChangeScreen(this, new Battle.BattleScreen(Game, Graphics, 100));
                            break;

                        case 3:
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

        protected override void DoStep(GameTime elapsed) {
            _ui.Reset();

            _ui.DrawImage("logo_buster", 640, 100, 0.1f, Alignment.Center);

            _ui.DrawText("main", "New Game", 600, 300, 0.2f, Color.White);
            _ui.DrawText("main", "Continue", 600, 335, 0.2f, Color.White);
            _ui.DrawText("main", "Load Game", 600, 370, 0.2f, Color.White);
            _ui.DrawText("main", "Quit", 600, 405, 0.2f, Color.White);

            _ui.DrawText("main", "v" + _version, 1275, 705, 0.2f, Color.White, Alignment.Right, size: 0.5f);

            _ui.DrawImage("pointer", 595, 300 + 35 * _menu, 0.3f, Alignment.Right);
        }
    }
}

