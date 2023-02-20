using Braver.Field;
using Ficedula.FF7;
using Ficedula.FF7.Battle;
using Ficedula.FF7.Field;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpDX.Direct2D1.Effects;
using SharpDX.X3DAudio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Braver.Battle {
    public class Model {

        private List<PFile> _pfiles = new();
        private class RenderNode {
            public int VertOffset, IndexOffset, TriCount;
            public Texture2D Texture;
        }
        private Dictionary<PFileChunk, RenderNode> _nodes = new();
        private BasicEffect _texEffect, _colEffect;
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private GraphicsDevice _graphics;
        private Animations _animations;
        private BBone _root;
        private Texture2D _fadeTexture;

        public Vector3 Rotation2 { get; set; }
        public Vector3 Rotation { get; set; }
        public Vector3 Translation { get; set; }
        public Vector3 Translation2 { get; set; }
        public float Scale { get; set; } = 1f;
        public bool Visible { get; set; } = true;
        public float GlobalAnimationSpeed { get; set; } = 1f;
        public AnimationState AnimationState { get; set; }

        public Vector3 MinBounds { get; private set; }
        public Vector3 MaxBounds { get; private set; }

        public float? DeathFade { get; set; }

        private Model(GraphicsDevice graphics, FGame game, string folder, string skeleton, string anims, IEnumerable<string> texs, Func<System.IO.Stream> NextData) {
            _graphics = graphics;
            List<VertexPositionNormalColorTexture> verts = new();
            List<int> indices = new();

            var textures = texs
                .Select(t => {
                    using (var s = game.TryOpen(folder, t))
                        return s == null ? null : graphics.LoadTex(new Ficedula.FF7.TexFile(s), 0);
                })
                .Where(tex => tex != null)
                .ToArray();
            _fadeTexture = new Texture2D(graphics, 2, 2, false, SurfaceFormat.Color);

            using (var s = game.Open(folder, anims))
                _animations = new Animations(s);

            using (var s = game.Open(folder, skeleton)) {
                _root = BBone.Decode(s);
            }

            foreach (var bone in _root.ThisAndDescendants().Where(b => b.PFileIndex != null).OrderBy(b => b.PFileIndex.Value)) {
                while (_pfiles.Count <= bone.PFileIndex.Value)
                    _pfiles.Add(null);
                _pfiles[bone.PFileIndex.Value] = new PFile(NextData());
            }

            foreach(var pfile in _pfiles) {
                foreach (var chunk in pfile.Chunks) {
                    _nodes[chunk] = new RenderNode {
                        IndexOffset = indices.Count,
                        Texture = chunk.Texture == null ? null : textures[chunk.Texture.Value],
                        TriCount = chunk.Indices.Count / 3,
                        VertOffset = verts.Count,
                    };
                    indices.AddRange(chunk.Indices);
                    verts.AddRange(
                        chunk.Verts
                        .Select(v => new VertexPositionNormalColorTexture {
                            Position = v.Position.ToX(),
                            Normal = v.Normal.ToX(),
                            Color = new Color(v.Colour),
                            TexCoord = v.TexCoord.ToX(),
                        })
                    );
                }
            }

            _texEffect = new BasicEffect(graphics) {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false,
            };
            _colEffect = new BasicEffect(graphics) {
                TextureEnabled = false,
                VertexColorEnabled = true,
                LightingEnabled = false,
            };

            _vertexBuffer = new VertexBuffer(graphics, typeof(VertexPositionNormalColorTexture), verts.Count, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(verts.ToArray());
            _indexBuffer = new IndexBuffer(graphics, typeof(int), indices.Count, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices.ToArray());

            PlayAnimation(0, true, 1f);

            Vector3 minBounds = Vector3.Zero, maxBounds = Vector3.Zero;
            Descend(_root, Matrix.Identity,
                (chunk, m) => {
                    var transformed = chunk.Verts
                        .Select(v => Vector3.Transform(v.Position.ToX(), m));
                    minBounds = new Vector3(
                        Math.Min(minBounds.X, transformed.Min(v => v.X)),
                        Math.Min(minBounds.Y, transformed.Min(v => v.Y)),
                        Math.Min(minBounds.Z, transformed.Min(v => v.Z))
                    );
                    maxBounds = new Vector3(
                        Math.Max(maxBounds.X, transformed.Max(v => v.X)),
                        Math.Max(maxBounds.Y, transformed.Max(v => v.Y)),
                        Math.Max(maxBounds.Z, transformed.Max(v => v.Z))
                    );
                }
            );
            MinBounds = minBounds;
            MaxBounds = maxBounds;
            System.Diagnostics.Debug.WriteLine($"Model {skeleton} with min bounds {minBounds}, max {maxBounds}");
        }

        private void Descend(BBone bone, Matrix m, Action<PFileChunk, Matrix> onChunk) {
            var frame = _animations.Anims[AnimationState.Animation].Frames[AnimationState.Frame];
            Matrix child = m;
            var rotation = frame.Rotations[bone.Index + 1];

            if (bone.Index >= 0) {
                child =
                    Matrix.CreateFromYawPitchRoll(
                      rotation.rY * 360f / 4096f * (float)Math.PI / 180,
                      rotation.rX * 360f / 4096f * (float)Math.PI / 180,
                      rotation.rZ * 360f / 4096f * (float)Math.PI / 180
                    )
                    /*
                                          Matrix.CreateRotationZ(rotation.rZ * 360f / 4096f * (float)Math.PI / 180)
                                        * Matrix.CreateRotationX(rotation.rX * 360f / 4096f * (float)Math.PI / 180)
                                        * Matrix.CreateRotationY(rotation.rY * 360f / 4096f * (float)Math.PI / 180)
                    */
                    * child
                ;
            } else {
                child =
                      Matrix.CreateFromYawPitchRoll(
                          rotation.rY * 360f / 4096f * (float)Math.PI / 180,
                          rotation.rX * 360f / 4096f * (float)Math.PI / 180,
                          rotation.rZ * 360f / 4096f * (float)Math.PI / 180
                      )
                    * Matrix.CreateTranslation(frame.X, frame.Y, frame.Z) //TODO check ordering here
                    * child
                ;
            }

            if (bone.PFileIndex != null) {
                foreach (var chunk in _pfiles[bone.PFileIndex.Value].Chunks) {
                    onChunk(chunk, child);
                }
            }

            child = Matrix.CreateTranslation(0, 0, bone.Length) * child;

            foreach (var cb in bone.Children)
                Descend(cb, child, onChunk);
        }

        public void Render(Viewer viewer) {
            _texEffect.View = viewer.View;
            _texEffect.Projection = viewer.Projection;
            _colEffect.View = viewer.View;
            _colEffect.Projection = viewer.Projection;

            _graphics.Indices = _indexBuffer;
            _graphics.SetVertexBuffer(_vertexBuffer);

            GraphicsState graphicsState = null;
            if (DeathFade.HasValue) {
                _fadeTexture.SetData(Enumerable.Repeat(new Color(1f, 0f, 0f, DeathFade.Value), 4).ToArray());
                graphicsState = new GraphicsState(_graphics, BlendState.Additive, DepthStencilState.DepthRead);
            }

            using (graphicsState) {
                Descend(
                    _root,
                      Matrix.CreateRotationX(0 * (float)Math.PI / 180)
                    * Matrix.CreateRotationZ((-Rotation.Z + Rotation2.Z) * (float)Math.PI / 180)
                    * Matrix.CreateRotationX((-Rotation.X + Rotation2.X) * (float)Math.PI / 180)
                    * Matrix.CreateRotationY((-Rotation.Y + Rotation2.Y) * (float)Math.PI / 180)
                    * Matrix.CreateScale(Scale, Scale, Scale)
                    * Matrix.CreateTranslation(Translation + Translation2)
                    ,
                      (chunk, m) => {
                          var rn = _nodes[chunk];
                          BasicEffect effect;
                          if (DeathFade.HasValue) {
                              _texEffect.World = m;
                              _texEffect.Texture = _fadeTexture;
                              effect = _texEffect;
                          } else {
                              _texEffect.World = _colEffect.World = m;
                              _texEffect.Texture = rn.Texture;
                              effect = rn.Texture == null ? _colEffect : _texEffect;
                          }

                          foreach (var pass in effect.CurrentTechnique.Passes) {
                              pass.Apply();
                              _graphics.DrawIndexedPrimitives(
                                  PrimitiveType.TriangleList, rn.VertOffset, rn.IndexOffset, rn.TriCount
                              );
                          }
                      }
                );
            }
        }

        private float _animCountdown;
        public void FrameStep() {
            _animCountdown -= AnimationState.AnimationSpeed * GlobalAnimationSpeed / 4f;
            if (_animCountdown <= 0) {
                _animCountdown = 1;
                if (AnimationState.Frame == AnimationState.EndFrame) {
                    if (AnimationState.AnimationLoop)
                        AnimationState.Frame = AnimationState.StartFrame;
                } else {
                    AnimationState.Frame++;
                }
            }
        }

        public void PlayAnimation(int animation, bool loop, float speed, int startFrame = 0, int endFrame = -1, bool onlyIfDifferent = true) {
            if ((AnimationState != null) && (AnimationState.Animation == animation) &&
                (AnimationState.AnimationLoop == loop) && (AnimationState.AnimationSpeed == speed) &&
                (AnimationState.StartFrame == startFrame) && (AnimationState.EndFrame == endFrame))
                return;

            AnimationState = new AnimationState {
                Animation = animation,
                AnimationLoop = loop,
                AnimationSpeed = speed,
                StartFrame = startFrame,
                Frame = startFrame,
                EndFrame = endFrame < 0 ? _animations.Anims[animation].Frames.Length - 1 : endFrame,
            };
            _animCountdown = 1;
        }


        public static Model LoadBattleModel(GraphicsDevice graphics, FGame game, string code) {
            var texs = Enumerable.Range((int)'c', 10).Select(i => code + "a" + ((char)i).ToString());
            int codecounter = 12;
            Func<System.IO.Stream> NextData = () => {
                System.IO.Stream stream;
                do {
                    char c1 = (char)('a' + (codecounter / 26));
                    char c2 = (char)('a' + (codecounter % 26));
                    codecounter++;
                    stream = game.TryOpen("battle", code + c1.ToString() + c2.ToString());
                } while (stream == null && (codecounter < 260));
                return stream;
            };
            return new Model(graphics, game, "battle", code + "aa", code + "da", texs, NextData);
        }
        public static Model LoadSummonModel(GraphicsDevice graphics, FGame game, string code) {
            var texs = Enumerable.Range(0, 99).Select(i => code + ".t" + i.ToString("00"));
            int part = 0;
            Func<System.IO.Stream> NextData = () => {
                System.IO.Stream s;
                do {
                    s = game.TryOpen("battle", code + ".p" + (part++).ToString("00"));
                } while (s == null && part <= 99);
                return s;
            };
            return new Model(graphics, game, "magic", code + ".d", code + ".a00", texs, NextData);
        }

    }
}
