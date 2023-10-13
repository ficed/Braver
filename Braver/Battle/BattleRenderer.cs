// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Net;
using Ficedula.FF7.Battle;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Braver.Battle {

    public interface ICameraView {
        PerspView3D View { get; }
    }

    public static class CameraUtil {
        public static PerspView3D ToView3D(this BattleCamera cam) {
            return new PerspView3D {
                CameraPosition = new Vector3(cam.X, cam.Y, cam.Z),
                CameraForwards = new Vector3(cam.LookAtX - cam.X, cam.LookAtY - cam.Y, cam.LookAtZ - cam.Z),
                CameraUp = -Vector3.UnitY, //TODO!!
                ZNear = 100,
                ZFar = 100000,
                FOV = 25f,
            };
        }
    }

    public class BattleRenderer<T> {
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

        private Screen _ui;
        private ICameraView _view;

        public FGame Game { get; private set; }
        public GraphicsDevice Graphics { get; private set; }
        public Dictionary<T, Model> Models { get; } = new();
        public SpriteRenderer Sprites { get; }

        public BattleRenderer(FGame game, GraphicsDevice graphics, Screen uiScreen, ICameraView view) {
            Game = game;
            Graphics = graphics;
            _ui = uiScreen;
            _view = view;
            Sprites = new SpriteRenderer(graphics);
        }

        public void LoadBackground(int locationID) {
            string prefix = SceneDecoder.LocationIDToFileName(locationID);

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

        public void Step(GameTime elapsed) {
            foreach (var model in Models.Values) {
                model.FrameStep();
            }
            Sprites.FrameStep();
            _ui.Step(elapsed);
        }

        public void Render() {
            Graphics.DepthStencilState = DepthStencilState.Default;
            Graphics.RasterizerState = RasterizerState.CullClockwise;
            Graphics.SamplerStates[0] = SamplerState.LinearWrap;

            Graphics.Indices = _indexBuffer;
            Graphics.SetVertexBuffer(_vertexBuffer);

            foreach (var chunk in _backgroundChunks) {
                chunk.Effect.View = _view.View.View;
                chunk.Effect.Projection = _view.View.Projection;
                foreach (var pass in chunk.Effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    Graphics.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList, chunk.VertOffset, chunk.IndexOffset, chunk.TriCount
                    );
                }
            }

            foreach (int pass in Enumerable.Range(0, 2)) {
                foreach (var model in Models.Values)
                    if (model.Visible)
                        model.Render(_view.View, pass == 1);
            }

            Sprites.Render();

            _ui.Render();
        }

    }
}
