using Ficedula.FF7.Field;
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
        public bool Active { get; set; } = true;

        public bool IntersectsWith(Vector3 entity, float entityRadius) {
            if (!Active)
                return false;

            var dist =
                ((P1.X - P0.X) * (P0.Y - entity.Y) - (P0.X - entity.X) * (P1.Y - P0.Y))
                /
                (P1.XY() - P0.XY()).Length();
            return dist < entityRadius;
        }
    }

    [Flags]
    public enum FieldOptions {
        None = 0,
        PlayerControls = 0x1,
        //LinesActive = 0x2,
        MenuEnabled = 0x4, 
        CameraTracksPlayer = 0x8,
        CameraIsAsyncScrolling = 0x10,

        DEFAULT = PlayerControls | MenuEnabled | CameraTracksPlayer,  
    }

    public class FieldScreen : Screen {

        private OrthoView3D _view3D; 
        private View2D _view2D;
        private FieldDebug _debug;
        private FieldInfo _info;
        private Vector2 _base3DOffset;

        private bool _debugMode = false;
        private bool _renderBG = true, _renderDebug = true, _renderModels = true;
        private float _controlRotation;

        private List<WalkmeshTriangle> _walkmesh;

        private FieldDestination _avatarDestination;

        public override Color ClearColor => Color.Black;

        public Background Background { get; }
        public List<Entity> Entities { get; }
        public Entity Player { get; private set; }
        public List<FieldModel> FieldModels { get; }
        public DialogEvent FieldDialog { get; }
        public FieldOptions Options { get; set; } = FieldOptions.DEFAULT;
        public Dialog Dialog { get; }

        public FieldScreen(FieldDestination destination, FGame g, GraphicsDevice graphics) : base(g, graphics) {

            var mapList = g.Singleton(() => new MapList(g.Open("field", "maplist")));
            string file = mapList.Items[destination.DestinationFieldID];

            using (var s = g.Open("field", file)) {
                var field = new FieldFile(s);
                Background = new Background(graphics, field.GetBackground());
                FieldDialog = field.GetDialogEvent();

                Entities = FieldDialog.Entities
                    .Select(e => new Entity(e, this))
                    .ToList();

                FieldModels = field.GetModels()
                    .Models
                    .Select(m => {
                        var model = new FieldModel(
                            graphics, g, m.HRC,
                            m.Animations.Select(s => System.IO.Path.ChangeExtension(s, ".a"))
                        ) {
                            Scale = float.Parse(m.Scale) / 128f,
                            Rotation2 = new Vector3(0, 0, 180),
                        };
                        model.Translation2 = new Vector3(
                            0, 
                            0,
                            model.Scale * model.MaxBounds.Y
                        );
                        return model;
                    })
                    .ToList();

                var tg = field.GetTriggersAndGateways();
                _controlRotation = 360f * tg.ControlDirection / 256f;

                _walkmesh = field.GetWalkmesh().Triangles;

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

                    Vector3 Project(FieldVertex v) {
                        return Vector3.Transform(new Vector3(v.X, v.Y, v.Z), vp);
                    }

                    Vector3 vMin, vMax;
                    vMin = vMax = Project(_walkmesh[0].V0);

                    foreach(var wTri in _walkmesh) {
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

                    var allW = _walkmesh.SelectMany(t => new[] { t.V0, t.V1, t.V2 });
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

            Dialog = new Dialog(g, graphics);

            _avatarDestination = destination;

            g.Memory.ResetScratch();

            foreach (var entity in Entities) {
                entity.Call(0, 0, null);
                entity.Run(true);
            }

            _view2D = new View2D {
                Width = 1280,
                Height = 720,
                ZNear = 0,
                ZFar = -1,
            };

            Entity.DEBUG_OUT = false;
        }

        private int _nextModelIndex = 0;
        public int GetNextModelIndex() {
            return _nextModelIndex++;
        }

        protected override void DoRender() {
            Graphics.DepthStencilState = DepthStencilState.Default;
            Graphics.BlendState = BlendState.AlphaBlend;
            if (_renderBG)
                Background.Render(_view2D);

            if (_renderDebug)
                _debug.Render(_view3D);

            if (_renderModels)
                foreach (var entity in Entities)
                    if ((entity.Model != null) && entity.Model.Visible)
                        entity.Model.Render(_view3D);

            Dialog.Render();
        }

        private class FrameProcess {
            public int Frames;
            public Func<int, bool> Process;
        }

        private List<FrameProcess> _processes = new();

        public void StartProcess(Func<int, bool> process) {
            _processes.Add(new FrameProcess { Process = process });
        }

        int frame = 0;
        protected override void DoStep(GameTime elapsed) {
            if ((frame++ % 4) == 0) {
                foreach (var entity in Entities) {
                    entity.Run();
                    entity.Model?.FrameStep();
                }
            }

            for (int i = _processes.Count - 1; i >= 0; i--) {
                var process = _processes[i];
                if (process.Process(process.Frames++))
                    _processes.RemoveAt(i);
            }

            Dialog.Step();
            Background.Step();
        }

        public (int x, int y) GetBGScroll() {
            return (
                (int)(_view2D.CenterX / 3),
                (int)(_view2D.CenterY / 3)
            );
        }
        public void BGScroll(float x, float y) {
            BGScrollOffset(x - (_view2D.CenterX / 3), y - (_view2D.CenterY / 3));
        }
        public void BGScrollOffset(float ox, float oy) {
            var _3dScrollAmount = new Vector2(_view3D.Width / 427f, _view3D.Height / 240f);
            _view2D.CenterX += 3 * ox;
            _view3D.CenterX += _3dScrollAmount.X * ox;
            _view2D.CenterY += 3 * oy;
            _view3D.CenterY += _3dScrollAmount.Y * oy;
        }

        private void ReportAllModelPositions() {
            foreach(var entity in Entities.Where(e => e.Model != null)) {
                System.Diagnostics.Debug.WriteLine($"Entity {entity.Name} at pos {entity.Model.Translation}, 2D background pos {ModelToBGPosition(entity.Model.Translation)}");
            }
        }

        public Vector2 ClampBGScrollToViewport(Vector2 bgScroll) {
            int minX, maxX, minY, maxY;

            if (Background.Width < (1280f / 3))
                minX = maxX = 0;
            else {
                minX = Background.MinX + (1280 / 3) / 2;
                maxX = (Background.MinX + Background.Width) - (1280 / 3) / 2;
            }

            if (Background.Height < (720f / 3))
                minY = maxY = 0;
            else {
                minY = Background.MinY + (720 / 3) / 2;
                maxY = (Background.MinY + Background.Height) - (730 / 3) / 2;
            }

            return new Vector2(
                Math.Min(Math.Max(minX, bgScroll.X), maxX),
                Math.Min(Math.Max(minY, bgScroll.Y), maxY)
            );
        }

        public Vector2 ModelToBGPosition(Vector3 modelPosition) {
            var vp = _view3D.View * _view3D.Projection;
            var screenPos = Vector3.Transform(modelPosition, vp);

            float tx = (_view2D.CenterX / 3) + screenPos.X * 0.5f * (1280f / 3),
                  ty = (_view2D.CenterY / 3) + screenPos.Y * 0.5f * (720f / 3);

            return new Vector2(tx, ty);
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);
            if (input.IsJustDown(InputKey.Start))
                _debugMode = !_debugMode;

            if (input.IsJustDown(InputKey.Debug1))
                _renderBG = !_renderBG;
            if (input.IsJustDown(InputKey.Debug2))
                _renderDebug = !_renderDebug;
            if (input.IsJustDown(InputKey.Debug3)) {
                _renderModels = !_renderModels;
                ReportAllModelPositions();
            }

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
            } else {

                if (Dialog.IsActive) {
                    Dialog.ProcessInput(input);
                    return;
                }

                if (input.IsJustDown(InputKey.Menu) && Options.HasFlag(FieldOptions.MenuEnabled)) {
                    Game.PushScreen(new UI.Layout.LayoutScreen(Game, Graphics, "MainMenu"));
                    return;
                }

                //Normal controls
                if ((Player != null) && Options.HasFlag(FieldOptions.PlayerControls)) {

                    if (input.IsJustDown(InputKey.OK) && (Player != null)) {
                        var talkTo = Player.CollidingWith
                            .Where(e => e.Flags.HasFlag(EntityFlags.CanTalk))
                            .FirstOrDefault();
                        if (talkTo != null) {
                            if (!talkTo.Call(7, 1, null))
                                System.Diagnostics.Debug.WriteLine($"Could not start talk script for entity {talkTo}");
                        }
                    }

                    int desiredAnim = 0;
                    float animSpeed = 1f;
                    if (input.IsAnyDirectionDown() && Options.HasFlag(FieldOptions.PlayerControls)) {
                        //TODO actual use controldirection
                        var forwards = _view3D.CameraForwards.WithZ(0);
                        forwards.Normalize();
                        var right = Vector3.Transform(forwards, Matrix.CreateRotationZ(90f * (float)Math.PI / 180f));
                        var move = Vector2.Zero;

                        if (input.IsDown(InputKey.Up))
                            move += new Vector2(forwards.X, forwards.Y);
                        else if (input.IsDown(InputKey.Down))
                            move -= new Vector2(forwards.X, forwards.Y);

                        if (input.IsDown(InputKey.Left))
                            move += new Vector2(right.X, right.Y);
                        else if (input.IsDown(InputKey.Right))
                            move -= new Vector2(right.X, right.Y);

                        if (move != Vector2.Zero) {
                            move.Normalize();
                            if (input.IsDown(InputKey.Cancel)) {
                                animSpeed = 5f;
                                move *= 5f;
                                desiredAnim = 2;
                            } else
                                desiredAnim = 1;

                            TryWalk(Player, Player.Model.Translation + new Vector3(move.X, move.Y, 0), true);
                            Player.Model.Rotation = Player.Model.Rotation.WithZ((float)(Math.Atan2(-move.X, -move.Y) * 180f / Math.PI));

                            var oldLines = Player.LinesCollidingWith.ToArray();
                            Player.LinesCollidingWith.Clear();
                            foreach (var lineEnt in Entities.Where(e => e.Line != null)) {
                                if (lineEnt.Line.IntersectsWith(Player.Model.Translation, Player.CollideDistance))
                                    Player.LinesCollidingWith.Add(lineEnt);
                            }

                            foreach(var entered in Player.LinesCollidingWith.Except(oldLines)) {
                                System.Diagnostics.Debug.WriteLine($"Player has entered line {entered}");
                                entered.Call(3, 5, null); //TODO PRIORITY!?!
                            }
                            foreach (var left in oldLines.Except(Player.LinesCollidingWith)) {
                                System.Diagnostics.Debug.WriteLine($"Player has left line {left}");
                                left.Call(3, 6, null); //TODO PRIORITY!?!
                            }

                            if (Options.HasFlag(FieldOptions.CameraTracksPlayer)) {
                                var posOnBG = ModelToBGPosition(Player.Model.Translation);
                                var scroll = GetBGScroll();
                                var newScroll = scroll;
                                if (posOnBG.X > (scroll.x + 150))
                                    newScroll.x = (int)posOnBG.X - 150;
                                else if (posOnBG.X < (scroll.x - 150))
                                    newScroll.x = (int)posOnBG.X + 150;

                                if (posOnBG.Y > (scroll.y + 100))
                                    newScroll.y = (int)posOnBG.Y - 100;
                                else if (posOnBG.Y < (scroll.y - 110))
                                    newScroll.y = (int)posOnBG.Y + 110;

                                if (newScroll != scroll)
                                    BGScroll(newScroll.x, newScroll.y);
                            }
                        } else {
                            //
                        }
                    }

                    if ((Player.Model.AnimationState.Animation != desiredAnim) || (Player.Model.AnimationState.AnimationSpeed != animSpeed))
                        Player.Model.PlayAnimation(desiredAnim, true, animSpeed, null);
                }
            }
        }

        private static bool LineIntersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, out float aDist) {
            float denominator = ((a1.X - a0.X) * (b1.Y - b0.Y)) - ((a1.Y - a0.Y) * (b1.X - b0.X));
            float numerator1 = ((a0.Y - b0.Y) * (b1.X - b0.X)) - ((a0.X - b0.X) * (b1.Y - b0.Y));
            float numerator2 = ((a0.Y - b0.Y) * (a1.X - a0.X)) - ((a0.X - b0.X) * (a1.Y - a0.Y));


            if (denominator == 0) {
                aDist = 0; 
                return numerator1 == 0 && numerator2 == 0;
            }

            aDist = numerator1 / denominator;
            float s = numerator2 / denominator;

            return (aDist >= 0 && aDist <= 1) && (s >= 0 && s <= 1);
        }

        public bool TryWalk(Entity eMove, Vector3 newPosition, bool doCollide) {
            //TODO: Collision detection against other models!

            if (doCollide) {
                eMove.CollidingWith.Clear();

                var toCheck = Entities
                    .Where(e => e.Flags.HasFlag(EntityFlags.CanCollide))
                    .Where(e => e.Model != null)
                    .Where(e => e != eMove);

                foreach (var entity in toCheck) {
                    if (entity.Model != null) {
                        var dist = (entity.Model.Translation.XY() - newPosition.XY()).Length();
                        var collision = eMove.CollideDistance + entity.CollideDistance;
                        if (dist <= collision) {
                            System.Diagnostics.Debug.WriteLine($"Entity {eMove} is now colliding with {entity}");
                            eMove.CollidingWith.Add(entity);
                        }
                    }
                }
                if (eMove.CollidingWith.Any())
                    return false;
            }

            var currentTri = _walkmesh[eMove.WalkmeshTri];
            var newHeight = HeightInTriangle(currentTri.V0.ToX(), currentTri.V1.ToX(), currentTri.V2.ToX(), newPosition.X, newPosition.Y);
            if (newHeight != null) {
                //We're staying in the same tri, so just update height
                eMove.Model.Translation = newPosition.WithZ(newHeight.Value);
                return true;
            } else {
                short? newTri;
                if (LineIntersect(
                    currentTri.V0.ToX().XY(),
                    currentTri.V1.ToX().XY(),
                    eMove.Model.Translation.XY(),
                    newPosition.XY(),
                    out float dist
                    )) {
                    newTri = currentTri.V01Tri;
                } else if (LineIntersect(
                    currentTri.V1.ToX().XY(),
                    currentTri.V2.ToX().XY(),
                    eMove.Model.Translation.XY(),
                    newPosition.XY(),
                    out dist
                    )) {
                    newTri = currentTri.V12Tri;
                } else if (LineIntersect(
                    currentTri.V2.ToX().XY(),
                    currentTri.V0.ToX().XY(),
                    eMove.Model.Translation.XY(),
                    newPosition.XY(),
                    out dist
                    )) {
                    newTri = currentTri.V20Tri;
                } else {
                    System.Diagnostics.Debug.WriteLine($"Moving from {eMove.Model.Translation} to {newPosition}");
                    System.Diagnostics.Debug.WriteLine($"V0 {currentTri.V0}, V1 {currentTri.V1}, V2 {currentTri.V2}");
                    throw new Exception($"Reality failure: Can't find route out of triangle");
                }

                if (newTri == null) {
                    //Just can't leave by this side, oh well
                    return false;
                } else {
                    var movingToTri = _walkmesh[newTri.Value];
                    newHeight = HeightInTriangle(
                        movingToTri.V0.ToX(), movingToTri.V1.ToX(), movingToTri.V2.ToX(),
                        newPosition.X, newPosition.Y
                    );
                    if (newHeight == null) {
                        //Argh, we've moved straight through a triangle? TODO: HANDLE THIS!
                        throw new Exception($"This needs handling");
                    } else {
                        eMove.WalkmeshTri = newTri.Value;
                        eMove.Model.Translation = newPosition.WithZ(newHeight.Value);
                        return true;
                    }
                }
            }
        }

        private static float? HeightInTriangle(Vector3 p0, Vector3 p1, Vector3 p2, float x, float y) {
            var denominator = (p1.Y - p2.Y) * (p0.X - p2.X) + (p2.X - p1.X) * (p0.Y - p2.Y);
            var a = ((p1.Y - p2.Y) * (x - p2.X) + (p2.X - p1.X) * (y - p2.Y)) / denominator;
            var b = ((p2.Y - p0.Y) * (x - p2.X) + (p0.X - p2.X) * (y - p2.Y)) / denominator;
            var c = 1 - a - b;

            if (a < 0) return null;
            if (b < 0) return null;
            if (c < 0) return null;
            if (a > 1) return null;
            if (b > 1) return null;
            if (c > 1) return null;

            return p0.Z * a + p1.Z * b + p2.Z * c;
        }

        public void DropToWalkmesh(Entity e, Vector2 position, int walkmeshTri) {
            var tri = _walkmesh[walkmeshTri];

            e.Model.Translation = new Vector3(
                position.X,
                position.Y,
                HeightInTriangle(tri.V0.ToX(), tri.V1.ToX(), tri.V2.ToX(), position.X, position.Y).Value
            );
            e.WalkmeshTri = walkmeshTri;
        }

        public void CheckPendingPlayerSetup() {
            if ((_avatarDestination != null) && (Player.Model != null)) {
                DropToWalkmesh(Player, new Vector2(_avatarDestination.X, _avatarDestination.Y), _avatarDestination.Triangle);
                Player.Model.Rotation = new Vector3(0, 0, 360f * _avatarDestination.Orientation / 255f);
                _avatarDestination = null;
            }
        }

        public void SetPlayer(int whichEntity) {
            Player = Entities[whichEntity]; //TODO: also center screen etc.
            CheckPendingPlayerSetup();
        }

        public void SetPlayerControls(bool enabled) {
            if (enabled)
                Options |= FieldOptions.PlayerControls | FieldOptions.CameraTracksPlayer; //Seems like cameratracksplayer MUST be turned on now or things break...?
            else {
                Options &= ~FieldOptions.PlayerControls;
                if (Player?.Model != null)
                    Player.Model.PlayAnimation(0, true, 1f, null);
                //TODO - is this reasonable? Disable current (walking) animation when we take control away from the player? 
                //(We don't want e.g. walk animation to be continuing after our control is disabled and we're not moving any more!)
            }
        }
    }
}
