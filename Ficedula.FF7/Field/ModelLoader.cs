using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7.Field {

    public class LoadModel {
        public string Name { get; }
        public string HRC { get; }
        public string Scale { get; }
        public uint Light1Color { get; }
        public FieldVertex Light1Pos { get; }
        public uint Light2Color { get; }
        public FieldVertex Light2Pos { get; }
        public uint Light3Color { get; }
        public FieldVertex Light3Pos { get; }
        public uint GlobalLightColor { get; }
        public List<string> Animations { get; }

        public LoadModel(Stream s) {

            string GetStr(ushort? size) {
                size ??= s.ReadU16();
                byte[] buffer = new byte[size.Value];
                s.Read(buffer, 0, buffer.Length);
                return Encoding.ASCII.GetString(buffer).Trim('\0');
            }

            Name = GetStr(null);
            s.ReadU16(); //???
            HRC = GetStr(8);
            Scale = GetStr(4);

            ushort animCount = s.ReadU16();
            Light1Color = s.ReadU32() & 0xffffff;
            s.Seek(-1, SeekOrigin.Current);
            Light1Pos = new FieldVertex(s, false);

            Light2Color = s.ReadU32() & 0xffffff;
            s.Seek(-1, SeekOrigin.Current);
            Light2Pos = new FieldVertex(s, false);

            Light3Color = s.ReadU32() & 0xffffff;
            s.Seek(-1, SeekOrigin.Current);
            Light3Pos = new FieldVertex(s, false);

            GlobalLightColor = s.ReadU32() & 0xffffff;
            s.Seek(-1, SeekOrigin.Current);

            Animations = Enumerable.Range(0, animCount)
                .Select(_ => {
                    string anim = GetStr(null);
                    s.ReadU16();
                    return anim;
                })
                .ToList();
        }
    }

    public class FieldModels {
        public short ModelScale { get; }
        public List<LoadModel> Models { get; }

        public FieldModels(Stream source) {
            source.Position = 0;
            source.ReadI16();
            int count = source.ReadI16();
            ModelScale = source.ReadI16();
            Models = Enumerable.Range(0, count)
                .Select(_ => new LoadModel(source))
                .ToList();
        }
    }
}
