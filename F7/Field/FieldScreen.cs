using Ficedula.FF7.Field;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Braver.Field {

    public class BattleOptions {
        public string OverrideMusic { get; set; }
        public string PostBattleMusic { get; set; } //will play in field
        public bool BattlesEnabled { get; set; } = true; //TODO - reasonable default?
        public Battle.BattleFlags Flags { get; set; } = Battle.BattleFlags.None;
    }

    public class FieldInfo {
        public float OriginalBGZFrom { get; set; }
        public float OriginalBGZTo { get; set; }
        public float BGZFrom { get; set; }
        public float BGZTo { get; set; }
    }

    public class FieldLine {
        public Vector3 P0 { get; set; }
        public Vector3 P1 { get; set; }
        public bool Active { get; set; } = true;

        public bool IntersectsWith(FieldModel m, float entityRadius) {
            if (!Active)
                return false;

            if ((m.Translation.Z - 5) > Math.Max(P0.Z, P1.Z)) return false;
            float entHeight = (m.MaxBounds.Y - m.MinBounds.Y) * m.Scale;
            if ((m.Translation.Z + entHeight + 5) < Math.Min(P0.Z, P1.Z)) return false;

            return GraphicsUtil.LineCircleIntersect(P0.XY(), P1.XY(), m.Translation.XY(), entityRadius);
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
        MusicLocked = 0x20,
        UseMovieCam = 0x40,

        NoScripts = 0x100,

        DEFAULT = PlayerControls | MenuEnabled | CameraTracksPlayer | UseMovieCam,   
    }

    public class FieldScreen : Screen, Net.IListen<Net.FieldModelMessage>, Net.IListen<Net.FieldBGMessage>,
        Net.IListen<Net.FieldEntityModelMessage>, Net.IListen<Net.FieldBGScrollMessage> {

        private PerspView3D _view3D;
        private View2D _view2D;
        private FieldDebug _debug;
        private FieldInfo _info;
        private float _bgZFrom = 1025f, _bgZTo = 1092f;
        private string _file;

        private bool _debugMode = false;
        private bool _renderBG = true, _renderDebug = false, _renderModels = true;
        private float _controlRotation;
        private bool _renderUI = true;

        private string _debugEntity = "___";

        private List<WalkmeshTriangle> _walkmesh;


        public override Color ClearColor => Color.Black;

        public Entity Player { get; private set; }

        public Action WhenPlayerSet { get; set; }

        public HashSet<int> DisabledWalkmeshTriangles { get; } = new HashSet<int>();
        public Background Background { get; private set; }
        public Movie Movie { get; private set; }
        public List<Entity> Entities { get; private set; }
        public List<FieldModel> FieldModels { get; private set; }
        public DialogEvent FieldDialog { get; private set; }
        public TriggersAndGateways TriggersAndGateways { get; private set; }

        private EncounterTable[] _encounters;
        public FieldOptions Options { get; set; } = FieldOptions.DEFAULT;
        public Dialog Dialog { get; private set; }
        public FieldUI FieldUI { get; private set; }
        public Overlay Overlay { get; private set; }
        public IInputCapture InputCapture { get; set; }

        public int BattleTable { get; set; }
        public BattleOptions BattleOptions { get; } = new();

        private HashSet<Trigger> _activeTriggers = new();

        private FieldDestination _destination;
        private short _fieldID;
        public FieldScreen(FieldDestination destination) {
            _destination = destination;
            _fieldID = destination.DestinationFieldID;
        }

        private void SetPlayerIfNecessary() {
            if (Player == null) {
                var autoPlayer = Entities
                    .Where(e => e.Character != null)
                    .FirstOrDefault(e => e.Character == Game.SaveData.Party.FirstOrDefault());
                if (autoPlayer != null)
                    SetPlayer(Entities.IndexOf(autoPlayer));
            }
        }

        private PerspView3D ViewFromCamera(CameraMatrix cam) {
            if (cam == null) return null;

            double fovy = (2 * Math.Atan(240.0 / (2.0 * cam.Zoom))) * 57.29577951;

            var camPosition = cam.CameraPosition.ToX() * 4096f;

            var camDistances = _walkmesh
                .SelectMany(tri => new[] { tri.V0.ToX(), tri.V1.ToX(), tri.V2.ToX() })
                .Select(v => (camPosition - v).Length());

            float nearest = camDistances.Min(), furthest = camDistances.Max();

            return new PerspView3D {
                FOV = (float)fovy,
                ZNear = nearest * 0.75f,
                ZFar = furthest * 1.25f,
                CameraPosition = camPosition,
                CameraForwards = cam.Forwards.ToX(),
                CameraUp = cam.Up.ToX(),
            };
        }

        private static bool _isFirstLoad = true;

        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);

            UpdateSaveLocation();
            if (g.DebugOptions.AutoSaveOnFieldEntry && !_isFirstLoad)
                Game.AutoSave();
            _isFirstLoad = false;

            g.Net.Listen<Net.FieldModelMessage>(this);
            g.Net.Listen<Net.FieldBGMessage>(this);
            g.Net.Listen<Net.FieldEntityModelMessage>(this);
            g.Net.Listen<Net.FieldBGScrollMessage>(this);

            g.Audio.StopLoopingSfx(true);

            Overlay = new Overlay(g, graphics);

            g.Net.Send(new Net.FieldScreenMessage { Destination = _destination });

            FieldFile field;

            var mapList = g.Singleton(() => new MapList(g.Open("field", "maplist")));
            _file = mapList.Items[_destination.DestinationFieldID];
            var cached = g.Singleton(() => new CachedField());
            if (cached.FieldID == _destination.DestinationFieldID)
                field = cached.FieldFile;
            else {
                using (var s = g.Open("field", _file))
                    field = new FieldFile(s);
            }

            Background = new Background(g, graphics, field.GetBackground());
            Movie = new Movie(g, graphics);
            FieldDialog = field.GetDialogEvent();
            _encounters = field.GetEncounterTables().ToArray();

            Entities = FieldDialog.Entities
                .Select(e => new Entity(e, this))
                .ToList();

            FieldModels = field.GetModels()
                .Models
                .Select((m, index) => {
                    var model = new FieldModel(
                        graphics, g, index, m.HRC,
                        m.Animations.Select(s => System.IO.Path.ChangeExtension(s, ".a")),
                        globalLightColour: m.GlobalLightColor,
                        light1Colour: m.Light1Color, light1Pos: m.Light1Pos.ToX(),
                        light2Colour: m.Light2Color, light2Pos: m.Light2Pos.ToX(),
                        light3Colour: m.Light3Color, light3Pos: m.Light3Pos.ToX()
                    ) {
                        Scale = float.Parse(m.Scale) / 128f,
                        Rotation2 = new Vector3(0, 0, 0),
                    };
                    model.Translation2 = new Vector3(
                        0,
                        0,
                        model.Scale * model.MaxBounds.Y
                    );
                    return model;
                })
                .ToList();

            TriggersAndGateways = field.GetTriggersAndGateways();
            _controlRotation = 360f * TriggersAndGateways.ControlDirection / 256f;

            _walkmesh = field.GetWalkmesh().Triangles;

            using (var sinfo = g.TryOpen("field", _file + ".xml")) {
                if (sinfo != null) {
                    _info = Serialisation.Deserialise<FieldInfo>(sinfo);
                } else
                    _info = new FieldInfo();
            }

            var cam = field.GetCameraMatrices().First();

            /*
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

                System.Diagnostics.Trace.WriteLine($"Walkmap range {wMin} - {wMax}");
                System.Diagnostics.Trace.WriteLine($"Transformed {vMin} - {vMax}");
                System.Diagnostics.Trace.WriteLine($"Walkmap covers range {xRange}/{yRange}");
                System.Diagnostics.Trace.WriteLine($"Background is size {Background.Width} x {Background.Height}");
                System.Diagnostics.Trace.WriteLine($"Background covers {Background.Width / 320f} x {Background.Height / 240f} screens");
                System.Diagnostics.Trace.WriteLine($"...or in widescreen, {Background.Width / 427f} x {Background.Height / 240f} screens");

                camWidth = 1280f * xRange / (Background.Width / 320f);
                camHeight = 720f * yRange / (Background.Height / 240f);
                System.Diagnostics.Trace.WriteLine($"Auto calculated ortho w/h to {camWidth}/{camHeight}");

                camWidth = 1280f * xRange / (Background.Width / 427f);
                camHeight = 720f * yRange / (Background.Height / 240f);
                System.Diagnostics.Trace.WriteLine($"...or in widescreen, {camWidth}/{camHeight}");

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
            */

            _view3D = ViewFromCamera(cam);

            var vp = System.Numerics.Matrix4x4.CreateLookAt(
                cam.CameraPosition * 4096f, cam.CameraPosition * 4096f + cam.Forwards, cam.Up
            ) * System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView(
                (float)(_view3D.FOV * Math.PI / 180.0), 1280f / 720f, 0.001f * 4096f, 1000f * 4096f
            );

            float minZ = 1f, maxZ = 0f;
            foreach (var wTri in _walkmesh) {
                System.Numerics.Vector4 v0 = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(wTri.V0.X, wTri.V0.Y, wTri.V0.Z, 1), vp),
                    v1 = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(wTri.V1.X, wTri.V1.Y, wTri.V1.Z, 1), vp),
                    v2 = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(wTri.V2.X, wTri.V2.Y, wTri.V2.Z, 1), vp);
                /*
                System.Diagnostics.Trace.WriteLine(v0 / v0.W);
                System.Diagnostics.Trace.WriteLine(v1 / v1.W);
                System.Diagnostics.Trace.WriteLine(v2 / v2.W);
                */
                minZ = Math.Min(minZ, v0.Z / v0.W);
                minZ = Math.Min(minZ, v1.Z / v1.W);
                minZ = Math.Min(minZ, v2.Z / v2.W);
                maxZ = Math.Max(maxZ, v0.Z / v0.W);
                maxZ = Math.Max(maxZ, v1.Z / v1.W);
                maxZ = Math.Max(maxZ, v2.Z / v2.W);
            }
            System.Diagnostics.Trace.WriteLine($"Walkmesh Z varies from {minZ}-{maxZ} (recip {1f / minZ} to {1f / maxZ}");
            _debug = new FieldDebug(graphics, field);

            if (_info.BGZFrom != 0) {
                _bgZFrom = _info.BGZFrom;
                _bgZTo = _info.BGZTo;
            } else {
                _bgZFrom = Background.AutoDetectZFrom;
                _bgZTo = Background.AutoDetectZTo;
            }

            Dialog = new Dialog(g, graphics);
            FieldUI = new FieldUI(g, graphics);

            g.Memory.ResetScratch();

            _view2D = new View2D {
                Width = 1280,
                Height = 720,
                ZNear = 0,
                ZFar = -1,
            };

            if (g.Net is Net.Server) {
                if (!Game.DebugOptions.NoFieldScripts) {
                    foreach (var entity in Entities) {
                        entity.Call(0, 0, null);
                        entity.Run(9999, true);
                    }
                }
                SetPlayerIfNecessary(); //TODO - is it OK to delay doing this? But until the entity scripts run we don't know which entity corresponds to which party member...

                var scroll = GetBGScroll();
                if ((scroll.x == 0) && (scroll.y == 0)) //don't bring player into view if script appears to have scrolled away
                    BringPlayerIntoView();

                if (!Overlay.HasTriggered)
                    Overlay.Fade(30, GraphicsUtil.BlendSubtractive, Color.White, Color.Black, null);

                g.Net.Send(new Net.ScreenReadyMessage());
            }
            Entity.DEBUG_OUT = false;
        }

        private int _nextModelIndex = 0;
        public int GetNextModelIndex() {
            return _nextModelIndex++;
        }

        protected override void DoRender() {
            //System.Diagnostics.Trace.WriteLine($"FieldScreen:Render");
            Graphics.DepthStencilState = DepthStencilState.Default;
            Graphics.BlendState = BlendState.AlphaBlend;
            if (_renderBG) {
                //Render non-transparent background (or movie, if it's active)
                if (Movie.Active)
                    Movie.Render();
                else
                    Background.Render(_view2D, _bgZFrom, _bgZTo, false);
            }

            Viewer viewer3D = null;
            if (Options.HasFlag(FieldOptions.UseMovieCam) && Movie.Active)
                viewer3D = ViewFromCamera(Movie.Camera);
            viewer3D ??= _view3D;

            if (_renderDebug)
                _debug.Render(viewer3D);

            if (_renderModels) {
                using (var state = new GraphicsState(Graphics, rasterizerState: RasterizerState.CullClockwise)) {
                    foreach (var entity in Entities)
                        if ((entity.Model != null) && entity.Model.Visible)
                            entity.Model.Render(viewer3D);
                }
            }

            //Now render blend layers over actual background + models
            if (_renderBG && !Movie.Active)
                Background.Render(_view2D, _bgZFrom, _bgZTo, true);

            Overlay.Render();

            if (_renderUI && !Movie.Active && Options.HasFlag(FieldOptions.PlayerControls))
                FieldUI.Render();
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

        private int _frame = 0;
        protected override void DoStep(GameTime elapsed) {
            if (Game.Net is Net.Server) {
                if ((_frame % 2) == 0) {
                    Overlay.Step();
                    foreach (var entity in Entities) {
                        if (!Game.DebugOptions.NoFieldScripts && !Options.HasFlag(FieldOptions.NoScripts))
                            entity.Run(100);
                        entity.Model?.FrameStep();
                    }
                }

                for (int i = _processes.Count - 1; i >= 0; i--) {
                    var process = _processes[i];
                    if (process.Process(process.Frames++))
                        _processes.RemoveAt(i);
                }

                FieldUI.Step(this);
                Dialog.Step();
                Movie.Step();
                Background.Step();
            } else {
                if ((_frame % 2) == 0) {
                    foreach (var entity in Entities)
                        entity.Model?.FrameStep();
                }
            }
            _frame++;
        }

        public (int x, int y) GetBGScroll() {
            return (
                (int)(-_view2D.CenterX / 3),
                (int)(_view2D.CenterY / 3)
            );
        }
        public void BGScroll(float x, float y) {
            BGScrollOffset(x - (-_view2D.CenterX / 3), y - (_view2D.CenterY / 3));
        }
        public void BGScrollOffset(float ox, float oy) {

            _view2D.CenterX -= 3 * ox;
            _view2D.CenterY += 3 * oy;

            var newScroll = GetBGScroll();
            _view3D.ScreenOffset = new Vector2(newScroll.x * 3f * 2 / 1280, newScroll.y * -3f * 2 / 720);

            Game.Net.Send(new Net.FieldBGScrollMessage {
                X = _view2D.CenterX / 3,
                Y = _view2D.CenterY / 3,
            });
        }

        private void ReportAllModelPositions() {
            foreach (var entity in Entities.Where(e => e.Model != null)) {
                System.Diagnostics.Trace.WriteLine($"Entity {entity.Name} at pos {entity.Model.Translation}, 2D background pos {ModelToBGPosition(entity.Model.Translation)}");
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

        public void BringPlayerIntoView() {
            if (Options.HasFlag(FieldOptions.CameraIsAsyncScrolling)) return;

            if (Player != null) {
                var posOnBG = ModelToBGPosition(Player.Model.Translation);
                float playerHeight = (Player.Model.MaxBounds.Y - Player.Model.MinBounds.Y) * Player.Model.Scale;
                var highPosOnBG = ModelToBGPosition(Player.Model.Translation + new Vector3(0, 0, playerHeight));
                var scroll = GetBGScroll();
                var newScroll = scroll;
                if (posOnBG.X > (scroll.x + 100))
                    newScroll.x = (int)posOnBG.X - 100;
                else if (posOnBG.X < (scroll.x - 100))
                    newScroll.x = (int)posOnBG.X + 100;

                if (highPosOnBG.Y > (scroll.y + 85))
                    newScroll.y = (int)highPosOnBG.Y - 85;
                else if (posOnBG.Y < (scroll.y - 85))
                    newScroll.y = (int)posOnBG.Y + 85;

                if (newScroll != scroll) {
                    System.Diagnostics.Trace.WriteLine($"BringPlayerIntoView: Player at BG pos {posOnBG}, BG scroll is {scroll}, needs to be {newScroll}");
                    BGScroll(newScroll.x, newScroll.y);
                }
            }
        }

        public Vector2 ModelToBGPosition(Vector3 modelPosition, Matrix? transformMatrix = null, bool debug = false) {
            transformMatrix ??= _view3D.View * _view3D.Projection;
            var screenPos = Vector4.Transform(modelPosition, transformMatrix.Value);
            screenPos = screenPos / screenPos.W;

            float tx = (_view2D.CenterX / 3) + screenPos.X * 0.5f * (1280f / 3),
                  ty = (_view2D.CenterY / 3) + screenPos.Y * 0.5f * (720f / 3);

            if (debug)
                System.Diagnostics.Trace.WriteLine($"ModelToBG: {modelPosition} -> screen {screenPos} -> BG {tx}/{ty}");

            return new Vector2(-tx, ty);
        }

        private InputState _lastInput;

        internal InputState LastInput => _lastInput;

        private void UpdateSaveLocation() {
            Game.SaveData.Module = Module.Field;
            Game.SaveData.FieldDestination = _destination ?? new FieldDestination {
                Triangle = (ushort)Player.WalkmeshTri,
                X = (short)Player.Model.Translation.X,
                Y = (short)Player.Model.Translation.Y,
                Orientation = (byte)(Player.Model.Rotation.Y * 255 / 360),
                DestinationFieldID = _fieldID,
            };
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);
            if (!(Game.Net is Net.Server)) return;

            _lastInput = input;
            if (input.IsJustDown(InputKey.Start))
                _debugMode = !_debugMode;

            if (input.IsJustDown(InputKey.Select))
                _renderUI = !_renderUI;

            if (input.IsJustDown(InputKey.Debug1))
                _renderBG = !_renderBG;
            if (input.IsJustDown(InputKey.Debug2))
                _renderDebug = !_renderDebug;
            if (input.IsJustDown(InputKey.Debug3)) {
                _renderModels = !_renderModels;
                ReportAllModelPositions();
            }

            if (input.IsJustDown(InputKey.Debug5))
                Game.PushScreen(new UI.Layout.LayoutScreen("FieldDebugger", parm: this));

            if (input.IsDown(InputKey.Debug4)) {
                if (input.IsDown(InputKey.Up))
                    _bgZFrom++;
                else if (input.IsDown(InputKey.Down))
                    _bgZFrom--;

                if (input.IsDown(InputKey.Left))
                    _bgZTo--;
                else if (input.IsDown(InputKey.Right))
                    _bgZTo++;

                if (input.IsDown(InputKey.Start)) {
                    using (var s = Game.WriteDebugBData("field", _file + ".xml")) {
                        var info = new FieldInfo {
                            BGZFrom = _bgZFrom, BGZTo = _bgZTo,
                            OriginalBGZFrom = Background.AutoDetectZFrom, OriginalBGZTo = Background.AutoDetectZTo,
                        };
                        Serialisation.Serialise(info, s);
                    }
                }

                System.Diagnostics.Trace.WriteLine($"BGZFrom {_bgZFrom} ZTo {_bgZTo}");
                return;
            }

            if (_debugMode) {

                if (input.IsDown(InputKey.PanLeft))
                    BGScrollOffset(0, -1);
                else if (input.IsDown(InputKey.PanRight))
                    BGScrollOffset(0, +1);

                if (input.IsDown(InputKey.Up))
                    _view3D.CameraPosition += _view3D.CameraUp;
                if (input.IsDown(InputKey.Down))
                    _view3D.CameraPosition -= _view3D.CameraUp;

            } else {

                if (Dialog.IsActive) {
                    Dialog.ProcessInput(input);
                    return;
                }

                if (InputCapture != null) {
                    InputCapture.ProcessInput(input);
                    return;
                }

                if (input.IsJustDown(InputKey.Menu) && Options.HasFlag(FieldOptions.MenuEnabled)) {
                    UpdateSaveLocation();
                    Game.PushScreen(new UI.Layout.LayoutScreen("MainMenu"));
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
                                System.Diagnostics.Trace.WriteLine($"Could not start talk script for entity {talkTo}");
                        }
                    }

                    int desiredAnim = 0;
                    float animSpeed = 1f;
                    if (input.IsAnyDirectionDown() && Options.HasFlag(FieldOptions.PlayerControls)) {
                        //TODO actual use controldirection
                        var forwards = Vector3.Transform(_view3D.CameraForwards.WithZ(0), Matrix.CreateRotationZ((_controlRotation + 180) * (float)Math.PI / 180f));
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
                            move *= 2;
                            if (input.IsDown(InputKey.Cancel)) {
                                animSpeed = 2f;
                                move *= 4f;
                                desiredAnim = 2;
                            } else
                                desiredAnim = 1;

                            TryWalk(Player, Player.Model.Translation + new Vector3(move.X, move.Y, 0), true);
                            Player.Model.Rotation = Player.Model.Rotation.WithZ((float)(Math.Atan2(move.X, -move.Y) * 180f / Math.PI));

                            var oldLines = Player.LinesCollidingWith.ToArray();
                            Player.LinesCollidingWith.Clear();
                            foreach (var lineEnt in Entities.Where(e => e.Line != null)) {
                                if (lineEnt.Line.IntersectsWith(Player.Model, Player.CollideDistance))
                                    Player.LinesCollidingWith.Add(lineEnt);
                            }

                            foreach (var entered in Player.LinesCollidingWith.Except(oldLines)) {
                                System.Diagnostics.Trace.WriteLine($"Player has entered line {entered}");
                                entered.Call(3, 5, null); //TODO PRIORITY!?!
                            }

                            foreach (var left in oldLines.Except(Player.LinesCollidingWith)) {
                                System.Diagnostics.Trace.WriteLine($"Player has left line {left}");
                                left.Call(3, 6, null); //TODO PRIORITY!?!
                            }

                            /*
                            foreach (var inside in Player.LinesCollidingWith) {
                                inside.Call(2, 3, null); //TODO PRIORITY!?!
                            }
                            */

                            foreach (var gateway in TriggersAndGateways.Gateways) {
                                if (GraphicsUtil.LineCircleIntersect(gateway.V0.ToX().XY(), gateway.V1.ToX().XY(), Player.Model.Translation.XY(), Player.CollideDistance)) {
                                    Options &= ~FieldOptions.PlayerControls;
                                    desiredAnim = 0; //stop player walking as they won't move any more!
                                    FadeOut(() => {
                                        Game.ChangeScreen(this, new FieldScreen(gateway.Destination));
                                    });
                                }
                            }
                            foreach (var trigger in TriggersAndGateways.Triggers) {
                                bool active = GraphicsUtil.LineCircleIntersect(trigger.V0.ToX().XY(), trigger.V1.ToX().XY(), Player.Model.Translation.XY(), Player.CollideDistance);
                                if (active != _activeTriggers.Contains(trigger)) {

                                    bool setOn = false, setOff = false;
                                    switch (trigger.Behaviour) {
                                        case TriggerBehaviour.OnNone:
                                            if (active)
                                                setOn = true;
                                            break;
                                        case TriggerBehaviour.OffNone:
                                            if (active)
                                                setOff = true;
                                            break;
                                        case TriggerBehaviour.OnOff:
                                        case TriggerBehaviour.OnOffPlus: //TODO - plus side only
                                            setOn = active;
                                            setOff = !active;
                                            break;
                                        case TriggerBehaviour.OffOn:
                                        case TriggerBehaviour.OffOnPlus: //TODO - plus side only
                                            setOn = !active;
                                            setOff = active;
                                            break;
                                        default:
                                            throw new NotImplementedException();
                                    }

                                    if (setOn)
                                        Background.ModifyParameter(trigger.BackgroundID, i => i | (1 << trigger.BackgroundState));
                                    if (setOff)
                                        Background.ModifyParameter(trigger.BackgroundID, i => i & ~(1 << trigger.BackgroundState));

                                    if ((setOn || setOff) && (trigger.SoundID != 0))
                                        Game.Audio.PlaySfx(trigger.SoundID - 1, 1f, 0f);

                                    if (active)
                                        _activeTriggers.Add(trigger);
                                    else
                                        _activeTriggers.Remove(trigger);
                                }
                            }

                            if (Options.HasFlag(FieldOptions.CameraTracksPlayer))
                                BringPlayerIntoView();

                            if ((_frame % 20) == 0) {
                                Game.SaveData.FieldDangerCounter += (int)(1024 * animSpeed * animSpeed / _encounters[BattleTable].Rate);
                                if (_r.Next(256) < (Game.SaveData.FieldDangerCounter / 256)) {
                                    System.Diagnostics.Trace.WriteLine($"FieldDangerCounter: trigger encounter and reset");
                                    Game.SaveData.FieldDangerCounter = 0;
                                    if (BattleOptions.BattlesEnabled && _encounters[BattleTable].Enabled) {
                                        Battle.BattleScreen.Launch(Game, _encounters[BattleTable], BattleOptions.Flags, _r);
                                    }
                                }
                            }

                        } else {
                            //
                        }

                        //Lines we're moving through
                        //Both events seem necessary; presumably priorities should be different though...?
                        foreach (var isIn in Player.LinesCollidingWith) {
                            System.Diagnostics.Trace.WriteLine($"Player colliding with {isIn.Name}, triggering script 2");
                            isIn.Call(3, 2, null); //TODO PRIORITY!?!
                        }
                        foreach (var isIn in Player.LinesCollidingWith) {
                            System.Diagnostics.Trace.WriteLine($"Player colliding with {isIn.Name}, triggering script 3");
                            isIn.Call(2, 3, null); //TODO PRIORITY!?!
                        }
                    }

                    if ((Player.Model.AnimationState.Animation != desiredAnim) || (Player.Model.AnimationState.AnimationSpeed != animSpeed))
                        Player.Model.PlayAnimation(desiredAnim, true, animSpeed);

                    //Lines we're just within
                    foreach (var isIn in Player.LinesCollidingWith) {
                        isIn.Call(2, 4, null); //TODO PRIORITY!?!
                    }
                }
            }

        }

        private static bool LineIntersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, out float aDist) {
            double denominator = ((a1.X - a0.X) * (b1.Y - b0.Y)) - ((a1.Y - a0.Y) * (b1.X - b0.X));
            double numerator1 = 1.0 * ((a0.Y - b0.Y) * (b1.X - b0.X)) - 1.0 * ((a0.X - b0.X) * (b1.Y - b0.Y));
            double numerator2 = 1.0 * ((a0.Y - b0.Y) * (a1.X - a0.X)) - 1.0 * ((a0.X - b0.X) * (a1.Y - a0.Y));


            if (denominator == 0) {
                aDist = 0;
                return numerator1 == 0 && numerator2 == 0;
            }

            aDist = (float)Math.Round(numerator1 / denominator, 2);
            double s = Math.Round(numerator2 / denominator, 2);

            return (aDist >= 0 && aDist <= 1) && (s >= 0 && s <= 1);
        }

        private bool CalculateTriLeave(Vector2 startPos, Vector2 endPos, WalkmeshTriangle tri, out float dist, out short? newTri, out Vector2 tv0, out Vector2 tv1) {
            tv0 = tri.V0.ToX().XY();
            tv1 = tri.V1.ToX().XY();
            if (LineIntersect(startPos, endPos, tv0, tv1, out dist)) {
                newTri = tri.V01Tri;
                return true;
            }

            tv0 = tri.V1.ToX().XY();
            tv1 = tri.V2.ToX().XY();
            if (LineIntersect(startPos, endPos, tv0, tv1, out dist)) {
                newTri = tri.V12Tri;
                return true;
            }

            tv0 = tri.V2.ToX().XY();
            tv1 = tri.V0.ToX().XY();
            if (LineIntersect(startPos, endPos, tv0, tv1, out dist)) {
                newTri = tri.V20Tri;
                return true;
            }

            newTri = null;
            return false;
        }

        private enum LeaveTriResult {
            Failure,
            Success,
            SlideCurrentTri,
            SlideNewTri,
        }

        private void FindOtherVerts(WalkmeshTriangle tri, FieldVertex v, out FieldVertex v1, out FieldVertex v2) {
            if (tri.V0 == v) {
                v1 = tri.V1;
                v2 = tri.V2;
            } else if (tri.V1 == v) {
                v1 = tri.V0;
                v2 = tri.V2;
            } else if (tri.V2 == v) {
                v1 = tri.V0;
                v2 = tri.V1;
            } else
                throw new NotImplementedException();
        }

        private void FindAdjacentTris(WalkmeshTriangle tri, FieldVertex v, out short? t0, out short? t1) {
            if (tri.V0 == v) {
                t0 = tri.V01Tri;
                t1 = tri.V20Tri;
            } else if (tri.V1 == v) {
                t0 = tri.V01Tri;
                t1 = tri.V12Tri;
            } else if (tri.V2 == v) {
                t0 = tri.V12Tri;
                t1 = tri.V20Tri;
            } else
                throw new NotImplementedException();
        }

        private double AngleBetweenVectors(Vector2 v0, Vector2 v1) {
            double angle = Math.Atan2(v0.Y, v0.X) - Math.Atan2(v1.Y, v1.X);
            while (angle > Math.PI) angle -= 2 * Math.PI;
            while (angle <= -Math.PI) angle += 2 * Math.PI;
            return angle;
        }

        private static Random _r = new();

        private LeaveTriResult DoesLeaveTri(Vector2 startPos, Vector2 endPos, WalkmeshTriangle tri, bool allowSlide, out short? newTri, out Vector2 newDestination) {
            newDestination = Vector2.Zero;

            var origDir = (endPos - startPos);
            var origDistance = origDir.Length();
            origDir.Normalize();

            //Now see if we're exactly on a vert. If so, find ALL the tris which join that vert.
            //We'll try and shift into one of them and then when the move is retried, we'll hopefully make some progress... :/

            foreach (var vert in tri.AllVerts()) {
                if ((vert.X == (short)startPos.X) && (vert.Y == (short)startPos.Y)) {

                    var candidates = _walkmesh
                        .SelectMany((t, index) => t.AllVerts()
                            .Where(v => v != vert)
                            .Select(otherV => {
                                var dir = otherV.ToX().XY() - vert.ToX().XY();
                                dir.Normalize();
                                return new {
                                    Tri = t,
                                    TIndex = index,
                                    VStart = vert.ToX().XY(),
                                    VEnd = otherV.ToX().XY(),
                                    Angle = AngleBetweenVectors(dir, origDir)
                                };
                            })
                        )
                        .Where(a => !DisabledWalkmeshTriangles.Contains(a.TIndex))
                        .Where(a => a.Tri.AllVerts().Any(v => v == vert))
                        .OrderBy(a => Math.Abs(a.Angle));

                    if (candidates.Any()) {
                        var choice = candidates.First();
                        if (choice.Tri != tri) {
                            newDestination = choice.VStart;
                            newTri = (short)choice.TIndex;
                            return LeaveTriResult.SlideNewTri;
                        } else {
                            var edge = choice.VEnd - choice.VStart;
                            var distance = edge.Length();
                            edge.Normalize();
                            if (distance < origDistance)
                                newDestination = choice.VEnd;
                            else
                                newDestination = startPos + edge * origDistance;
                            newTri = null;
                            return LeaveTriResult.SlideCurrentTri;
                        }
                    }
                }
            }

            bool TestTri(short? t, Vector2 pos) {
                if (t == null)
                    return false;
                else {
                    var tt = _walkmesh[t.Value];
                    return HeightInTriangle(tt, pos.X, pos.Y, false) != null;
                }
            }

            newTri = null;

            var vector = endPos - startPos;
            vector.Normalize();
            var currentTri = tri;
            int steps = (int)Math.Ceiling((endPos - startPos).Length());
            foreach (int step in Enumerable.Range(1, steps)) {
                var pos = startPos + vector * step;

                if (HeightInTriangle(currentTri, pos.X, pos.Y, false) != null) {
                    continue;
                }

                if (TestTri(currentTri.V01Tri, pos)) {
                    newTri = currentTri.V01Tri.Value;
                    newDestination = pos;
                } else if (TestTri(currentTri.V12Tri, pos)) {
                    newTri = currentTri.V12Tri.Value;
                    newDestination = pos;
                } else if (TestTri(currentTri.V20Tri, pos)) {
                    newTri = currentTri.V20Tri.Value;
                    newDestination = pos;
                } else
                    break;

            }

            if (newTri != null)
                return LeaveTriResult.Success;

            if (!CalculateTriLeave(startPos, endPos, tri, out _, out _, out var tv0, out var tv1))
                return LeaveTriResult.Failure;

            if (allowSlide) {

                //If we get here, we're not exactly on one of the current tri's verts, but may be able
                //to slide along an edge to end up closer to our desired end point.
                //Calculate angles from end-start-v0 and end-start-v1 to find which vert we can slide towards
                //while minimising the change in direction from our original heading.
                //Only slide if the edge is < 60 degrees off our original heading as it's weird otherwise!

                var v0dir = (tv0 - startPos);
                var v0Distance = v0dir.Length();
                v0dir.Normalize();

                var v1dir = (tv1 - startPos);
                var v1Distance = v1dir.Length();
                v1dir.Normalize();

                double v0angle = AngleBetweenVectors(v0dir, origDir),
                    v1angle = AngleBetweenVectors(v1dir, origDir);

                if ((Math.Abs(v0angle) < Math.Abs(v1angle)) && (v0angle < (Math.PI / 3))) {
                    //Try to slide towards v0
                    if (v0Distance < origDistance)
                        newDestination = tv0;
                    else
                        newDestination = startPos + v0dir * origDistance;
                    return LeaveTriResult.SlideCurrentTri;
                } else if (Math.Abs(v1angle) < (Math.PI / 3)) {
                    //Try to slide towards v1
                    if (v1Distance < origDistance)
                        newDestination = tv1;
                    else
                        newDestination = startPos + v1dir * origDistance;
                    return LeaveTriResult.SlideCurrentTri;
                }

            }

            return LeaveTriResult.Failure;
        }


        public bool TryWalk(Entity eMove, Vector3 newPosition, bool doCollide) {
            //TODO: Collision detection against other models!

            if (doCollide) {
                eMove.CollidingWith.Clear();
                Entities.ForEach(e => e.CollidingWith.Remove(eMove));

                var toCheck = Entities
                    .Where(e => e.Flags.HasFlag(EntityFlags.CanCollide))
                    .Where(e => e.Model != null)
                    .Where(e => e != eMove);

                foreach (var entity in toCheck) {
                    if (entity.Model != null) {
                        var dist = (entity.Model.Translation.XY() - newPosition.XY()).Length();
                        var collision = eMove.CollideDistance + entity.CollideDistance;
                        if (dist <= collision) {
                            System.Diagnostics.Trace.WriteLine($"Entity {eMove} is now colliding with {entity}");
                            eMove.CollidingWith.Add(entity);
                            entity.CollidingWith.Add(eMove);
                        }
                    }
                }
                if (eMove.CollidingWith.Any())
                    return false;
            }

            var currentTri = _walkmesh[eMove.WalkmeshTri];
            var newHeight = HeightInTriangle(currentTri, newPosition.X, newPosition.Y, false);
            if (newHeight != null) {
                //We're staying in the same tri, so just update height

                if (!CalculateTriLeave(newPosition.XY(), new Vector2(9999, 9999), currentTri, out _, out _, out _, out _))
                    throw new Exception($"Sanity check failed");

                eMove.Model.Translation = newPosition.WithZ(newHeight.Value);
                ReportDebugEntityPos(eMove);
                return true;
            } else {
                switch (DoesLeaveTri(eMove.Model.Translation.XY(), newPosition.XY(), currentTri, true, out short? newTri, out Vector2 newDest)) {
                    case LeaveTriResult.Failure:
                        return false;
                    case LeaveTriResult.SlideCurrentTri:
                        ClampToTriangle(ref newDest, currentTri);
                        break;
                    case LeaveTriResult.Success:
                    case LeaveTriResult.SlideNewTri:
                        eMove.WalkmeshTri = newTri.Value;
                        currentTri = _walkmesh[newTri.Value];
                        ClampToTriangle(ref newDest, currentTri);
                        break; //Treat same as success, code below will move us accordingly
                    default:
                        throw new NotImplementedException();
                }

                newHeight = HeightInTriangle(currentTri, newDest.X, newDest.Y, true);
                if (newHeight == null)
                    throw new Exception();
                eMove.Model.Translation = new Vector3(newDest.X, newDest.Y, newHeight.Value);
                ReportDebugEntityPos(eMove);
                return true;
            }
        }

        private void ClampToTriangle(ref Vector2 position, WalkmeshTriangle tri) {
            CalculateBarycentric(tri.V0.ToX(), tri.V1.ToX(), tri.V2.ToX(), position,
                out float a, out float b, out float c);

            if ((a < 0) || (a > 1) || (b < 0) || (b > 1) || (c < 0) || (c > 1)) {
                a = Math.Min(Math.Max(a, 0), 1);
                b = Math.Min(Math.Max(b, 0), 1);
                c = Math.Min(Math.Max(c, 0), 1);

                position = new Vector2(
                    tri.V0.X * a + tri.V1.X * b + tri.V2.X * c,
                    tri.V0.Y * a + tri.V1.Y * b + tri.V2.Y * c
                );
            }
        }

        public float? HeightInTriangle(int triID, float x, float y, bool allowClampAndRound) {
            return HeightInTriangle(_walkmesh[triID], x, y, allowClampAndRound);
        }
        private static float? HeightInTriangle(WalkmeshTriangle tri, float x, float y, bool allowClampAndRound) {
            return HeightInTriangle(tri.V0.ToX(), tri.V1.ToX(), tri.V2.ToX(), x, y, allowClampAndRound);
        }
        private static float? HeightInTriangle(Vector3 p0, Vector3 p1, Vector3 p2, float x, float y, bool allowClampAndRound) {

            CalculateBarycentric(p0, p1, p2, new Vector2(x, y), out var a, out var b, out var c);

            if (allowClampAndRound) {
                //For height specifically, when we've already determined this is definitely the tri we're inside,
                //allow being *slightly* outside the triangle due to floating point imprecision
                a = (float)Math.Round(a, 4);
                b = (float)Math.Round(b, 4);
                c = (float)Math.Round(c, 4);
            }

            if (a < 0) return null;
            if (b < 0) return null;
            if (c < 0) return null;
            if (a > 1) return null;
            if (b > 1) return null;
            if (c > 1) return null;

            return (float)(p0.Z * a + p1.Z * b + p2.Z * c);
        }

        private static void CalculateBarycentric(Vector3 va, Vector3 vb, Vector3 vc, Vector2 pos, out float a, out float b, out float c) {
            double denominator = (vb.Y - vc.Y) * (va.X - vc.X) + (vc.X - vb.X) * (va.Y - vc.Y);
            
            a = (float)(((vb.Y - vc.Y) * (pos.X - vc.X) + (vc.X - vb.X) * (pos.Y - vc.Y)) / denominator);
            b = (float)(((vc.Y - va.Y) * (pos.X - vc.X) + (va.X - vc.X) * (pos.Y - vc.Y)) / denominator);

            c = 1 - a - b;
        }

        private void ReportDebugEntityPos(Entity e) {
            if (e.Name == _debugEntity) {
                System.Diagnostics.Trace.WriteLine($"Ent {e.Name} at pos {e.Model.Translation} wmtri {e.WalkmeshTri}");
                var tri = _walkmesh[e.WalkmeshTri];
                CalculateBarycentric(tri.V0.ToX(), tri.V1.ToX(), tri.V2.ToX(), e.Model.Translation.XY(), out var a, out var b, out var c);
                System.Diagnostics.Trace.WriteLine($"---Barycentric pos {a} / {b} / {c}");
            }
        }

        public void DropToWalkmesh(Entity e, Vector2 position, int walkmeshTri, bool exceptOnFailure = true) {
            var tri = _walkmesh[walkmeshTri];

            var height = HeightInTriangle(tri, position.X, position.Y, true);

            if ((height == null) && exceptOnFailure)
                throw new Exception($"Cannot DropToWalkmesh - position {position} does not have a height in walkmesh tri {walkmeshTri}");

            e.Model.Translation = new Vector3(position.X, position.Y, height.GetValueOrDefault());
            e.WalkmeshTri = walkmeshTri;
            ReportDebugEntityPos(e);
        }

        public void CheckPendingPlayerSetup() {
            if ((_destination != null) && (Player.Model != null)) {
                DropToWalkmesh(Player, new Vector2(_destination.X, _destination.Y), _destination.Triangle, false);
                Player.Model.Rotation = new Vector3(0, 0, 360f * _destination.Orientation / 255f);
                _destination = null;
            }
        }

        public void SetPlayer(int whichEntity) {
            Player = Entities[whichEntity]; //TODO: also center screen etc.
            //TODO - is this reasonable...? Probably?!
            if (Player.CollideDistance == 0)
                Player.CollideDistance = 20;
            CheckPendingPlayerSetup();
            WhenPlayerSet?.Invoke();
        }

        public void SetPlayerControls(bool enabled) {
            if (enabled)
                Options |= FieldOptions.PlayerControls | FieldOptions.CameraTracksPlayer; //Seems like cameratracksplayer MUST be turned on now or things break...?
            else {
                Options &= ~FieldOptions.PlayerControls;
                if (Player?.Model != null)
                    Player.Model.PlayAnimation(0, true, 1f);
                //TODO - is this reasonable? Disable current (walking) animation when we take control away from the player? 
                //(We don't want e.g. walk animation to be continuing after our control is disabled and we're not moving any more!)
            }
        }

        public void TriggerBattle(int which) {
            Battle.BattleScreen.Launch(Game, which, BattleOptions.Flags);
        }


        public void Received(Net.FieldModelMessage message) {
            var model = FieldModels[message.ModelID];
            if (message.Visible.HasValue)
                model.Visible = message.Visible.Value;
            if (message.Translation.HasValue)
                model.Translation = message.Translation.Value;
            if (message.Translation2.HasValue)
                model.Translation2 = message.Translation2.Value;
            if (message.Rotation.HasValue)
                model.Rotation = message.Rotation.Value;
            if (message.Rotation2.HasValue)
                model.Rotation2 = message.Rotation2.Value;
            if (message.Scale.HasValue)
                model.Scale = message.Scale.Value;
            if (message.AnimationState != null)
                model.AnimationState = message.AnimationState;
        }

        public void Received(Net.FieldBGMessage message) {
            Background.SetParameter(message.Parm, message.Value);
        }
        public void Received(Net.FieldBGScrollMessage message) {
            BGScroll(message.X, message.Y);
        }

        public void Received(Net.FieldEntityModelMessage message) {
            Entities[message.EntityID].Model = FieldModels[message.ModelID];
        }
    }

    public class CachedField {
        public int FieldID { get; set; } = -1;
        public FieldFile FieldFile { get; set; }

        public void Load(FGame g, int fieldID) {
            var mapList = g.Singleton(() => new MapList(g.Open("field", "maplist")));
            string file = mapList.Items[fieldID];
            using (var s = g.Open("field", file))
                FieldFile = new FieldFile(s);
            FieldID = fieldID;
        }
    }

    public interface IInputCapture {
        void ProcessInput(InputState input);
    }
}
