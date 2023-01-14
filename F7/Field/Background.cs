using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Field {
    public class Background {

        public int ScrollX { get; set; }
        public int ScrollY { get; set; }

        public int Width => _bg.Width;
        public int Height => _bg.Height;
        public int MinX { get; private set; }
        public int MinY { get; private set; }

        private enum BlendType {
            Blend,
            Additive,
            Subtractive,
            QuarterAdd,
            None = 0xff,
        }

        private class TexLayer {
            public Texture2D Tex;
            public VertexPositionTexture[] Verts;
            public IEnumerable<Ficedula.FF7.Field.Sprite> Sprites;
            public List<uint[]> Data;
            public BlendType Blend;
            public int Parameter;
            public int Mask;
            public int OffsetX, OffsetY;
            public bool FixedZ;
        }

        private List<TexLayer> _layers = new();
        private Ficedula.FF7.Field.Background _bg;
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

        public Background(GraphicsDevice graphics, Ficedula.FF7.Field.Background bg) {
            _bg = bg;
            _graphics = graphics;
            _effect = new AlphaTestEffect(graphics) {
                VertexColorEnabled = false,
                FogEnabled = false,
            };

            MinX = bg.AllSprites.Min(s => s.DestX);
            MinY = bg.AllSprites.Min(s => s.DestY);

            var zCoords = bg.AllSprites
                .Select(spr => spr.ID)
                .Where(z => z > 1 && z < 4095);
            AutoDetectZFrom = zCoords.Min();
            AutoDetectZTo = zCoords.Max();

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
                    if (group.First().ID == 4095)
                        zcoord = 1f;
                    else
                        zcoord = group.First().ID;

                    TexLayer tl = new TexLayer {
                        Tex = new Texture2D(graphics, texWidth, texHeight, false, SurfaceFormat.Color),
                        OffsetX = -minX,
                        OffsetY = -minY,
                        Blend = (BlendType)group.First().TypeTrans,
                        Sprites = group.ToArray(),
                        Data = Enumerable.Range(0, texHeight)
                            .Select(_ => new uint[texWidth])
                            .ToList(),
                        Parameter = group.First().Param,
                        Mask = group.First().State,
                        FixedZ = group.First().ID == 4095,
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
                }
            }
        }

        public void SetParameter(int parm, int value) {
            _parameters[parm] = value;
        }
        public void ModifyParameter(int parm, Func<int, int> modify) {
            int value;
            _parameters.TryGetValue(parm, out value);
            _parameters[parm] = modify(value);
        }

        public void Step() {
        }

        public void Render(Viewer viewer, float zFrom, float zTo) {

            using (var state = new GraphicsState(_graphics, BlendState.AlphaBlend, DepthStencilState.Default)) {

                _effect.Projection = viewer.Projection;
                _effect.View = viewer.View;

                int L = 0;

                _graphics.SamplerStates[0] = SamplerState.PointClamp;

                foreach (var layer in _layers) {
                    if ((layer.Mask != 0) && (_parameters[layer.Parameter] & layer.Mask) == 0)
                        continue;

                    switch (layer.Blend) {
                        case BlendType.None:
                        case BlendType.Blend:
                            _graphics.BlendState = BlendState.AlphaBlend;
                            break;
                        case BlendType.Additive:
                            _graphics.BlendState = BlendState.Additive;
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
