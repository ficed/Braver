// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7 {
    public enum BlendType {
        Blend,
        Additive,
        Subtractive,
        QuarterAdd,
        None0,
        None1 = 0xff,
    }

    public class PFileVert {
        public Vector3 Position { get; set; }
        public Vector3 Normal { get; set; }
        public uint Colour { get; set; }
        public Vector2 TexCoord { get; set; }
    }

    public class PFileChunk {
        public int? Texture { get; }
        public List<PFileVert> Verts { get; }
        public List<int> Indices { get; }

        public PFileHundred RenderState { get; }

        public PFileChunk(int? texture, List<PFileVert> verts, List<int> indices, PFileHundred renderState) {
            Texture = texture;
            Verts = verts;
            Indices = indices;
            RenderState = renderState;
        }
    }

    [Flags]
    public enum RenderEffect : uint {
        None = 0,
        Wireframe = 0x1,
        Texture = 0x2,
        LinearFilter = 0x4,
        Perspective = 0x8,
        TMapBlend = 0x10,
        WrapU = 0x20,
        WrapV = 0x40,
        ColorKey = 0x100,
        Dither = 0x200,
        AlphaBlend = 0x400,
        AlphaTest = 0x800,
        AntiAlias = 0x1000,
        CullFace = 0x2000,
        NoCull = 0x4000,
        DepthTest = 0x8000,
        DepthMask = 0x10000,
        ShadeMode = 0x20000,
        Specular = 0x40000,
        LightState = 0x80000,
        Fog = 0x100000,
        TexAddr = 0x200000,
        AlphaFunc = 0x1000000,
        AlphaRef = 0x2000000,        
    }

    public struct PFileHundred {
        public uint Unk0, Unk4;
        public uint Options;
        public RenderEffect Features;
        public uint Unk10;
        public IntPtr TextureSet; //presumably set at runtime - not read from file
        public uint Unk18, Unk1C, Unk20;
        public uint ShadeMode;
        public uint LightStateAmbient;
        public uint Unk2C;
        public IntPtr LightStateMaterialPointer;
        public uint SrcBlend, DestBlend;
        public uint Unk3C;
        public uint AlphaRef;
        public BlendType BlendMode;
        public uint ZSort;
        public uint Unk4C, Unk50, Unk54, Unk58;
        public uint VertexAlpha;
        public uint Unk60;

        public PFileHundred(Stream s) {
            Unk0 = s.ReadU32(); Unk4 = s.ReadU32();
            Options = s.ReadU32(); Features = (RenderEffect)s.ReadU32();
            Unk10 = s.ReadU32();
            s.ReadU32(); TextureSet = IntPtr.Zero;
            Unk18 = s.ReadU32(); Unk1C = s.ReadU32(); Unk20 = s.ReadU32();
            ShadeMode = s.ReadU32(); LightStateAmbient = s.ReadU32();
            Unk2C = s.ReadU32();
            s.ReadU32(); LightStateMaterialPointer = IntPtr.Zero;
            SrcBlend = s.ReadU32(); DestBlend = s.ReadU32();
            Unk3C = s.ReadU32();
            AlphaRef = s.ReadU32(); 
            BlendMode = (BlendType)s.ReadU32(); 
            ZSort = s.ReadU32();
            Unk4C = s.ReadU32(); Unk50 = s.ReadU32(); Unk54 = s.ReadU32(); Unk58 = s.ReadU32();
            VertexAlpha = s.ReadU32();
            Unk60 = s.ReadU32();

            //System.Diagnostics.Debug.WriteLine($"Hundred features {Features} SrcBlend {SrcBlend} DestBlend {DestBlend} BlendMode {BlendMode} ZSort {ZSort}");
        }
    }

    public class PFile {

        public List<PFileChunk> Chunks { get; } = new();
        //public List<PFileHundred> Hundreds { get; } = new();

        private class Polygon {
            public ushort U0,
                V0, V1, V2,
                N0, N1, N2,
                E0, E1, E2,
                U1, U2;

            public Polygon(System.IO.Stream s) {
                U0 = s.ReadU16();
                V0 = s.ReadU16();
                V1 = s.ReadU16();
                V2 = s.ReadU16();
                N0 = s.ReadU16();
                N1 = s.ReadU16();
                N2 = s.ReadU16();
                E0 = s.ReadU16();
                E1 = s.ReadU16();
                E2 = s.ReadU16();
                U1 = s.ReadU16();
                U2 = s.ReadU16();
            }
        }

        private class Group {
            public int PrimitiveType, PolygonStartIndex, NumPolygons,
                VerticesStartIndex, NumVertices, EdgeStartIndex,
                NumEdges, U1, U2, U3, U4,
                TexCoordStartIndex, AreTexturesUsed, TextureNumber;

            public Group(Stream s) {
                PrimitiveType = s.ReadI32();
                PolygonStartIndex = s.ReadI32();
                NumPolygons = s.ReadI32();
                VerticesStartIndex = s.ReadI32();
                NumVertices = s.ReadI32();
                EdgeStartIndex = s.ReadI32();
                NumEdges = s.ReadI32();
                U1 = s.ReadI32();
                U2 = s.ReadI32();
                U3 = s.ReadI32();
                U4 = s.ReadI32();
                TexCoordStartIndex = s.ReadI32();
                AreTexturesUsed = s.ReadI32();
                TextureNumber = s.ReadI32();

                //System.Diagnostics.Debug.WriteLine($"Reading group: Prim {PrimitiveType} U {U1}/{U2}/{U3}/{U4} Tex {AreTexturesUsed}");
            }
        }

        public PFile(Stream s) {
            s.Position = 12;
            int numVertices = s.ReadI32(),
                numNormals = s.ReadI32(),
                Dummy1 = s.ReadI32(),
                numTexCoords = s.ReadI32(),
                numVertexColours = s.ReadI32(),
                numEdges = s.ReadI32(),
                numPolys = s.ReadI32(),
                numUnknown2 = s.ReadI32(),
                numUnknown3 = s.ReadI32(),
                numHundreds = s.ReadI32(),
                numGroups = s.ReadI32(),
                numBoundingBoxes = s.ReadI32();
            s.Seek(17 * 4, SeekOrigin.Current);

            Vector3[] pVerts = Enumerable.Range(0, numVertices)
                .Select(_ => new Vector3(s.ReadF32(), s.ReadF32(), s.ReadF32()))
                .ToArray();

            Vector3[] pNormals = Enumerable.Range(0, numNormals)
                .Select(_ => new Vector3(s.ReadF32(), s.ReadF32(), s.ReadF32()))
                .ToArray();

            s.Seek(12 * Dummy1, SeekOrigin.Current);

            Vector2[] pTexCoord = Enumerable.Range(0, numTexCoords)
                .Select(_ => new Vector2(s.ReadF32(), s.ReadF32()))
                .ToArray();

            uint[] pVertColours = Enumerable.Range(0, numVertexColours)
                .Select(_ => Utils.BSwap(s.ReadU32()))
                .ToArray();

            uint[] pPolyColours = Enumerable.Range(0, numPolys)
                .Select(_ => s.ReadU32())
                .ToArray();

            s.Seek(4 * numEdges, System.IO.SeekOrigin.Current);

            Polygon[] pPolygons = Enumerable.Range(0, numPolys)
                .Select(_ => new Polygon(s))
                .ToArray();

            s.Seek(24 * numUnknown2, System.IO.SeekOrigin.Current);

            s.Seek(3 * numUnknown3, System.IO.SeekOrigin.Current);

            //s.Seek(100 * numHundreds, System.IO.SeekOrigin.Current);
            var hundreds = Enumerable.Range(0, numHundreds)
                .Select(_ => new PFileHundred(s))
                .ToArray();

            Group[] pGroups = Enumerable.Range(0, numGroups)
                .Select(_ => new Group(s))
                .ToArray();

            List<Vector3> verts = new List<Vector3>(),
                normals = new List<Vector3>(),
                texcoords = new List<Vector3>();
            List<uint> colours = new List<uint>();

            foreach (var group in pGroups) {
                Dictionary<(int, int), int> vertMap = new();
                List<PFileVert> gverts = new();
                List<int> gindices = new();

                void Emit(int v, int n) {
                    if (vertMap.TryGetValue((v, n), out int i))
                        gindices.Add(i);
                    else {
                        vertMap[(v, n)] = gverts.Count;
                        gindices.Add(gverts.Count);
                        var pv = new PFileVert {
                            Position = pVerts[group.VerticesStartIndex + v],
                            Normal = pNormals.Any() ? pNormals[n] : Vector3.Zero,
                            Colour = pVertColours[group.VerticesStartIndex + v]
                        };
                        if (group.AreTexturesUsed != 0)
                            pv.TexCoord = pTexCoord[group.TexCoordStartIndex + v];
                        gverts.Add(pv);
                    }
                }

                foreach (int iPoly in Enumerable.Range(group.PolygonStartIndex, group.NumPolygons)) {
                    var poly = pPolygons[iPoly];
                    Emit(poly.V0, poly.N0);
                    Emit(poly.V2, poly.N2);
                    Emit(poly.V1, poly.N1);
                }

                Chunks.Add(new PFileChunk(
                    group.AreTexturesUsed != 0 ? group.TextureNumber : null,
                    gverts,
                    gindices,
                    hundreds[Chunks.Count]
                ));
            }
        }
    }
}
