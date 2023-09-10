// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace Ficedula.FF7.Field {
    public class TileGroup {
        public BlendType Blend { get; set; }
        public int Parameter { get; set; }
        public int Mask { get; set; }

        public int Initial { get; set; }
        public int Count { get; set; }
    }

    public struct Sprite {

        public short DestX, DestY, ZZ2a, ZZ2b, SrcX, SrcY;
        public short SrcX2, SrcY2, Width, Height, PaletteID, Flags;
        public byte Param, State, Blending, ZZ3, TypeTrans, ZZ4;
        public short TextureID, TextureID2, Depth;
        public int ZParam, UParam, VParam;
        public int ZZ7;

        public long SortKey => ((long)TypeTrans << 48) | ((long)Param << 40) | ((long)State << 32) | Flags;

        public int SortKeyHigh => (TypeTrans << 16) | (Param << 8) | State;

        public float CalculatedZ(int layer, int fieldID) {
            switch (layer) {
                case 1:
                    return 0.9997f;
                case 2: //Some special cases in original code?! e.g. FieldID 0x43, 0xcc, or 0x75
                case 3: //No, extra logic
                case 4: //No, extra logic
                    return ZParam / 10000000f;
                default:
                    throw new NotImplementedException();
            }
        }

        public Sprite(Stream source, int layer) {
            DestX = source.ReadI16();
            DestY = source.ReadI16();
            ZZ2a = source.ReadI16();
            ZZ2b = source.ReadI16();
            SrcX = source.ReadI16();
            SrcY = source.ReadI16();
            SrcX2 = source.ReadI16();
            SrcY2 = source.ReadI16();
            Width = source.ReadI16();
            Height = source.ReadI16();
            PaletteID = source.ReadI16();
            Flags = source.ReadI16();
            Param = (byte)source.ReadByte();
            State = (byte)source.ReadByte();
            Blending = (byte)source.ReadByte();
            ZZ3 = (byte)source.ReadByte();
            TypeTrans = (byte)source.ReadByte();
            ZZ4 = (byte)source.ReadByte();
            TextureID = source.ReadI16();
            TextureID2 = source.ReadI16();
            Depth = source.ReadI16();
            ZParam = source.ReadI32();

            UParam = source.ReadI32();
            VParam = source.ReadI32();
            ZZ7 = source.ReadI32();

            FixUp(layer);

        }

        public void FixUp(int layerID) {
            if (layerID > 0 && TextureID2 > 0) {
                SrcX = SrcX2; SrcY = SrcY2; TextureID = TextureID2;
            }
            if (layerID == 0) {
                Param = State = Blending = 0;
            }
            if (Blending == 0) TypeTrans = 0xff;
        }
    }

    public class BackgroundPalette {
        public uint[] Colours;
    }

    public class TexturePage {
        public List<byte[]> Data;
    }

    public class Background {
        private List<TileGroup> _groups = new();

        public Dictionary<int, TexturePage> Pages { get; }

        public List<BackgroundPalette> Palettes { get; }

        public short Width { get; private set; }
        public short Height { get; private set; }
        public IEnumerable<Sprite> Layer1 { get; private set; }
        public IEnumerable<Sprite> Layer2 { get; private set; }
        public IEnumerable<Sprite> Layer3 { get; private set; }
        public IEnumerable<Sprite> Layer4 { get; private set; }
        public IEnumerable<Sprite> AllSprites => Layer1.Concat(Layer2).Concat(Layer3).Concat(Layer4);
        public IEnumerable<IEnumerable<Sprite>> Layers => new[] { Layer1, Layer2, Layer3, Layer4 };

        private static uint ExpandColour(ushort c) {
            uint r = (uint)(255f * (c & 0x1f) / 31f);
            uint g = (uint)(255f * ((c >> 5) & 0x1f) / 31f);
            uint b = (uint)(255f * ((c >> 10) & 0x1f) / 31f);
            //uint a = (c & 0x8000) != 0 ? (uint)0xff : 0;
            uint a = 0xff; //TODO
            return (a << 24) | (b << 16) | (g << 8) | r;
        }

        public Background(Stream source, Palettes palettes) {
            byte[] palFlags = new byte[16];

            Palettes = palettes
                .PaletteData
                .Select(cols => new BackgroundPalette {
                    Colours = cols.Select(us => ExpandColour(us)).ToArray()
                })
                .ToList();

            int bgOffset = 0;
            source.Position = bgOffset + 0x28;
            Width = source.ReadI16();
            Height = source.ReadI16();
            short numSprites = source.ReadI16();

            source.Position = bgOffset + 12;

            source.Read(palFlags, 0, 16);


            foreach (int i in Enumerable.Range(0, Palettes.Count)) {
                if (palFlags[i] != 0)
                    Palettes[i].Colours[0] = 0;
#if DUMP_FIELD
                ImageLoader.DumpImage(palettes[i], $@"C:\temp\palette{i}.png");
#endif
            }

            source.Position = bgOffset + 0x34;
            Sprite[] layer1 = Enumerable.Range(0, numSprites)
                .Select(_ => new Sprite(source, 0))
                .ToArray();

            Sprite[] layer2, layer3, layer4;
            layer2 = layer3 = layer4 = new Sprite[0];

            int numSprites2 = 0, numSprites3 = 0, numSprites4 = 0;

            source.Position = bgOffset + 0x34 + 52 * numSprites;

            if (source.ReadByte() != 0) {
                source.Seek(4, SeekOrigin.Current);
                numSprites2 = source.ReadI16();
                if (numSprites2 > 0) {
                    source.Seek(20, SeekOrigin.Current);
                    layer2 = Enumerable.Range(0, numSprites2)
                        .Select(_ => new Sprite(source, 1))
                        .ToArray();
                }
            }

            if (source.ReadByte() != 0) {
                source.Seek(4, SeekOrigin.Current);
                numSprites3 = source.ReadI16();
                if (numSprites3 > 0) {
                    source.Seek(14, SeekOrigin.Current);
                    layer3 = Enumerable.Range(0, numSprites3)
                        .Select(_ => new Sprite(source, 2))
                        .ToArray();
                }
            }

            if (source.ReadByte() != 0) {
                source.Seek(4, SeekOrigin.Current);
                numSprites4 = source.ReadI16();
                if (numSprites4 > 0) {
                    source.Seek(14, SeekOrigin.Current);
                    layer4 = Enumerable.Range(0, numSprites3)
                        .Select(_ => new Sprite(source, 3))
                        .ToArray();
                }
            }

            Layer1 = layer1;
            Layer2 = layer2;
            Layer3 = layer3;
            Layer4 = layer4;

            byte[] pageData = new byte[256 * 256];
            //int numPages = allSprites.Max(s => s.TextureID) + 1;

            Pages = new();

            source.Seek(7, SeekOrigin.Current);

            Dictionary<int, byte[]> pages = new Dictionary<int, byte[]>();

            foreach (int i in Enumerable.Range(0, 42)) {
                bool exists = source.ReadI16() != 0;
                if (exists) {
                    short size = source.ReadI16(), depth = source.ReadI16();
                    System.Diagnostics.Trace.Assert(depth == 1);
                    source.Read(pageData, 0, pageData.Length);

                    TexturePage page = new TexturePage {
                        Data = new List<byte[]>()
                    };
                    foreach (int y in Enumerable.Range(0, 256)) {
                        byte[] pixels = new byte[256];
                        Buffer.BlockCopy(pageData, 256 * y, pixels, 0, 256);
                        page.Data.Add(pixels);
                    }
                    /*
                    foreach (int dy in Enumerable.Range(0, 256))
                        foreach (int dx in Enumerable.Range(0, 256))
                            pageData[dx + (dy << 8)] = (byte)(dy & 0xf);
                    */

#if DUMP_FIELD
                    ImageLoader.DumpImage(img, $@"C:\temp\page{i}.png");
#endif
                    Pages[i] = page;

                    pages[i] = pageData.ToArray();
                }
            }


#if DUMP_FIELD
            int offsetX = -allSprites.Min(s => s.DestX),
                offsetY = -allSprites.Min(s => s.DestY);

            int L = 0;
            foreach (var layer in new[] { layer1, layer2 }) {
                foreach (var tiles in layer.GroupBy(s => s.Blending)) {
                    List<int[]> render = Enumerable.Range(0, height)
                        .Select(_ => new int[width])
                        .ToList();
                    foreach (var tile in tiles.OrderBy(t => t.ID)) {
                        int destX = tile.DestX + offsetX, destY = tile.DestY + offsetY;
                        var src = pages[tile.TextureID];
                        var pal = paletteData[tile.PaletteID];
                        foreach (int y in Enumerable.Range(0, 16)) {
                            foreach (int x in Enumerable.Range(0, 16)) {
                                byte p = src[tile.SrcX + x + (tile.SrcY + y) * 256];
                                //                        if (p != 0)
                                render[destY + y][destX + x] = pal[p];
                            }
                        }
                    }
                    ImageLoader.DumpImage(render, $@"C:\temp\layer{L}_{tiles.Key}.png");
                }
                L++;
            }
#endif

            var groups = AllSprites
                //                .Where(s => s.DestX == 0 && s.DestY == 0)
                //                .Skip(1)
                .GroupBy(s => s.SortKey)
                .OrderByDescending(group => group.Key)
                ;
            /*
            foreach(var spr in groups.First())
            foreach (int y in Enumerable.Range(0, 16))
                foreach (int x in Enumerable.Range(0, 16)) {
                    int index = pages[spr.TextureID][spr.SrcX + x + (spr.SrcY + y) * 256];
                    int colour = paletteData[spr.PaletteID][index];
                    System.Diagnostics.Trace.WriteLine($"X{x} Y{y} Data {index} Colour {colour:x8}");
                }
                */

            /*
            foreach (var group in groups) {
                TileGroup tg = new TileGroup {
                    Blend = (BlendType)group.First().TypeTrans,
                    Parameter = group.First().Param,
                    Mask = group.First().State,
                    Initial = AddTiles(group),
                    Count = group.Count() * 6,
                };
                _groups.Add(tg);
            }
            */
        }
    }
}
