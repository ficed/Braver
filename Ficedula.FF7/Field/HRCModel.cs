using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7.Field {
    public class HRCModel {

        public List<Bone> Bones { get; } = new();
        public Bone Root { get; }
        public string Name { get; }

        public class BonePolygon {
            public PFile PFile { get; }
            public List<TexFile> Textures { get; }

            public BonePolygon(PFile pFile, List<TexFile> textures) {
                PFile = pFile;
                Textures = textures;
            }
        }

        public class Bone {
            public List<Bone> Children { get; } = new();
            public float Length { get; }
            public List<BonePolygon> Polygons { get; } = new();
            public int Index { get; }

            public Bone(float length, int index) {
                Length = length;
                Index = index;
            }
        }


        public HRCModel(Func<string, Stream> dataProvider, string hrcFile) {
            using (var hrc = dataProvider(hrcFile)) {
                var lines = hrc.ReadAllLines().ToArray();
                Name = lines[1].Split(null).Last();
                int numBones = int.Parse(lines[2].Split(null).Last());
                Dictionary<string, Bone> bones = new Dictionary<string, Bone>(StringComparer.InvariantCultureIgnoreCase);
                
                if (numBones == 0) //special case, bah
                    numBones++;

                Root = new Bone(0, -1);
                bones.Add("root", Root);

                foreach (int b in Enumerable.Range(0, numBones)) {
                    Bone bone = new Bone(float.Parse(lines[6 + 5 * b]), b);
                    foreach (string rsd in lines[7 + 5 * b].Split(null).Skip(1)) {
                        if (string.IsNullOrWhiteSpace(rsd)) continue;
                        var rsdLines = dataProvider(rsd + ".RSD").ReadAllLines().ToArray();
                        string pFile = rsdLines
                            .First(s => s.StartsWith("PLY=", StringComparison.InvariantCultureIgnoreCase))
                            .Substring(4)
                            .Replace(".PLY", ".P");
                        int numTex = int.Parse(rsdLines
                            .First(s => s.StartsWith("NTEX="))
                            .Substring(5)
                            );
                        BonePolygon bp = new BonePolygon(
                            new PFile(dataProvider(pFile)),
                            Enumerable.Range(0, numTex)
                                .Select(n => rsdLines.First(s => s.StartsWith($"TEX[{n}]=")).Substring(7).Replace(".TIM", ".TEX"))
                                .Select(t => new TexFile(dataProvider(t)))
                                .ToList()
                        );
                        bone.Polygons.Add(bp);
                    }
                    bones.Add(lines[4 + 5 * b], bone);
                    bones[lines[5 + 5 * b]].Children.Add(bone);
                    Bones.Add(bone);
                }
            }
        }

        public HRCModel(LGPFile lgp, string hrcFile) : this(lgp.Open, hrcFile) { }

    }

    public class FieldAnim {

        public int BoneCount { get; private set; }
        public List<Frame> Frames { get; private set; }

        public class Frame {
            public Vector3 Translation { get; }
            public Vector3 Rotation { get; }
            public List<Vector3> Bones { get; }

            internal Frame(Stream s, int boneCount) {
                Rotation = new Vector3(s.ReadF32(), s.ReadF32(), s.ReadF32());
                Translation = new Vector3(s.ReadF32(), s.ReadF32(), s.ReadF32());
                Bones = Enumerable.Range(0, boneCount)
                    .Select(_ => new Vector3(s.ReadF32(), s.ReadF32(), s.ReadF32()))
                    .ToList();
            }
        }

        public FieldAnim(Stream s) {
            Debug.Assert(s.ReadI32() == 1);
            int frameCount = s.ReadI32();
            BoneCount = s.ReadI32();
            int rotationOrder = s.ReadI32();
            Debug.Assert(rotationOrder == 0x020001);
            s.Seek(20, SeekOrigin.Current);
            Frames = Enumerable.Range(0, frameCount)
                .Select(_ => new Frame(s, BoneCount))
                .ToList();
        }
    }
}
