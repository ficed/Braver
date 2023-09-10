namespace Ficedula.FF7.Exporters {

    public class FieldLayerState {
        public SkiaSharp.SKBitmap Bitmap { get; init; }
        public int Layer { get; set; }
        public int Key { get; set; }
    }
    public static class FieldExport {
        public static IEnumerable<FieldLayerState> Export(this Field.Background background) {
            int offsetX = -background.AllSprites.Min(s => s.DestX),
                offsetY = -background.AllSprites.Min(s => s.DestY);

            int L = 0;
            foreach (var layer in new[] { background.Layer1, background.Layer2 }) {
                foreach (var tiles in layer.GroupBy(s => s.Blending)) {
                    var bmp = new SkiaSharp.SKBitmap(background.Width, background.Height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);
                    foreach (var tile in tiles.OrderBy(t => t.Flags)) {
                        int destX = tile.DestX + offsetX, destY = tile.DestY + offsetY;
                        var src = background.Pages[tile.TextureID].Data;
                        var pal = background.Palettes[tile.PaletteID].Colours;
                        foreach (int y in Enumerable.Range(0, 16)) {
                            foreach (int x in Enumerable.Range(0, 16)) {
                                byte p = src[tile.SrcY + y][tile.SrcX + x];
                                //                        if (p != 0)
                                uint c = pal[p];
                                c = (c & 0xff00ff00) | ((c & 0xff) << 16) | ((c & 0xff0000) >> 16);
                                if ((c >> 24) != 0)
                                    bmp.SetPixel(destX + x, destY + y, new SkiaSharp.SKColor(c));
                            }
                        }
                    }

                    yield return new FieldLayerState {
                        Bitmap = bmp,
                        Layer = L,
                        Key = tiles.Key,
                    };
                }
                L++;
            }
        }
    }
}