// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ficedula.FF7.Battle {

    public abstract class BaseCameraData : IDisposable {
        protected Stream _source;
        protected List<int[]> _offsets;

        public int CameraCount { get; protected set; }

        protected byte[] Read(int group, int camera) {
            int[] offsets = _offsets[group];
            int offset = offsets[camera],
                next = offsets[camera + 1];
            byte[] data = new byte[next - offset];
            _source.Position = offset;
            _source.Read(data, 0, data.Length);
            return data;
        }

        public void Dispose() {
            _source.Dispose();
        }
    }

    public class EmbeddedCameraData : BaseCameraData {

        public EmbeddedCameraData(Stream source, int baseAddress, int positionTableOffset, int focusTableOffset) {
            _source = source;

            List<int> GetOffsets(int start) {
                var offsets = new List<int>();
                source.Position = start;
                while (true) {
                    int offset = source.ReadI32();
                    if (offset < baseAddress) break;
                    offsets.Add(offset - baseAddress);
                }
                return offsets;
            }

            var posOffset = GetOffsets(positionTableOffset);
            var focusOffset = GetOffsets(focusTableOffset);

            CameraCount = posOffset.Count;

            if (posOffset.Last() > focusOffset.Last()) {
                focusOffset.Add(posOffset.Last());
                posOffset.Add(Math.Min(positionTableOffset, focusTableOffset));
            } else {
                posOffset.Add(focusOffset.Last());
                focusOffset.Add(Math.Min(positionTableOffset, focusTableOffset));
            }

            _offsets = new List<int[]> {
                posOffset.ToArray(), focusOffset.ToArray()
            };
        }

        public CameraPositionScript ReadPosition(int camera) {
            return new CameraPositionScript(Read(0, camera));
        }
        public CameraFocusScript ReadFocus(int camera) {
            return new CameraFocusScript(Read(1, camera));
        }
    }

    public class CameraData : BaseCameraData {

        private byte[] Read(int camera, int index, bool focus, bool victoryCamera) {
            return Read((focus ? 1 : 0) + (victoryCamera ? 2 : 0), camera * 3 + index);
        }

        public CameraPositionScript ReadPosition(int camera, int index, bool isVictory) {
            return new CameraPositionScript(Read(camera, index, false, isVictory));
        }
        public CameraFocusScript ReadFocus(int camera, int index, bool isVictory) {
            return new CameraFocusScript(Read(camera, index, true, isVictory));
        }

        public CameraData(Stream source) {
            _source = source;

            var offsets = Enumerable.Range(0, 4)
                .Select(_ => (int)(source.ReadU32() - 0x801A0000))
                .ToArray();
            CameraCount = (offsets[1] - offsets[0]) / 12; //4 byte pointer, 3 cameras per entry

            _offsets = Enumerable.Range(0, 4)
                .Select(i => {
                    int count = i < 2 ? CameraCount : 1;
                    source.Position = offsets[i];
                    int[] pointers = Enumerable.Range(0, count)
                        .Select(_ => (int)(source.ReadU32() - 0x801A0000))
                        .ToArray();
                    return pointers;
                })
                .ToList();
        }
    }

    public abstract class CameraScript<T> where T : struct {

        protected static Dictionary<T, int[]> _opParams;

        private byte[] _bytecode;
        private int _ip;
        private int? _previousIP;

        protected CameraScript(byte[] bytecode) {
            _bytecode = bytecode;
        }

        protected abstract T FromByte(byte b);

        public void Rewind() {
            if (_previousIP != null) {
                _ip = _previousIP.Value;
                _previousIP = null;
            } else
                throw new NotSupportedException();
        }

        public void ConditionalRestart(int waitCounter) {
            if ((waitCounter == 0) && _bytecode[_ip] == 0xc0) {
                _ip = 0;
            }
        }

        public DecodedCameraOp<T> NextOp() {
            _previousIP = _ip;
            var op = new DecodedCameraOp<T> {
                Opcode = FromByte(_bytecode[_ip++]),
            };

            if (_opParams.TryGetValue(op.Opcode, out int[] parms)) {
                op.Operands = parms
                    .Select(i => {
                        switch (i) {
                            case 1:
                                return (int)_bytecode[_ip++];
                            case 2:
                                short s = BitConverter.ToInt16(_bytecode, _ip);
                                _ip += 2;
                                return s;
                            default:
                                throw new NotImplementedException();
                        }
                    })
                    .ToArray();
            }
            return op;
        }
    }

    public class CameraPositionScript : CameraScript<CameraPositionOpcode> {

        static CameraPositionScript() {
            _opParams = new() {
                [CameraPositionOpcode.UnknownD5] = new[] { 2 },
                [CameraPositionOpcode.UnknownD7] = new[] { 1, 1 },
                [CameraPositionOpcode.UnknownD8] = new[] { 1, 1, 2, 2, 2, 1 },
                [CameraPositionOpcode.SetActiveIdleCamera] = new[] { 1 },
                [CameraPositionOpcode.UnknownDE] = new[] { 1 },
                [CameraPositionOpcode.UnknownE0] = new[] { 1, 1 },
                [CameraPositionOpcode.TransitionToIdle] = new[] { 1 },
                [CameraPositionOpcode.UnknownE3] = new[] { 1, 1, 2, 2, 2, 1 },
                [CameraPositionOpcode.TransitionToAttackerJoint] = new[] { 1, 2, 2, 2, 1 },
                [CameraPositionOpcode.TransitionToTargetJoint] = new[] { 1, 2, 2, 2, 1 },
                [CameraPositionOpcode.UnknownE6] = new[] { 2, 2, 2, 1 },
                [CameraPositionOpcode.UnknownE7] = new[] { 1, 2, 2, 2, 1 },
                [CameraPositionOpcode.UnknownE9] = new[] { 1, 2, 2, 2, 1 },
                [CameraPositionOpcode.UnknownEB] = new[] { 1, 1, 2, 2, 2, 1 },
                [CameraPositionOpcode.UnknownEF] = new[] { 1, 1, 2, 2, 2 },
                [CameraPositionOpcode.JumpToAttackerJoint] = new[] { 1, 2, 2, 2 },
                [CameraPositionOpcode.UnknownF2] = new[] { 1, 2, 2 },
                [CameraPositionOpcode.UnknownF3] = new[] { 1, 2, 2 },
                [CameraPositionOpcode.SetWait] = new[] { 1 },
                [CameraPositionOpcode.UnknownF7] = new[] { 1, 2, 2, 2 },
                [CameraPositionOpcode.UnknownF8] = new[] { 2, 2, 2, 2, 2, 2 },
                [CameraPositionOpcode.LoadPoint] = new[] { 2, 2, 2 },
            };
        }

        public CameraPositionScript(byte[] bytecode) : base(bytecode) {
        }

        protected override CameraPositionOpcode FromByte(byte b) {
            return (CameraPositionOpcode)b;
        }
    }

    public class CameraFocusScript : CameraScript<CameraFocusOpcode> {

        static CameraFocusScript() {
            _opParams = new() {
                [CameraFocusOpcode.UnknownD8] = new[] { 1, 1, 2, 2, 2, 1 },
                [CameraFocusOpcode.SetActiveIdleCamera] = new[] { 1 },
                [CameraFocusOpcode.UnknownDE] = new[] { 1 },
                [CameraFocusOpcode.UnknownE0] = new[] { 1, 1 },
                [CameraFocusOpcode.TransitionToIdle] = new[] { 1 },
                [CameraFocusOpcode.UnknownE3] = new[] { 1, 1, 2, 2, 2, 1 },
                [CameraFocusOpcode.TransitionToAttackerJoint] = new[] { 1, 1, 2, 2, 2, 1 },
                [CameraFocusOpcode.TransitionToTargetJoint] = new[] { 1, 2, 2, 2, 1 },
                [CameraFocusOpcode.UnknownE6] = new[] { 2, 2, 2, 1 },
                [CameraFocusOpcode.JumpToAttackerJoint] = new[] { 1, 2, 2, 2, 1 },
                [CameraFocusOpcode.UnknownEA] = new[] { 1, 2, 2, 2, 1 },
                [CameraFocusOpcode.UnknownEC] = new[] { 1, 1, 2, 2, 2, 1 },
                [CameraFocusOpcode.UnknownF0] = new[] { 1, 1, 2, 2, 2 },
                [CameraFocusOpcode.SetWait] = new[] { 1 },
                [CameraFocusOpcode.UnknownF8] = new[] { 1, 2, 2, 2 },
                [CameraFocusOpcode.UnknownF9] = new[] { 1, 2, 2, 2 },
                [CameraFocusOpcode.LoadPoint] = new[] { 2, 2, 2 },
            };
        }

        public CameraFocusScript(byte[] bytecode) : base(bytecode) {
        }

        protected override CameraFocusOpcode FromByte(byte b) {
            return (CameraFocusOpcode)b;
        }
    }

    public struct DecodedCameraOp<T> where T : struct {
        public T Opcode { get; set; }
        public int[] Operands { get; set; }
    }

    public enum CameraPositionOpcode {
        UnknownD5 = 0xD5,
        UnknownD6 = 0xD6,
        UnknownD7 = 0xD7,
        UnknownD8 = 0xD8,
        UnknownD9 = 0xD9,
        UnknownDA = 0xDA,
        UnknownDB = 0xDB,
        UnknownDC = 0xDC,
        SetActiveIdleCamera = 0xDD,
        UnknownDE = 0xDE,
        UnknownDF = 0xDF,
        UnknownE0 = 0xE0,
        LoadIdleCameraPos = 0xE1,
        TransitionToIdle = 0xE2,
        UnknownE3 = 0xE3,
        TransitionToAttackerJoint = 0xE4,
        TransitionToTargetJoint = 0xE5,
        UnknownE6 = 0xE6,
        UnknownE7 = 0xE7,
        UnknownE9 = 0xE9,
        UnknownEB = 0xEB,
        UnknownEF = 0xEF,
        JumpToAttackerJoint = 0xF0,
        UnknownF1 = 0xF1,
        UnknownF2 = 0xF2,
        UnknownF3 = 0xF3,
        Wait = 0xF4,
        SetWait = 0xF5,
        UnknownF7 = 0xF7,
        UnknownF8 = 0xF8,
        LoadPoint = 0xF9,
        ConditionalRestart = 0xFE,
        ScriptEnd = 0xFF,
    }

    public enum CameraFocusOpcode {
        UnknownD8 = 0xD8,
        UnknownD9 = 0xD9,
        UnknownDB = 0xDB,
        UnknownDC = 0xDC,
        SetActiveIdleCamera = 0xDD,
        UnknownDE = 0xDE,
        UnknownDF = 0xDF,
        UnknownE0 = 0xE0,
        LoadIdleCameraPos = 0xE1,
        TransitionToIdle = 0xE2,
        UnknownE3 = 0xE3,
        TransitionToAttackerJoint = 0xE4,
        TransitionToTargetJoint = 0xE5,
        UnknownE6 = 0xE6,
        JumpToAttackerJoint = 0xE8,
        UnknownEA = 0xEA,
        UnknownEC = 0xEC,
        UnknownF0 = 0xF0,
        Wait = 0xF4,
        SetWait = 0xF5,
        UnknownF8 = 0xF8,
        UnknownF9 = 0xF9,
        LoadPoint = 0xFA,
        ConditionalRestart = 0xFE,
        ScriptEnd = 0xFF,
    }
}
