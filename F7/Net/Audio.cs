using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Net {
    public class SfxMessage : ServerMessage {
        public int Which { get; set; }
        public float Volume { get; set; }
        public float Pan { get; set; }

        public override void Load(NetDataReader reader) {
            Which = reader.GetInt();
            Volume = reader.GetFloat();
            Pan = reader.GetFloat();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(Which);
            writer.Put(Volume);
            writer.Put(Pan);
        }
    }

    public class MusicMessage : ServerMessage {
        public string Track { get; set; }
        public bool IsPush { get; set; }
        public bool IsPop { get; set; }

        public override void Load(NetDataReader reader) {
            Track = reader.GetString();
            IsPush = reader.GetBool();
            IsPop = reader.GetBool();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(Track);
            writer.Put(IsPush);
            writer.Put(IsPop);
        }
    }
}
