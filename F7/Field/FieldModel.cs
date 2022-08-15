using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Field {

    public class AnimationState {
        public float AnimationSpeed { get; set; }
        public int Animation { get; set; }
        public int Frame { get; set; }
        public Action AnimationComplete { get; set; }
        public bool AnimationLoop { get; set; }
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
    }

    public class FieldModel {
        public Vector3 Rotation2 { get; set; }
        public Vector3 Rotation { get; set; }
        public Vector3 Translation { get; set; }
        public Vector3 Translation2 { get; set; }
        public float Scale { get; set; } = 1f;
        public bool Visible { get; set; } = true;
        public float GlobalAnimationSpeed { get; set; } = 1f;
        public AnimationState AnimationState { get; set; }
        public Vector3 MinBounds { get; }
        public Vector3 MaxBounds { get; }
        public bool ZUp { get; set; } = true;

        private class RenderNode {
            public int VertOffset, IndexOffset, TriCount;
            public Texture2D Texture;
        }

        private Dictionary<Ficedula.FF7.PFileChunk, RenderNode> _nodes = new();
        private BasicEffect _texEffect, _colEffect;
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private Ficedula.FF7.Field.HRCModel _hrcModel;
        private GraphicsDevice _graphics;
        private List<Ficedula.FF7.Field.FieldAnim> _animations = new();

        //TODO dedupe textures
        public FieldModel(GraphicsDevice graphics, FGame g, string hrc, IEnumerable<string> animations, string category = "field") {
            _graphics = graphics;
            _hrcModel = new Ficedula.FF7.Field.HRCModel(s => g.Open(category, s), hrc);

            List<VertexPositionNormalColorTexture> verts = new();
            List<int> indices = new();

            void DescendBuild(Ficedula.FF7.Field.HRCModel.Bone bone) {

                foreach (var poly in bone.Polygons) {
                    var textures = poly.Textures
                        .Select(t => graphics.LoadTex(t, 0))
                        .ToArray();
                    foreach (var chunk in poly.PFile.Chunks) {
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

                foreach (var child in bone.Children)
                    DescendBuild(child);
            }
            DescendBuild(_hrcModel.Root);

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

            _animations = animations
                .Select(a => new Ficedula.FF7.Field.FieldAnim(g.Open(category, a)))
                .ToList();

            PlayAnimation(0, true, 1f, null);

            Vector3 minBounds = Vector3.Zero, maxBounds = Vector3.Zero;
            Descend(_hrcModel.Root, Matrix.Identity,
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
            System.Diagnostics.Debug.WriteLine($"Model {hrc} with min bounds {minBounds}, max {maxBounds}");
        }

        private void Descend(Ficedula.FF7.Field.HRCModel.Bone bone, Matrix m, Action<Ficedula.FF7.PFileChunk, Matrix> onChunk) {
            var frame = _animations[AnimationState.Animation].Frames[AnimationState.Frame];
            Matrix child = m;
            if (bone.Index >= 0) {
                var rotation = frame.Bones[bone.Index];
                child =
                      Matrix.CreateRotationZ(rotation.Z * (float)Math.PI / 180)
                    * Matrix.CreateRotationX(rotation.X * (float)Math.PI / 180)
                    * Matrix.CreateRotationY(rotation.Y * (float)Math.PI / 180)
                    * child
                ;
            } else {
                child =
                      Matrix.CreateRotationZ(frame.Rotation.Z * (float)Math.PI / 180)
                    * Matrix.CreateRotationX(frame.Rotation.X * (float)Math.PI / 180)
                    * Matrix.CreateRotationY(frame.Rotation.Y * (float)Math.PI / 180)
                    * Matrix.CreateTranslation(frame.Translation.ToX()) //TODO check ordering here
                    * child
                ;
            }

            if (bone.Polygons.Any()) {
                foreach (var poly in bone.Polygons) {
                    foreach (var chunk in poly.PFile.Chunks) {
                        onChunk(chunk, child);
                    }
                }
            }

            child = Matrix.CreateTranslation(0, 0, -bone.Length) * child;

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

            Descend(
                _hrcModel.Root,
                  Matrix.CreateRotationX((ZUp ? -90 : 0) * (float)Math.PI / 180)
                * Matrix.CreateRotationZ((-Rotation.Z + Rotation2.Z) * (float)Math.PI / 180)
                * Matrix.CreateRotationX((-Rotation.X + Rotation2.X) * (float)Math.PI / 180)
                * Matrix.CreateRotationY((-Rotation.Y + Rotation2.Y) * (float)Math.PI / 180)
                * Matrix.CreateScale(Scale, -Scale, Scale)
                * Matrix.CreateTranslation(Translation + Translation2)
                ,
                  (chunk, m) => {
                      _texEffect.World = _colEffect.World = m;
                      var rn = _nodes[chunk];
                      _texEffect.Texture = rn.Texture;

                      var effect = rn.Texture == null ? _colEffect : _texEffect;
                      foreach (var pass in effect.CurrentTechnique.Passes) {
                          pass.Apply();
                          _graphics.DrawIndexedPrimitives(
                              PrimitiveType.TriangleList, rn.VertOffset, rn.IndexOffset, rn.TriCount
                          );
                      }
                  }
            );
        }

        private float _animCountdown;
        public void FrameStep() {
            _animCountdown -= AnimationState.AnimationSpeed * GlobalAnimationSpeed;
            if (_animCountdown <= 0) {
                _animCountdown = 1;
                if (AnimationState.Frame == AnimationState.EndFrame) {
                    AnimationState.AnimationComplete?.Invoke();
                    AnimationState.AnimationComplete = null;
                    if (AnimationState.AnimationLoop)
                        AnimationState.Frame = AnimationState.StartFrame;
                } else {
                    AnimationState.Frame++;
                }
            }
        }

        public void PlayAnimation(int animation, bool loop, float speed, Action onComplete, int startFrame = 0, int endFrame = -1) {
            AnimationState = new AnimationState {
                Animation = animation,
                AnimationLoop = loop,
                AnimationSpeed = speed,
                AnimationComplete = onComplete,
                StartFrame = startFrame,
                Frame = startFrame,
                EndFrame = endFrame < 0 ? _animations[animation].Frames.Count - 1 : endFrame,
            }; 
            _animCountdown = 1;
        }

    }
}
