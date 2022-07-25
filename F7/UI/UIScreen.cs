using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace F7.UI {
    public class UIScreen : Screen {

        private UIBatch _ui;

        public UIScreen(FGame g, GraphicsDevice graphics) : base(g, graphics) {
            _ui = new UIBatch(graphics, g);
        }

        protected override void DoRender() {
            _ui.Render();
        }

        protected override void DoStep(GameTime elapsed) {
            _ui.Reset();
            _ui.DrawText("main", "Welcome to F7", 300, 100, 0.5f, Color.White);
            _ui.DrawBox(new Rectangle(300, 400, 128, 64), 0.4f);
            _ui.DrawImage("portrait_aeris", 600, 200, 0.5f);
            _ui.DrawBox(new Rectangle(650, 250, 128, 64), 0.4f);
        }
    }
}
