// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

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
        public bool AnimationLoop { get; set; }
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
        public int CompletionCount { get; set; }
    }

    public class FieldModel {

        private Vector3 _rotation, _rotation2, _translation, _translation2;
        private float _scale;
        private bool _visible = true, _shineEffect;
        private AnimationState _animationState;
        private int _modelID;

        private void DoSetNet<T>(ref T field, T value, Action<Net.FieldModelMessage> setNet) {
            field = value;
            var msg = new Net.FieldModelMessage {
                ModelID = _modelID
            };
            setNet(msg);
            _game.Net.Send(msg);
        }

        public Vector3 Rotation2 {
            get => _rotation2;
            set => DoSetNet(ref _rotation2, value, msg => msg.Rotation2 = value);
        }
        public Vector3 Rotation {
            get => _rotation;
            set => DoSetNet(ref _rotation, value, msg => msg.Rotation = value);
        }
        public Vector3 Translation {
            get => _translation;
            set => DoSetNet(ref _translation, value, msg => msg.Translation = value);
        }
        public Vector3 Translation2 {
            get => _translation2;
            set => DoSetNet(ref _translation2, value, msg => msg.Translation2 = value);
        }
        public float Scale {
            get => _scale;
            set => DoSetNet(ref _scale, value, msg => msg.Scale = value);
        }
        public bool Visible {
            get => _visible;
            set => DoSetNet(ref _visible, value, msg => msg.Visible = value);
        }
        public AnimationState AnimationState {
            get => _animationState;
            set => DoSetNet(ref _animationState, value, msg => msg.AnimationState = value);
        }

        public Vector3 AmbientLightColour {
            get => _colEffect.AmbientLightColor;
            set {
                _colEffect.AmbientLightColor = _texEffect.AmbientLightColor = value;
                //TODO net message
            }
        }

        public bool ShineEffect {
            get => _shineEffect;
            set {
                _shineEffect = value;
                _texEffect.DirectionalLight0.SpecularColor = _colEffect.DirectionalLight0.SpecularColor = _shineEffect ? Vector3.One : Vector3.Zero;
            }
        } //TODO net message

        public float GlobalAnimationSpeed { get; set; } = 1f;
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
        private FGame _game;
        private List<Ficedula.FF7.Field.FieldAnim> _animations = new();

        private Vector3 _light1Pos, _light2Pos, _light3Pos;

        //TODO dedupe textures
        public FieldModel(GraphicsDevice graphics, FGame g, int modelID, string hrc, IEnumerable<string> animations, 
            string category = "field", uint? globalLightColour = null,
            uint? light1Colour = null, Vector3? light1Pos = null,
            uint? light2Colour = null, Vector3? light2Pos = null,
            uint? light3Colour = null, Vector3? light3Pos = null
            ) {
            _graphics = graphics;
            _game = g;
            _modelID = modelID;
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
            };
            _colEffect = new BasicEffect(graphics) {
                TextureEnabled = false,
                VertexColorEnabled = true,
            };

            foreach(var effect in new[] { _texEffect, _colEffect }) {
                if (globalLightColour == null)
                    effect.LightingEnabled = false;
                else {
                    effect.LightingEnabled = true;
                    effect.PreferPerPixelLighting = true;
                    effect.AmbientLightColor = new Color(globalLightColour.Value).ToVector3();

                    _light1Pos = light1Pos.Value;
                    _light2Pos = light2Pos.Value;
                    _light3Pos = light3Pos.Value;

                    effect.DirectionalLight0.DiffuseColor = new Color(light1Colour.Value).ToVector3();
                    effect.DirectionalLight0.Enabled = true;

                    effect.DirectionalLight1.DiffuseColor = new Color(light2Colour.Value).ToVector3();
                    effect.DirectionalLight1.Enabled = true;

                    effect.DirectionalLight2.DiffuseColor = new Color(light3Colour.Value).ToVector3();
                    effect.DirectionalLight2.Enabled = true;

                }
            }

            if (verts.Any()) {
                _vertexBuffer = new VertexBuffer(graphics, typeof(VertexPositionNormalColorTexture), verts.Count, BufferUsage.WriteOnly);
                _vertexBuffer.SetData(verts.ToArray());
                _indexBuffer = new IndexBuffer(graphics, typeof(int), indices.Count, BufferUsage.WriteOnly);
                _indexBuffer.SetData(indices.ToArray());
            }

            _animations = animations
                .Select(a => new Ficedula.FF7.Field.FieldAnim(g.Open(category, a)))
                .ToList();

            PlayAnimation(0, true, 1f);

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
            System.Diagnostics.Trace.WriteLine($"Model {hrc} with min bounds {minBounds}, max {maxBounds}");
        }

        private void Descend(Ficedula.FF7.Field.HRCModel.Bone bone, Matrix m, Action<Ficedula.FF7.PFileChunk, Matrix> onChunk) {
            var frame = _animations[AnimationState.Animation].Frames[AnimationState.Frame];
            Matrix child = m;
            if (bone.Index >= 0) {
                var rotation = frame.Bones.ElementAtOrDefault(bone.Index); //Single-element HRCs don't actually have bone data in the anim :/
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
                    * Matrix.CreateTranslation(frame.Translation.ToX() * new Vector3(1, -1, 1)) //TODO check ordering here
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
            if (_vertexBuffer == null) return;

            if (_texEffect.LightingEnabled) {

                Matrix lightRotate;
                int r = (_shineRotation * 6) % 720;
                if (ShineEffect && (r < 360))
                    lightRotate = Matrix.CreateRotationZ(r * (float)Math.PI / 180);
                else
                    lightRotate = Matrix.Identity;
                    

                Vector3 direction = Translation - _light1Pos;
                direction.Normalize();
                direction = Vector3.Transform(direction, lightRotate);
                _texEffect.DirectionalLight0.Direction = _colEffect.DirectionalLight0.Direction = direction;

                direction = Translation - _light2Pos;
                direction.Normalize();
                direction = Vector3.Transform(direction, lightRotate);
                _texEffect.DirectionalLight1.Direction = _colEffect.DirectionalLight1.Direction = direction;

                direction = Translation - _light3Pos;
                direction.Normalize();
                direction = Vector3.Transform(direction, lightRotate);
                _texEffect.DirectionalLight2.Direction = _colEffect.DirectionalLight2.Direction = direction;
            }

            _texEffect.View = viewer.View;
            _texEffect.Projection = viewer.Projection;
            _colEffect.View = viewer.View;
            _colEffect.Projection = viewer.Projection;

            _graphics.Indices = _indexBuffer;
            _graphics.SetVertexBuffer(_vertexBuffer);

            _graphics.SamplerStates[0] = SamplerState.AnisotropicWrap;

            Descend(
                _hrcModel.Root,
                  Matrix.CreateRotationX((ZUp ? -90 : 0) * (float)Math.PI / 180)
                * Matrix.CreateRotationZ((Rotation.Z + Rotation2.Z) * (float)Math.PI / 180)
                * Matrix.CreateRotationX((Rotation.X + Rotation2.X) * (float)Math.PI / 180)
                * Matrix.CreateRotationY((Rotation.Y + Rotation2.Y) * (float)Math.PI / 180)
                * Matrix.CreateScale(Scale, Scale, Scale)
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
        private int _shineRotation;

        public void FrameStep() {
            _animCountdown -= AnimationState.AnimationSpeed * GlobalAnimationSpeed;
            _shineRotation++;
            if (_animCountdown <= 0) {
                _animCountdown = 1;
                if (AnimationState.Frame == AnimationState.EndFrame) {
                    if (AnimationState.AnimationLoop) {
                        AnimationState.Frame = AnimationState.StartFrame;
                        AnimationState.CompletionCount++;
                    } else
                        AnimationState.CompletionCount = 1;
                } else {
                    AnimationState.Frame++;
                }
            }
        }

        public void PlayAnimation(int animation, bool loop, float speed, int startFrame = 0, int endFrame = -1) {            
            AnimationState = new AnimationState {
                Animation = animation,
                AnimationLoop = loop,
                AnimationSpeed = speed,
                StartFrame = startFrame,
                Frame = startFrame,
                EndFrame = endFrame < 0 ? _animations[animation].Frames.Count - 1 : endFrame,
            }; 
            _animCountdown = 1;
        }

    }
}
