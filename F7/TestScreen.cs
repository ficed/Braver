using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace Braver {
    internal class TestScreen : Screen {

        public override Color ClearColor => Color.AliceBlue;

        private Field.FieldModel _model;
        private Viewer _viewer;
        private int _anim;
        private string[] _anims = new[] { "ACFE.a", "AAFF.a", "AAGA.a", "BVJF.a" };

        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);

            graphics.BlendState = BlendState.AlphaBlend;

            _model = new Field.FieldModel(graphics, g, 0, "AAAA.hrc", _anims, "field");
            _model.PlayAnimation(_anim, true, 1f);
            _model.Scale = 0.5f;
            //_model.Rotation2 = new Vector3(0, 0, 180);

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
                _model.PlayAnimation(_anim, true, 1f);
            }
        }

        protected override void DoRender() {
            Graphics.DepthStencilState = DepthStencilState.Default;
            Graphics.RasterizerState = RasterizerState.CullClockwise;
            _model.Render(_viewer);
        }

        private float _z = 5;
        private int _frame;
        protected override void DoStep(GameTime elapsed) {
            //_model.Rotation = new Vector3(0, 0, 90);
            //_model.Translation = new Vector3(0, 0, _z);
            if ((_frame++ % 4) == 0)
                _model.FrameStep();
        }
    }
}
