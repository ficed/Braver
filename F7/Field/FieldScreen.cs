using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace F7.Field {
    public class FieldScreen : Screen {

        private View3D _view3D; 
        private Viewer _view2D;
        private FieldDebug _debug;

        private bool _debugMode = false;

        public Background Background { get; }
        public List<Entity> Entities { get; }
        public FieldModel Player { get; set; }
        public List<FieldModel> FieldModels { get; }
        public Ficedula.FF7.Field.DialogEvent Dialog { get; }

        public FieldScreen(string file, FGame g, GraphicsDevice graphics) : base(g, graphics) {
            using (var s = g.Open("field", file)) {
                var field = new Ficedula.FF7.Field.FieldFile(s);
                Background = new Background(graphics, field.GetBackground());
                Dialog = field.GetDialogEvent();

                Entities = Dialog.Entities
                    .Select(e => new Entity(e, this))
                    .ToList();

                FieldModels = field.GetModels()
                    .Models
                    .Select(m => new FieldModel(
                        graphics, g, m.HRC, 
                        m.Animations.Select(s => System.IO.Path.ChangeExtension(s, ".a"))
                    ) {
                        //Scale = float.Parse(m.Scale)
                    })
                    .ToList();

                var cam = field.GetCameraMatrices().First();
                _view3D = new OrthoView3D {
                    CameraPosition = new Vector3(cam.CameraPosition.X, cam.CameraPosition.Z, cam.CameraPosition.Y),
                    CameraForwards = new Vector3(cam.Forwards.X, cam.Forwards.Z, cam.Forwards.Y),
                    CameraUp = new Vector3(cam.Up.X, cam.Up.Z, cam.Up.Y),
                    Width = cam.Zoom,
                    Height = cam.Zoom * 720f / 1280f,
                };

                _debug = new FieldDebug(graphics, field);
            }

            g.Memory.ResetScratch();

            foreach (var entity in Entities) {
                entity.Call(0, 0, null);
                entity.Run(true);
            }

            _view2D = new View2D {
                Width = 1280,
                Height = 720
            };
        }

        private int _nextModelIndex = 0;
        public int GetNextModelIndex() {
            return _nextModelIndex++;
        }

        public override void Render() {
            Background.Render(_view2D);
            _debug.Render(_view3D);
            foreach (var entity in Entities)
                entity.Model?.Render(_view3D);
        }

        int frame = 0;
        public override void Step(GameTime elapsed) {
            if ((frame++ % 4) == 0) {
                foreach (var entity in Entities) {
                    entity.Run();
                    entity.Model?.FrameStep();
                }
            }
            Background.Step();
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);
            if (input.IsJustDown(InputKey.Start))
                _debugMode = !_debugMode;

            if (_debugMode) {
                var forwards = new Vector3(_view3D.CameraForwards.X, _view3D.CameraForwards.Y, 0);
                forwards.Normalize();
                var right = new Vector3(forwards.Y, forwards.X, 0);

                if (input.IsDown(InputKey.Up)) {
                    _view3D.CameraPosition += forwards;
                }
                if (input.IsDown(InputKey.Down)) {
                    _view3D.CameraPosition -= forwards;
                }
                if (input.IsDown(InputKey.Left)) {
                    _view3D.CameraPosition += right;
                }
                if (input.IsDown(InputKey.Right)) {
                    _view3D.CameraPosition -= right;
                }
            }
        }
    }
}
