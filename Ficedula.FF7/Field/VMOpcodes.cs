// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

namespace Ficedula.FF7.Field {

    public class DisassembledOp {
        public int Offset { get; set; }
        public byte Opcode { get; set; }
        public string OpcodeName { get; set; }
        public List<(string arg, int value)> Arguments { get; } = new();

        public override string ToString() {
            return $"{Offset:x3}: {OpcodeName} {string.Join(" ", Arguments.Select(a => a.arg + "=" + a.value))}";
        }
    }

    public class Opcode {
        public string OpcodeName { get; set; }
        public List<(int numBytes, string name)> Arguments { get; } = new();
    }

    public static class VMOpcodes {

        private static Dictionary<byte, Opcode> _opcodes = new();

        static VMOpcodes() {
            using (var src = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Ficedula.FF7.Field.Opcodes.txt")) {
                using (var sr = new System.IO.StreamReader(src)) {
                    string line;
                    while ((line = sr.ReadLine()) != null) {
                        string[] parts = line.Split('\t');
                        Opcode op = new Opcode {
                            OpcodeName = parts[1],
                        };
                        foreach (string arg in parts.Skip(2)) {
                            string[] argParts = arg.Split(':');
                            int size;
                            if (argParts[0] == "u8")
                                size = 1;
                            else if (argParts[0] == "u16")
                                size = 2;
                            else if (argParts[0] == "u32")
                                size = 4;
                            else if (argParts[0] == "u8*")
                                size = 0;
                            else
                                throw new NotImplementedException();
                            op.Arguments.Add((size, argParts[1]));
                        }
                        _opcodes[byte.Parse(parts[0], System.Globalization.NumberStyles.AllowHexSpecifier)] = op;
                    }
                }
            }
        }

        public static IEnumerable<DisassembledOp> Disassemble(byte[] script, int initialOffset) {
            int ip = 0;
            while (ip < script.Length) {
                DisassembledOp dop = new DisassembledOp {
                    Offset = initialOffset + ip,
                    Opcode = script[ip++],
                };
                var op = _opcodes[dop.Opcode];
                dop.OpcodeName = op.OpcodeName;

                int opL = 0;
                foreach(var arg in op.Arguments) {
                    if (arg.numBytes == 0) {
                        if (opL == 0)
                            throw new NotSupportedException();
                        foreach (int i in Enumerable.Range(0, opL - 1 - op.Arguments.Sum(a => a.numBytes)))
                            dop.Arguments.Add(($"VArg{i}", script[ip++]));
                    } else {
                        int value = 0;
                        foreach (int i in Enumerable.Range(0, arg.numBytes)) {
                            value |= script[ip++] << (8 * i);
                        }
                        if (arg.name == "L") opL = value;
                        dop.Arguments.Add((arg.name, value));
                    }
                }

                yield return dop;
            }
        }
    }
}
