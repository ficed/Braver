using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace F7 {
    public abstract class Screen {

        public FGame Game { get; }

        protected GraphicsDevice _graphics;

        protected Screen(FGame g, GraphicsDevice graphics) {
            Game = g;
            _graphics = graphics;
        }

        public abstract void Step(GameTime elapsed);
        public abstract void Render();

        public virtual void ProcessInput(InputState input) { }
    }
}
