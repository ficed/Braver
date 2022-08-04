using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Braver.Field {

    public class FieldCameraInfo {
        [XmlAttribute]
        public float Width { get; set; }
        [XmlAttribute]
        public float Height { get; set; }
        [XmlAttribute]
        public float CenterX { get; set; }
        [XmlAttribute]
        public float CenterY { get; set; }
    }
    public class FieldInfo {
        [XmlElement("Camera")]
        public List<FieldCameraInfo> Cameras { get; set; } = new();
    }

    public class FieldLine {
        public Vector3 P0 { get; set; }
        public Vector3 P1 { get; set; }
    }

    public class FieldScreen : Screen {

        private OrthoView3D _view3D; 
        private View2D _view2D;
        private FieldDebug _debug;
        private FieldInfo _info;
        private Vector2 _base3DOffset;

        private bool _debugMode = false;
        private bool _renderBG = true, _renderDebug = true, _renderModels = true;

        public override Color ClearColor => Color.Black;

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
                        Scale = float.Parse(m.Scale) / 128f,
                        Rotation2 = new Vector3(0, 0, 180),
                    })
                    .ToList();

                using (var sinfo = g.TryOpen("field", file + ".xml")) {
                    if (sinfo != null) {
                        _info = Serialisation.Deserialise<FieldInfo>(sinfo);
                    } else
                        _info = new FieldInfo();
                }

                var cam = field.GetCameraMatrices().First();

                float camWidth, camHeight;
                if (_info.Cameras.Any()) {
                    camWidth = _info.Cameras[0].Width;
                    camHeight = _info.Cameras[0].Height;
                    _base3DOffset = new Vector2(_info.Cameras[0].CenterX, _info.Cameras[0].CenterY);
                } else {
                    //Autodetect...
                    var testCam = new OrthoView3D {
                        CameraPosition = new Vector3(cam.CameraPosition.X, cam.CameraPosition.Z, cam.CameraPosition.Y),
                        CameraForwards = new Vector3(cam.Forwards.X, cam.Forwards.Z, cam.Forwards.Y),
                        CameraUp = new Vector3(cam.Up.X, cam.Up.Z, cam.Up.Y),
                        Width = 1280,
                        Height = 720,
                    };
                    var vp = testCam.View * testCam.Projection;
                    var tris = field.GetWalkmesh().Triangles;

                    Vector3 Project(Ficedula.FF7.Field.FieldVertex v) {
                        return Vector3.Transform(new Vector3(v.X, v.Y, v.Z), vp);
                    }

                    Vector3 vMin, vMax;
                    vMin = vMax = Project(tris[0].V0);

                    foreach(var wTri in tris) {
                        Vector3 v0 = Vector3.Transform(new Vector3(wTri.V0.X, wTri.V0.Y, wTri.V0.Z), vp),
                            v1 = Vector3.Transform(new Vector3(wTri.V1.X, wTri.V1.Y, wTri.V1.Z), vp),
                            v2 = Vector3.Transform(new Vector3(wTri.V2.X, wTri.V2.Y, wTri.V2.Z), vp);
                        vMin = new Vector3(
                            Math.Min(vMin.X, Math.Min(Math.Min(v0.X, v1.X), v2.X)),
                            Math.Min(vMin.Y, Math.Min(Math.Min(v0.Y, v1.Y), v2.Y)),
                            Math.Min(vMin.Z, Math.Min(Math.Min(v0.Z, v1.Z), v2.Z))
                        );
                        vMax = new Vector3(
                            Math.Max(vMax.X, Math.Max(Math.Max(v0.X, v1.X), v2.X)),
                            Math.Max(vMax.Y, Math.Max(Math.Max(v0.Y, v1.Y), v2.Y)),
                            Math.Max(vMax.Z, Math.Max(Math.Max(v0.Z, v1.Z), v2.Z))
                        );
                    }

                    var allW = tris.SelectMany(t => new[] { t.V0, t.V1, t.V2 });
                    Vector3 wMin = new Vector3(allW.Min(v => v.X), allW.Min(v => v.Y), allW.Min(v => v.Z)),
                        wMax = new Vector3(allW.Max(v => v.X), allW.Max(v => v.Y), allW.Max(v => v.Z)); 

                    float xRange = (vMax.X - vMin.X) * 0.5f,
                        yRange = (vMax.Y - vMin.Y) * 0.5f;

                    //So now we know the walkmap would cover xRange screens across and yRange screens down
                    //Compare that to the background width/height and scale it to match...

                    System.Diagnostics.Debug.WriteLine($"Walkmap range {wMin} - {wMax}");
                    System.Diagnostics.Debug.WriteLine($"Transformed {vMin} - {vMax}");
                    System.Diagnostics.Debug.WriteLine($"Walkmap covers range {xRange}/{yRange}");
                    System.Diagnostics.Debug.WriteLine($"Background is size {Background.Width} x {Background.Height}");
                    System.Diagnostics.Debug.WriteLine($"Background covers {Background.Width / 320f} x {Background.Height / 240f} screens");
                    System.Diagnostics.Debug.WriteLine($"...or in widescreen, {Background.Width / 427f} x {Background.Height / 240f} screens");

                    camWidth = 1280f * xRange / (Background.Width / 320f);
                    camHeight = 720f * yRange / (Background.Height / 240f);
                    System.Diagnostics.Debug.WriteLine($"Auto calculated ortho w/h to {camWidth}/{camHeight}");

                    camWidth = 1280f * xRange / (Background.Width / 427f);
                    camHeight = 720f * yRange / (Background.Height / 240f);
                    System.Diagnostics.Debug.WriteLine($"...or in widescreen, {camWidth}/{camHeight}");

                    _base3DOffset = Vector2.Zero;
                }

                _view3D = new OrthoView3D {
                    CameraPosition = new Vector3(cam.CameraPosition.X, cam.CameraPosition.Z, cam.CameraPosition.Y),
                    CameraForwards = new Vector3(cam.Forwards.X, cam.Forwards.Z, cam.Forwards.Y),
                    CameraUp = new Vector3(cam.Up.X, cam.Up.Z, cam.Up.Y),
                    Width = camWidth,
                    Height = camHeight,
                    CenterX = _base3DOffset.X,
                    CenterY = _base3DOffset.Y,
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

            Entity.DEBUG_OUT = false;
        }

        private int _nextModelIndex = 0;
        public int GetNextModelIndex() {
            return _nextModelIndex++;
        }

        protected override void DoRender() {
            if (_renderBG)
                Background.Render(_view2D);

            if (_renderDebug)
                _debug.Render(_view3D);

            if (_renderModels)
                foreach (var entity in Entities)
                    entity.Model?.Render(_view3D);
        }

        int frame = 0;
        protected override void DoStep(GameTime elapsed) {
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

            if (input.IsJustDown(InputKey.Debug1))
                _renderBG = !_renderBG;
            if (input.IsJustDown(InputKey.Debug2))
                _renderDebug = !_renderDebug;
            if (input.IsJustDown(InputKey.Debug3))
                _renderModels = !_renderModels;

            if (input.IsJustDown(InputKey.Debug5))
                Entity.DEBUG_OUT = !Entity.DEBUG_OUT;


            if (_debugMode) {
                if (input.IsAnyDirectionDown() || input.IsJustDown(InputKey.Select)) {

                    //Now calculate 3d scroll amount
                    var _3dScrollAmount = new Vector2(_view3D.Width / 427f, _view3D.Height / 240f);
                    System.Diagnostics.Debug.WriteLine($"To scroll 3d view by one BG pixel, it will move {_3dScrollAmount}");

                    if (input.IsJustDown(InputKey.Select)) {
                        _view3D.CenterX += _3dScrollAmount.X * _view2D.CenterX / -3;
                        _view3D.CenterY += _3dScrollAmount.Y * _view2D.CenterY / -3;
                    }

                    if (input.IsDown(InputKey.OK)) {
                        if (input.IsDown(InputKey.Up)) {
                            _view2D.CenterY += 3;
                            _view3D.CenterY += _3dScrollAmount.Y;
                        }
                        if (input.IsDown(InputKey.Down)) {
                            _view2D.CenterY -= 3;
                            _view3D.CenterY -= _3dScrollAmount.Y;
                        }
                        if (input.IsDown(InputKey.Left)) {
                            _view2D.CenterX -= 3;
                            _view3D.CenterX -= _3dScrollAmount.X;
                        }
                        if (input.IsDown(InputKey.Right)) {
                            _view2D.CenterX += 3;
                            _view3D.CenterX += _3dScrollAmount.X;
                        }

                    } else if (input.IsDown(InputKey.Cancel)) {
                        if (input.IsDown(InputKey.Up))
                            _view2D.CenterY++;
                        if (input.IsDown(InputKey.Down))
                            _view2D.CenterY--;
                        if (input.IsDown(InputKey.Left))
                            _view2D.CenterX--;
                        if (input.IsDown(InputKey.Right))
                            _view2D.CenterX++;

                    } else {
                        if (input.IsDown(InputKey.Menu)) {

                            if (input.IsDown(InputKey.Up))
                                _view3D.Height++;
                            if (input.IsDown(InputKey.Down))
                                _view3D.Height--;
                            if (input.IsDown(InputKey.Left))
                                _view3D.Width--;
                            if (input.IsDown(InputKey.Right))
                                _view3D.Width++;

                        } else {
                            if (input.IsDown(InputKey.Up)) {
                                _view3D.CenterY++;
                            }
                            if (input.IsDown(InputKey.Down)) {
                                _view3D.CenterY--;
                            }
                            if (input.IsDown(InputKey.Left)) {
                                _view3D.CenterX--;
                            }
                            if (input.IsDown(InputKey.Right)) {
                                _view3D.CenterX++;
                            }
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"View2D Center: {_view2D.CenterX}/{_view2D.CenterY}");
                    System.Diagnostics.Debug.WriteLine($"View3D: {_view3D}");
                }
            }
        }
    }
}
