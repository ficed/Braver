﻿// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

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
        public List<ushort> AkaoMusicIDs { get; }

        public byte[] ScriptBytecode { get; }

        public DialogEvent(Stream source) {
            source.Position = 0;
            Trace.Assert(source.ReadI16() == 0x0502);

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

            Trace.WriteLine($"Field.DialogEvent: Creator {Creator}, Name {Name}, entities {string.Join(" / ", entNames)}");

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

            AkaoMusicIDs = new();
            foreach(int offset in akaoOffsets) {
                source.Position = offset;
                int size = akaoOffsets
                    .Where(os => os > offset)
                    .OrderBy(os => os)
                    .FirstOrDefault((int)source.Length)
                    - offset;
                if (size < 6)
                    continue;
                byte[] data = new byte[size];
                source.Read(data, 0, size);
                if (Encoding.ASCII.GetString(data, 0, 4) == "AKAO") {
                    AkaoMusicIDs.Add(BitConverter.ToUInt16(data, 4));
                }
            }
        }
    }
}
