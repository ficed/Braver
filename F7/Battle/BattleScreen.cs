using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;

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

        private BackgroundKind _backgroundKind;

        private class BackgroundChunk {
            public BasicEffect Effect;
            public int IndexOffset;
            public int VertOffset;
            public int TriCount;
        }

        private List<BackgroundChunk> _backgroundChunks = new();
        private GraphicsDevice _graphics;
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;

        private PerspView3D _view;

        public BattleScreen(FGame g, GraphicsDevice graphics, int formationID) : base(g, graphics) {
            _graphics = graphics;

            var scene = g.Singleton(() => new BattleSceneCache(g)).Scenes[formationID];

            string prefix = Ficedula.FF7.Battle.SceneDecoder.LocationIDToFileName(scene.LocationID);

            string NumToFile(int num) {
                char c1 = (char)('a' + (num / 26)),
                    c2 = (char)('a' + (num % 26));
                return $"{prefix}{c1}{c2}";
            }

            List<Texture2D> texs = new();

            int num = 2; //start with ac for texs
            while (true) {
                using (var stex = g.TryOpen("battle", NumToFile(num++))) {
                    if (stex == null)
                        break;
                    texs.Add(graphics.LoadTex(new Ficedula.FF7.TexFile(stex), 0));
                }
            }

            List<VertexPositionColorTexture> verts = new();
            List<int> indices = new();

            List<Ficedula.FF7.PFile> pfiles = new();
            num = 12;
            while (true) {
                using (var sp = g.TryOpen("battle", NumToFile(num++))) {
                    if (sp == null)
                        break;
                    pfiles.Add(new Ficedula.FF7.PFile(sp));
                }
            }

            foreach (var group in pfiles.SelectMany(p => p.Chunks).GroupBy(c => c.Texture)) {
                var bchunk = new BackgroundChunk {
                    Effect = new BasicEffect(graphics) {
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

            _vertexBuffer = new VertexBuffer(graphics, typeof(VertexPositionColorTexture), verts.Count, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(verts.ToArray());
            _indexBuffer = new IndexBuffer(graphics, typeof(int), indices.Count, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices.ToArray());

            var cam = scene.Cameras[0];
            _view = new PerspView3D {
                CameraPosition = new Vector3(cam.X, cam.Y, cam.Z),
                CameraForwards = new Vector3(cam.LookAtX - cam.X, cam.LookAtY - cam.Y, cam.LookAtZ - cam.Z),
                CameraUp = -Vector3.UnitY, //TODO!!
                ZNear = 100,
                ZFar = 100000,
            };

            g.Audio.PlayMusic("bat"); //TODO!
        }

        public override Color ClearColor => Color.Black;

        protected override void DoStep(GameTime elapsed) {
            //
        }

        protected override void DoRender() {

            _graphics.RasterizerState = RasterizerState.CullNone;
            _graphics.SamplerStates[0] = SamplerState.LinearWrap;

            _graphics.Indices = _indexBuffer;
            _graphics.SetVertexBuffer(_vertexBuffer);

            foreach(var chunk in _backgroundChunks) {
                chunk.Effect.View = _view.View;
                chunk.Effect.Projection = _view.Projection;
                foreach (var pass in chunk.Effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    _graphics.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList, chunk.VertOffset, chunk.IndexOffset, chunk.TriCount
                    );
                }
            }
        }

    }
}
