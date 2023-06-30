// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Braver.WorldMap {

    internal class WMScreen : Screen {

        private const int SECTOR_SIZE = 8192;
        private const int BLOCK_SIZE = SECTOR_SIZE * 4;
        private const int BLOCKS_X = 9;
        private const int BLOCKS_Y = 7;
        private const int MAP_WIDTH = BLOCK_SIZE * BLOCKS_X;
        private const int MAP_HEIGHT = BLOCK_SIZE * BLOCKS_Y;

        public override string Description => "World Map";

        private class SkyBox {
            private BasicEffect _effect;
            private Texture2D _texture;
            private VertexBuffer _vertices;
            private IndexBuffer _indices;


            private const int SKYBOX_HEIGHT = 1024 * 12;
            private const int SKYBOX_DISTANCE = BLOCK_SIZE * 3;
            private const int SKYBOX_SEGMENTS = 64;

            public SkyBox(FGame g, GraphicsDevice graphics) {
                using (var s = g.Open("wm", "wm_kumo.tex"))
                    _texture = graphics.LoadTex(new Ficedula.FF7.TexFile(s), 0);

                _effect = new BasicEffect(graphics) {
                    FogEnabled = false,
                    LightingEnabled = false,
                    TextureEnabled = true,
                    VertexColorEnabled = false,
                    Texture = _texture,
                };

                List<VertexPositionTexture> verts = new();
                List<int> indices = new();

                foreach (int i in Enumerable.Range(0, SKYBOX_SEGMENTS + 1)) {
                    double angle = i * Math.PI * 2 / SKYBOX_SEGMENTS;
                    (double sin, double cos) = Math.SinCos(angle);
                    float x = (float)(SKYBOX_DISTANCE * sin),
                        y = (float)(SKYBOX_DISTANCE * cos);

                    verts.Add(new VertexPositionTexture {
                        Position = new Vector3(x, 0, y),
                        TextureCoordinate = new Vector2(i * 0.25f, 1),
                    });
                    verts.Add(new VertexPositionTexture {
                        Position = new Vector3(x, SKYBOX_HEIGHT, y),
                        TextureCoordinate = new Vector2(i * 0.25f, 0),
                    });

                    if (i < SKYBOX_SEGMENTS) {
                        indices.Add(i * 2);
                        indices.Add(i * 2 + 2);
                        indices.Add(i * 2 + 1);

                        indices.Add(i * 2 + 2);
                        indices.Add(i * 2 + 3);
                        indices.Add(i * 2 + 1);
                    }
                }

                _vertices = new VertexBuffer(graphics, typeof(VertexPositionTexture), verts.Count, BufferUsage.WriteOnly);
                _vertices.SetData(verts.ToArray());
                _indices = new IndexBuffer(graphics, typeof(int), indices.Count, BufferUsage.WriteOnly);
                _indices.SetData(indices.ToArray());
            }

            public void Render(GraphicsDevice graphics, View3D view) {
                _effect.Projection = view.Projection;
                _effect.View = view.View;
                _effect.World = Matrix.CreateTranslation(view.CameraPosition.X, 0, view.CameraPosition.Z);

                graphics.SetVertexBuffer(_vertices);
                graphics.Indices = _indices;

                //TODO implement using(...) to restore blend
                graphics.BlendState = BlendState.Additive;
                
                foreach(var pass in _effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _indices.IndexCount / 3);
                }

                graphics.BlendState = BlendState.AlphaBlend;
            }
        }

        private class MapBlock {
            private List<VertexPositionNormalTexture> _verts = new();
            public VertexBuffer Buffer;

            public List<Ficedula.FF7.WorldMap.MapSector> Sectors { get; }

            public struct TriGroup {
                public int Texture;
                public int Offset;
                public int Count;
            }

            public List<TriGroup> Groups { get; } = new();

            private Vector3 ToX(Ficedula.FF7.WorldMap.MapVert v, int xOffset, int yOffset) {
                return new Vector3(v.X + xOffset, v.Y, v.Z + yOffset);
            }
            private Vector3 ToXNorm(Ficedula.FF7.WorldMap.MapVert v) {
                var x = new Vector3(v.X, v.Y, v.Z);
                x.Normalize();
                return x;
            }

            public MapBlock(IEnumerable<Ficedula.FF7.WorldMap.MapSector> sectors) {
                Sectors = sectors.ToList();

                var byTex = sectors
                    .SelectMany(s => s.Triangles.Select(tri => new { Sector = s, Tri = tri }))
                    .GroupBy(a => a.Tri.Texture);

                foreach (var group in byTex) {
                    int start = _verts.Count;
                    foreach (var tri in group) {
                        tri.Tri.GetCorrectedTextureInfo(out _, out var uv0, out var uv1, out var uv2);
                        _verts.Add(new VertexPositionNormalTexture {
                            Position = ToX(tri.Sector.Vertices[tri.Tri.Vert0], tri.Sector.OffsetX, tri.Sector.OffsetY),
                            Normal = ToXNorm(tri.Sector.Normals[tri.Tri.Vert0]),
                            TextureCoordinate = new Vector2(uv0.X, uv0.Y),
                        });
                        _verts.Add(new VertexPositionNormalTexture {
                            Position = ToX(tri.Sector.Vertices[tri.Tri.Vert1], tri.Sector.OffsetX, tri.Sector.OffsetY),
                            Normal = ToXNorm(tri.Sector.Normals[tri.Tri.Vert1]),
                            TextureCoordinate = new Vector2(uv1.X, uv1.Y),
                        });
                        _verts.Add(new VertexPositionNormalTexture {
                            Position = ToX(tri.Sector.Vertices[tri.Tri.Vert2], tri.Sector.OffsetX, tri.Sector.OffsetY),
                            Normal = ToXNorm(tri.Sector.Normals[tri.Tri.Vert2]),
                            TextureCoordinate = new Vector2(uv2.X, uv2.Y),
                        });
                    }
                    Groups.Add(new TriGroup {
                        Texture = group.Key,
                        Offset = start,
                        Count = _verts.Count - start,
                    });
                }
            }

            public void Upload(GraphicsDevice graphics) { 
                Buffer = new VertexBuffer(graphics, typeof(VertexPositionNormalTexture), _verts.Count, BufferUsage.WriteOnly);
                Buffer.SetData(_verts.ToArray());
            }
        }

        public override Color ClearColor => new Color(0xffff3f3f);

        private enum MapBlockState { None, Loading, Ready };

        //private BasicEffect _effect;
        private AlphaTestEffect _effect;
        private PerspView3D _view;
        private Vector3 _cameraOffset;
        private PerspView3D _transitionCameraFrom;
        private float _transitionCameraProgress;

        private MapBlock[,] _blocks;
        private MapBlockState[,] _blockState;
        private Texture2D[][] _textures;

        private Ficedula.FF7.WorldMap _source;

        private Field.FieldModel _avatarModel;
        private Avatar _avatar;
        private WalkmapType _walkingOn;
        private bool _wasMoving, _isDebug, _panMode;
        private SkyBox _skybox;

        private UI.UIBatch _ui;

        private Channel<(int x, int y)> _load = Channel.CreateUnbounded<(int x, int y)>();

        private static float? HeightInTriangle(Vector3 p0, Vector3 p1, Vector3 p2, float x, float y) {
            var denominator = (p1.Z - p2.Z) * (p0.X - p2.X) + (p2.X - p1.X) * (p0.Z - p2.Z);
            var a = ((p1.Z - p2.Z) * (x - p2.X) + (p2.X - p1.X) * (y - p2.Z)) / denominator;
            var b = ((p2.Z - p0.Z) * (x - p2.X) + (p0.X - p2.X) * (y - p2.Z)) / denominator;
            var c = 1 - a - b;

            if (a < 0) return null;
            if (b < 0) return null;
            if (c < 0) return null;
            if (a > 1) return null;
            if (b > 1) return null;
            if (c > 1) return null;

            return p0.Y * a + p1.Y * b + p2.Y * c;
        }

        private bool CalculateLocation(float x, float z, out float height, out WalkmapType walkmapType) {
            int bx = (int)Math.Floor(x / BLOCK_SIZE),
                by = (int)Math.Floor(z / BLOCK_SIZE);

            switch(_blockState[bx, by]) {
                case MapBlockState.Ready:
                    var block = _blocks[bx, by];
                    float blockX = x - bx * BLOCK_SIZE, blockY = z - by * BLOCK_SIZE;
                    int sx = (int)Math.Floor(blockX / SECTOR_SIZE),
                        sy = (int)Math.Floor(blockY / SECTOR_SIZE);

                    var sector = block.Sectors[sx + sy * 4];
                    float sectorX = blockX - sx * SECTOR_SIZE,
                        sectorY = blockY - sy * SECTOR_SIZE;

                    Vector3 GetV(Ficedula.FF7.WorldMap.MapVert v) {
                        return new Vector3(v.X, v.Y, v.Z);
                    }

                    foreach(var tri in sector.Triangles) {
                        var h = HeightInTriangle(
                            GetV(sector.Vertices[tri.Vert0]),
                            GetV(sector.Vertices[tri.Vert1]),
                            GetV(sector.Vertices[tri.Vert2]),
                            sectorX, sectorY
                        );
                        if (h != null) {
                            height = h.Value;
                            walkmapType = (WalkmapType)(1U << tri.Walkmap);
                            return true;
                        }
                    }

                    break;

                default:
                    break;
            }

            walkmapType = WalkmapType.Unused4;
            height = 0;
            return false;
        }

        private async Task Loader() {
            while (true) {
                var which = await _load.Reader.ReadAsync();
                if (which.x < 0) return;
                var block = new MapBlock(_source.ExportBlock(which.x + which.y * BLOCKS_X));
                Game.InvokeOnMainThread(() => {
                    if (_blockState != null) { //things may have already been disposed, if so, don't upload!
                        Trace.WriteLine($"Uploading block {which.x},{which.y}");
                        block.Upload(Graphics);
                        _blockState[which.x, which.y] = MapBlockState.Ready;
                        _blocks[which.x, which.y] = block;

                        if (BlockForAvatar() == which) {
                            TryMoveAvatarTo(_avatarModel.Translation); //to set height
                        }
                    }
                });
            }
        }

        private void AnimateAvatar(string name) {
            int index = _avatar.Animations.FindIndex(a => a.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
            _avatarModel.PlayAnimation(index, true, 0.5f);
        }

        private float _initAvatarX, _initAvatarY;
        public WMScreen(float avatarX, float avatarY) {
            _initAvatarX = avatarX;
            _initAvatarY = avatarY;
        }

        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);
            _effect = new AlphaTestEffect(graphics) {
                VertexColorEnabled = false,
                FogEnabled = true,
                FogColor = new Vector3(0.75f),
                FogStart = BLOCK_SIZE * 0.90f,
                FogEnd = BLOCK_SIZE * 1.3f,
                AlphaFunction = CompareFunction.GreaterEqual,
                ReferenceAlpha = 4,
            };

            using (var s = g.Open("wm", g.SaveData.WorldMapAvatar + ".xml"))
                _avatar = Serialisation.Deserialise<Avatar>(s);

            _avatarModel = new Field.FieldModel(
                graphics, g, 0, _avatar.HRC + ".hrc", 
                _avatar.Animations.Select(a => a.File),
                "wm"
            );
            _avatarModel.ZUp = false;
            _avatarModel.Scale = _avatar.Scale;
            _avatarModel.Rotation = new Vector3(180, 0, 0);
            AnimateAvatar("stand");
            _wasMoving = false;

            //_textures = new Texture2D[282];
            _textures = Ficedula.FF7.WorldMap.MapTri.TexFiles
                .Select(tex => {
                    Texture2D frame0;
                    using (var s = g.Open("wm", tex + ".tex"))
                        frame0 = graphics.LoadTex(new Ficedula.FF7.TexFile(s), 0);
                    if (tex.EndsWith("1")) {
                        List<Texture2D> frames = new List<Texture2D> { frame0 };
                        int c = 2;
                        while (true) {
                            using (var s = g.TryOpen("wm", tex.Substring(0, tex.Length - 1) + c + ".tex")) {
                                if (s == null)
                                    break;
                                frames.Add(graphics.LoadTex(new Ficedula.FF7.TexFile(s), 0));
                                c++;
                            }
                        }
                        return frames.ToArray();
                    } else
                        return new[] { frame0 };
                })
                .ToArray();
            _blocks = new MapBlock[BLOCKS_X, BLOCKS_Y];
            _blockState = new MapBlockState[BLOCKS_X, BLOCKS_Y];

            _source = new Ficedula.FF7.WorldMap(g.Open("wm", "wm0.map"));

            Task.Run(Loader);

            TryMoveAvatarTo(new Vector3(_initAvatarX, 0, _initAvatarY));

            _view = new PerspView3D {
//                CameraPosition = new Vector3(avatarX, 500, avatarY - 500),
//                CameraForwards = new Vector3(0, -1, 1),
//                CameraUp = new Vector3(0, 1, 1),                
                ZNear = 100,
                ZFar = 100000,
            };
            _cameraOffset = new Vector3(0, 2500, -250);

            _skybox = new SkyBox(g, graphics);

            _ui = new UI.UIBatch(graphics, g);

            FadeIn(null);

            g.Audio.PlayMusic("ta");
        }

        private void TryMoveAvatarTo(Vector3 pos) {
            if (pos.X < 0)
                pos.X += MAP_WIDTH;
            if (pos.Z < 0)
                pos.Z += MAP_HEIGHT;
            if (pos.X >= MAP_WIDTH)
                pos.X -= MAP_WIDTH;
            if (pos.Z >= MAP_HEIGHT)
                pos.Z -= MAP_HEIGHT;

            if (CalculateLocation(pos.X, pos.Z, out float height, out var walkmap)) {
                if ((_avatar.CanWalkOn & walkmap) != 0) {
                    pos.Y = height + _avatarModel.Scale * _avatarModel.MaxBounds.Y;
                    _walkingOn = walkmap;
                } else {
                    Trace.WriteLine($"Denying move to {pos} because destination walkmap is {walkmap}");
                    return;
                }
            }            

            _avatarModel.Translation = pos;
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);
            if (_isDebug) {
                if (input.IsDown(InputKey.Up))
                    _cameraOffset += new Vector3(0, 0, 60);
                if (input.IsDown(InputKey.Down))
                    _cameraOffset += new Vector3(0, 0, -60);
                if (input.IsDown(InputKey.Left))
                    _cameraOffset += new Vector3(60, 0, 0);
                if (input.IsDown(InputKey.Right))
                    _cameraOffset += new Vector3(-60, 0, 0);
                if (input.IsDown(InputKey.OK))
                    _cameraOffset += new Vector3(0, 60, 0);
                if (input.IsDown(InputKey.Cancel))
                    _cameraOffset += new Vector3(0, -60, 0);
            } else {
                Vector3 move = Vector3.Zero;

                if (input.IsDown(InputKey.Up))
                    move += new Vector3(0, 0, 1);
                if (input.IsDown(InputKey.Down))
                    move += new Vector3(0, 0, -1);
                if (input.IsDown(InputKey.Left))
                    move  += new Vector3(1, 0, 0);
                if (input.IsDown(InputKey.Right))
                    move  += new Vector3(-1, 0, 0);

                if (move == Vector3.Zero) {
                    if (_wasMoving) {
                        AnimateAvatar("stand");
                        _wasMoving = false;
                    }
                } else {
                    if (!_wasMoving) {
                        AnimateAvatar("run");
                        _wasMoving = true;
                    }

                    if (_panMode) {
                        var forwards = -_cameraOffset;
                        var left = new Vector3(-_cameraOffset.Z, 0, _cameraOffset.X);
                        forwards.Normalize(); left.Normalize();
                        move = forwards * move.Z + left * move.X;
                    }

                    move.Normalize();
                    TryMoveAvatarTo(_avatarModel.Translation + move * 15);
                    var angle = Math.Atan2(move.X, move.Z) * 180 / Math.PI;
                    _avatarModel.Rotation = new Vector3(180, (float)angle, 0);
                }

                if (input.IsJustDown(InputKey.Select)) {
                    _panMode = !_panMode;
                    _transitionCameraFrom = _view.Clone();
                    _transitionCameraProgress = 0f;
                    if (_panMode) 
                        _cameraOffset = new Vector3(0, 400, -750);
                    else
                        _cameraOffset = new Vector3(0, 2500, -250);
                }

                if (_panMode) {
                    double angle = 0;
                    if (input.IsDown(InputKey.PanLeft))
                        angle += Math.PI / 180;
                    else if (input.IsDown(InputKey.PanRight))
                        angle -= Math.PI / 180;

                    _cameraOffset = Vector3.Transform(_cameraOffset, Matrix.CreateRotationY((float)angle));
                }
            }

            if (input.IsJustDown(InputKey.Menu)) {
                InputEnabled = false;
                FadeOut(() => {
                    Game.SaveData.WorldMapX = _avatarModel.Translation.X;
                    Game.SaveData.WorldMapY = _avatarModel.Translation.Z;
                    Game.SaveData.Module = Module.WorldMap;
                    Game.SaveMap.MenuLocked &= ~(MenuMask.Save | MenuMask.PHS);
                    Game.SaveMap.MenuVisible |= MenuMask.Save | MenuMask.PHS;
                    //TODO - can we skip these on the assumption script should have configured them already?!
                    Game.PushScreen(new UI.Layout.LayoutScreen("MainMenu"));
                    InputEnabled = true;
                });
            }

            if (input.IsJustDown(InputKey.Debug1))
                _isDebug = !_isDebug;

            if (input.IsJustDown(InputKey.Debug2))
                Trace.WriteLine($"Position {_avatarModel.Translation}, walking on {_walkingOn}");
        }

        private double _frame = 0;

        protected override void DoStep(GameTime elapsed) {
            _frame += elapsed.ElapsedGameTime.TotalSeconds * 4;

            if (_transitionCameraFrom != null) {
                if (_transitionCameraProgress >= 1f)
                    _transitionCameraFrom = null;
                else
                    _transitionCameraProgress += 1f / 60f;
            }

            _avatarModel.FrameStep();

            _ui.Reset();

            _ui.DrawImage("wm_minimap", 1060, 550, 0.1f, color: Color.White.WithAlpha(0x80));
        }

        private void RequestBlock(int bx, int by) {
            _blockState[bx, by] = MapBlockState.Loading;
            Trace.WriteLine($"Loading block {bx},{by}");
            _load.Writer.TryWrite((bx, by));
        }

        private (int bx, int by) BlockForAvatar() {
            return (
                (int)Math.Floor(_avatarModel.Translation.X / BLOCK_SIZE),
                (int)Math.Floor(_avatarModel.Translation.Z / BLOCK_SIZE)
            );
        }

        protected override void DoRender() {
            Graphics.BlendState = BlendState.AlphaBlend;
            Graphics.SamplerStates[0] = SamplerState.PointWrap;
            Graphics.DepthStencilState = DepthStencilState.Default;

            PerspView3D finalView;

            _view.CameraPosition = _avatarModel.Translation + _cameraOffset;
            _view.CameraForwards = _cameraOffset * -1;
            if (_panMode)
                _view.CameraUp = Vector3.UnitY;
            else
                _view.CameraUp = new Vector3(-Math.Sign(_cameraOffset.X), 1, -Math.Sign(_cameraOffset.Z));

            if (_transitionCameraFrom != null) {
                finalView = _transitionCameraFrom.Blend(_view, _transitionCameraProgress);
            } else
                finalView = _view;

            _effect.View = finalView.View;
            _effect.Projection = finalView.Projection;
            _effect.World = Matrix.Identity;

            (int avatarX, int avatarY) = BlockForAvatar();
            bool shouldRender = _blockState[avatarX, avatarY] == MapBlockState.Ready;

            foreach (int y in Enumerable.Range(-1, 3)) {
                foreach (int x in Enumerable.Range(-1, 3)) {
                    int bx = (avatarX + x + BLOCKS_X) % BLOCKS_X,
                        by = (avatarY + y + BLOCKS_Y) % BLOCKS_Y;

                    switch (_blockState[bx, by]) {
                        case MapBlockState.Ready:
                            if (shouldRender) {
                                _effect.World = Matrix.CreateTranslation((avatarX + x) * BLOCK_SIZE, 0, (avatarY + y) * BLOCK_SIZE);
                                var block = _blocks[bx, by];
                                Graphics.SetVertexBuffer(block.Buffer);
                                foreach (var group in block.Groups) {
                                    var texs = _textures[group.Texture];
                                    _effect.Texture = texs[(int)_frame % texs.Length];
                                    foreach (var pass in _effect.CurrentTechnique.Passes) {
                                        pass.Apply();
                                        Graphics.DrawPrimitives(PrimitiveType.TriangleList, group.Offset, group.Count / 3);
                                    }
                                }
                            }
                            break;
                        case MapBlockState.Loading:
                            //Well, bah
                            break;
                        case MapBlockState.None:
                            RequestBlock(bx, by);
                            break;
                    }
                }

            }

            _skybox.Render(Graphics, finalView);

            //TODO: Need to adjust avatar render coordinates in case camera is on edge of map
            using (var state = new GraphicsState(Graphics, rasterizerState: RasterizerState.CullClockwise)) {
                foreach (int pass in Enumerable.Range(1, 2))
                    _avatarModel.Render(finalView, pass == 2);
            }

            _ui.Render();
        }

        public override void Dispose() {
            base.Dispose();
            _load.Writer.TryWrite((-1, -1));
            _blockState = null;
            foreach (var tex in _textures.SelectMany(a => a))
                tex.Dispose();
            foreach(var block in _blocks)
                block?.Buffer?.Dispose();
        }

    }
}
