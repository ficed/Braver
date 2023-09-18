// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7.Battle;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7.Exporters {
    public class BattleModel : ModelBase {
        private LGPFile _lgp;

        public float Scale { get; set; } = 1f;

        public BattleModel(LGPFile lgp) {
            _lgp = lgp;
        }

        private SharpGLTF.Schema2.ModelRoot BuildScene(string skeleton, string anims, IEnumerable<string> texs, Func<string?> nextFile) {
            var scene = new SceneBuilder();

            Animations animations;
            using (var s = _lgp.Open(anims))
                animations = new Animations(s);
            BBone rootBone;
            using(var s = _lgp.Open(skeleton))
                rootBone = BBone.Decode(s);

            var textures = texs
                .Select(t => {
                    using (var s = _lgp.TryOpen(t))
                        return s == null ? null : new Ficedula.FF7.TexFile(s);
                })
                .Where(tex => tex != null)
                .ToArray();

            var materials = ConvertTextures(textures);
            var orderedMaterials = textures.Select(t => materials[t]).ToList();
            MaterialBuilder defMaterial = GetUntexturedMaterial();

            List<PFile> pFiles = new();
            foreach (var bone in rootBone.ThisAndDescendants().Where(b => b.PFileIndex != null).OrderBy(b => b.PFileIndex.Value)) {
                while (pFiles.Count <= bone.PFileIndex.Value)
                    pFiles.Add(null);
                using (var s = _lgp.Open(nextFile()))
                    pFiles[bone.PFileIndex.Value] = new PFile(s);
            }

            var firstFrame = animations.Anims[0].Frames[0];

            int maxBone = 0;
            var allNodes = new Dictionary<int, NodeBuilder>();

            void Descend(SceneBuilder scene, BBone bone, NodeBuilder node, System.Numerics.Vector3 translation, System.Numerics.Vector3? scale) {
                maxBone = Math.Max(maxBone, bone.Index);

                var rotation = System.Numerics.Quaternion.CreateFromYawPitchRoll(
                    (360 * firstFrame.Rotations[bone.Index + 1].rY / 4096f) * (float)Math.PI / 180,
                    (360 * firstFrame.Rotations[bone.Index + 1].rX / 4096f) * (float)Math.PI / 180,
                    (360 * firstFrame.Rotations[bone.Index + 1].rZ / 4096f) * (float)Math.PI / 180
                );

                node.LocalTransform = SharpGLTF.Transforms.AffineTransform.CreateFromAny(
                    null, scale ?? System.Numerics.Vector3.One, rotation, translation
                );

                foreach (var child in bone.Children) {
                    var c = node.CreateNode(child.Index.ToString());
                    allNodes[child.Index] = c;
                    Descend(scene, child, c, new System.Numerics.Vector3(0, 0, bone.Length), null);
                }
            }

            void DescendMesh(SceneBuilder scene, BBone bone, NodeBuilder node, Matrix4x4 transform, NodeBuilder[] joints) {
                if (bone.PFileIndex != null) {
                    foreach (var mesh in BuildMeshes(pFiles[bone.PFileIndex.Value], orderedMaterials, defMaterial, bone.Index)) {
                        scene.AddSkinnedMesh(mesh, transform, joints);
                    }
                }
                foreach (var child in bone.Children) {
                    var childNode = allNodes[child.Index];
                    var childTransform = childNode.LocalMatrix * transform;
                    DescendMesh(scene, child, childNode, childTransform, joints);
                }
            }

            var root = new NodeBuilder("-1");
            Descend(scene, rootBone, root, System.Numerics.Vector3.Zero, new System.Numerics.Vector3(Scale, -Scale, Scale));
            DescendMesh(
                scene, rootBone, root, root.LocalMatrix,
                allNodes.Where(kv => kv.Key >= 0).OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray()
            );
            scene.AddNode(root);

            var settings = SceneBuilderSchema2Settings.Default;
            settings.UseStridedBuffers = false;
            var model = scene.ToGltf2(settings);

            foreach (var anim in animations.Anims) {
                if (anim == null)
                    continue;
                if (anim.Bones <= (maxBone + 1))
                    continue;

                var mAnim = model.CreateAnimation();
                mAnim.Name = "Anim" + (model.LogicalAnimations.Count - 1);

                foreach (var node in model.LogicalNodes) {
                    int c = 0;
                    if (!int.TryParse(node.Name, out int boneIndex))
                        continue;

                    Dictionary<float, System.Numerics.Quaternion> rots = new Dictionary<float, System.Numerics.Quaternion>();
                    Dictionary<float, System.Numerics.Vector3> trans = new Dictionary<float, System.Numerics.Vector3>();

                    foreach (var frame in anim.Frames) {
                        if (node.VisualRoot == node)
                            trans[c / 15f] = new System.Numerics.Vector3(frame.X, -frame.Y, frame.Z) * Scale;

                        var rotation = System.Numerics.Quaternion.CreateFromYawPitchRoll(
                            (360 * frame.Rotations[boneIndex + 1].rY / 4096f) * (float)Math.PI / 180,
                            (360 * frame.Rotations[boneIndex + 1].rX / 4096f) * (float)Math.PI / 180,
                            (360 * frame.Rotations[boneIndex + 1].rZ / 4096f) * (float)Math.PI / 180
                        );

                        rots[c / 15f] = rotation;

                        c++;
                    }

                    if (node.VisualRoot == node)
                        mAnim.CreateTranslationChannel(node, trans);
                    mAnim.CreateRotationChannel(node, rots);
                }
            }

            return model;
        }

        public SharpGLTF.Schema2.ModelRoot BuildSceneFromModel(string modelCode) {
            //TODO - very similar to code in main Braver project - move into Ficedula.FF7
            var texs = Enumerable.Range((int)'c', 10).Select(i => modelCode + "a" + ((char)i).ToString());
            int codecounter = 12;
            Func<string?> NextData = () => {
                char c1, c2;
                string data;
                do {
                    if (codecounter >= 260)
                        return null;
                    c1 = (char)('a' + (codecounter / 26));
                    c2 = (char)('a' + (codecounter % 26));
                    codecounter++;
                    data = modelCode + c1.ToString() + c2.ToString();
                } while (!_lgp.Exists(data));
                return data;
            };
            return BuildScene(modelCode + "aa", modelCode + "da", texs, NextData);
        }
    }
}
