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

    public class WorldMap : IDisposable {

        public struct MapTri {
            public byte Vert0, Vert1, Vert2;
            public byte Walkmap;
            public byte U0, V0, U1, V1, U2, V2;
            public int Texture;
            public byte Location;

            public MapTri(Stream s) {
                Vert0 = (byte)s.ReadByte();
                Vert1 = (byte)s.ReadByte();
                Vert2 = (byte)s.ReadByte();
                Walkmap = (byte)(s.ReadByte() & 0x1f);
                U0 = (byte)s.ReadByte();
                V0 = (byte)s.ReadByte();
                U1 = (byte)s.ReadByte();
                V1 = (byte)s.ReadByte();
                U2 = (byte)s.ReadByte();
                V2 = (byte)s.ReadByte();
                ushort tl = s.ReadU16();
                Texture = tl & 0x1ff;
                Location = (byte)(tl >> 9);
            }

            public void GetCorrectedTextureInfo(out string texFile, out Vector2 uv0, out Vector2 uv1, out Vector2 uv2) {
                var tex = _texMap[Texture];
                texFile = tex.Filename;
                uv0 = new Vector2((U0 - tex.UOffset) * 1f / tex.Width, (V0 - tex.VOffset) * 1f / tex.Height);
                uv1 = new Vector2((U1 - tex.UOffset) * 1f / tex.Width, (V1 - tex.VOffset) * 1f / tex.Height);
                uv2 = new Vector2((U2 - tex.UOffset) * 1f / tex.Width, (V2 - tex.VOffset) * 1f / tex.Height);
            }

            public static IEnumerable<string> TexFiles => _texMap.Select(t => t.Filename);

            private class TexMap {
                public string Filename;
                public int Width, Height, UOffset, VOffset;

                public TexMap(string filename, int w, int h, int uo, int vo) {
                    Filename = filename;
                    Width = w;
                    Height = h;
                    UOffset = uo;
                    VOffset = vo;
                }
            }

            private static TexMap[] _texMap = new[] {
                 new TexMap("pond",  32,     32,     0,  352),
                 new TexMap("riv_m2",    32,     32,     128,    64),
                 new TexMap("was_gs",    64,     64,     64,     192),
                 new TexMap("wpcltr",    32,     128,    0,  256),
                 new TexMap("wpcltr2",   32,     32,     160,    64),
                 new TexMap("bzdun",     64,     64,     192,    192),
                 new TexMap("bone",  32,     32,     224,    384),
                 new TexMap("bone2",     32,     32,     224,    416),
                 new TexMap("bornwd",    64,     64,     160,    320),
                 new TexMap("bridge",    32,     64,     192,    0),
                 new TexMap("bridge2",   32,     32,     224,    0),
                 new TexMap("cave",  32,     32,     224,    448),
                 new TexMap("cave2",     32,     32,     224,    320),
                 new TexMap("cave_s",    32,     32,     160,    224),
                 new TexMap("cdl_cl2",   64,     32,     96,     96),
                 new TexMap("cf01",  64,     32,     192,    288),
                 new TexMap("clf_bgs",   64,     32,     192,    384),
                 new TexMap("clf_ggl",   64,     64,     128,    256),
                 new TexMap("clf_ggs",   64,     32,     192,    352),
                 new TexMap("clf_l",     64,     64,     0,  0),
                 new TexMap("clf_ld",    64,     64,     64,     0),
                 new TexMap("clf_lf",    64,     64,     128,    0),
                 new TexMap("clf_lg",    32,     64,     0,  96),
                 new TexMap("clf_lr",    32,     64,     128,    0),
                 new TexMap("clf_lsg",   32,     64,     64,     64),
                 new TexMap("clf_r",     32,     32,     0,  96),
                 new TexMap("clf_s",     64,     32,     192,    0),
                 new TexMap("clf_sd",    64,     32,     192,    32),
                 new TexMap("clf_sf",    64,     32,     0,  64),
                 new TexMap("clf_sg",    32,     32,     32,     96),
                 new TexMap("clf_sg2",   32,     32,     0,  160),
                 new TexMap("clf_sr",    32,     32,     32,     96),
                 new TexMap("clf_ss",    32,     32,     32,     128),
                 new TexMap("clf_ssd",   32,     32,     0,  224),
                 new TexMap("clf_ssw",   32,     32,     224,    32),
                 new TexMap("clf_sw",    32,     32,     192,    32),
                 new TexMap("clf_w02",   64,     64,     128,    64),
                 new TexMap("clf_w03",   64,     64,     192,    64),
                 new TexMap("clf_was",   64,     32,     64,     64),
                 new TexMap("clfeg",     32,     32,     192,    320),
                 new TexMap("clfegd",    32,     32,     0,  320),
                 new TexMap("clftop",    64,     32,     192,    64),
                 new TexMap("clftop2",   32,     32,     128,    64),
                 new TexMap("cndl_cl",   64,     32,     96,     128),
                 new TexMap("cndlf",     64,     64,     160,    64),
                 new TexMap("cndlf02",   64,     64,     208,    64),
                 new TexMap("comtr",     16,     32,     144,    96),
                 new TexMap("cosinn",    32,     32,     224,    416),
                 new TexMap("cosinn2",   32,     32,     192,    448),
                 new TexMap("csmk",  32,     32,     64,     64),
                 new TexMap("csmk2",     32,     32,     96,     64),
                 new TexMap("cstds01",   32,     32,     224,    160),
                 new TexMap("cstds02",   64,     64,     0,  448),
                 new TexMap("des01",     32,     32,     160,    320),
                 new TexMap("desert",    64,     64,     128,    128),
                 new TexMap("desor",     64,     32,     160,    64),
                 new TexMap("ds1",   32,     32,     0,  256),
                 new TexMap("ds_s1",     32,     32,     192,    288),
                 new TexMap("dsee1",     32,     32,     96,     288),
                 new TexMap("dsrt_d",    32,     32,     224,    0),
                 new TexMap("dsrt_e",    64,     128,    64,     128),
                 new TexMap("edes01",    32,     32,     224,    320),
                 new TexMap("elm01",     32,     32,     160,    0),
                 new TexMap("elm02",     32,     32,     64,     96),
                 new TexMap("elm_gro",   64,     64,     0,  96),
                 new TexMap("elm_r",     32,     32,     192,    0),
                 new TexMap("elm_r2",    32,     32,     224,    0),
                 new TexMap("fall1",     32,     32,     128,    256),
                 new TexMap("farm01",    32,     32,     160,    32),
                 new TexMap("farm02",    32,     32,     192,    32),
                 new TexMap("farm_g",    32,     32,     128,    64),
                 new TexMap("farm_r",    32,     16,     128,    48),
                 new TexMap("fld",   64,     64,     64,     96),
                 new TexMap("fld_02",    64,     64,     0,  64),
                 new TexMap("fld_s",     64,     64,     0,  160),
                 new TexMap("fld_s2",    32,     32,     224,    256),
                 new TexMap("fld_sw",    64,     64,     128,    192),
                 new TexMap("fld_v",     128,    128,    0,  128),
                 new TexMap("fld_vd",    32,     64,     96,     128),
                 new TexMap("fld_vd2",   32,     64,     192,    416),
                 new TexMap("fvedge",    32,     64,     0,  0),
                 new TexMap("gclf_d",    128,    64,     128,    128),
                 new TexMap("gclf_g",    32,     64,     224,    128),
                 new TexMap("gclfwa",    128,    64,     64,     320),
                 new TexMap("gclfwa2",   32,     64,     160,    320),
                 new TexMap("gclfwag",   32,     64,     32,     320),
                 new TexMap("gg_gro",    64,     64,     64,     448),
                 new TexMap("gg_mts",    64,     128,    0,  128),
                 new TexMap("ggmk",  64,     64,     128,    448),
                 new TexMap("ggmt",  128,    128,    0,  0),
                 new TexMap("ggmt_e",    128,    32,     0,  96),
                 new TexMap("ggmt_ed",   128,    32,     128,    96),
                 new TexMap("ggmt_eg",   32,     32,     96,     224),
                 new TexMap("ggmtd",     128,    128,    128,    0),
                 new TexMap("ggs_g",     32,     32,     32,     64),
                 new TexMap("ggshr",     32,     32,     192,    96),
                 new TexMap("ggshrg",    32,     32,     224,    96),
                 new TexMap("gia",   64,     32,     64,     224),
                 new TexMap("gia2",  64,     32,     0,  224),
                 new TexMap("gia_d",     64,     32,     128,    224),
                 new TexMap("gia_d2",    64,     32,     64,     224),
                 new TexMap("gia_g",     32,     32,     192,    224),
                 new TexMap("gia_g2",    32,     32,     128,    224),
                 new TexMap("gmt_eda",   32,     32,     0,  352),
                 new TexMap("gonclf",    128,    64,     96,     64),
                 new TexMap("gredge",    32,     32,     192,    192),
                 new TexMap("hyouga",    64,     64,     192,    64),
                 new TexMap("iceclf",    64,     32,     64,     96),
                 new TexMap("iceclfd",   64,     32,     128,    96),
                 new TexMap("iceclfg",   32,     32,     32,     224),
                 new TexMap("jun",   64,     64,     192,    192),
                 new TexMap("jun_d",     64,     64,     128,    192),
                 new TexMap("jun_e",     64,     16,     0,  240),
                 new TexMap("jun_gro",   64,     64,     0,  64),
                 new TexMap("junmk",     32,     32,     0,  96),
                 new TexMap("junn01",    32,     32,     160,    112),
                 new TexMap("junn02",    32,     32,     192,    112),
                 new TexMap("junn03",    32,     32,     224,    112),
                 new TexMap("junn04",    32,     32,     64,     128),
                 new TexMap("jutmpl01",  64,     64,     128,    192),
                 new TexMap("lake-e",    32,     32,     96,     192),
                 new TexMap("lake_ef",   32,     32,     128,    224),
                 new TexMap("lake_fl",   128,    32,     160,    224),
                 new TexMap("lostclf",   32,     64,     128,    384),
                 new TexMap("lostmt",    128,    32,     128,    448),
                 new TexMap("lostmtd",   128,    32,     128,    480),
                 new TexMap("lostmts",   64,     32,     160,    384),
                 new TexMap("lostwd_e",  32,     32,     64,     480),
                 new TexMap("lostwod",   64,     64,     0,  448),
                 new TexMap("lst1",  32,     32,     192,    256),
                 new TexMap("lstwd_e2",  32,     32,     96,     480),
                 new TexMap("mzes",  32,     32,     224,    128),
                 new TexMap("mzmt_e",    128,    64,     128,    64),
                 new TexMap("mzmt_ed",   128,    32,     128,    128),
                 new TexMap("mzmt_edw",  128,    32,     128,    160),
                 new TexMap("mzmt_ew",   128,    32,     0,  128),
                 new TexMap("mzmt_o",    128,    32,     64,     416),
                 new TexMap("mzmt_od",   128,    32,     64,     448),
                 new TexMap("mzmt_s",    128,    32,     0,  192),
                 new TexMap("mzmt_sd",   128,    32,     0,  160),
                 new TexMap("md01",  32,     32,     96,     16),
                 new TexMap("md02",  128,    128,    0,  0),
                 new TexMap("md03",  16,     16,     112,    64),
                 new TexMap("md04",  32,     32,     128,    16),
                 new TexMap("md05",  64,     16,     96,     0),
                 new TexMap("md06",  16,     32,     96,     48),
                 new TexMap("md07",  16,     16,     112,    48),
                 new TexMap("md_mt",     128,    128,    128,    0),
                 new TexMap("md_mtd",    128,    128,    0,  0),
                 new TexMap("md_mts",    64,     128,    64,     160),
                 new TexMap("md_snow",   128,    32,     128,    0),
                 new TexMap("md_snw2",   128,    32,     128,    32),
                 new TexMap("md_snwd",   128,    64,     0,  128),
                 new TexMap("md_snwe",   64,     64,     96,     320),
                 new TexMap("md_snws",   64,     64,     128,    128),
                 new TexMap("md_snwt",   128,    32,     0,  192),
                 new TexMap("md_snww",   32,     32,     224,    224),
                 new TexMap("md_sw_s",   128,    128,    0,  0),
                 new TexMap("md_swd2",   32,     32,     192,    256),
                 new TexMap("md_swnp",   128,    128,    0,  96),
                 new TexMap("mdsrt_e",   128,    32,     128,    192),
                 new TexMap("mdsrt_ed",  128,    32,     128,    224),
                 new TexMap("mdsrt_eg",  32,     32,     64,     224),
                 new TexMap("midil",     32,     32,     32,     192),
                 new TexMap("midild",    32,     32,     224,    192),
                 new TexMap("mt_ewg",    32,     32,     64,     96),
                 new TexMap("mt_road",   64,     64,     192,    128),
                 new TexMap("mt_se",     32,     32,     160,    416),
                 new TexMap("mt_se2",    64,     64,     128,    256),
                 new TexMap("mt_sg01",   32,     32,     0,  224),
                 new TexMap("mt_sg02",   32,     32,     32,     224),
                 new TexMap("mt_sg03",   32,     32,     0,  192),
                 new TexMap("mt_sg04",   32,     32,     160,    384),
                 new TexMap("mtcoin",    64,     64,     0,  256),
                 new TexMap("mtwas_e",   128,    32,     0,  224),
                 new TexMap("mtwas_ed",  128,    32,     0,  224),
                 new TexMap("ncol_gro",  64,     64,     64,     384),
                 new TexMap("ncole01",   32,     32,     224,    384),
                 new TexMap("ncole02",   32,     32,     192,    416),
                 new TexMap("nivl_gro",  64,     64,     128,    384),
                 new TexMap("nivl_mt",   128,    64,     0,  0),
                 new TexMap("nivl_top",  32,     32,     0,  64),
                 new TexMap("nivlr",     32,     32,     192,    384),
                 new TexMap("port",  32,     32,     96,     224),
                 new TexMap("port_d",    32,     32,     160,    0),
                 new TexMap("rzclf02",   64,     64,     128,    128),
                 new TexMap("rct_gro",   64,     128,    0,  416),
                 new TexMap("riv_cls",   64,     64,     64,     0),
                 new TexMap("riv_l1",    32,     32,     96,     320),
                 new TexMap("riv_m",     32,     32,     0,  224),
                 new TexMap("rivr",  32,     32,     64,     224),
                 new TexMap("rivrclf",   64,     64,     128,    192),
                 new TexMap("rivs1",     32,     32,     128,    320),
                 new TexMap("rivshr",    64,     64,     192,    192),
                 new TexMap("rivssr",    64,     32,     192,    224),
                 new TexMap("rivstrt",   32,     32,     192,    160),
                 new TexMap("rm1",   32,     32,     32,     288),
                 new TexMap("rocet",     32,     32,     128,    160),
                 new TexMap("rs_ss",     32,     32,     96,     224),
                 new TexMap("sango",     32,     32,     224,    320),
                 new TexMap("sango2",    32,     32,     224,    352),
                 new TexMap("sango3",    32,     32,     128,    384),
                 new TexMap("sango4",    64,     64,     0,  384),
                 new TexMap("sdun",  64,     64,     0,  160),
                 new TexMap("sdun02",    64,     64,     64,     160),
                 new TexMap("sh1",   32,     32,     32,     256),
                 new TexMap("sh_s1",     32,     32,     224,    288),
                 new TexMap("shedge",    32,     64,     160,    160),
                 new TexMap("shlm_1",    32,     32,     192,    320),
                 new TexMap("shol",  128,    128,    128,    96),
                 new TexMap("shol_s",    64,     64,     192,    192),
                 new TexMap("shor",  128,    128,    0,  0),
                 new TexMap("shor_s",    64,     64,     128,    192),
                 new TexMap("shor_s2",   32,     32,     224,    416),
                 new TexMap("shor_v",    32,     32,     192,    0),
                 new TexMap("silo",  32,     32,     224,    32),
                 new TexMap("slope",     128,    32,     0,  384),
                 new TexMap("snow_es",   32,     32,     192,    480),
                 new TexMap("snow_es2",  32,     32,     224,    480),
                 new TexMap("snow_es3",  32,     32,     224,    448),
                 new TexMap("snw_mt",    128,    128,    0,  0),
                 new TexMap("snw_mtd",   128,    128,    128,    0),
                 new TexMap("snw_mte",   64,     32,     0,  96),
                 new TexMap("snw_mted",  64,     32,     64,     96),
                 new TexMap("snw_mts",   64,     128,    64,     0),
                 new TexMap("snw_mts2",  64,     32,     128,    192),
                 new TexMap("snwfld",    64,     64,     0,  64),
                 new TexMap("snwfld_s",  64,     32,     128,    128),
                 new TexMap("snwgra",    64,     64,     192,    192),
                 new TexMap("snwhm01",   32,     32,     32,     0),
                 new TexMap("snwhm02",   32,     32,     32,     32),
                 new TexMap("snwods",    32,     32,     224,    192),
                 new TexMap("snwood",    64,     64,     192,    128),
                 new TexMap("snwtrk",    32,     64,     96,     256),
                 new TexMap("sse_s1",    32,     32,     32,     320),
                 new TexMap("ssee1",     32,     32,     64,     288),
                 new TexMap("sst1",  32,     32,     224,    256),
                 new TexMap("stown_r",   32,     32,     192,    256),
                 new TexMap("stw_gro",   64,     64,     0,  384),
                 new TexMap("subrg2",    32,     32,     224,    160),
                 new TexMap("susbrg",    64,     64,     192,    96),
                 new TexMap("sw_se",     64,     64,     0,  0),
                 new TexMap("swclf_l",   64,     64,     64,     128),
                 new TexMap("swclf_ld",  64,     64,     192,    128),
                 new TexMap("swclf_lg",  32,     64,     0,  192),
                 new TexMap("swclf_s",   64,     32,     128,    96),
                 new TexMap("swclf_sd",  64,     32,     192,    96),
                 new TexMap("swclf_sg",  32,     32,     32,     192),
                 new TexMap("swclf_wg",  32,     32,     192,    192),
                 new TexMap("swfld_s2",  64,     32,     128,    160),
                 new TexMap("swfld_s3",  32,     32,     160,    192),
                 new TexMap("swmd_cg",   32,     32,     128,    192),
                 new TexMap("swmd_clf",  64,     32,     64,     192),
                 new TexMap("swp1",  32,     32,     0,  288),
                 new TexMap("trk",   64,     64,     128,    0),
                 new TexMap("tyo_f",     128,    128,    128,    128),
                 new TexMap("tyosnw",    64,     128,    64,     384),
                 new TexMap("uf1",   32,     32,     160,    256),
                 new TexMap("utai01",    32,     32,     32,     96),
                 new TexMap("utai02",    32,     32,     224,    64),
                 new TexMap("utai_gro",  64,     64,     128,    96),
                 new TexMap("utaimt",    32,     32,     0,  128),
                 new TexMap("utaimtd",   32,     32,     96,     96),
                 new TexMap("utaimtg",   32,     32,     96,     128),
                 new TexMap("wa1",   32,     32,     192,    320),
                 new TexMap("wzs1",  32,     32,     128,    288),
                 new TexMap("wzshr",     32,     32,     160,    32),
                 new TexMap("wzshr2",    32,     32,     32,     128),
                 new TexMap("wzshrs",    32,     32,     32,     160),
                 new TexMap("was",   128,    128,    0,  96),
                 new TexMap("was_d",     64,     32,     0,  224),
                 new TexMap("was_g",     64,     64,     0,  192),
                 new TexMap("was_s",     128,    128,    128,    0),
                 new TexMap("wasfld",    64,     64,     64,     256),
                 new TexMap("wdedge",    64,     64,     64,     160),
                 new TexMap("we1",   32,     32,     96,     256),
                 new TexMap("we_s1",     32,     32,     160,    288),
                 new TexMap("wedged",    32,     64,     128,    160),
                 new TexMap("wod-e2",     32,     32,     64,     224),
                 new TexMap("wood",  64,     64,     192,    0),
                 new TexMap("wood_d",    64,     64,     192,    160),
                 new TexMap("wtrk",  32,     64,     64,     96)
            };

        }

        public struct MapVert {
            public short X, Y, Z, W;

            public MapVert(Stream s) {
                X = s.ReadI16();
                Y = s.ReadI16();
                Z = s.ReadI16();
                W = s.ReadI16();
            }
        }

        public class MapSector {
            public List<MapVert> Vertices { get; }
            public List<MapVert> Normals { get; }
            public List<MapTri> Triangles { get; }
            public int OffsetX { get; }
            public int OffsetY { get; }

            public MapSector(Stream s, int number) {
                int numTris = s.ReadI16(), numVerts = s.ReadI16();
                Triangles = Enumerable.Range(0, numTris)
                    .Select(_ => new MapTri(s))
                    .ToList();

                Vertices = Enumerable.Range(0, numVerts)
                    .Select(_ => new MapVert(s))
                    .ToList();

                Normals = Enumerable.Range(0, numVerts)
                    .Select(_ => new MapVert(s))
                    .ToList();

                OffsetX = 8192 * (number % 4);
                OffsetY = 8192 * (number / 4);
            }
        }

        private Stream _source;

        public WorldMap(Stream source) {
            _source = source;
        }

        public List<MapSector> ExportBlock(int block) {
            int offset = block * 0xB800;
            _source.Position = offset;

            int[] offsets = Enumerable.Range(0, 16)
                .Select(_ => _source.ReadI32() + offset)
                .ToArray();

            return Enumerable.Range(0, 16)
                .Select(i => {
                    _source.Position = offsets[i];
                    int size = _source.ReadI32();
                    byte[] data = new byte[size];
                    _source.Read(data, 0, data.Length);
                    var decoded = Lzss.Decode(new MemoryStream(data), false);
                    return new MapSector(decoded, i);
                })
                .ToList();
        }

        public void Dispose() {
            _source.Dispose();
        }
    }
}
