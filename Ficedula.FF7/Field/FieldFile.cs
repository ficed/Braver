using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace Ficedula.FF7.Field {

    public struct FieldBounds {
        public short Left { get; private set; }
        public short Bottom { get; private set; }
        public short Right { get; private set; }
        public short Top { get; private set; }

        public FieldBounds(Stream s) {
            Left = s.ReadI16();
            Bottom = s.ReadI16();
            Right  = s.ReadI16();
            Top = s.ReadI16();
        }

        public override string ToString() => $"Left: {Left} Top: {Top} Right: {Right} Bottom: {Bottom}";
    }

    public class FieldDestination {
        public short X { get; set; }
        public short Y { get; set; }
        public ushort Triangle { get; set; }
        public short DestinationFieldID { get; set; }
        public byte Orientation { get; set; }

        public FieldDestination() { }
        public FieldDestination(Stream s) {
            X = s.ReadI16();
            Y = s.ReadI16();
            Triangle = s.ReadU16();
            DestinationFieldID = s.ReadI16();
            Orientation = (byte)(s.ReadI32() & 0xff);
        }
    }

    public class Gateway {
        public FieldVertex V0 { get; private set; }
        public FieldVertex V1 { get; private set; }
        public FieldDestination Destination { get; private set; }
        public bool ShowArrow { get; set; }

        public Gateway(Stream s) {
            V0 = new FieldVertex(s, false);
            V1 = new FieldVertex(s, false);
            Destination = new FieldDestination(s);
        }
    }

    public enum TriggerBehaviour : byte {
        OnNone = 0,
        OffNone = 1,
        OnOff = 2,
        OffOn = 3,
        OnOffPlus = 4,
        OffOnPlus = 5,
    }
    public class Trigger {
        public FieldVertex V0 { get; private set; }
        public FieldVertex V1 { get; private set; }
        public byte BackgroundID { get; private set; }
        public byte BackgroundState { get; private set; }
        public TriggerBehaviour Behaviour { get; private set; }
        public byte SoundID { get; private set; }

        public Trigger(Stream s) {
            V0 = new FieldVertex(s, false);
            V1 = new FieldVertex(s, false);
            BackgroundID = s.ReadU8();
            BackgroundState = s.ReadU8();
            Behaviour = (TriggerBehaviour)s.ReadU8();
            SoundID = s.ReadU8();
        }
    }

    public enum ArrowType : int {
        Disabled = 0,
        Red = 1,
        Green = 2,
    }

    public class Arrow {
        public Vector3 Position { get; private set; }
        public ArrowType Type { get; private set; }

        public Arrow(Stream s) {
            Position = new Vector3(s.ReadI32(), s.ReadI32(), s.ReadI32());
            Type = (ArrowType)s.ReadI32();
        }
    }

    public class TriggersAndGateways {
        public string Name { get; private set; }
        public byte ControlDirection { get; private set; }
        public short CameraFocus { get; private set; }
        public FieldBounds CameraRange { get; private set; }
        public short? BG3AnimWidth { get; private set; }
        public short? BG3AnimHeight { get; private set; }
        public short? BG4AnimWidth { get; private set; }
        public short? BG4AnimHeight { get; private set; }

        public List<Gateway> Gateways { get; }
        public List<Trigger> Triggers { get; }
        public List<Arrow> Arrows { get; }

        public TriggersAndGateways(Stream s) {
            s.Position = 0;
            byte[] bname = new byte[9];
            s.Read(bname, 0, 9);
            Name = Encoding.ASCII.GetString(bname).Trim();
            ControlDirection = s.ReadU8();
            CameraFocus = s.ReadI16();
            CameraRange = new FieldBounds(s);
            s.ReadI32();
            BG3AnimWidth = Util.ValueOrNull(s.ReadI16(), (short)1024);
            BG3AnimHeight = Util.ValueOrNull(s.ReadI16(), (short)1024);
            BG4AnimWidth = Util.ValueOrNull(s.ReadI16(), (short)1024);
            BG4AnimHeight = Util.ValueOrNull(s.ReadI16(), (short)1024);
            s.Seek(24, SeekOrigin.Current); //....
            Gateways = Enumerable.Range(0, 12)
                .Select(_ => new Gateway(s))
                .ToList();
            Triggers = Enumerable.Range(0, 12)
                .Select(_ => new Trigger(s))
                .ToList();
            foreach (var gateway in Gateways)
                gateway.ShowArrow = s.ReadU8() != 0;
            Arrows = Enumerable.Range(0, 12)
                .Select(_ => new Arrow(s))
                .ToList();
        }
    }

    public class CameraMatrix {
        public Vector3 Forwards { get; private set; }
        public Vector3 Up { get; private set; }
        public Vector3 Right { get; private set; }
        public Vector3 CameraPosition { get; private set; }
        public short Zoom { get; private set; }

        public CameraMatrix(Stream s) {
            Vector3 DecodeVec() {
                return new Vector3(
                    s.ReadI16() / 4096f,
                    s.ReadI16() / 4096f,
                    s.ReadI16() / 4096f
                );
            }

            Right = DecodeVec();
            Up = -DecodeVec();
            Forwards = DecodeVec();

            s.ReadI16();

            float ox = s.ReadI32() / 4096f, oy = -s.ReadI32() / 4096f, oz = s.ReadI32() / 4096f;

            CameraPosition = -(ox * Right + oy * Up + oz * Forwards);

            float tx = -(ox * Right.X + oy * Up.X + oz * Forwards.X),
                ty = -(ox * Right.Y + oy * Up.Y + oz * Forwards.Y),
                tz = -(ox * Right.Z + oy * Up.Z + oz * Forwards.Z);

            CameraPosition = new Vector3(tx, ty, tz);

            s.ReadI32();

            Zoom = s.ReadI16();
        }
    }

    public struct FieldVertex : IEquatable<FieldVertex> {
        public short X { get; set; }
        public short Y { get; set; }
        public short Z { get; set; }

        public FieldVertex(Stream s, bool withPadding) {
            X = s.ReadI16();
            Y = s.ReadI16();
            Z = s.ReadI16();
            if (withPadding)
                s.ReadI16();
        }

        public override string ToString() => $"X:{X} Y:{Y} Z:{Z}";

        public bool Equals(FieldVertex other) {
            return (X == other.X) && (Y == other.Y) && (Z == other.Z);
        }

        public override bool Equals(object obj) {
            return obj is FieldVertex && Equals((FieldVertex)obj);
        }

        public override int GetHashCode() {
            return HashCode.Combine(X, Y, Z);
        }

        public static bool operator ==(FieldVertex left, FieldVertex right) {
            return left.Equals(right);
        }

        public static bool operator !=(FieldVertex left, FieldVertex right) {
            return !(left == right);
        }
    }

    public class WalkmeshTriangle {
        public FieldVertex V0 { get; set; }
        public FieldVertex V1 { get; set; }
        public FieldVertex V2 { get; set; }
        public short? V01Tri { get; set; }
        public short? V12Tri { get; set; }
        public short? V20Tri { get; set; }

        public IEnumerable<FieldVertex> AllVerts() {
            yield return V0;
            yield return V1;
            yield return V2;
        }
    }

    public class Walkmesh {
        public List<WalkmeshTriangle> Triangles { get; private set; }

        public Walkmesh(Stream source) {
            source.Position = 0;
            int count = source.ReadI32();
            Triangles = Enumerable.Range(0, count)
                .Select(_ => new WalkmeshTriangle {
                    V0 = new FieldVertex(source, true),
                    V1 = new FieldVertex(source, true),
                    V2 = new FieldVertex(source, true)
                })
                .ToList();

            foreach(int i in Enumerable.Range(0, count)) {
                Triangles[i].V01Tri = Util.ValueOrNull(source.ReadI16(), (short)-1);
                Triangles[i].V12Tri = Util.ValueOrNull(source.ReadI16(), (short)-1);
                Triangles[i].V20Tri = Util.ValueOrNull(source.ReadI16(), (short)-1);
            }
        }
    }

    public class Encounter {
        public byte Frequency { get; set; }
        public ushort EncounterID { get; set; }

        public Encounter(ushort value) {
            Frequency = (byte)(value >> 10);
            EncounterID = (ushort)(value & 0x3ff);
        }
    }

    public class EncounterTable {
        public bool Enabled { get; set; }
        public byte Rate { get; set; }
        public List<Encounter> StandardEncounters { get; }
        public Encounter BackAttack1 { get; set; }
        public Encounter BackAttack2 { get; set; }
        public Encounter SideAttack { get; set; }
        public Encounter BothSidesAttack { get; set; }

        public IEnumerable<Encounter> SpecialEncounters => new[] { BackAttack1, BackAttack2, SideAttack, BothSidesAttack };
        public IEnumerable<Encounter> AllEncounters => StandardEncounters
            .Concat(SpecialEncounters);

        public EncounterTable(Stream s) {
            Enabled = s.ReadByte() != 0;
            Rate = (byte)s.ReadByte();
            StandardEncounters = Enumerable.Range(0, 6)
                .Select(_ => new Encounter(s.ReadU16()))
                .ToList();
            BackAttack1 = new Encounter(s.ReadU16());
            BackAttack2 = new Encounter(s.ReadU16());
            SideAttack = new Encounter(s.ReadU16());
            BothSidesAttack = new Encounter(s.ReadU16());

            s.ReadU16();
        }
    }

    public class Palettes {
        public short PalX { get; private set; }
        public short PalY { get; private set; }
        public List<ushort[]> PaletteData { get; private set; }

        public Palettes(Stream source) {
            source.Position = 4;
            PalX = source.ReadI16();
            PalY = source.ReadI16();
            int colours = source.ReadI16(), palcount = source.ReadI16();
            PaletteData = new List<ushort[]>();
            PaletteData = Enumerable.Range(0, palcount)
                .Select(_ => {
                    ushort[] data = new ushort[256];
                    foreach (int i in Enumerable.Range(0, 256)) {
                        ushort colour = source.ReadU16();
                        if (colour != 0)
                            data[i] = colour;
                        else
                            data[i] = data[0];
                    }
                    return data;
                })
                .ToList();
        }
    }

    public class FieldFile {

        private List<Stream> _sections;

        public FieldFile(Stream source) {
            using(var data = Lzss.Decode(source, true)) {
                data.Position = 2;
                if (data.ReadI32() != 9)
                    throw new FFException($"Invalid field file number of section (should be 9)");

                var offsets = Enumerable.Range(0, 9)
                    .Select(_ => data.ReadI32())
                    .ToArray();

                _sections = offsets
                    .Select(offset => {
                        data.Position = offset;
                        int size = data.ReadI32();
                        byte[] section = new byte[size];
                        data.Read(section, 0, section.Length);
                        return (Stream)new MemoryStream(section);
                    })
                    .ToList();
            }
        }

        public Palettes GetPalettes() => new Palettes(_sections[3]);
        public Walkmesh GetWalkmesh() => new Walkmesh(_sections[4]);
        public TriggersAndGateways GetTriggersAndGateways() => new TriggersAndGateways(_sections[7]);
        public Background GetBackground() => new Background(_sections[8], GetPalettes());
        public DialogEvent GetDialogEvent() => new DialogEvent(_sections[0]);
        public FieldModels GetModels() => new FieldModels(_sections[2]);

        public IEnumerable<EncounterTable> GetEncounterTables() {
            _sections[6].Position = 0;
            var table0 = new EncounterTable(_sections[6]);
            var table1 = new EncounterTable(_sections[6]);
            return new[] { table0, table1 };
        }

        public IEnumerable<CameraMatrix> GetCameraMatrices() {
            _sections[1].Position = 0;
            List<CameraMatrix> matrices = new();
            while (_sections[1].Position < _sections[1].Length)
                matrices.Add(new CameraMatrix(_sections[1]));
            return matrices.AsReadOnly();
        }

    }

    public class MovieCam {
        private List<CameraMatrix> _cameras = new();

        public IReadOnlyList<CameraMatrix> Camera => _cameras.AsReadOnly();

        public MovieCam(Stream s) {
            while(s.Position < s.Length) {
                _cameras.Add(new CameraMatrix(s));
                s.ReadI16();
            }
        }
    }
}
