﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Braver.UI {
    public class Font {

        public class Glyph {
            [XmlText]
            public char Character { get; set; }
            [XmlAttribute]
            public int X { get; set; }
            [XmlAttribute]
            public int Y { get; set; }
            [XmlAttribute]
            public int W { get; set; }
            [XmlAttribute]
            public int H { get; set; }
            [XmlAttribute]
            public int Layer { get; set; }
        }

        public class FontSet {
            public string Name { get; set; }
            public int DefaultSize { get; set; }
            public List<Glyph> Glyphs { get; private set; }

            [XmlIgnore]
            public Dictionary<char, Glyph> GlyphsDict { get; private set; }

            public FontSet() {
                Glyphs = new List<Glyph>();
            }

            public void IndexAndFixUp() {
                GlyphsDict = Glyphs.ToDictionary(g => g.Character, g => g);
            }
        }

        public class FontData {
            public List<FontSet> Fonts { get; private set; }
            [XmlElement("Texture")]
            public List<string> Textures { get; private set; }
            public FontData() {
                Fonts = new List<FontSet>();
                Textures = new List<string>();
            }
        }

        public Dictionary<string, FontSet> FontSets { get; private set; }
        public Texture2D Texture { get; private set; }

        public Font(GraphicsDevice graphics, FGame game) {
            FontData data;
            using (var s = game.Open("ui", "font.xml"))
                data = Serialisation.Deserialise<FontData>(s);

            var texs = data.Textures
                .Select(t => {
                    string[] parts = t.Split('\\', ',');
                    int.TryParse(parts.ElementAtOrDefault(2) ?? "", out int pal);
                    using (var s = game.Open(parts[0], parts[1])) {
                        return new { Tex = new Ficedula.FF7.TexFile(s), Pal = pal };
                    }
                })
                .ToArray();

            Texture = new Texture2D(graphics, texs[0].Tex.Width, texs[0].Tex.Height * texs.Length);

            int offset = 0;
            foreach (var tex in texs) {
                var pixels = tex.Tex.ApplyPalette(tex.Pal);
                Texture.SetData(0,
                    new Rectangle(0, offset, tex.Tex.Width, tex.Tex.Height),
                    pixels.SelectMany(row => {
                        Filter(row);
                        return row;
                    }).ToArray(),
                    0,
                    tex.Tex.Width * tex.Tex.Height
                );
                offset += 256;
            }

            foreach (var set in data.Fonts)
                set.IndexAndFixUp();

            FontSets = data.Fonts
                .ToDictionary(
                    s => s.Name,
                    s => s,
                    StringComparer.InvariantCultureIgnoreCase
                );
        }

        private void Filter(uint[] pixels) {
            foreach(int i in Enumerable.Range(0, pixels.Length)) {
                if ((pixels[i] & 0xff) > 0x40)
                    pixels[i] = 0xffffffff;
            }
        }

    }

    public class CompositeTexture {

        public class SourceChunk {
            [XmlAttribute]
            public int X { get; set; }
            [XmlAttribute]
            public int Y { get; set; }
            [XmlAttribute]
            public int W { get; set; }
            [XmlAttribute]
            public int H { get; set; }
            [XmlAttribute]
            public bool Flip { get; set; }
            [XmlText]
            public string Name { get; set; }
        }
        public class SourceImage {
            public string Texture { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            [XmlElement("Chunk")]
            public List<SourceChunk> Chunks { get; set; } = new List<SourceChunk>();
        }

        public int Width { get; set; }
        public int Height { get; set; }

        public List<SourceImage> Images { get; set; } = new List<SourceImage>();
    }

    public class CompositeImages {

        private class LoadedChunk {
            public Texture2D Texture { get; set; }
            public CompositeTexture.SourceImage Image { get; set; }
            public CompositeTexture.SourceChunk Chunk { get; set; }
        }

        private Dictionary<string, LoadedChunk> _chunks = new Dictionary<string, LoadedChunk>(StringComparer.InvariantCultureIgnoreCase);

        public CompositeImages(GraphicsDevice graphics, FGame g) {
            var files = g.ScanData("ui")
                .Where(s => System.IO.Path.GetExtension(s).Equals(".ctex", StringComparison.InvariantCultureIgnoreCase));

            foreach (string file in files) {
                using (var s = g.Open("ui", file)) {
                    var images = Serialisation.Deserialise<CompositeTexture>(s);
                    var texture = new Texture2D(graphics, images.Width, images.Height);

                    foreach (var image in images.Images) {
                        string[] parts = image.Texture.Split(',', '\\');
                        using (var ts = g.Open(parts[0], parts[1])) {
                            if (System.IO.Path.GetExtension(parts[1]).Equals(".tex")) {
                                var t = new Ficedula.FF7.TexFile(ts);
                                int pal = int.Parse(parts.ElementAtOrDefault(2) ?? "0");
                                var pixels = t.ApplyPalette(pal);
                                texture.SetData(
                                    0, new Rectangle(image.X, image.Y, t.Width, t.Height),
                                    pixels.SelectMany(row => row).ToArray(),
                                    0, t.Width * t.Height
                                );
                            } else if (System.IO.Path.GetExtension(parts[1]).Equals(".png")) {
                                using (var png = SixLabors.ImageSharp.Image.Load<Rgba32>(ts)) {
                                    byte[] pixels = new byte[png.Width * png.Height * 4];
                                    png.CopyPixelDataTo(new Span<byte>(pixels));
                                    texture.SetData(
                                        0, new Rectangle(image.X, image.Y, png.Width, png.Height),
                                        pixels,
                                        0, png.Width * png.Height * 4
                                    );
                                }
                            }
                        }
                        foreach(var chunk in image.Chunks) {
                            _chunks[chunk.Name] = new LoadedChunk {
                                Chunk = chunk,
                                Image = image,
                                Texture = texture
                            };
                        }
                    }
                }
            }
        }

        public void Find(string name, out Texture2D texture, out Rectangle rect, out bool flip) {
            var chunk = _chunks[name];
            texture = chunk.Texture;
            flip = chunk.Chunk.Flip;
            rect = new Rectangle(
                chunk.Image.X + chunk.Chunk.X, chunk.Image.Y + chunk.Chunk.Y,
                chunk.Chunk.W, chunk.Chunk.H
            );
        }

    }

    public class Boxes {
        public Texture2D BoxTex { get; private set; }
        public Texture2D GradientTex { get; private set; }

        public Boxes(GraphicsDevice graphics, FGame g) {
            using (var s = g.Open("ui", "box.png"))
                BoxTex = Texture2D.FromStream(graphics, s);
            using (var s = g.Open("ui", "gradient.png"))
                GradientTex = Texture2D.FromStream(graphics, s);
        }
    }

    public enum Alignment {
        Left,
        Center,
        Right,
    }

    public class UIBatch {
        private GraphicsDevice _graphics;
        private Font _font;
        private Boxes _boxes;
        private SpriteBatch _spriteBatch;
        private CompositeImages _images;

        public UIBatch(GraphicsDevice graphics, FGame g) {
            _graphics = graphics;
            _font = g.Singleton(() => new Font(graphics, g));
            _boxes = g.Singleton(() => new Boxes(graphics, g));
            _images = g.Singleton(() => new CompositeImages(graphics, g));
            _spriteBatch = new SpriteBatch(graphics);
        }

        private struct TextItem {
            public string fontSet;
            public string text;
            public int x;
            public int y;
            public float z;
            public Color colour;
            public Alignment Alignment;
        }

        private struct ImageItem {
            public int X, Y;
            public float Z;
            public string Image;
            public Alignment Alignment;
            public float Scale;
            public Point? DestSize;
            public Color Color;
        }

        private List<TextItem> _items = new();
        private List<(Rectangle, float)> _boxItems = new();
        private List<ImageItem> _imageItems = new();

        public void DrawImage(string image, int x, int y, float z, Alignment alignment = Alignment.Left, 
            float scale = 1f, Color? color = null) {
            _imageItems.Add(new ImageItem {
                X = x, Y = y, Z = z, Image = image, Alignment = alignment, Scale = scale,
                Color = color ?? Color.White,
            });
        }
        public void DrawImage(string image, int x, int y, float z, Point destSize, Alignment alignment = Alignment.Left,
            Color? color = null) {
            _imageItems.Add(new ImageItem {
                X = x, Y = y, Z = z, Image = image, Alignment = alignment, DestSize = destSize,
                Color = color ?? Color.White,
            });
        }

        public void DrawBox(Rectangle location, float z) {
            _boxItems.Add((location, z));
        }

        public void DrawText(string fontSet, string text, int x, int y, float z, Color colour, Alignment alignment = Alignment.Left) {
            _items.Add(new TextItem {
                fontSet = fontSet,
                text = text,
                x = x,
                y = y,
                z = z,
                colour = colour,
                Alignment = alignment,
            });
        }

        public int TextWidth(string fontSet, string text) {
            var f = _font.FontSets[fontSet];
            int dx = 0;
            foreach (char c in text) {
                switch (c) {
                    case ' ':
                        dx += f.GlyphsDict['n'].W * 1;
                        break;
                    case '\t':
                        dx += f.GlyphsDict['n'].W * 3;
                        break;
                    default:
                        var glyph = f.GlyphsDict[c];
                        dx += glyph.W + 2;
                        break;
                }
            }
            return dx;
        }

        private static Rectangle[] _sourceBox = new[] {
            new Rectangle(0, 0, 10, 6),
            new Rectangle(10, 0, 12, 6),
            new Rectangle(22, 0, 10, 6),

            new Rectangle(0, 6, 6, 20),
            new Rectangle(26, 6, 6, 20),

            new Rectangle(0, 26, 10, 6),
            new Rectangle(10, 26, 12, 6),
            new Rectangle(22, 26, 10, 6),
        };
        private static (int sx, int ex, int sy, int ey)[] _destBox = new[] {
            (0, 1, 0, 1),
            (1, 2, 0, 1),
            (2, 3, 0, 1),

            (0, 1, 1, 2),
            (2, 3, 1, 2),

            (0, 1, 2, 3),
            (1, 2, 2, 3),
            (2, 3, 2, 3),
        };

        public void Render() {
            float scale = _graphics.Viewport.Width / 1280f;
            _spriteBatch.Begin(
                samplerState: SamplerState.PointClamp,
                transformMatrix: Matrix.CreateScale(scale, scale, 1f),
                sortMode: SpriteSortMode.FrontToBack
            );
            foreach (var item in _items) {
                var f = _font.FontSets[item.fontSet];
                int dx = item.x;
                switch (item.Alignment) {
                    case Alignment.Center:
                        dx -= TextWidth(item.fontSet, item.text) / 2;
                        break;
                    case Alignment.Right:
                        dx -= TextWidth(item.fontSet, item.text);
                        break;
                }
                foreach (char c in item.text) {
                    switch (c) {
                        case ' ':
                            dx += f.GlyphsDict['n'].W * 1;
                            break;
                        case '\t':
                            dx += f.GlyphsDict['n'].W * 3;
                            break;
                        default:
                            var glyph = f.GlyphsDict[c];
                            _spriteBatch.Draw(_font.Texture,
                                new Rectangle(dx, item.y, glyph.W, glyph.H),
                                new Rectangle(glyph.X, glyph.Y + 256 * glyph.Layer, glyph.W, glyph.H),
                                item.colour,
                                0,
                                Vector2.Zero,
                                SpriteEffects.None,
                                item.z
                            );
                            dx += glyph.W + 2;
                            break;
                    }
                }
            }

            foreach(var image in _imageItems) {
                _images.Find(image.Image, out var tex, out var source, out bool flip);

                int x;
                switch (image.Alignment) {
                    case Alignment.Right:
                        x = image.X - (int)(source.Width * image.Scale);
                        break;
                    case Alignment.Center:
                        x = image.X - (int)(source.Width * image.Scale * 0.5f);
                        break;
                    default:
                        x = image.X;
                        break;
                }

                int w, h;
                if (image.DestSize == null) {
                    w = (int)(source.Width * image.Scale);
                    h = (int)(source.Height * image.Scale);
                } else {
                    (w, h) = image.DestSize.GetValueOrDefault();
                }

                _spriteBatch.Draw(tex,
                    new Rectangle(x, image.Y, w, h),
                    source,
                    image.Color, 0, Vector2.Zero, 
                    flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 
                    image.Z
                );
            }

            foreach(var box in _boxItems) {
                _spriteBatch.Draw(_boxes.GradientTex,
                    new Rectangle(box.Item1.X + 6, box.Item1.Y + 6, box.Item1.Width - 12, box.Item1.Height - 12),
                    null, Color.White, 0, Vector2.Zero, SpriteEffects.None, box.Item2
                );

                int[] xCoords = new[] {
                    box.Item1.X, box.Item1.X + 6, box.Item1.Right - 6, box.Item1.Right,
                };
                int[] yCoords = new[] {
                    box.Item1.Y, box.Item1.Y + 6, box.Item1.Bottom - 6, box.Item1.Bottom,
                };

                foreach (int c in Enumerable.Range(0, 8)) {
                    var db = _destBox[c];
                    var dest = new Rectangle(
                        xCoords[db.sx], yCoords[db.sy],
                        xCoords[db.ex] - xCoords[db.sx], yCoords[db.ey] - yCoords[db.sy]
                    );
                    _spriteBatch.Draw(_boxes.BoxTex,
                        dest,
                        _sourceBox[c], 
                        Color.White, 0, Vector2.Zero, SpriteEffects.None, box.Item2 + Z_ITEM_OFFSET * 0.5f
                    );
                }
            }

            _spriteBatch.End();
        }

        public void Reset() {
            _items.Clear();
            _boxItems.Clear();
            _imageItems.Clear();
        }

        public const float Z_ITEM_OFFSET = 1f / 1024f;

    }

}
