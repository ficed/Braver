// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7.Battle {
    public class AnimationScript {

        private List<byte[]> _scripts = new();

        public IReadOnlyList<byte[]> Scripts => _scripts.AsReadOnly();

        public AnimationScript(Stream s) {
            byte[] header = new byte[0x68];
            s.Read(header, 0, header.Length);

            var offsets = new List<int>();
            do {
                offsets.Add(s.ReadI32());
            } while (s.Position < offsets[0]);

            foreach(int offset in Enumerable.Range(0, offsets.Count)) {
                int start = offsets[offset];
                int end = offsets
                    .Where(i => i > start)
                    .OrderBy(i => i)
                    .FirstOrDefault((int)s.Length);
                byte[] data = new byte[end - start];
                s.Position = start;
                s.Read(data, 0, data.Length);
                _scripts.Add(data);
            }
        }
    }

    public enum AnimScriptOp : byte {
        WaitForAllAnimationDamage = 0x9e,
        ResetDefendingActorPosition = 0xa6,
        MoveRelativeCover = 0xab,
        JumpTargetNOP = 0xb2,
        JumpNotSmall = 0xb3,
        BackPincerSetFacing = 0xb4,
        MoveXTimed = 0xc4,
        JumpTargetNOP_2 = 0xc9,
        MoveRelativeTargetX = 0xd0,
        MoveRelativeTargetXY = 0xd1,
        AttackSfxDelayed = 0xd8,
        LimitCharge = 0xe0,
        ResetStandingPosition = 0xe5,
        MagicCharge = 0xe6,
        LoadEffect = 0xe8,
        DisplayActionName = 0xea,
        WaitForEffectLoad = 0xec,
        RunIdleAnimScript = 0xee,
        FootDustEffect = 0xf0,
        WaitForTimer = 0xf3,
        SetWaitTimer = 0xf4,
        PlaySfxQueueReactionAndTriggerDamage = 0xf7,
        ReturnToPreviousPosition = 0xfa,
        SetAttackerTargetDirections = 0xfc,
        ConditionalEndScript = 0xfe,
    }

    public struct DecodedAnimScriptOp {
        public AnimScriptOp Op { get; set; }
        public int[]? Operands { get; set; }

        public override string ToString() {
            string o = Op.ToString();
            if (int.TryParse(o, out _)) { //No description, defaulted to numeric value
                if ((byte)Op < 0x8E)
                    o = "Anim " + (byte)Op;
                else
                    o = $"Op {(byte)Op:x2}";
            }
            if (Operands == null)
                return o;
            else
                return $"{o} ({string.Join(",", Operands)})";
        }
    }

    public enum AnimScriptJumpKind {
        Relative,
        ToFirstByte,
    }

    public class AnimationScriptDecoder {

        private static Dictionary<byte, int[]> _opSizes = new();

        private static void Register(IEnumerable<byte> ops, params int[] operandSizes) {
            foreach (byte op in ops)
                _opSizes[op] = operandSizes;
        }

        static AnimationScriptDecoder() {
            Register(new byte[] { //Single byte operands
                0x91, 0x98, 0x9D, 0xA0, 0xA2, 0xA3, 0xA7, 0xAC, 0xAF,
                0xB6, 0xB9, 0xBC, 0xBE, 0xC2, 0xC6, 0xCC, 0xCE,
                0xD6, 0xDA, 0xE7, 0xF4, 0xF5, 0xF7, 0xF8, 0xFE,
            }, 1);
            Register(new byte[] { //Ops with one word operand
                0xBA,
            }, 2);
            Register(new byte[] { //Ops with two single-byte operands
                0x96, 0x97, 0xA1, 0xA8, 0xA9, 0xBF, 0xD7, 0xDD, 0xDE,
            }, 1, 1);
            Register(new byte[] { //Ops with byte+word operands
                0x90, 0xD8, 0xDC,
            }, 1, 2);
            Register(new byte[] { //Ops with word+byte operands
                0xC4, 0xC7, 0xD0, 0xD4, 0xE9,
            }, 2, 1);
            Register(new byte[] { //Ops with word+word operands
                0x9A, 0xAB, 0xBD, 0xFB,
            }, 2, 2);
            Register(new byte[] { //Ops with word+word+byte operands
                0x94, 0xC8, 0xD1, 0xDB,
            }, 2, 2, 1);
            //Now a bunch of uniques
            Register(new byte[] { 0xFD }, 2, 2, 2);
            Register(new byte[] { 0xCF, 0xD5 }, 2, 2, 2, 1, 1);
            Register(new byte[] { 0x99 }, 1, 2, 2, 1);
            Register(new byte[] { 0xAD }, 1, 2, 1, 1);
            Register(new byte[] { 0xB5 }, 2, 2, 2, 1, 2, 2);
            Register(new byte[] { 0xCB }, 1, 2, 1, 1, 1, 1, 1);
        }

        private byte[] _data;
        private int _ip;

        public AnimationScriptDecoder(byte[] data) {
            _data = data;
        }

        public DecodedAnimScriptOp? DecodeNext() {
            if (_ip >= _data.Length) return null;

            byte op = _data[_ip++];
            int[]? operands = null;
            if (_opSizes.TryGetValue(op, out int[] sizes)) {
                operands = sizes
                    .Select(size => {
                        switch (size) {
                            case 1:
                                return (int)_data[_ip++];
                            case 2:
                                ushort value = BitConverter.ToUInt16(_data, _ip);
                                _ip += 2;
                                return value;
                            default:
                                throw new NotImplementedException();
                        }
                    })
                    .ToArray();
            }
            return new DecodedAnimScriptOp {
                Op = (AnimScriptOp)op,
                Operands = operands,
            };
        }

        public void Jump(AnimScriptJumpKind jumpKind, int param) {
            switch (jumpKind) {
                case AnimScriptJumpKind.Relative:
                    _ip += param;
                    break;
                case AnimScriptJumpKind.ToFirstByte:
                    _ip = Array.FindIndex(_data, b => b == param);
                    break;
            }
        }
    }
}
