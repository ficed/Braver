using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7.Field {

    public class Entity {
        public string Name { get; }
        public List<int> Scripts { get; }

        public Entity(string name, IEnumerable<int> scripts) {
            Name = name;
            Scripts = scripts.ToList();
        }
    }

    public class DialogEvent {

        public string Creator { get; }
        public string Name { get; }
        public short Scale { get; }
        public List<Entity> Entities { get; }
        public List<string> Dialogs { get; }

        public byte[] ScriptBytecode { get; }

        public DialogEvent(Stream source) {
            source.Position = 0;
            Debug.Assert(source.ReadI16() == 0x0502);

            string ReadName() {
                byte[] buffer = new byte[8];
                source.Read(buffer, 0, buffer.Length);
                return Encoding.UTF8.GetString(buffer).Trim('\0');
            }

            byte nEntities = source.ReadU8(), nModels = source.ReadU8();
            ushort strOffset = source.ReadU16(), nAkaoOffsets = source.ReadU16();
            Scale = source.ReadI16();
            source.Seek(6, SeekOrigin.Current);

            Creator = ReadName();
            Name = ReadName();

            string[] entNames = Enumerable.Range(0, nEntities)
                .Select(_ => ReadName())
                .ToArray();

            int[] akaoOffsets = Enumerable.Range(0, nAkaoOffsets)
                .Select(_ => source.ReadI32())
                .ToArray();

            ushort[][] scripts = Enumerable.Range(0, nEntities)
                .Select(_ =>
                    Enumerable.Range(0, 32)
                    .Select(_ => source.ReadU16())
                    .ToArray()
                )
                .ToArray();


            ScriptBytecode = new byte[strOffset - scripts[0][0]];
            source.Position = scripts[0][0];
            source.Read(ScriptBytecode, 0, ScriptBytecode.Length);

            Entities = new();
            foreach(int e in Enumerable.Range(0, nEntities)) {
                Entities.Add(new Entity(entNames[e], scripts[e].Select(us => us - scripts[0][0])));
            }


            source.Position = strOffset;
            ushort numDialog = source.ReadU16();
            ushort[] dlgOffsets = Enumerable.Range(0, numDialog)
                .Select(_ => source.ReadU16())
                .ToArray();

            Dialogs = Enumerable.Range(0, numDialog)
                .Select(d => {
                    source.Position = strOffset + dlgOffsets[d];
                    List<byte> chars = new();
                    byte c;
                    while (true) {
                        c = source.ReadU8();
                        if (c == 0xff) break;
                        chars.Add(c);
                    }
                    return Text.Convert(chars.ToArray(), 0, chars.Count);
                })
                .ToList();

            //TODO: AKAO
        }
    }
}
