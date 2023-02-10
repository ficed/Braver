using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Field {
    public class Background {

        public int ScrollX { get; set; }
        public int ScrollY { get; set; }

        public int Width => _bg.Width;
        public int Height => _bg.Height;
        public int MinX { get; private set; }
        public int MinY { get; private set; }
        public int MaxX => _bg.Width + MinX;
        public int MaxY => _bg.Height + MinY;

        private class TexLayer {
            public Texture2D Tex;
            public VertexPositionTexture[] Verts;
            public IEnumerable<Ficedula.FF7.Field.Sprite> Sprites;
            public List<uint[]> Data;
            public Ficedula.FF7.Field.BlendType Blend;
            public int Parameter;
            public int Mask;
            public int OffsetX, OffsetY;
            public bool FixedZ;
        }

        private List<TexLayer> _layers = new();
        private Ficedula.FF7.Field.Background _bg;
        private FGame _game;
        private GraphicsDevice _graphics;
        private AlphaTestEffect _effect;
        private Dictionary<int, int> _parameters = new();

        public float AutoDetectZFrom { get; private set; }
        public float AutoDetectZTo { get; private set; }

        private void Draw(IEnumerable<Ficedula.FF7.Field.Sprite> sprites, List<uint[]> data, int offsetX, int offsetY, bool clear) {
            foreach(var tile in sprites) {
                int destX = tile.DestX + offsetX, destY = tile.DestY + offsetY;
                var src = _bg.Pages[tile.TextureID].Data;
                var pal = _bg.Palettes[tile.PaletteID].Colours;
                foreach (int y in Enumerable.Range(0, 16)) {
                    foreach (int x in Enumerable.Range(0, 16)) {
                        byte p = src[tile.SrcY + y][tile.SrcX + x];
                        uint c = pal[p];
                        if (((c >> 24) != 0) || clear)
                            data[destY + y][destX + x] = c;
                    }
                }
            }
        }

        public Background(FGame game, GraphicsDevice graphics, Ficedula.FF7.Field.Background bg) {
            _game = game;
            _bg = bg;
            _graphics = graphics;
            _effect = new AlphaTestEffect(graphics) {
                VertexColorEnabled = false,
                FogEnabled = false,
            };

            MinX = bg.AllSprites.Min(s => s.DestX);
            MinY = bg.AllSprites.Min(s => s.DestY);

            var zCoords = bg.AllSprites
                .Where(spr => spr.State == 0)
                .Select(spr => spr.ID)
                .Where(z => z > 1 && z < 4032)
                ;
            if (zCoords.Any()) {
                AutoDetectZFrom = zCoords.Min() * 0.75f;
                AutoDetectZTo = zCoords.Max() * 1.25f;
            } else {
                AutoDetectZFrom = 1f;
                AutoDetectZTo = 4095f;
            }

            foreach (var layer in bg.Layers.Where(L => L.Any())) {

                var groups = layer
                    .GroupBy(s => s.SortKey)
                    .OrderByDescending(group => group.Key);

                foreach (var group in groups) {

                    int minX = group.Min(s => s.DestX),
                        minY = group.Min(s => s.DestY),
                        maxX = group.Max(s => s.DestX + 16),
                        maxY = group.Max(s => s.DestY + 16);

                    int texWidth = GraphicsUtil.MakePowerOfTwo(maxX - minX),
                        texHeight = GraphicsUtil.MakePowerOfTwo(maxY - minY);
                    float maxS = 1f * (maxX - minX) / texWidth,
                        maxT = 1f * (maxY - minY) / texHeight;

                    float zcoord;
                    bool isFixedZ;
                    if (group.First().ID >= 4032) {
                        zcoord = 1f; isFixedZ = true;
                    } else if (group.First().ID <= 1) {
                        zcoord = 0f; isFixedZ = true;
                    } else {
                        zcoord = group.First().ID; isFixedZ = false;
                    }

                    TexLayer tl = new TexLayer {
                        Tex = new Texture2D(graphics, texWidth, texHeight, false, SurfaceFormat.Color),
                        OffsetX = -minX,
                        OffsetY = -minY,
                        FixedZ = isFixedZ,
                        Blend = (Ficedula.FF7.Field.BlendType)group.First().TypeTrans,
                        Sprites = group.ToArray(),
                        Data = Enumerable.Range(0, texHeight)
                            .Select(_ => new uint[texWidth])
                            .ToList(),
                        Parameter = group.First().Param,
                        Mask = group.First().State,
                        Verts = new[] {
                            new VertexPositionTexture {
                                Position = new Vector3(minX, -minY, zcoord),
                                TextureCoordinate = new Vector2(0, 0)
                            },
                            new VertexPositionTexture {
                                Position = new Vector3(maxX, -minY, zcoord),
                                TextureCoordinate = new Vector2(maxS, 0)
                            },
                            new VertexPositionTexture {
                                Position = new Vector3(maxX, -maxY, zcoord),
                                TextureCoordinate = new Vector2(maxS, maxT)
                            },

                            new VertexPositionTexture {
                                Position = new Vector3(minX, -minY, zcoord),
                                TextureCoordinate = new Vector2(0, 0)
                            },
                            new VertexPositionTexture {
                                Position = new Vector3(maxX, -maxY, zcoord),
                                TextureCoordinate = new Vector2(maxS, maxT)
                            },
                            new VertexPositionTexture {
                                Position = new Vector3(minX, -maxY, zcoord),
                                TextureCoordinate = new Vector2(0, maxT)
                            },
                        }
                    };

                    _layers.Add(tl);
                    Draw(tl.Sprites, tl.Data, -minX, -minY, false);
                    foreach (int y in Enumerable.Range(0, tl.Tex.Height))
                        tl.Tex.SetData(0, new Rectangle(0, y, tl.Tex.Width, 1), tl.Data[y], 0, tl.Tex.Width);
                    using (var fs = new System.IO.FileStream($@"C:\temp\BG{_layers.Count}.png", System.IO.FileMode.Create))
                        tl.Tex.SaveAsPng(fs, tl.Tex.Width, tl.Tex.Height);
                }
            }
        }

        public void SetParameter(int parm, int value) {
            _parameters[parm] = value;
            _game.Net.Send(new Net.FieldBGMessage { Parm = parm, Value = value });
        }
        public void ModifyParameter(int parm, Func<int, int> modify) {
            int value;
            _parameters.TryGetValue(parm, out value);
            _parameters[parm] = modify(value);
            _game.Net.Send(new Net.FieldBGMessage { Parm = parm, Value = _parameters[parm] });
        }

        public void Step() {
        }

        public void Render(Viewer viewer, float zFrom, float zTo, bool blendLayers) {

            var depth = blendLayers ? DepthStencilState.None : DepthStencilState.Default;

            using (var state = new GraphicsState(_graphics, depthStencilState: depth, forceSaveAll: true)) {

                _effect.Projection = viewer.Projection;
                _effect.View = viewer.View;

                _graphics.SamplerStates[0] = SamplerState.PointClamp;

                foreach (var layer in _layers) {
                    _parameters.TryGetValue(layer.Parameter, out int parmValue); //Parameters not always actually initialised - eg. elevtr1 - so we should probably assume defaulting to zero? 
                    if ((layer.Mask != 0) && (parmValue & layer.Mask) == 0)
                        continue;

                    switch (layer.Blend) {
                        case Ficedula.FF7.Field.BlendType.None:
                        case Ficedula.FF7.Field.BlendType.Blend:
                            if (blendLayers) continue;
                            _graphics.BlendState = BlendState.AlphaBlend;
                            break;
                        case Ficedula.FF7.Field.BlendType.Additive:
                            if (!blendLayers) continue;
                            _graphics.BlendState = BlendState.Additive;
                            break;
                        case Ficedula.FF7.Field.BlendType.QuarterAdd:
                            if (!blendLayers) continue;
                            _graphics.BlendState = GraphicsUtil.BlendQuarterAdd;
                            break;
                        default: //TODO NO
                            _graphics.BlendState = BlendState.Opaque;
                            break;
                    }

                    float zs = layer.FixedZ ? 1f : 1f / (zTo - zFrom);

                    _effect.World = Matrix.CreateTranslation(ScrollX, ScrollY, layer.FixedZ ? 0 : -zFrom)
                        * Matrix.CreateScale(3f, 3f, zs);
                    _effect.Texture = layer.Tex;

                    foreach (var pass in _effect.CurrentTechnique.Passes) {
                        pass.Apply();
                        _graphics.DrawUserPrimitives(PrimitiveType.TriangleList, layer.Verts, 0, layer.Verts.Length / 3);
                    }
                }

            }
        }
    }
}
