using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Battle {

    public class BattleSceneCache {
        public Dictionary<int, Ficedula.FF7.Battle.BattleScene> Scenes { get; }

        public BattleSceneCache(FGame g) {
            Scenes = Ficedula.FF7.Battle.SceneDecoder.Decode(g.Open("battle", "scene.bin"))
                .Where(s => s.Enemies.Any())
                .ToDictionary(s => s.FormationID, s => s);
        }
    }

    enum BackgroundKind {
        HorizontalScroll = 0,
        Normal = 1,
        VerticalScroll = 2,
        Lifestream = 3,
        Rotating = 4,
        NormalAgain = 5,
    }

    public class BattleScreen : Screen {

        private class UIHandler : UI.Layout.LayoutModel {

        }


        private BackgroundKind _backgroundKind;

        private class BackgroundChunk {
            public BasicEffect Effect;
            public int IndexOffset;
            public int VertOffset;
            public int TriCount;
        }

        private List<BackgroundChunk> _backgroundChunks = new();
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;

        private List<Model> _models = new();
        private Ficedula.FF7.Battle.BattleScene _scene;

        private PerspView3D _view;

        private UI.Layout.LayoutScreen _ui;

        private bool _debugCamera = false;

        private void AddModel(string code, Vector3 position) {
            var model = Model.LoadBattleModel(Graphics, Game, code);
            model.Translation = position;
            model.Scale = 1;
            if (position.Z < 0)
                model.Rotation = new Vector3(0, 180, 0);
            model.PlayAnimation(0, true, 1f, null);
            _models.Add(model);
        }

        private void LoadBackground() {
            string prefix = Ficedula.FF7.Battle.SceneDecoder.LocationIDToFileName(_scene.LocationID);

            string NumToFile(int num) {
                char c1 = (char)('a' + (num / 26)),
                    c2 = (char)('a' + (num % 26));
                return $"{prefix}{c1}{c2}";
            }

            List<Texture2D> texs = new();

            int num = 2; //start with ac for texs
            while (true) {
                using (var stex = Game.TryOpen("battle", NumToFile(num++))) {
                    if (stex == null)
                        break;
                    texs.Add(Graphics.LoadTex(new Ficedula.FF7.TexFile(stex), 0));
                }
            }

            List<VertexPositionColorTexture> verts = new();
            List<int> indices = new();

            List<Ficedula.FF7.PFile> pfiles = new();
            num = 12;
            while (true) {
                using (var sp = Game.TryOpen("battle", NumToFile(num++))) {
                    if (sp == null)
                        break;
                    pfiles.Add(new Ficedula.FF7.PFile(sp));
                }
            }

            foreach (var group in pfiles.SelectMany(p => p.Chunks).GroupBy(c => c.Texture)) {
                var bchunk = new BackgroundChunk {
                    Effect = new BasicEffect(Graphics) {
                        VertexColorEnabled = true,
                        LightingEnabled = false,
                        TextureEnabled = group.Key.HasValue,
                        Texture = texs.ElementAtOrDefault(group.Key.GetValueOrDefault(99999)),
                        World = Matrix.Identity,
                    },
                    IndexOffset = indices.Count,
                    VertOffset = verts.Count,
                    TriCount = group.Sum(c => c.Indices.Count) / 3,
                };
                int vcount = 0;
                foreach (var pchunk in group) {
                    indices.AddRange(pchunk.Indices.Select(i => i + vcount));
                    vcount += pchunk.Verts.Count;
                    verts.AddRange(
                        pchunk.Verts
                        .Select(v => new VertexPositionColorTexture {
                            Position = v.Position.ToX(),
                            Color = new Color(v.Colour),
                            TextureCoordinate = v.TexCoord.ToX(),
                        })
                    );
                }
                _backgroundChunks.Add(bchunk);
            }

            _vertexBuffer = new VertexBuffer(Graphics, typeof(VertexPositionColorTexture), verts.Count, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(verts.ToArray());
            _indexBuffer = new IndexBuffer(Graphics, typeof(int), indices.Count, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices.ToArray());
        }

        private static Vector3[] _playerPositions = new[] {
            new Vector3(-7 * 256, 0, 6 * 256),
            new Vector3(0 * 256, 0, 6 * 256),
            new Vector3(7 * 256, 0, 6 * 256),
        };

        private int _formationID;
        public BattleScreen(int formationID) {
            _formationID = formationID;
        }
        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);
            _scene = g.Singleton(() => new BattleSceneCache(g)).Scenes[_formationID];

            LoadBackground();

            var cam = _scene.Cameras[0];
            _view = new PerspView3D {
                CameraPosition = new Vector3(cam.X, cam.Y, cam.Z),
                CameraForwards = new Vector3(cam.LookAtX - cam.X, cam.LookAtY - cam.Y, cam.LookAtZ - cam.Z),
                CameraUp = -Vector3.UnitY, //TODO!!
                ZNear = 100,
                ZFar = 100000,
                FOV = 51f, //Seems maybe vaguely correct, more or less what Proud Clod uses for its preview...
            };
            foreach(var enemy in _scene.Enemies) {
                AddModel(
                    Ficedula.FF7.Battle.SceneDecoder.ModelIDToFileName(enemy.Enemy.ID),
                    new Vector3(enemy.PositionX, enemy.PositionY, enemy.PositionZ)
                );
            }

            foreach(var player in Game.SaveData.Party.Zip(_playerPositions)) {
                AddModel(player.First.BattleModel, player.Second);
            }

            _ui = new UI.Layout.LayoutScreen("battle", new UIHandler());
            _ui.Init(Game, Graphics);

            g.Audio.PlayMusic("bat"); //TODO!
        }

        public override Color ClearColor => Color.Black;

        protected override void DoStep(GameTime elapsed) {
            foreach (var model in _models) {
                model.FrameStep();
            }

            _ui.Step(elapsed);
        }

        protected override void DoRender() {

            Graphics.DepthStencilState = DepthStencilState.Default;
            Graphics.RasterizerState = RasterizerState.CullClockwise;
            Graphics.SamplerStates[0] = SamplerState.LinearWrap;

            Graphics.Indices = _indexBuffer;
            Graphics.SetVertexBuffer(_vertexBuffer);

            foreach(var chunk in _backgroundChunks) {
                chunk.Effect.View = _view.View;
                chunk.Effect.Projection = _view.Projection;
                foreach (var pass in chunk.Effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    Graphics.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList, chunk.VertOffset, chunk.IndexOffset, chunk.TriCount
                    );
                }
            }

            foreach (var model in _models)
                model.Render(_view);

            _ui.Render();
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);

            if (input.IsJustDown(InputKey.Debug1))
                _debugCamera = !_debugCamera;

            if (_debugCamera) {
                if (input.IsDown(InputKey.Up))
                    _view.CameraPosition += new Vector3(0, 0, -100);
                if (input.IsDown(InputKey.Down))
                    _view.CameraPosition += new Vector3(0, 0, 100);
                if (input.IsDown(InputKey.Left))
                    _view.CameraPosition += new Vector3(100, 0, 0);
                if (input.IsDown(InputKey.Right))
                    _view.CameraPosition += new Vector3(-100, 0, 0);
            }
        }

    }
}
