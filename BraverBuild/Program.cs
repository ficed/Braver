//BraverBuild [kind] [inputfile] [outputfile]

using System;

if (args.Length < 3)
    throw new Exception($"Bad arguments");

if (args[0] == "SAVEMAP") {
    Dictionary<MapSize, string> _csTypes = new Dictionary<MapSize, string> {
        [MapSize.u8] = "byte",
        [MapSize.u16] = "ushort",
        [MapSize.s8] = "sbyte",
        [MapSize.s16] = "short",
    };

    var output = new List<string>();

    foreach(string line in File.ReadAllLines(args[1])) {
        if (line.Trim().StartsWith("#"))
            continue;
        var parts = line.Split('\t').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        if (parts.Length < 3)
            continue;

        string name = parts[1];
        MapSize size = Enum.Parse<MapSize>(parts[2]);
        int address = int.Parse(parts[0], System.Globalization.NumberStyles.HexNumber);
        int offset = (address - 0xBA4) & 0xff;
        string typ = _csTypes[size];
        string access = "_memory.Read(" + GetBank(address, Is16Bit(size)) + ", 0x" + offset.ToString("x2") + ")";
        string setter = "_memory.Write(" + GetBank(address, Is16Bit(size)) + ", 0x" + offset.ToString("x2") + ", (" + typ + ")value)";

        if (parts.Length == 4) {
            string enumType = parts[3];
            output.Add($"\t\tpublic {enumType} {name} {{ get => ({enumType}){access}; set => {setter}; }}");
        } else {
            output.Add($"\t\tpublic {typ} {name} {{ get => ({typ}){access}; set => {setter}; }}");
        }
    }

    var finalOutput = new[] {
        "namespace Braver {",
        "   public class SaveMap {",
        "       private VMM _memory;",
        "       public SaveMap(VMM memory) { _memory = memory; }",
        "",
        string.Join("\r\n", output),
        "   }",
        "}",
    };
    File.WriteAllLines(args[2], finalOutput);
}

bool Is16Bit(MapSize size) {
    switch (size) {
        case MapSize.u16:
        case MapSize.s16:
            return true;
        default:
            return false;
    }
}
int GetBank(int address, bool is16Bit) {
    switch((address - 0xBA4) >> 16) {
        case 0x0:
            return is16Bit ? 2 : 1;
        case 0x1:
            return is16Bit ? 4 : 3;
        case 0x2:
            return is16Bit ? 0xC : 0xB;
        case 0x3:
            return is16Bit ? 0xE : 0xD;
        case 0x4:
            return is16Bit ? 0xF : 7;
        default:
            throw new NotImplementedException();
    }
}

enum MapSize {
    u8,
    u16,
//    u32,
    s8,
    s16,
//    s32
}
