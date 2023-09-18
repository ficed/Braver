// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7.Field;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Ficedula.FF7.Exporters {
    public class FieldModel {
        private LGPFile _lgp;

        public bool ConvertSRGBToLinear { get; set; }

        public FieldModel(LGPFile lgp) {
            _lgp = lgp;
        }

        private static double SRGBToLinear(double value) {
            const double a = 0.055;
            if (value <= 0.04045)
                return value / 12.92;
            else
                return Math.Pow((value + a) / (1 + a), 2.4);
        }

        private Vector4 UnpackColour(uint colour) {
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

        private IEnumerable<IMeshBuilder<MaterialBuilder>> BuildMeshes(PFile poly, IEnumerable<MaterialBuilder> materials, MaterialBuilder defMaterial, int boneIndex) {
            foreach (var group in poly.Chunks) {
                if (group.Texture != null) {
                    var mesh = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexJoints4>();
                    for (int i = 0; i < group.Indices.Count; i += 3) {
                        var v0 = group.Verts[group.Indices[i]];
                        var v1 = group.Verts[group.Indices[i + 1]];
                        var v2 = group.Verts[group.Indices[i + 2]];

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
                        var v1 = group.Verts[group.Indices[i + 1]];
                        var v2 = group.Verts[group.Indices[i + 2]];

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

        public SharpGLTF.Schema2.ModelRoot BuildScene(string modelHRC, IEnumerable<string> animFiles) {
            var scene = new SceneBuilder();

            var model = new Field.HRCModel(_lgp, modelHRC);
            var animations = animFiles
                .Select(file => new { Anim = new Field.FieldAnim(_lgp.Open(file)), Name = Path.GetFileNameWithoutExtension(file) })
                .ToList();

            Dictionary<TexFile, MaterialBuilder> materials = new();

            var allTextures = model.Bones
                .SelectMany(bone => bone.Polygons)
                .SelectMany(poly => poly.Textures);
            foreach (var texture in allTextures) {
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

            MaterialBuilder defMaterial = new MaterialBuilder("Def")
                .WithDoubleSide(false)
                .WithAlpha(SharpGLTF.Materials.AlphaMode.OPAQUE)
                .WithBaseColor(Vector4.One)
                //.WithBaseColor(new System.Numerics.Vector4(1, 1, 1, 0.5f))
                .WithUnlitShader();

            int maxBone = 0;

            var firstFrame = animations[0].Anim.Frames[0];

            var allNodes = new Dictionary<int, NodeBuilder>();

            void Descend(SceneBuilder scene, HRCModel.Bone bone, NodeBuilder node, Vector3 translation, Vector3? scale) {
                maxBone = Math.Max(maxBone, bone.Index);

                var rotation = Quaternion.CreateFromYawPitchRoll(
                    firstFrame.Rotation.Y * (float)Math.PI / 180,
                    firstFrame.Rotation.X * (float)Math.PI / 180,
                    firstFrame.Rotation.Z * (float)Math.PI / 180
                );

                node.LocalTransform = SharpGLTF.Transforms.AffineTransform.CreateFromAny(
                    null, scale ?? Vector3.One, rotation, translation
                );

                foreach (var child in bone.Children) {
                    var c = node.CreateNode(child.Index.ToString());
                    allNodes[child.Index] = c;
                    Descend(scene, child, c, new Vector3(0, 0, -bone.Length), null);
                }
            }

            void DescendMesh(SceneBuilder scene, HRCModel.Bone bone, NodeBuilder node, Matrix4x4 transform, NodeBuilder[] joints) {
                foreach (var poly in bone.Polygons) {
                    foreach (var mesh in BuildMeshes(poly.PFile, poly.Textures.Select(tex => materials[tex]), defMaterial, bone.Index))
                        scene.AddSkinnedMesh(mesh, transform, joints);
                }
                foreach(var child in bone.Children) {
                    var childNode = allNodes[child.Index];
                    var childTransform = childNode.LocalMatrix * transform;
                    DescendMesh(scene, child, childNode, childTransform, joints);
                }
            }

            var root = new NodeBuilder("-1");
            Descend(scene, model.Root, root, Vector3.Zero, null);
            DescendMesh(
                scene, model.Root, root, root.LocalMatrix, 
                allNodes.Where(kv => kv.Key >= 0).Select(kv => kv.Value).ToArray()
            );
            scene.AddNode(root);

            var settings = SceneBuilderSchema2Settings.Default;
            settings.UseStridedBuffers = false;
            var output = scene.ToGltf2(settings);


            foreach (var anim in animations) {

                var mAnim = output.CreateAnimation();
                mAnim.Name = anim.Name;

                foreach (var node in output.LogicalNodes) {
                    int c = 0;
                    if (!int.TryParse(node.Name, out int boneIndex))
                        continue;

                    var rots = new Dictionary<float, Quaternion>();
                    var trans = new Dictionary<float, Vector3>();

                    foreach (var frame in anim.Anim.Frames) {
                        Quaternion rotation;

                        if (node.VisualRoot == node) {
                            trans[c / 15f] = new Vector3(frame.Translation.X, frame.Translation.Y, frame.Translation.Z);
                            rotation = Quaternion.CreateFromYawPitchRoll(
                                frame.Rotation.Y * (float)Math.PI / 180,
                                frame.Rotation.X * (float)Math.PI / 180,
                                (180 + frame.Rotation.Z) * (float)Math.PI / 180
                            );
                        } else {
                            rotation = Quaternion.CreateFromYawPitchRoll(
                                frame.Bones[boneIndex].Y * (float)Math.PI / 180,
                                frame.Bones[boneIndex].X * (float)Math.PI / 180,
                                frame.Bones[boneIndex].Z * (float)Math.PI / 180
                            );
                        }
                        rots[c / 15f] = rotation;

                        c++;
                    }

                    if (node.VisualRoot == node)
                        mAnim.CreateTranslationChannel(node, trans);
                    mAnim.CreateRotationChannel(node, rots);
                }
            }

            return output;
        }
    }
}