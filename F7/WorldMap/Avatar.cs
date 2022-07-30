using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace F7.WorldMap {

    [Flags]
    public enum WalkmapType : uint {
        None = 0,
        Grass = 0x1,
        Forest = 0x2,
        Mountain = 0x4,
        Sea = 0x8,
        RiverCrossing = 0x10,
        River = 0x20,
        Water = 0x40,
        Swamp = 0x80,
        Desert = 0x100,
        Wasteland = 0x200,
        Snow = 0x400,
        Riverside = 0x800,
        Cliff =0x1000,
        CorelBridge = 0x2000,
        WutaiBridge = 0x4000,
        Unused1 = 0x8000,
        Hillside = 0x10000,
        Beach = 0x20000,
        SubPen = 0x40000,
        Canyon = 0x80000,
        MountainPass = 0x100000,
        Unknown2 = 0x200000,
        Waterfall = 0x400000,
        Unused3 = 0x800000,
        GoldSaucerDesert = 0x1000000,
        Jungle = 0x2000000,
        Sea2 = 0x4000000,
        NorthernCave = 0x8000000,
        GoldSaucerDesertBorder = 0x10000000,
        Bridgehead = 0x20000000,
        BackEntrance = 0x40000000,
        Unused4 = 0x80000000
    }

    public class AvatarAnim {
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string File { get; set; }
    }

    public class Avatar {
        public string HRC { get; set; }

        public float Scale { get; set; }
        public WalkmapType CanWalkOn { get; set; }

        [XmlElement("Animation")]
        public List<AvatarAnim> Animations { get; set; }
    }
}
