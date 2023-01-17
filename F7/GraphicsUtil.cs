using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Braver {

    public class GraphicsState : IDisposable {
        private GraphicsDevice _graphics;
        private BlendState _blend;
        private DepthStencilState _depthStencilState;

        public GraphicsState(GraphicsDevice graphics, BlendState blend, DepthStencilState depthStencilState = null) {
            _graphics = graphics;
            _blend = graphics.BlendState;
            _depthStencilState = graphics.DepthStencilState;
            if (blend != null)
                graphics.BlendState = blend;
            if (depthStencilState != null)
                graphics.DepthStencilState = depthStencilState;
        }

        public void Dispose() {
            _graphics.BlendState = _blend;
            _graphics.DepthStencilState = _depthStencilState;
        }
    }

    public static class GraphicsUtil {

        public static readonly BlendState BlendSubtractive = new BlendState {
            AlphaBlendFunction = BlendFunction.ReverseSubtract,
            AlphaDestinationBlend = Blend.One,
            AlphaSourceBlend = Blend.SourceAlpha,
            ColorBlendFunction = BlendFunction.ReverseSubtract,
            ColorDestinationBlend = Blend.One,
            ColorSourceBlend = Blend.SourceAlpha,
        };
        public static readonly BlendState BlendDarken = new BlendState {
            AlphaBlendFunction = BlendFunction.ReverseSubtract,
            AlphaDestinationBlend = Blend.One,
            AlphaSourceBlend = Blend.One,
            ColorBlendFunction = BlendFunction.ReverseSubtract,
            ColorDestinationBlend = Blend.One,
            ColorSourceBlend = Blend.InverseSourceColor,
        };

        public static int MakePowerOfTwo(int i) {
            int n = 1;
            while (n < i)
                n <<= 1;
            return n;
        }

        public delegate void TexFilter(ref uint pixel);

        public static Texture2D LoadTex(this GraphicsDevice graphics, Ficedula.FF7.TexFile tex, int palette, TexFilter filter = null) {
            var texture = new Texture2D(graphics, tex.Width, tex.Height, false, SurfaceFormat.Color); //TODO MIPMAPS!
            texture.SetData(
                tex.ApplyPalette(palette)
                .SelectMany(row => {
                    if (filter != null) {
                        foreach (int i in Enumerable.Range(0, row.Length))
                            filter(ref row[i]);
                    }
                    return row;
                })
                .ToArray()
            );
            return texture;
        }

        public static Vector2 ToX(this System.Numerics.Vector2 v) {
            return new Vector2(v.X, v.Y);
        }
        public static Vector3 ToX(this System.Numerics.Vector3 v) {
            return new Vector3(v.X, v.Y, v.Z);
        }

        public static Vector3 ToX(this Ficedula.FF7.Field.FieldVertex v) {
            return new Vector3(v.X, v.Y, v.Z);
        }

        public static Color WithAlpha(this Color c, byte alpha) {
            c.A = alpha;
            return c;
        }

        public static Vector3 WithZ(this Vector3 v, float z) {
            v.Z = z;
            return v;
        }

        public static Vector2 XY(this Vector3 v) {
            return new Vector2(v.X, v.Y);
        }

        public static bool LineCircleIntersect(Vector2 line0, Vector2 line1, Vector2 center, float radius) {
            Vector2 ac = center - line0;
            Vector2 ab = line1 - line0;
            float ab2 = Vector2.Dot(ab, ab);
            float acab = Vector2.Dot(ac, ab);
            float t = acab / ab2;

            if (t < 0)
                t = 0;
            else if (t > 1)
                t = 1;

            Vector2 h = ((ab * t) + line0) - center;
            float h2 = Vector2.Dot(h, h);

            return h2 <= (radius * radius);
        }
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPositionNormalColorTexture : IVertexType {
        public Vector3 Position;
        public Vector3 Normal;
        public Color Color;
        public Vector2 TexCoord;

        public static VertexDeclaration VertexDeclaration = new VertexDeclaration
        (
              new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
              new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
              new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0),
              new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0)
        );
        VertexDeclaration IVertexType.VertexDeclaration { get { return VertexDeclaration; } }
    }

}
