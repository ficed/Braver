using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace F7.Field {
    public class FieldDebug {

        private VertexBuffer _vertexBuffer;
        private int _walkMeshTris;
        private BasicEffect _effect;
        private GraphicsDevice _graphics;

        public FieldDebug(GraphicsDevice graphics, Ficedula.FF7.Field.FieldFile field) {
            _graphics = graphics;

            List<VertexPositionColor> verts = new();

            foreach(var tri in field.GetWalkmesh().Triangles) {
                verts.Add(new VertexPositionColor {
                    Position = new Vector3(tri.V0.X, tri.V0.Y, tri.V0.Z),
                    Color = Color.Red.WithAlpha(0x80),
                });
                verts.Add(new VertexPositionColor {
                    Position = new Vector3(tri.V2.X, tri.V2.Y, tri.V2.Z),
                    Color = Color.Green.WithAlpha(0x80),
                });
                verts.Add(new VertexPositionColor {
                    Position = new Vector3(tri.V1.X, tri.V1.Y, tri.V1.Z),
                    Color = Color.Blue.WithAlpha(0x80),
                });
                _walkMeshTris++;
            }

            var minWM = new Vector3(
                verts.Select(v => v.Position.X).Min(),
                verts.Select(v => v.Position.Y).Min(),
                verts.Select(v => v.Position.Z).Min()
            );
            var maxWM = new Vector3(
                verts.Select(v => v.Position.X).Max(),
                verts.Select(v => v.Position.Y).Max(),
                verts.Select(v => v.Position.Z).Max()
            );
            System.Diagnostics.Debug.WriteLine($"Walkmesh min bounds {minWM} max {maxWM}");

            _vertexBuffer = new VertexBuffer(graphics, typeof(VertexPositionColor), verts.Count, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(verts.ToArray());

            _effect = new BasicEffect(graphics) {
                FogEnabled = false,
                LightingEnabled = false,
                TextureEnabled = false,
                VertexColorEnabled = true,
            };
        }

        public void Render(Viewer viewer) {
            //_graphics.RasterizerState = RasterizerState.CullNone;
            _graphics.SetVertexBuffer(_vertexBuffer);

            _effect.View = viewer.View;
            _effect.Projection = viewer.Projection;
            _effect.World = Matrix.Identity;

            foreach(var pass in _effect.CurrentTechnique.Passes) {
                pass.Apply();
                _graphics.DrawPrimitives(PrimitiveType.TriangleList, 0, _walkMeshTris);
            }
        }
    }
}
