// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Field {
    internal class HRCFieldModel : Plugins.Field.FieldModelRenderer {
        private Vector3 _minBounds, _maxBounds;

        public override Vector3 MinBounds => _minBounds;
        public override Vector3 MaxBounds => _maxBounds;
        public override int AnimationCount => _animations.Count;


        private List<Ficedula.FF7.Field.FieldAnim> _animations = new();
        private class RenderNode {
            public int VertOffset, IndexOffset, TriCount;
            public Texture2D Texture;
            public BlendType BlendType;
            public bool LightingEnabled;
            public bool IsEye;
        }

        private Dictionary<PFileChunk, RenderNode> _nodes = new();
        private BasicEffect _texEffect, _colEffect;
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private Ficedula.FF7.Field.HRCModel _hrcModel;
        private GraphicsDevice _graphics;
        private Vector3 _light1Pos, _light2Pos, _light3Pos;
        private bool _lightingEnabled, _shineEffect;
        private int _shineRotation;

        private void Descend(Ficedula.FF7.Field.HRCModel.Bone bone, Matrix m, Action<PFileChunk, Matrix> onChunk, int anim, int animFrame) {
            var frame = _animations[anim].Frames[animFrame];
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
                Descend(cb, child, onChunk, anim, animFrame);
        }

        public override void ConfigureLighting(Vector3 ambient, bool shineEffect) {
            _shineEffect = shineEffect;
            _colEffect.AmbientLightColor = _texEffect.AmbientLightColor = ambient;
            _texEffect.DirectionalLight0.SpecularColor = _colEffect.DirectionalLight0.SpecularColor = shineEffect ? Vector3.One : Vector3.Zero;
        }

        public override int GetFrameCount(int anim) => _animations[anim].Frames.Count;

        public override void Init(BGame game, GraphicsDevice graphics, string category, string hrc, 
            IEnumerable<string> animations,
            uint? globalLightColour = null,
            uint? light1Colour = null, Vector3? light1Pos = null,
            uint? light2Colour = null, Vector3? light2Pos = null,
            uint? light3Colour = null, Vector3? light3Pos = null) {
            _graphics = graphics;
            _hrcModel = new Ficedula.FF7.Field.HRCModel(s => game.Open(category, s), hrc);

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
                            BlendType = chunk.RenderState.BlendMode,
                            LightingEnabled = chunk.RenderState.Features.HasFlag(RenderEffect.ShadeMode) && chunk.RenderState.ShadeMode == 2,
                            IsEye = (textures.Length == 3) && (chunk.Texture != null) && (chunk.Texture.Value <= 1),
                            //TODO - there must be a flag somewhere to indicate eyes in a model
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

            foreach (var effect in new[] { _texEffect, _colEffect }) {
                if (globalLightColour == null)
                    _lightingEnabled = effect.LightingEnabled = false;
                else {
                    _lightingEnabled = effect.LightingEnabled = true;
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
                .Select(a => new Ficedula.FF7.Field.FieldAnim(game.Open(category, a)))
                .ToList();

            _minBounds = Vector3.Zero;
            _maxBounds = Vector3.Zero;
            Descend(_hrcModel.Root, Matrix.Identity,
                (chunk, m) => {
                    var transformed = chunk.Verts
                        .Select(v => Vector3.Transform(v.Position.ToX(), m));
                    _minBounds = new Vector3(
                        Math.Min(_minBounds.X, transformed.Min(v => v.X)),
                        Math.Min(_minBounds.Y, transformed.Min(v => v.Y)),
                        Math.Min(_minBounds.Z, transformed.Min(v => v.Z))
                    );
                    _maxBounds = new Vector3(
                        Math.Max(_maxBounds.X, transformed.Max(v => v.X)),
                        Math.Max(_maxBounds.Y, transformed.Max(v => v.Y)),
                        Math.Max(_maxBounds.Z, transformed.Max(v => v.Z))
                    );
                },
                0, 0
            );
        }

        public override void FrameStep() {
            _shineRotation++;
        }

        public override void Render(Vector3 modelPosition, Matrix view, Matrix projection, Matrix transform, 
            int animation, int frame, bool eyeBlink, bool transparentGroups) {
            if (_vertexBuffer == null) return;

            var depth = transparentGroups ? DepthStencilState.DepthRead : DepthStencilState.Default;

            using (var state = new GraphicsState(_graphics, depthStencilState: depth, forceSaveAll: true)) {

                if (_lightingEnabled) {
                    Matrix lightRotate;
                    int r = (_shineRotation * 6) % 720;
                    if (_shineEffect && (r < 360))
                        lightRotate = Matrix.CreateRotationZ(r * (float)Math.PI / 180);
                    else
                        lightRotate = Matrix.Identity;


                    Vector3 direction = modelPosition - _light1Pos;
                    direction.Normalize();
                    direction = Vector3.Transform(direction, lightRotate);
                    _texEffect.DirectionalLight0.Direction = _colEffect.DirectionalLight0.Direction = direction;

                    direction = modelPosition - _light2Pos;
                    direction.Normalize();
                    direction = Vector3.Transform(direction, lightRotate);
                    _texEffect.DirectionalLight1.Direction = _colEffect.DirectionalLight1.Direction = direction;

                    direction = modelPosition - _light3Pos;
                    direction.Normalize();
                    direction = Vector3.Transform(direction, lightRotate);
                    _texEffect.DirectionalLight2.Direction = _colEffect.DirectionalLight2.Direction = direction;
                }

                _texEffect.View = view;
                _texEffect.Projection = projection;
                _colEffect.View = view;
                _colEffect.Projection = projection;

                _graphics.Indices = _indexBuffer;
                _graphics.SetVertexBuffer(_vertexBuffer);

                _graphics.SamplerStates[0] = SamplerState.AnisotropicWrap;

                Descend(
                    _hrcModel.Root, transform,
                      (chunk, m) => {
                          _texEffect.World = _colEffect.World = m;
                          var rn = _nodes[chunk];

                          bool isBlending;
                          switch (rn.BlendType) {
                              case BlendType.Subtractive:
                              case BlendType.QuarterAdd:
                              case BlendType.Additive:
                                  isBlending = true; break;
                              default:
                                  isBlending = false; break;
                          }

                          if (isBlending != transparentGroups)
                              return;

                          if (eyeBlink && rn.IsEye)
                              return;

                          _texEffect.Texture = rn.Texture;
                          _texEffect.LightingEnabled = _colEffect.LightingEnabled = _lightingEnabled && rn.LightingEnabled;

                          switch (rn.BlendType) {
                              case BlendType.None0:
                              case BlendType.None1:
                              case BlendType.Blend:
                                  _graphics.BlendState = BlendState.AlphaBlend;
                                  break;
                              case BlendType.Subtractive:
                                  _graphics.BlendState = GraphicsUtil.BlendSubtractive;
                                  break;
                              case BlendType.Additive:
                                  _graphics.BlendState = BlendState.Additive;
                                  break;
                              case BlendType.QuarterAdd:
                                  _graphics.BlendState = GraphicsUtil.BlendQuarterAdd;
                                  break;
                              default:
                                  throw new NotImplementedException();
                          }

                          var effect = rn.Texture == null ? _colEffect : _texEffect;
                          foreach (var pass in effect.CurrentTechnique.Passes) {
                              pass.Apply();
                              _graphics.DrawIndexedPrimitives(
                                  PrimitiveType.TriangleList, rn.VertOffset, rn.IndexOffset, rn.TriCount
                              );
                          }
                      },
                      animation, frame
                );
            }
        }
    }
}
