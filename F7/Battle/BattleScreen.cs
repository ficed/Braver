using Ficedula.FF7.Battle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Battle {

    public class BattleSceneCache {
        public Dictionary<int, BattleScene> Scenes { get; }

        public BattleSceneCache(FGame g) {
            Scenes = SceneDecoder.Decode(g.Open("battle", "scene.bin"))
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

        private class Callbacks : AICallbacks {
            public Callbacks(VMM vmm) {
                _vmm = vmm;
            }

            public override void DisplayText(byte[] text) {
                //TODO encoding?!?!
                Console.WriteLine(Encoding.ASCII.GetString(text));
            }
        }

        private class UIHandler : UI.Layout.LayoutModel {
            public UI.Layout.Label
                lHP0, lHP1, lHP2,
                lMaxHP0, lMaxHP1, lMaxHP2,
                lMP0, lMP1, lMP2,
                lMaxMP0, lMaxMP1, lMaxMP2;
            public UI.Layout.Gauge
                gHP0, gHP1, gHP2,
                gMP0, gMP1, gMP2,
                gLimit0, gLimit1, gLimit2,
                gTime0, gTime1, gTime2;
            public UI.Layout.Box
                bMenu0, bMenu1, bMenu2;


            private List<CharacterCombatant> _combatants;

            public IReadOnlyList<CharacterCombatant> Combatants => _combatants.AsReadOnly();

            public override bool IsRazorModel => true;

            public UIHandler(IEnumerable<CharacterCombatant> combatants) {
                _combatants = combatants.ToList();
            }

            private void DoUpdate(CharacterCombatant combatant,
                UI.Layout.Label lHP, UI.Layout.Label lMaxHP, UI.Layout.Label lMP, UI.Layout.Label lMaxMP,
                UI.Layout.Gauge gHP, UI.Layout.Gauge gMP, UI.Layout.Gauge gLimit, UI.Layout.Gauge gTime,
                UI.Layout.Box bMenu) {
                
                if (combatant == null) return;

                lHP.Text = combatant.Character.CurrentHP.ToString();
                lMP.Text = combatant.Character.CurrentMP.ToString();
                gHP.Current = combatant.Character.CurrentHP;
                gMP.Current = combatant.Character.CurrentMP;
                gLimit.Current = combatant.Character.LimitBar;
                gTime.Current = 255 * combatant.TTimer.Fill;
            }

            public void Update() {
                DoUpdate(_combatants.ElementAtOrDefault(0), lHP0, lMaxHP0, lMP0, lMaxMP0, gHP0, gMP0, gLimit0, gTime0, bMenu0);
                DoUpdate(_combatants.ElementAtOrDefault(1), lHP1, lMaxHP1, lMP1, lMaxMP1, gHP1, gMP1, gLimit1, gTime1, bMenu1);
                DoUpdate(_combatants.ElementAtOrDefault(2), lHP2, lMaxHP2, lMP2, lMaxMP2, gHP2, gMP2, gLimit2, gTime2, bMenu2);
            }
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
        private UIHandler _uiHandler;

        private Engine _engine;


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

            InitEngine();

            _uiHandler = new UIHandler(_engine.Combatants.OfType<CharacterCombatant>());
            _ui = new UI.Layout.LayoutScreen("battle", _uiHandler);
            _ui.Init(Game, Graphics);

            g.Audio.PlayMusic("bat"); //TODO!
        }

        private void InitEngine() {

            ICombatant[] combatants = new ICombatant[16];

            int index = 0;
            foreach (var chr in Game.SaveData.Party)
                combatants[index++] = new CharacterCombatant(Game, chr);

            index = 4;
            foreach (var group in _scene.Enemies.GroupBy(ei => ei.Enemy.ID)) {
                int c = 0;
                foreach (var enemy in group) {
                    combatants[index++] = new EnemyCombatant(enemy, group.Count() == 1 ? null : c++);
                }
            }

            var callbacks = new Callbacks(Game.Memory);
            _engine = new Engine(128, combatants, Game, callbacks);

            _engine.ReadyForAction += c => { };
            _engine.ActionQueued += a => { };

        }

        public override Color ClearColor => Color.Black;

        protected override void DoStep(GameTime elapsed) {
            foreach (var model in _models) {
                model.FrameStep();
            }
            _engine.Tick();
            _uiHandler.Update();
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
