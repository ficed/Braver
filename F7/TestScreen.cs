using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {
    internal class TestScreen : Screen {

        public override Color ClearColor => Color.AliceBlue;

        private Field.FieldModel _model;
        private Viewer _viewer;
        private int _anim;
        private string[] _anims = new[] { "aze.a", "baa.a", "bab.a", "bac.a", "bad.a", "bae.a",
                        "bba.a", "bbb.a", "bbc.a", "bbd.a", "bid.a", "bie.a",
                        "bja.a", "bjb.a", "bjc.a", "bjd.a", "bje.a", "bka.a",
                        "bkb.a", "bkc.a" };
        public TestScreen(FGame g, GraphicsDevice graphics) : base(g, graphics) {

            graphics.BlendState = BlendState.AlphaBlend;

            _model = new Field.FieldModel(graphics, g, "BBE.hrc", _anims, "wm");
            _model.PlayAnimation(_anim, true, 1f, null);

            _viewer = new PerspView3D {
                CameraPosition = new Vector3(0, 50f, 10f),
                CameraForwards = new Vector3(0, -50f, -5f),
                CameraUp = Vector3.UnitZ,                
            };
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);
            if (input.IsJustDown(InputKey.OK)) {
                _anim = (_anim + 1) % _anims.Length;
                System.Diagnostics.Debug.WriteLine($"Anim: {_anims[_anim]}");
                _model.PlayAnimation(_anim, true, 1f, null);
            }
        }

        protected override void DoRender() {
            _model.Render(_viewer);
        }

        private float _z = 5;
        protected override void DoStep(GameTime elapsed) {
            _model.Rotation = new Vector3(0, 0, 90);
            _model.Translation = new Vector3(0, 0, _z);
            _model.FrameStep();
        }
    }
}
