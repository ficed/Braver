using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7.Exporters {
    public static class TexFileUtil {
        public unsafe static SKBitmap ToBitmap(this TexFile tex, int palette) {
            List<uint[]> data;
            if (tex.BytesPerPixel == 4)
                data = tex.As32Bit();
            else
                data = tex.ApplyPalette(palette);
            var bmp = new SKBitmap(tex.Width, tex.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            foreach(int y in Enumerable.Range(0, tex.Height)) {
                var row = data[y];
                IntPtr pRow = bmp.GetAddress(0, y);
                foreach (int x in Enumerable.Range(0, tex.Width))
                    Marshal.WriteInt32(pRow, x * 4, (int)row[x]);
            }

            return bmp;
        }
    }
}
