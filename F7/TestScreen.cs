using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace F7 {
    internal class TestScreen : Screen {

        private Field.FieldModel _model;
        private Viewer _viewer;

        public TestScreen(FGame g, GraphicsDevice graphics) : base(g, graphics) {

            graphics.BlendState = BlendState.AlphaBlend;

            _model = new Field.FieldModel(graphics, g, "AUFF.hrc", new[] { "AVBF.a", "AVCA.a" });
            _model.PlayAnimation(1, true, 1f, null);

            _viewer = new PerspView3D {
                CameraPosition = new Vector3(0, -50f, 10f),
                CameraForwards = new Vector3(0, 50f, -5f),
                CameraUp = Vector3.UnitZ,                
            };
        }

        protected override void DoRender() {
            _model.Render(_viewer);
        }

        protected override void DoStep(GameTime elapsed) {
            _model.FrameStep();
        }
    }
}
