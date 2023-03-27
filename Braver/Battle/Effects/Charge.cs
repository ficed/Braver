// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Battle.Effects {
    internal class Charge {

        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;
        private GraphicsDevice _graphics;
        private Texture2D _tex;
        private AlphaTestEffect _effect;

        private const float HEIGHT = 1000; //TODO?!
        private const float MAX_SIZE = 700; //TODO?!
        private const int NUM_INSTANCES = 4;

        //4 effect instances
        //Start fade at frame 22
        //Frame 30 is last frame per instance
        //Each instance starts 8? frames after previous
        //So 54 frames frames for entire process


        public Charge(GraphicsDevice graphics, Stream tex) {
            _graphics = graphics;

            var texFile = new Ficedula.FF7.TexFile(tex);
            _tex = graphics.LoadTex(texFile, 0);

            _effect = new AlphaTestEffect(graphics) {
                FogEnabled = false,
                VertexColorEnabled = false,                
                Texture = _tex,
            };

            List<VertexPositionTexture> verts = new();
            List<int> indices = new();

            foreach(int i in Enumerable.Range(0, 36)) {
                float angle = i * 10;
                float x = (float)Math.Sin(angle * Math.PI / 180),
                    z = (float)Math.Cos(angle * Math.PI / 180);

                verts.Add(new VertexPositionTexture {
                    Position = new Vector3(x, 0, z),
                    TextureCoordinate = new Vector2(i / 3f, 1f),
                });
                verts.Add(new VertexPositionTexture {
                    Position = new Vector3(x, 1f, z),
                    TextureCoordinate = new Vector2(i / 3f, 0f),
                });
            }
            foreach (int i in Enumerable.Range(0, 36)) {
                indices.Add(i * 2); indices.Add(i * 2 + 1);
                indices.Add((i * 2 + 2) % verts.Count);

                indices.Add(i * 2 + 1);
                indices.Add((i * 2 + 2) % verts.Count);
                indices.Add((i * 2 + 3) % verts.Count);
            }

            _vertexBuffer = new VertexBuffer(graphics, typeof(VertexPositionTexture), verts.Count, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(verts.ToArray());
            _indexBuffer = new IndexBuffer(graphics, typeof(int), indices.Count, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices.ToArray());
        }

        private void DoRender(float scale, float alpha, Vector3 center, Viewer view) {
            _effect.View = view.View;
            _effect.Projection = view.Projection;
            _effect.World = Matrix.CreateScale(scale, -HEIGHT, scale)
                * Matrix.CreateTranslation(center);
            _effect.Alpha = alpha;

            foreach (var pass in _effect.CurrentTechnique.Passes) {
                pass.Apply();
                _graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _indexBuffer.IndexCount / 3);
            }

        }

        public bool Render(Vector3 center, Viewer view, int frameProgress) {
            _graphics.Indices = _indexBuffer;
            _graphics.SetVertexBuffer(_vertexBuffer);

            using(var state = new GraphicsState(_graphics, blend: BlendState.Additive, depthStencilState: DepthStencilState.DepthRead, rasterizerState: RasterizerState.CullNone)) {
                int finished = 0;
                foreach(int instance in Enumerable.Range(0, NUM_INSTANCES)) {
                    int start = instance * 8,
                        falloff = 22 + instance * 8,
                        end = 30 + instance * 8;
                    if (frameProgress < start) {
                        //
                    } else if (frameProgress > end) {
                        finished++;
                    } else if (frameProgress < falloff) {
                        DoRender(MAX_SIZE * (frameProgress - start) / (end - start), 1f, center, view);
                    } else { //fading out
                        DoRender(MAX_SIZE * (frameProgress - start) / (end - start), 1f - (1f * frameProgress - falloff) / (end - falloff), center, view);
                    }
                }
                return finished >= NUM_INSTANCES;
            }
        }
    }
}
