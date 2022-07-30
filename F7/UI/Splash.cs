using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI {
    public class Splash : Screen {

        private UIBatch _ui;
        private int _menu = 0;

        public Splash(FGame g, GraphicsDevice graphics) : base(g, graphics) {
            _ui = new UIBatch(graphics, g);
            FadeIn(null);
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
                            Game.ChangeScreen(this, new WorldMap.WMScreen(Game, Graphics, 139348, 126329)); //TODO!
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

        protected override void DoStep(GameTime elapsed) {
            _ui.Reset();

            _ui.DrawImage("logo_buster", 640, 100, 0.1f, Alignment.Center);

            _ui.DrawText("main", "New Game", 600, 300, 0.2f, Color.White);
            _ui.DrawText("main", "Continue", 600, 335, 0.2f, Color.White);
            _ui.DrawText("main", "Load Game", 600, 370, 0.2f, Color.White);
            _ui.DrawText("main", "Quit", 600, 405, 0.2f, Color.White);

            _ui.DrawImage("pointer", 595, 300 + 35 * _menu, 0.3f, Alignment.Right);
        }
    }
}

