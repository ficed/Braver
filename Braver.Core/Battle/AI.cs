// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Braver.Battle {

    public enum ValueKind {
        Value,
        Address,
        List,
    }
    public enum ValueSize {
        Bit,
        Byte,
        Word,
        Triple,
    }
    public struct StackValue : IEquatable<StackValue> {
        public ValueKind Kind { get; set; }
        public ValueSize Size { get; set; }
        public int[] Data { get; set; }

        public bool IsZero => Data.All(i => i == 0);

        public bool Equals(StackValue other) {
            return (Data.Length == other.Data.Length) &&
                Data.Zip(other.Data).All(t => t.First == t.Second);
        }

        public override string ToString() => $"{Kind} ({Size}) {string.Join(",", Data)}";
    }

    public abstract class AICallbacks {
        protected VMM _vmm;

        public ushort ReadVMMBank1(int offset) {
            return (ushort)_vmm.Read(1, offset);
        }
        public void WriteVMMBank1(int offset, ushort value) {
            _vmm.Write(1, offset, value);
        }

    }

    public class CombatantMemory {

        private Engine _engine;
        private ICombatant _combatant;
        private byte[] _region0 = new byte[128], _region2 = new byte[60];


        public CombatantMemory(Engine engine, ICombatant combatant) {
            _engine = engine;
            _combatant = combatant;
        }

        private ushort ActorMask(Func<ICombatant, bool> getBit) {
            ushort value = 0;
            foreach (int i in Enumerable.Range(0, _engine.Combatants.Count))
                if (_engine.Combatants[i] != null)
                    if (getBit(_engine.Combatants[i]))
                        value |= (ushort)(1 << i);
            return value;
        }

        private void SetActorMask(int offset, Func<ICombatant, bool> getBit) {
            Write2(offset, new StackValue { Size = ValueSize.Word, Kind = ValueKind.Value, Data = new int[] { ActorMask(getBit) } });
        }

        public void ResetRegion2(int partyGil) {
            SetActorMask(0x050, _ => true); //Should check alive...? Active vs inactive?
            SetActorMask(0x060, c => c == _combatant);
            //070 - set by script
            SetActorMask(0x080, c => c.IsPlayer == _combatant.IsPlayer); //even if manipulated?
            SetActorMask(0x090, c => (c.IsPlayer == _combatant.IsPlayer) && c.IsAlive()); //even if manipulated?
            SetActorMask(0x0A0, c => c.IsPlayer != _combatant.IsPlayer); //even if manipulated?
            SetActorMask(0x0B0, c => (c.IsPlayer != _combatant.IsPlayer) && c.IsAlive()); //even if manipulated?
            SetActorMask(0x0C0, c => c.IsPlayer);
            SetActorMask(0x0E0, _ => true); //Should check alive...? Active vs inactive? Different to 0x050

            Write2(0x1C0, new StackValue { 
                Size = ValueSize.Word, Kind = ValueKind.Value, Data = new int[] { partyGil & 0xffff } 
            });
            Write2(0x1D0, new StackValue { 
                Size = ValueSize.Word, Kind = ValueKind.Value, Data = new int[] { partyGil >> 16 } 
            });

        }
        /*
0x2000 	Command Index of last action performed
0x2008 	Action Index of last action performed
0x2010 	Memory 1/2 Bank access value
0x2018 	DUMMY. Used in one script in a test enemy.
0x2020 	Battle Formation (side, pincer, pre-emptive, etc.)
0x2038 	Limit Level. Only used by Vincent during transformation.
0x2050 	Active Actor List. A bit mask of all active (scripts enabled) actors.
0x2060 	Single bit indicating which actor owns the script that is currently executing. Changes as scripts are triggered.
0x2070 	Bit mask of actors indicating targets of the current action. Should be set prior to any action.
0x2080 	Bit mask of actors indicating actors the current actor considers as allies. Changes as scripts are triggered.
0x2090 	Bit mask of active actors indicating actors the current actor considers as allies. Changes as scripts are triggered.
0x20A0 	Bit mask of actors indicating actors the current actor considers as enemies. Changes as scripts are triggered.
0x20B0 	Bit mask of active actors indicating actors the current actor considers as enemies. Changes as scripts are triggered.
0x20C0 	Bit mask of active player's characters
0x20E0 	Bit mask of all active and inactive actors present in the battle.
0x2110 	Set of flags indicating battle rewards (some functions unknown; most won't be set until end of battle).
	0x2111 	End battle; Marked as Escaped
0x2112 	End battle; Pose for Victory (if 0x2116 is unset)
0x2113 	End battle; No Reward
0x2114 	End battle (unsets 0x2113 unless escaped, unsets 0x2111 in that case)
0x2115 	(unsets 0x2112)
0x2116 	No Victory Pose (unsets 0x2115)
0x2120 	Elements of last performed action.
0x2140 	Formation Index of the current battle.
0x2150 	Index of last performed action.
0x2160 	Some sort of flags (unknown effect).
	0x2160 	
0x2161 	Don't apply poison/regen?
0x2162 	Other battles in sequence
0x2163 	Empty all players' Limit Bars (and other things)
0x2164 	Players can learn limits (never unset?)
0x2165 	No reward screen?
0x2170 	Special Attack Flags.
0x2180 	Unknown (divisor of some sort related to limits)
0x21A0 	During Emerald Weapon battle, keeps track of how many eyes are active. (Possible use in other battles, too)
0x21C0 	Party's Gil 
        */

        public ushort Read0(int offset) {
            return BitConverter.ToUInt16(_region0, offset >> 3);
        }
        public ushort Read2(int offset) {
            return BitConverter.ToUInt16(_region2, offset >> 3);
        }
        public ushort[] Read4(int offset) {
            Func<ICombatant, int, ushort> getValue;
            switch(offset & 0xff0) {
                case 0x000:
                    getValue = (c, index) => (ushort)((int)c.Statuses & 0xffff); break;
                case 0x010:
                    getValue = (c, index) => (ushort)((int)c.Statuses >> 16); break;

                case 0x020:
                    getValue = (c, index) => {
                        ushort v = 0;
                        if (c.IsPlayer == _combatant.IsPlayer) v |= 0x2;
                        if (c.IsDefending) v |= 0x20;
                        if (c.IsBackRow) v |= 0x40;
                        //TODO - 4027 Attack connects
                        //TODO - 4028  Immune to physical damage
                        //TODO - 4029  Immune to magical damage
                        //TODO - 402B  Was covered / Defers damage
                        //TODO - 402C  Immune to Death
                        //TODO - 402D  Actor is dead
                        //TODO - 402E  Actor is invisible
                        if (!c.IsAlive()) v |= 0x2000;
                        return v;
                    };
                    break;

                case 0x040:
                    getValue = (c, index) => (ushort)((c.Level << 8) | index); break;

                case 0x060:
                    getValue = (c, index) => {
                        if (c is EnemyCombatant enemy)
                            return (ushort)enemy.InstanceID;
                        else if (c is CharacterCombatant chr)
                            return (ushort)(chr.Character.CharIndex + 0x10);
                        else
                            throw new NotImplementedException();
                    };
                    break;

                case 0x080:
                    getValue = (c, index) => (ushort)((c.HurtBattleAnimation << 8) | c.IdleBattleAnimation); break;

                case 0x0D0:
                    getValue = (c, index) => (ushort)(c.LastAttacker != null ? 1 << _engine.Combatants.IndexOf(c.LastAttacker) : 0); break;
                case 0x0E0:
                    getValue = (c, index) => (ushort)(c.LastPhysicalAttacker != null ? 1 << _engine.Combatants.IndexOf(c.LastPhysicalAttacker) : 0); break;
                case 0x0F0:
                    getValue = (c, index) => (ushort)(c.LastMagicAttacker != null ? 1 << _engine.Combatants.IndexOf(c.LastMagicAttacker) : 0); break;

                case 0x100:
                    getValue = (c, index) => (ushort)c.ModifiedStats().Def; break;
                case 0x110:
                    getValue = (c, index) => (ushort)c.ModifiedStats().MDf; break;

                case 0x140:
                    getValue = (c, index) => (ushort)((c.MaxMP << 8) | c.MP); break;
                case 0x160:
                    getValue = (c, index) => (ushort)(c.HP & 0xffff); break;
                case 0x170:
                    getValue = (c, index) => (ushort)(c.HP >> 16); break;
                case 0x180:
                    getValue = (c, index) => (ushort)(c.MaxHP & 0xffff); break;
                case 0x190:
                    getValue = (c, index) => (ushort)(c.MaxHP >> 16); break;

                default:
                    throw new NotImplementedException();
            }
            var results = _engine.Combatants
                .Select((c, index) => c == null ? (ushort)0 : getValue(c, index))
                .ToArray();

            Console.WriteLine($"Reading bank 4 addr {offset:x4} gave {string.Join(",", results)}");

            return results;
        }

        /*
0x4000 	Bitmask of current Statuses
0x4020 	Set of flags relating to situation.
	0x4020 	
    0x4021 	Ally of current actor
    0x4022 	
    0x4023 	
    0x4024 	
    0x4025 	Defending
    0x4026 	Back row
    0x4027 	Attack connects
    0x4028 	Immune to physical damage
    0x4029 	Immune to magical damage
    0x402A 	
    0x402B 	Was covered / Defers damage
    0x402C 	Immune to Death
    0x402D 	Actor is dead
    0x402E 	Actor is invisible
0x4058 	Greatest Elemental Damage modifier (No damage, half, normal, etc.)
0x4060 	Character ID (+10h) for playable Characters, Instance for enemies
0x4068 	Physical Attack Power
0x4070 	Magic Attack Power
0x4078 	Physical Evade
0x4080 	Idle Animation ID
0x4088 	Damaged Animation ID
0x4090 	Back Damage Multiplier
0x4098 	Model Size (default is 16)
0x40A0 	Dexterity
0x40A8 	Luck
0x40B0 	Related to Idle Animations
0x40B8 	Character that was just covered. (Character index +10h)
0x40C0 	Target(s) of last action performed by actor
0x4100 	Physical Defense Rating
0x4110 	Magical Defense Rating
0x4120 	Index of actor
0x4130 	Absorbed Elements

0x41A0 	Unknown (Used by Schizo's heads to tell the other head that it is dead. Maybe elsewhere?)
0x4220 	Initial Statuses
0x4268 	Magic Evade
0x4270 	Row
0x4278 	Unknown (something to do with the camera?)
0x4280 	Gil stolen (Enemies only)
0x4290 	Item stolen (Enemies only)
0x42A0 	Nullified Elements?
0x42B0 	AP actor is worth
0x42C0 	Gil actor is worth
0x42E0 	EXP actor is worth          */

        private static void DoWrite(byte[] data, int offset, StackValue value) {
            switch (value.Size) {
                case ValueSize.Byte:
                    data[offset >> 3] = (byte)value.Data[0];
                    break;
                case ValueSize.Triple:
                    data[offset >> 3] = (byte)(value.Data[0] & 0xff);
                    data[(offset >> 3) + 1] = (byte)((value.Data[0] >> 8) & 0xff);
                    data[(offset >> 3) + 2] = (byte)((value.Data[0] >> 16) & 0xff);
                    break;
                case ValueSize.Word:
                    data[offset >> 3] = (byte)(value.Data[0] & 0xff);
                    data[(offset >> 3) + 1] = (byte)((value.Data[0] >> 8) & 0xff);
                    break;
                case ValueSize.Bit:
                    byte current = (byte)(data[offset >> 3] & ~(1 << (offset & 7)));
                    if (!value.IsZero)
                        current |= (byte)(1 << (offset & 7));
                    data[offset >> 3] = current;
                    break;
            }
        }

        public void Write0(int offset, StackValue value) {
            DoWrite(_region0, offset, value);
        }

        public void Write2(int offset, StackValue value) {
            DoWrite(_region2, offset, value);
        }

        public int Read0Triple(int offset) {
            return Read0(offset) | ((Read0(offset + 0x10) & 0xff) << 16);
        }
        public int Read2Triple(int offset) {
            return Read2(offset) | ((Read2(offset + 0x10) & 0xff) << 16);
        }
        public int[] Read4Triple(int offset) {
            ushort[] lower = Read4(offset), upper = Read4(offset + 0x10);
            return Enumerable.Range(0, lower.Length)
                .Select(i => lower[i] | ((upper[i] & 0xff) << 8))
                .ToArray();
        }
    }

    public enum AIScriptResult {
        Continue,
        End,
        Pause,
    }

    public enum AIScriptFunction {
        PreBattle = 0,
        Main = 1,
        GeneralCounter = 2,
        DeathCounter = 3,
        PhysicalCounter = 4,
        MagicCounter = 5,
        BattleVictory = 6,
        PreActionSetup = 7,
        CustomEvent0 = 8,
        CustomEvent1 = 9,
        CustomEvent2 = 10,
        CustomEvent3 = 11,
        CustomEvent4 = 12,
        CustomEvent5 = 13,
        CustomEvent6 = 14,
        CustomEvent7 = 15,
    }

    public class AI {

        private Stream[] _functions;
        private Stack<StackValue> _stack = new();
        private CombatantMemory _memory;
        private AICallbacks _callbacks;
        private Random _random = new();
        private List<byte[]> _queuedText = new();

        public ushort? ActionID { get; private set; }
        public ushort? ActionType { get; private set; }
        public IEnumerable<byte[]> QueuedText => _queuedText.AsReadOnly();

        public CombatantMemory Memory => _memory;

        public AI(byte[] aiWithTable, CombatantMemory memory, AICallbacks callbacks) {
            var offsets = Enumerable.Range(0, 16)
                .Select(i => BitConverter.ToUInt16(aiWithTable, i * 2))
                .ToArray();

            _functions = new Stream[offsets.Length];

            foreach(int i in Enumerable.Range(0, offsets.Length)) {
                if (offsets[i] != 0xffff) {
                    int next = offsets.Skip(i + 1)
                        .FirstOrDefault(os => os != 0xffff, (ushort)aiWithTable.Length);
                    _functions[i] = new MemoryStream(
                        aiWithTable
                        .Skip(offsets[i])
                        .Take(next - offsets[i])
                        .ToArray()
                    );
                }
            }

            _memory = memory;
            _callbacks = callbacks;
        }

        private StackValue MemRead(ushort addr, ValueSize size) {
            int[] value;
            switch (addr >> 12) {
                case 0:
                    if (size == ValueSize.Triple)
                        value = new int[] { _memory.Read0Triple(addr & 0xfff) };
                    else
                        value = new int[] { _memory.Read0(addr & 0xfff) };
                    break;
                case 2:
                    if (size == ValueSize.Triple)
                        value = new int[] { _memory.Read2Triple(addr & 0xfff) };
                    else
                        value = new int[] { _memory.Read2(addr & 0xfff) };
                    break;
                case 4:
                    if (size == ValueSize.Triple)
                        value = _memory.Read4Triple(addr & 0xfff);
                    else
                        value = _memory.Read4(addr & 0xfff).Select(u => (int)u).ToArray();
                    break;
                default:
                    throw new NotImplementedException();
            }

            switch (size) {
                case ValueSize.Byte:
                    value = value.Select(i => i & 0xff).ToArray();
                    break;
                case ValueSize.Bit:
                    value = value.Select(i => (i >> (addr & 0xf)) & 0x1).ToArray();
                    break;
            }

            return new StackValue {
                Data = value,
                Size = size,
                Kind = value.Length > 1 ? ValueKind.List : ValueKind.Value,
            };
        }

        private AIScriptResult Dispatch0x(byte opcode, Stream data) {
            var size = (ValueSize)(opcode & 0xf);
            ushort address = data.ReadU16();
            _stack.Push(MemRead(address, size));
            return AIScriptResult.Continue;
        }

        private AIScriptResult Dispatch1x(byte opcode, Stream data) {
            var size = (ValueSize)(opcode & 0xf);
            ushort address = data.ReadU16();
            _stack.Push(new StackValue {
                Kind = ValueKind.Address,
                Size = size,
                Data = new int[] { address }
            });
            return AIScriptResult.Continue;
        }

        private void PerformBinaryOp(Func<int, int, int> op, ValueSize? forceOutputSize = null) {
            var v1 = _stack.Pop();
            var v0 = _stack.Pop();
            if (v0.Kind == ValueKind.List) {
                _stack.Push(new StackValue {
                    Kind = ValueKind.List,
                    Size = forceOutputSize ?? v0.Size,
                    Data = Enumerable.Range(0, v0.Data.Length)
                        .Select(i => op(v0.Data[i], v1.Data[i]))
                        .ToArray()
                });
            } else {
                _stack.Push(new StackValue {
                    Kind = ValueKind.List,
                    Size = forceOutputSize ?? v0.Size,
                    Data = new[] { op(v0.Data[0], v1.Data[0]) }
                });
            }
        }
        private void PerformUnaryOp(Func<int, int> op, ValueSize? forceOutputSize = null) {
            var v0 = _stack.Pop();
            if (v0.Kind == ValueKind.List) {
                _stack.Push(new StackValue {
                    Kind = ValueKind.List,
                    Size = forceOutputSize ?? v0.Size,
                    Data = Enumerable.Range(0, v0.Data.Length)
                        .Select(i => op(v0.Data[i]))
                        .ToArray()
                });
            } else {
                _stack.Push(new StackValue {
                    Kind = ValueKind.List,
                    Size = forceOutputSize ?? v0.Size,
                    Data = new[] { op(v0.Data[0]) }
                });
            }
        }

        private AIScriptResult Dispatch3x(byte opcode) {
            switch (opcode) {
                case 0x30:
                    PerformBinaryOp((i1, i2) => i1 + i2);
                    break;
                case 0x31:
                    PerformBinaryOp((i1, i2) => i1 - i2);
                    break;
                case 0x32:
                    PerformBinaryOp((i1, i2) => i1 * i2);
                    break;
                case 0x33:
                    PerformBinaryOp((i1, i2) => i1 / i2);
                    break;
                case 0x34:
                    PerformBinaryOp((i1, i2) => i1 % i2);
                    break;
                case 0x35:
                    PerformBinaryOp((i1, i2) => i1 & i2);
                    break;
                case 0x36:
                    PerformBinaryOp((i1, i2) => i1 | i2);
                    break;
                case 0x37:
                    PerformUnaryOp(i => ~i);
                    break;
            }
            return AIScriptResult.Continue;
        }


        private void PerformComparison(Func<int, int, bool> op) {
            var v1 = _stack.Pop();
            var v0 = _stack.Pop();
            if (v0.Kind == ValueKind.List) {
                _stack.Push(new StackValue {
                    Kind = ValueKind.List,
                    Size = ValueSize.Bit,
                    Data = new[] { v0.Data.All(i => op(i, v1.Data[0])) ? 1 : 0 }
                });
            } else {
                _stack.Push(new StackValue {
                    Kind = ValueKind.List,
                    Size = ValueSize.Bit,
                    Data = new[] { op(v0.Data[0], v1.Data[0]) ? 1 : 0 }
                });
            }
        }

        private AIScriptResult Dispatch4x(byte opcode) {
            switch (opcode) {
                case 0x40:
                    PerformComparison((i1, i2) => i1 == i2);
                    break;
                case 0x41:
                    PerformComparison((i1, i2) => i1 != i2);
                    break;
                case 0x42:
                    PerformComparison((i1, i2) => i1 >= i2);
                    break;
                case 0x43:
                    PerformComparison((i1, i2) => i1 <= i2);
                    break;
                case 0x44:
                    PerformComparison((i1, i2) => i1 > i2);
                    break;
                case 0x45:
                    PerformComparison((i1, i2) => i1 < i2);
                    break;
            }
            return AIScriptResult.Continue;
        }
        private AIScriptResult Dispatch5x(byte opcode) {
            switch (opcode) {
                case 0x50:
                    PerformBinaryOp((i1, i2) => (i1 & i2) != 0 ? 1 : 0, ValueSize.Bit);
                    break;
                case 0x51:
                    PerformBinaryOp((i1, i2) => (i1 | i2) != 0 ? 1 : 0, ValueSize.Bit);
                    break;
                case 0x52:
                    PerformUnaryOp(i1 => i1 == 0 ? 1 : 0, ValueSize.Bit);
                    break;
            }
            return AIScriptResult.Continue;
        }
        private AIScriptResult Dispatch6x(byte opcode, Stream data) {
            switch (opcode) {
                case 0x60:
                    byte arg = data.ReadU8();
                    _stack.Push(new StackValue {
                        Kind = ValueKind.Value,
                        Size = ValueSize.Byte,
                        Data = new int[] { arg }
                    });
                    break;
                case 0x61:
                    ushort arg2 = data.ReadU16();
                    _stack.Push(new StackValue {
                        Kind = ValueKind.Value,
                        Size = ValueSize.Word,
                        Data = new int[] { arg2 }
                    });
                    break;
                case 0x62:
                    int triple = data.ReadI32() & 0xffffff;
                    data.Seek(-1, SeekOrigin.Current);
                    _stack.Push(new StackValue {
                        Kind = ValueKind.Value,
                        Size = ValueSize.Triple,
                        Data = new int[] { triple }
                    });
                    break;
            }
            return AIScriptResult.Continue;
        }
        private AIScriptResult Dispatch7x(byte opcode, Stream data) {
            switch (opcode) {
                case 0x70:
                    ushort target = data.ReadU16();
                    if (_stack.Pop().IsZero)
                        data.Position = target;
                    break;
                case 0x71:
                    target = data.ReadU16();
                    if (!_stack.Pop().Equals(_stack.Peek()))
                        data.Position = target;
                    break;
                case 0x72:
                    target = data.ReadU16();
                    data.Position = target;
                    break;
                case 0x73:
                    return AIScriptResult.End;
                case 0x75:
                    //75h 		One of any type 	Link all scripts of script owner to popped Character ID.              */
                    throw new NotImplementedException();
            }
            return AIScriptResult.Continue;
        }
        private AIScriptResult Dispatch8x(byte opcode) {
            switch (opcode) {
                case 0x80:
                    var value = _stack.Pop();
                    var mask = _stack.Pop();
                    StackValue newValue;
                    if (value.Kind == ValueKind.List) {

                        newValue = new StackValue {
                            Kind = ValueKind.List,
                            Size = value.Size,
                            Data = Utils.IndicesOfSetBits(mask.Data[0])
                                .Select(i => value.Data[i])
                                .ToArray()
                        };
                    } else {
                        newValue = new StackValue {
                            Kind = value.Kind,
                            Size = value.Size,
                            Data = new[] { value.Data[0] & mask.Data[0] }
                        };
                    }
                    Console.WriteLine($"Masking {value} with {mask} gave {newValue}");
                    _stack.Push(newValue);
                    break;
                case 0x81:
                    _stack.Push(new StackValue {
                        Kind = ValueKind.Value,
                        Size = ValueSize.Word,
                        Data = new int[] { _random.Next(0x10000) },
                    });
                    break;
                case 0x82:
                    value = _stack.Pop();

                    var setBits = Enumerable.Range(0, 16)
                        .Where(i => (value.Data[0] & (1 << i)) != 0)
                        .ToArray();
                    value = new StackValue {
                        Kind = ValueKind.Value,
                        Size = ValueSize.Word,
                        Data = new[] { 1 << setBits[_random.Next(setBits.Length)] },
                    };
                    _stack.Push(value);
                    break;
                case 0x83:
                    value = _stack.Pop();
                    switch (value.Size) {
                        case ValueSize.Byte:
                            _stack.Push(new StackValue {
                                Kind = ValueKind.Value,
                                Size = ValueSize.Byte,
                                Data = new[] { BitOperations.PopCount((uint)value.Data[0]) },
                            });
                            break;
                        default:
                            throw new NotImplementedException(); //Want to check how this is used
                    }
                    break;
                case 0x84:
                    value = _stack.Pop();
                    switch (value.Kind) {
                        case ValueKind.List:
                            int largest = value.Data.Max();
                            int lmask = 0;
                            foreach (int i in Enumerable.Range(0, value.Data.Length))
                                if (value.Data[i] == largest)
                                    lmask |= 1 << i;
                            _stack.Push(new StackValue {
                                Kind = ValueKind.Value,
                                Size = ValueSize.Word,
                                Data = new[] { lmask },
                            });
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    break;
                case 0x85:
                    value = _stack.Pop();
                    switch (value.Kind) {
                        case ValueKind.List:
                            int smallest = value.Data.Min();
                            int lmask = 0;
                            foreach (int i in Enumerable.Range(0, value.Data.Length))
                                if (value.Data[i] == smallest)
                                    lmask |= 1 << i;
                            _stack.Push(new StackValue {
                                Kind = ValueKind.Value,
                                Size = ValueSize.Word,
                                Data = new[] { lmask },
                            });
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    break;
                case 0x86:
                    throw new NotImplementedException(); //Get MP cost
                case 0x87:
                    value = _stack.Pop();
                    _stack.Push(new StackValue {
                        Kind = ValueKind.Value,
                        Size = ValueSize.Word,
                        Data = new[] { 1 << value.Data[0] },
                    });
                    break;
            }
            return AIScriptResult.Continue;
        }
        /*
         *90h 		One of type 1X, One of type 0X or 2X 	If first pop < 4000h; Stores second pop at first pop
90h 		One of type 1X, One of type 0X or 2X, One of type 1X 	If first pop >= 4000h; Stores second pop at first pop constrained by mask at third pop
        */
        private AIScriptResult Dispatch9x(byte opcode, Stream data) {
            switch (opcode) {
                case 0x90:
                    var value = _stack.Pop();
                    ushort addr = (ushort)_stack.Pop().Data[0];
                    if (addr < 0x4000) {
                        //
                    } else {
                        var mask = _stack.Pop();
                        throw new NotImplementedException(); //Verify!
                    }
                    switch (addr & 0x3000) {
                        case 0x0000:
                            _memory.Write0(addr & 0xfff, value);
                            break;
                        case 0x2000:
                            _memory.Write2(addr & 0xfff, value);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    break;
                case 0x91:
                    _stack.Pop();
                    break;
                case 0x92:
                    ActionID = (ushort)_stack.Pop().Data[0];
                    ActionType = (ushort)_stack.Pop().Data[0];
                    break;
                case 0x93:
                    List<byte> text = new();
                    byte b;
                    while ((b = data.ReadU8()) != 255)
                        text.Add(b);
                    _queuedText.Add(text.ToArray());
                    break;
                case 0x95:
                    var vmAddr = _stack.Pop();
                    System.Diagnostics.Debug.Assert(vmAddr.Kind == ValueKind.Address);
                    var direction = _stack.Pop();
                    if (direction.IsZero) { //read from VMM
                        ushort vvalue = _callbacks.ReadVMMBank1(vmAddr.Data[0]);
                        _memory.Write2(0x010, new StackValue { Size = ValueSize.Word, Kind = ValueKind.Value, Data = new int[] { vvalue } });
                    } else { //write to VMM
                        ushort vvalue = _memory.Read2(0x010);
                        _callbacks.WriteVMMBank1(vmAddr.Data[0], vvalue);
                    }
                    break;
                case 0x96:
                    //96h 		Two of any type 	Get fighter elemental defense.
                    throw new NotImplementedException(); //elemental defense
            }

            return AIScriptResult.Continue;
        }
        private AIScriptResult DispatchAx(byte opcode, Stream data) {
            switch (opcode) {
                case 0xA0:
                    List<byte> text = new();
                    byte b;
                    while ((b = data.ReadU8()) != 0)
                        text.Add(b);
                    string s = Encoding.ASCII.GetString(text.ToArray()); //VERY TODO
                    s = System.Text.RegularExpressions.Regex.Replace(
                        s,
                        "%d",
                        _ => string.Join(",", _stack.Pop().Data)
                    );
                    System.Diagnostics.Trace.WriteLine(s);
                    break;
                case 0xA1:
                    _stack.Pop();
                    _stack.Pop();
                    break;
            }
            return AIScriptResult.Continue;
        }

        public AIScriptResult Run(AIScriptFunction function) {
            ActionID = ActionType = null;
            _queuedText.Clear();
            var data = _functions[(int)function];
            if (data == null)
                return AIScriptResult.End;
            data.Position = 0;

            AIScriptResult result;
            do {
                byte opcode = data.ReadU8();
                switch (opcode & 0xf0) {
                    case 0x00:
                        result = Dispatch0x(opcode, data); break;
                    case 0x10:
                        result = Dispatch1x(opcode, data); break;
                    case 0x30:
                        result = Dispatch3x(opcode); break;
                    case 0x40:
                        result = Dispatch4x(opcode); break;
                    case 0x50:
                        result = Dispatch5x(opcode); break;
                    case 0x60:
                        result = Dispatch6x(opcode, data); break;
                    case 0x70:
                        result = Dispatch7x(opcode, data); break;
                    case 0x80:
                        result = Dispatch8x(opcode); break;
                    case 0x90:
                        result = Dispatch9x(opcode, data); break;
                    case 0xA0:
                        result = DispatchAx(opcode, data); break;
                    default:
                        throw new NotImplementedException();
                }
            } while (result == AIScriptResult.Continue); 
            return result;
        }
    }
}
