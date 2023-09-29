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
using SkiaSharp;
using SharpGLTF.Schema2;
using System.Security.Cryptography;

namespace Ficedula.FF7.Exporters {

    public class ModelBaseOptions {
        public bool ConvertSRGBToLinear { get; set; }
        public bool SwapWinding { get; set; }
        public bool BakeVertexColours { get; set; }
    }

    public class ModelBase {

        protected ModelBaseOptions _options;

        private Dictionary<(uint c0, uint c1, uint c2), (int x, int y)> _bakedColours = new();
        private SKBitmap _bakedTexture;
        private MaterialBuilder _bakedMaterial;

        private static double SRGBToLinear(double value) {
            const double a = 0.055;
            if (value <= 0.04045)
                return value / 12.92;
            else
                return Math.Pow((value + a) / (1 + a), 2.4);
        }

        protected SKColor ColourToSK(uint colour) {
            byte[] b = BitConverter.GetBytes(colour);
            return new SKColor(b[0], b[1], b[2], b[3]);
        }

        private (Vector2 tc0, Vector2 tc1, Vector2 tc2) GetBakedCoords(uint c0, uint c1, uint c2) {
            var sorted = new[] { c0, c1, c2 }.OrderBy(x => x).ToList();
            int x, y;
            if (_bakedColours.TryGetValue((sorted[0], sorted[1], sorted[2]), out var coord)) {
                (x, y) = coord;
            } else {
                x = 2 * (_bakedColours.Count % 128);
                y = 2 * (_bakedColours.Count / 128);

                SKColor s0 = ColourToSK(sorted[0]),
                    s1 = ColourToSK(sorted[1]),
                    s2 = ColourToSK(sorted[2]);
                SKColor sMix = new SKColor(
                    (byte)((s1.Red + s2.Red) / 2),
                    (byte)((s1.Green + s2.Green) / 2),
                    (byte)((s1.Blue + s2.Blue) / 2),
                    (byte)((s1.Alpha + s2.Alpha) / 2)
                );
                _bakedTexture.SetPixel(x, y, s0);
                _bakedTexture.SetPixel(x + 1, y, s1);
                _bakedTexture.SetPixel(x, y + 1, s2);
                _bakedTexture.SetPixel(x + 1, y + 1, sMix);

                _bakedColours[(sorted[0], sorted[1], sorted[2])] = (x, y);
            }

            Vector2[] coords = new[] {
                new Vector2((x + 0.5f) / 256, (y + 0.5f) / 256),
                new Vector2((x + 1.5f) / 256, (y + 0.5f) / 256),
                new Vector2((x + 0.5f) / 256, (y + 1.5f) / 256),
            };

            return (
                coords[sorted.IndexOf(c0)],
                coords[sorted.IndexOf(c1)],
                coords[sorted.IndexOf(c2)]
            );
        }

        protected Vector4 UnpackColour(uint colour) {
            var c = new Vector4(
                (colour & 0xff) / 255f,
                ((colour >> 8) & 0xff) / 255f,
                ((colour >> 16) & 0xff) / 255f,
                ((colour >> 24) & 0xff) / 255f
            );
            if (_options.ConvertSRGBToLinear) {
                c = new Vector4(
                    (float)SRGBToLinear(c.X),
                    (float)SRGBToLinear(c.Y),
                    (float)SRGBToLinear(c.Z),
                    (float)SRGBToLinear(c.W)
                );
            }
            return c;
        }

        private void SetupBaking() {
            if (_options.BakeVertexColours) {
                _bakedTexture ??= new SKBitmap(256, 256);
                _bakedMaterial ??= new MaterialBuilder("BakedVertexColours")
                    .WithDoubleSide(false)
                    .WithAlpha(SharpGLTF.Materials.AlphaMode.OPAQUE)
                    .WithUnlitShader();
            }
        }

        protected void FinishBaking() {
            if (_options.BakeVertexColours) {
                byte[] data = _bakedTexture
                    .Encode(SkiaSharp.SKEncodedImageFormat.Png, 100)
                    .ToArray();
                _bakedMaterial.WithChannelImage(KnownChannel.BaseColor, new SharpGLTF.Memory.MemoryImage(data));
            }
        }

        protected IEnumerable<IMeshBuilder<MaterialBuilder>> BuildMeshes(PFile poly, 
            IEnumerable<MaterialBuilder> materials, MaterialBuilder defMaterial, int boneIndex) {

            SetupBaking();

            foreach (var group in poly.Chunks) {
                if (group.Texture != null) {
                    var mesh = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexJoints4>();
                    for (int i = 0; i < group.Indices.Count; i += 3) {
                        var v0 = group.Verts[group.Indices[i]];
                        var v1 = group.Verts[group.Indices[i + (_options.SwapWinding ? 2 : 1)]];
                        var v2 = group.Verts[group.Indices[i + (_options.SwapWinding ? 1 : 2)]];

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
                } else if (_options.BakeVertexColours) {
                    var mesh = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>();
                    for (int i = 0; i < group.Indices.Count; i += 3) {
                        var v0 = group.Verts[group.Indices[i]];
                        var v1 = group.Verts[group.Indices[i + (_options.SwapWinding ? 2 : 1)]];
                        var v2 = group.Verts[group.Indices[i + (_options.SwapWinding ? 1 : 2)]];

                        (var tc0, var tc1, var tc2) = GetBakedCoords(v0.Colour, v1.Colour, v2.Colour);

                        var vb0 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>(
                            new VertexPositionNormal(v0.Position, v0.Normal),
                            new VertexTexture1(tc0),
                            new VertexJoints4(boneIndex)
                        );
                        var vb1 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>(
                            new VertexPositionNormal(v1.Position, v1.Normal),
                            new VertexTexture1(tc1),
                            new VertexJoints4(boneIndex)
                        );
                        var vb2 = new VertexBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>(
                            new VertexPositionNormal(v2.Position, v2.Normal),
                            new VertexTexture1(tc2),
                            new VertexJoints4(boneIndex)
                        );

                        mesh.UsePrimitive(_bakedMaterial)
                            .AddTriangle(vb0, vb1, vb2);
                    }
                    yield return mesh;
                } else {
                    var mesh = new MeshBuilder<VertexPositionNormal, VertexColor1, VertexJoints4>();
                    for (int i = 0; i < group.Indices.Count; i += 3) {
                        var v0 = group.Verts[group.Indices[i]];
                        var v1 = group.Verts[group.Indices[i + (_options.SwapWinding ? 2 : 1)]];
                        var v2 = group.Verts[group.Indices[i + (_options.SwapWinding ? 1 : 2)]];

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
