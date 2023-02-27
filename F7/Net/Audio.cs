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

    public class SfxChannelMessage : ServerMessage {
        public int Channel { get; set; }
        public bool DoStop { get; set; }
        public bool StopLoops { get; set; }
        public bool StopChannelLoops { get; set; }
        public float? Pan { get; set; }
        public float? Volume { get; set; }

        public override void Load(NetDataReader reader) {
            Channel = reader.GetInt();
            DoStop = reader.GetBool();
            StopLoops = reader.GetBool();
            StopChannelLoops = reader.GetBool();
            if (reader.GetBool())
                Pan = reader.GetFloat();
            else
                reader.GetFloat();
            if (reader.GetBool())
                Volume = reader.GetFloat();
            else
                reader.GetFloat();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(Channel);
            writer.Put(DoStop);
            writer.Put(StopLoops);
            writer.Put(StopChannelLoops);
            writer.Put(Pan.HasValue);
            writer.Put(Pan.GetValueOrDefault());
            writer.Put(Volume.HasValue);
            writer.Put(Volume.GetValueOrDefault());
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

    public class MusicVolumeMessage : ServerMessage {
        public byte? VolumeFrom { get; set; }
        public byte VolumeTo { get; set; }
        public float Duration { get; set; }

        public override void Load(NetDataReader reader) {
            bool f = reader.GetBool();
            byte fv = reader.GetByte();
            VolumeFrom = f ? fv : null;
            VolumeTo = reader.GetByte();
            Duration = reader.GetFloat();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(VolumeFrom.HasValue);
            writer.Put(VolumeFrom.GetValueOrDefault());
            writer.Put(VolumeTo);
            writer.Put(Duration);
        }
    }
}
