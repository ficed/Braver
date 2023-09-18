// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Geometry;
using SharpGLTF.Materials;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7.Exporters {
    public class ModelBase {
        public bool ConvertSRGBToLinear { get; set; }
        public bool SwapWinding { get; set; }


        private static double SRGBToLinear(double value) {
            const double a = 0.055;
            if (value <= 0.04045)
                return value / 12.92;
            else
                return Math.Pow((value + a) / (1 + a), 2.4);
        }

        protected Vector4 UnpackColour(uint colour) {
            var c = new Vector4(
                (colour & 0xff) / 255f,
                ((colour >> 8) & 0xff) / 255f,
                ((colour >> 16) & 0xff) / 255f,
                ((colour >> 24) & 0xff) / 255f
            );
            if (ConvertSRGBToLinear) {
                c = new Vector4(
                    (float)SRGBToLinear(c.X),
                    (float)SRGBToLinear(c.Y),
                    (float)SRGBToLinear(c.Z),
                    (float)SRGBToLinear(c.W)
                );
            }
            return c;
        }

        protected IEnumerable<IMeshBuilder<MaterialBuilder>> BuildMeshes(PFile poly, 
            IEnumerable<MaterialBuilder> materials, MaterialBuilder defMaterial, int boneIndex) {
            foreach (var group in poly.Chunks) {
                if (group.Texture != null) {
                    var mesh = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexJoints4>();
                    for (int i = 0; i < group.Indices.Count; i += 3) {
                        var v0 = group.Verts[group.Indices[i]];
                        var v1 = group.Verts[group.Indices[i + (SwapWinding ? 2 : 1)]];
                        var v2 = group.Verts[group.Indices[i + (SwapWinding ? 1 : 2)]];
                            
                        var vb0 = new VertexBuilder<VertexPositionNormal, VertexColor1Texture1, VertexJoints4>(
                            new VertexPositionNormal(v0.Position, v0.Normal),
                            new VertexColor1Texture1(UnpackColour(v0.Colour), v0.TexCoord),
                            new VertexJoints4(boneIndex)
                        );
                        var vb1 = new VertexBuilder<VertexPositionNormal, VertexColor1Texture1, VertexJoints4>(
                            new VertexPositionNormal(v1.Position, v1.Normal),
                            new VertexColor1Texture1(UnpackColour(v1.Colour), v1.TexCoord),
                            new VertexJoints4(boneIndex)
                        );
                        var vb2 = new VertexBuilder<VertexPositionNormal, VertexColor1Texture1, VertexJoints4>(
                            new VertexPositionNormal(v2.Position, v2.Normal),
                            new VertexColor1Texture1(UnpackColour(v2.Colour), v2.TexCoord),
                            new VertexJoints4(boneIndex)
                        );

                        mesh.UsePrimitive(materials.ElementAt(group.Texture.Value))
                            .AddTriangle(vb0, vb1, vb2);
                    }
                    yield return mesh;
                } else {
                    var mesh = new MeshBuilder<VertexPositionNormal, VertexColor1, VertexJoints4>();
                    for (int i = 0; i < group.Indices.Count; i += 3) {
                        var v0 = group.Verts[group.Indices[i]];
                        var v1 = group.Verts[group.Indices[i + (SwapWinding ? 2 : 1)]];
                        var v2 = group.Verts[group.Indices[i + (SwapWinding ? 1 : 2)]];

                        var vb0 = new VertexBuilder<VertexPositionNormal, VertexColor1, VertexJoints4>(
                            new VertexPositionNormal(v0.Position, v0.Normal),
                            new VertexColor1(UnpackColour(v0.Colour)),
                            new VertexJoints4(boneIndex)
                        );
                        var vb1 = new VertexBuilder<VertexPositionNormal, VertexColor1, VertexJoints4>(
                            new VertexPositionNormal(v1.Position, v1.Normal),
                            new VertexColor1(UnpackColour(v1.Colour)),
                            new VertexJoints4(boneIndex)
                        );
                        var vb2 = new VertexBuilder<VertexPositionNormal, VertexColor1, VertexJoints4>(
                            new VertexPositionNormal(v2.Position, v2.Normal),
                            new VertexColor1(UnpackColour(v2.Colour)),
                            new VertexJoints4(boneIndex)
                        );

                        mesh.UsePrimitive(defMaterial)
                            .AddTriangle(vb0, vb1, vb2);
                    }
                    yield return mesh;
                }
            }
        }

        protected Dictionary<TexFile, MaterialBuilder> ConvertTextures(IEnumerable<TexFile> texs) {
            Dictionary<TexFile, MaterialBuilder> materials = new();
            foreach (var texture in texs) {
                byte[] data = texture
                    .ToBitmap(0)
                    .Encode(SkiaSharp.SKEncodedImageFormat.Png, 100)
                    .ToArray();
                var mat = new MaterialBuilder("Mat" + materials.Count)
                    .WithDoubleSide(false)
                    .WithUnlitShader()
                    .WithAlpha(SharpGLTF.Materials.AlphaMode.BLEND)
                    .WithChannelImage(KnownChannel.BaseColor, new SharpGLTF.Memory.MemoryImage(data));
                //.WithChannelImage(KnownChannel.Diffuse, new SharpGLTF.Memory.MemoryImage(tex));
                materials[texture] = mat;
            }
            return materials;
        }

        protected MaterialBuilder GetUntexturedMaterial() {
            return new MaterialBuilder("Def")
                .WithDoubleSide(false)
                .WithAlpha(SharpGLTF.Materials.AlphaMode.OPAQUE)
                .WithBaseColor(Vector4.One)
                //.WithBaseColor(new System.Numerics.Vector4(1, 1, 1, 0.5f))
                .WithUnlitShader();
        }
    }
}
