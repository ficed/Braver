using SharpDX.MediaFoundation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Braver.Field {
    public enum OpCode {
        RET,
        REQ,
        REQSW,
        REQEW,
        PREQ,
        PRQSW,
        PRQEW,
        RETTO,
        JOIN,
        SPLIT,
        SPTYE,
        GTPYE,
        __unused0,
        __unused1,
        DSKCG,
        SPECIAL,
        JMPF,
        JMPFL,
        JMPB,
        JMPBL,
        IFUB,
        IFUBL,
        IFSW,
        IFSWL,
        IFUW,
        IFUWL,
        __unused2,
        __unused3,
        __unused4,
        __unused5,
        __unused6,
        __unused7,
        MINIGAME,
        TUTOR,
        BTMD2,
        BTRLD,
        WAIT,
        NFADE,
        BLINK,
        BGMOVIE,
        KAWAI,
        KAWIW,
        PMOVA,
        SLIP,
        BGPDH,
        BGSCR,
        WCLS,
        WSIZW,
        IFKEY,
        IFKEYON,
        IFKEYOFF,
        UC,
        PDIRA,
        PTURA,
        WSPCL,
        WNUMB,
        STTIM,
        GOLDu,
        GOLDd,
        CHGLD,
        HMPMAX1,
        HMPMAX2,
        MHMMX,
        HMPMAX3,
        MESSAGE,
        MPARA,
        MPRA2,
        MPNAM,
        __unused8,
        MPu,
        __unused9,
        MPd,
        ASK,
        MENU,
        MENU2,
        BTLTB,
        __unused10,
        HPu,
        __unused11,
        HPd,
        WINDOW,
        WMOVE,
        WMODE,
        WREST,
        WCLSE,
        WROW,
        GWCOL,
        SWCOL,
        STITM,
        DLITM,
        CKITM,
        SMTRA,
        DMTRA,
        CMTRA,
        SHAKE,
        NOP,
        MAPJUMP,
        SCRLO,
        SCRLC,
        SCRLA,
        SCR2D,
        SCRCC,
        SCR2DC,
        SCRLW,
        SCR2DL,
        MPDSP,
        VWOFT,
        FADE,
        FADEW,
        IDLCK,
        LSTMP,
        SCRLP,
        BATTLE,
        BTLON,
        BTLMD,
        PGTDR,
        GETPC,
        PXYZI,
        PLUS_,
        PLUS2_,
        MINUS_,
        MINUS2_,
        INC_,
        INC2_,
        DEC_,
        DEC2_,
        TLKON,
        RDMSD,
        SETBYTE,
        SETWORD,
        BITON,
        BITOFF,
        BITXOR,
        PLUS,
        PLUS2,
        MINUS,
        MINUS2,
        MUL,
        MUL2,
        DIV,
        DIV2,
        MOD,
        MOD2,
        AND,
        AND2,
        OR,
        OR2,
        XOR,
        XOR2,
        INC,
        INC2,
        DEC,
        DEC2,
        RANDOM,
        LBYTE,
        HBYTE,
        _2BYTE,
        SETX,
        GETX,
        SEARCHX,
        PC,
        CHAR,
        DFANM,
        ANIME1,
        VISI,
        XYZI,
        XYI,
        XYZ,
        MOVE,
        CMOVE,
        MOVA,
        TURA,
        ANIMW,
        FMOVE,
        ANIME2,
        ANIM_1,
        CANIM1,
        CANM_1,
        MSPED,
        DIR,
        TURNGEN,
        TURN,
        DIRA,
        GETDIR,
        GETAXY,
        GETAI,
        ANIM_2,
        CANIM2,
        CANM_2,
        ASPED,
        __unused12,
        CC,
        JUMP,
        AXYZI,
        LADER,
        OFST,
        OFSTW,
        TALKR,
        SLIDR,
        SOLID,
        PRTYP,
        PRTYM,
        PRTYE,
        IFPRTYQ,
        IFMEMBQ,
        MMBud,
        MMBLK,
        MMBUK,
        LINE,
        LINON,
        MPJPO,
        SLINE,
        SIN,
        COS,
        TLKR2,
        SLDR2,
        PMJMP,
        PMJMP2,
        AKAO2,
        FCFIX,
        CCANM,
        ANIMB,
        TURNW,
        MPPAL,
        BGON,
        BGOFF,
        BGROL,
        BGROL2,
        BGCLR,
        STPAL,
        LDPAL,
        CPPAL,
        RTPAL,
        ADPAL,
        MPPAL2,
        STPLS,
        LDPLS,
        CPPAL2,
        RTPAL2,
        ADPAL2,
        MUSIC,
        SOUND,
        AKAO,
        MUSVT,
        MUSVM,
        MULCK,
        BMUSC,
        CHMPH,
        PMVIE,
        MOVIE,
        MVIEF,
        MVCAM,
        FMUSC,
        CMUSC,
        CHMST,
        GAMEOVER,
    }

    public enum OpResult {
        Continue,
        Restart,
    }

    public delegate OpResult OpExecute(Fiber f, Entity e, FieldScreen s);

    public class Fiber {
        private byte[] _script;
        private FieldScreen _screen;
        private Entity _entity;
        private int _ip;
        private bool _inInit;
        public bool Active { get; private set; }
        public bool InProgress => _ip >= 0;
        public Action OnStop { get; set; }
        public int IP => _ip;

        public int OpcodeAttempts { get; private set; }

        public object ResumeState { get; set; }

        public Dictionary<string, object> OtherState { get; } = new();

        public byte ReadU8() {
            return _script[_ip++];
        }
        public ushort ReadU16() {
            ushort us = _script[_ip++];
            us |= (ushort)(_script[_ip++] << 8);
            return us;
        }
        public short ReadS16() {
            short s = _script[_ip++];
            s |= (short)(_script[_ip++] << 8);
            return s;
        }

        public Fiber(Entity e, FieldScreen s, byte[] scriptBytecode) {
            _entity = e;
            Active = false;
            _screen = s;
            _ip = -1;
            _script = scriptBytecode;
        }

        public void Start(int ip) {
            _ip = ip;
            Active = true;
            OpcodeAttempts = 0;
            ResumeState = null;
        }

        public void Pause() {
            Active = false;
        }
        public void Resume() {
            Active = true;
        }

        public void Stop() {
            Active = false;
            if (!_inInit) _ip = -1;
            OnStop?.Invoke();
        }

        public void Jump(int ip) {
            _ip = ip;
        }

        public void Run(int maxOps, bool isInit = false) {
            _inInit = isInit;
            while (Active && (maxOps-- > 0)) {
                int opIP = _ip;
                OpCode op = (OpCode)ReadU8();
                //if (_entity.Name == "FURYOA")
                //    System.Diagnostics.Debug.WriteLine($"Entity {_entity.Name} executing {op} ({(byte)op}) at IP {opIP}");
                switch (VM.Execute(op, this, _entity, _screen)) {
                    case OpResult.Continue:
                        OpcodeAttempts = 0;
                        ResumeState = null;
                        break;
                    case OpResult.Restart:
                        OpcodeAttempts++;
                        _ip = opIP;
                        return;
                }
            }
        }

    }

    public static class VM {

        public static bool ErrorOnUnknown { get; set; }

        private static OpExecute[] _executors = new OpExecute[256];

        private static void Register(Type t) {
            foreach (var method in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)) {
                OpCode op;
                if (Enum.TryParse(method.Name, out op))
                    _executors[(int)op] = (OpExecute)method.CreateDelegate(typeof(OpExecute));
            }
        }

        static VM() {
            Register(typeof(Flow));
            Register(typeof(BackgroundPal));
            Register(typeof(CameraAudioVideo));
            Register(typeof(WindowMenu));
            Register(typeof(PartyInventory));
            Register(typeof(FieldModels));
            Register(typeof(Maths));
            Register(typeof(SystemControl));
        }

        public static OpResult Execute(OpCode op, Fiber f, Entity e, FieldScreen s) {
            if (_executors[(int)op] == null) {
                if (ErrorOnUnknown)
                    throw new F7Exception($"Cannot execute opcode {op}");
                f.Stop();
                f.Jump(f.IP - 1); //So if we retry, the opcode is actually retried [and will fail again] rather than trying the next operand byte as an opcode
                System.Diagnostics.Debug.WriteLine($"Aborting script on {e.Name} due to unrecognised opcode {op}");
                return OpResult.Continue;
            } else
                return _executors[(int)op](f, e, s);
        }
    }

    internal static class Flow {

        public static OpResult RET(Fiber f, Entity e, FieldScreen s) {
            f.Stop();
            return OpResult.Continue;
        }

        public static OpResult WAIT(Fiber f, Entity e, FieldScreen s) {
            ushort frames = f.ReadU16();
            if (frames < f.OpcodeAttempts)
                return OpResult.Continue;
            else
                return OpResult.Restart;
        }

        public static OpResult JMPF(Fiber f, Entity e, FieldScreen s) {
            int newIP = f.IP + f.ReadU8();
            f.Jump(newIP);
            return OpResult.Continue;
        }
        public static OpResult JMPFL(Fiber f, Entity e, FieldScreen s) {
            int newIP = f.IP + f.ReadU16();
            f.Jump(newIP);
            return OpResult.Continue;
        }

        public static OpResult JMPB(Fiber f, Entity e, FieldScreen s) {
            int newIP = f.IP - 1 - f.ReadU8();
            f.Jump(newIP);
            return OpResult.Continue;
        }
        public static OpResult JMPBL(Fiber f, Entity e, FieldScreen s) {
            int newIP = f.IP - 1 - f.ReadU16();
            f.Jump(newIP);
            return OpResult.Continue;
        }

        private static OpResult IfImpl(Fiber f, FieldScreen s, byte banks, int iVal1, int iVal2, byte comparison, int newIP) {
            int val1 = s.Game.Memory.Read(banks >> 4, iVal1), val2 = s.Game.Memory.Read(banks & 0xf, iVal2);
            bool match;
            switch (comparison) {
                case 0:
                    match = val1 == val2; break;
                case 1:
                    match = val1 != val2; break;
                case 2:
                    match = val1 > val2; break;
                case 3:
                    match = val1 < val2; break;
                case 4:
                    match = val1 >= val2; break;
                case 5:
                    match = val1 <= val2; break;
                case 6:
                    match = (val1 & val2) != 0; break;
                case 7:
                    match = (val1 ^ val2) != 0; break;
                case 8:
                    match = (val1 | val2) != 0; break;
                case 9:
                    match = (val1 & (1 << val2)) != 0; break;
                case 0xA:
                    match = (val1 & (1 << val2)) == 0; break;
                default:
                    throw new F7Exception($"Unrecognised comparison {comparison}");
            }

            if (!match)
                f.Jump(newIP);
            return OpResult.Continue;
        }

        private static Dictionary<int, InputKey> _maskToKey = new Dictionary<int, InputKey> {
            //TODO - all the other keys!

            [0x0010] = InputKey.Menu,
            [0x0020] = InputKey.OK,
            [0x0040] = InputKey.Cancel,

            [0x0100] = InputKey.Select,
            [0x0800] = InputKey.Start,

            [0x1000] = InputKey.Up,
            [0x2000] = InputKey.Right,
            [0x4000] = InputKey.Down,
            [0x8000] = InputKey.Left,
        };

        private static IEnumerable<InputKey> GetIndicatedKeys(ushort mask) {
            for(int bit = 0; bit < 16; bit++) {
                if (mask == 0) yield break;
                if ((mask & 1) == 1) {
                    //Skip bits 9 and 10 - unknown input keys?
                    if ((bit != 9) && (bit != 10))
                        yield return _maskToKey[1 << bit];
                }
                mask >>= 1;
            }
        }

        public static OpResult IFKEY(Fiber f, Entity e, FieldScreen s) {
            ushort buttons = f.ReadU16();
            int newIP = f.IP + f.ReadU8();
            bool jump = !GetIndicatedKeys(buttons).Any(k => s.LastInput.IsDown(k));
            if (jump)
                f.Jump(newIP);
            return OpResult.Continue;
        }

        public static OpResult IFKEYON(Fiber f, Entity e, FieldScreen s) {
            ushort buttons = f.ReadU16();
            int newIP = f.IP + f.ReadU8();
            bool jump = !GetIndicatedKeys(buttons).Any(k => s.LastInput.IsJustDown(k));
            if (jump)
                f.Jump(newIP);
            return OpResult.Continue;
        }
        public static OpResult IFKEYOFF(Fiber f, Entity e, FieldScreen s) {
            ushort buttons = f.ReadU16();
            int newIP = f.IP + f.ReadU8();
            bool jump = GetIndicatedKeys(buttons).Any(k => s.LastInput.IsDown(k));
            if (jump)
                f.Jump(newIP);
            return OpResult.Continue;
        }

        public static OpResult IFUB(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), bVal1 = f.ReadU8(), bVal2 = f.ReadU8(), comparison = f.ReadU8();
            int newIP = f.IP + f.ReadU8();
            return IfImpl(f, s, banks, bVal1, bVal2, comparison, newIP);
        }
        public static OpResult IFUBL(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), bVal1 = f.ReadU8(), bVal2 = f.ReadU8(), comparison = f.ReadU8();
            int newIP = f.IP + f.ReadU16();
            return IfImpl(f, s, banks, bVal1, bVal2, comparison, newIP);
        }
        public static OpResult IFSW(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8();
            short sVal1 = (short)f.ReadU16(), sVal2 = (short)f.ReadU16();
            byte comparison = f.ReadU8();
            int newIP = f.IP + f.ReadU8();
            return IfImpl(f, s, banks, sVal1, sVal2, comparison, newIP);
        }
        public static OpResult IFSWL(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8();
            short sVal1 = (short)f.ReadU16(), sVal2 = (short)f.ReadU16();
            byte comparison = f.ReadU8();
            int newIP = f.IP + f.ReadU16();
            return IfImpl(f, s, banks, sVal1, sVal2, comparison, newIP);
        }
        public static OpResult IFUW(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8();
            ushort sVal1 = f.ReadU16(), sVal2 = f.ReadU16();
            byte comparison = f.ReadU8();
            int newIP = f.IP + f.ReadU8();
            return IfImpl(f, s, banks, sVal1, sVal2, comparison, newIP);
        }
        public static OpResult IFUWL(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8();
            ushort sVal1 = f.ReadU16(), sVal2 = f.ReadU16();
            byte comparison = f.ReadU8();
            int newIP = f.IP + f.ReadU16();
            return IfImpl(f, s, banks, sVal1, sVal2, comparison, newIP);
        }

        public static OpResult IFPRTYQ(Fiber f, Entity e, FieldScreen s) {
            byte chr = f.ReadU8(), jump = f.ReadU8();

            if (s.Game.SaveData.Party.Any(c => c.CharIndex == chr)) {
                //
            } else {
                f.Jump(f.IP + jump - 1);
            }
            return OpResult.Continue;
        }


        public static OpResult NOP(Fiber f, Entity e, FieldScreen s) {
            return OpResult.Continue;
        }

        public static OpResult REQ(Fiber f, Entity e, FieldScreen s) {
            int entity = f.ReadU8(), parm = f.ReadU8();
            s.Entities[entity].Call(parm >> 5, parm & 0x1f, null);
            return OpResult.Continue;
        }
        public static OpResult REQSW(Fiber f, Entity e, FieldScreen s) {
            int entity = f.ReadU8(), parm = f.ReadU8();
            if (s.Entities[entity].Call(parm >> 5, parm & 0x1f, null))
                return OpResult.Continue;
            else
                return OpResult.Restart;
        }
        public static OpResult REQEW(Fiber f, Entity e, FieldScreen s) {
            int entity = f.ReadU8(), parm = f.ReadU8();
            if (s.Entities[entity].Call(parm >> 5, parm & 0x1f, f.Resume)) {
                f.Pause();
                return OpResult.Continue;
            } else
                return OpResult.Restart;
        }
    }

    internal static class BackgroundPal {
        public static OpResult BGON(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), bArea = f.ReadU8(), bLayer = f.ReadU8();
            int area = s.Game.Memory.Read(banks & 0xf, bArea);
            int layer = s.Game.Memory.Read(banks >> 4, bLayer);
            s.Background.ModifyParameter(area, i => i | (1 << layer));
            return OpResult.Continue;
        }
        public static OpResult BGOFF(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), bArea = f.ReadU8(), bLayer = f.ReadU8();
            int area = s.Game.Memory.Read(banks & 0xf, bArea);
            int layer = s.Game.Memory.Read(banks >> 4, bLayer);
            s.Background.ModifyParameter(area, i => i & ~(1 << layer));
            return OpResult.Continue;
        }
        public static OpResult BGCLR(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8(), bArea = f.ReadU8();
            int area = s.Game.Memory.Read(bank, bArea);
            s.Background.SetParameter(area, 0);
            return OpResult.Continue;
        }
    }

    internal static class FieldModels {


        public static OpResult IDLCK(Fiber f, Entity e, FieldScreen s) {
            ushort triID = f.ReadU16();
            byte enabled = f.ReadU8();

            if (enabled != 0)
                s.DisabledWalkmeshTriangles.Add(triID);
            else
                s.DisabledWalkmeshTriangles.Remove(triID);

            return OpResult.Continue;
        }

        public static OpResult SLIP(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            //TODO!
            return OpResult.Continue;
        }

        public static OpResult CC(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            s.SetPlayer(parm);
            return OpResult.Continue;
        }

        public static OpResult CHAR(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            int modelIndex = s.GetNextModelIndex();
            if (parm != modelIndex)
                System.Diagnostics.Debug.WriteLine($"CHAR opcode - parameter {parm} did not match auto-assign ID {modelIndex}");
            e.Model = s.FieldModels[modelIndex];
            if (s.Player == e)
                s.CheckPendingPlayerSetup();
            s.Game.Net.Send(new Net.FieldEntityModelMessage {
                EntityID = s.Entities.IndexOf(e),
                ModelID = modelIndex,
            });
            return OpResult.Continue;
        }

        public static OpResult PC(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            e.Character = s.Game.SaveData.Characters[parm];
            if ((e.Character?.CharIndex ?? -1) != s.Game.SaveData.FieldAvatarCharID) {
                e.Model.Visible = false;
                e.Flags = EntityFlags.None;
            }
            return OpResult.Continue;
        }

        public static OpResult SPLIT(Fiber f, Entity e, FieldScreen s) {
            byte bankAXY = f.ReadU8(), bankADBX = f.ReadU8(), bankBYD = f.ReadU8();
            short xa = f.ReadS16(), ya = f.ReadS16();
            byte da = f.ReadU8();
            short xb = f.ReadS16(), yb = f.ReadS16();
            byte db = f.ReadU8(), speed = f.ReadU8();

            int frame = (int?)f.ResumeState ?? 0;

            var entities = s.Entities
                .Where(e => e.Character != null)
                .Where(e => e.Character != s.Player.Character);

            Entity entA = entities.ElementAtOrDefault(0),
                entB = entities.ElementAtOrDefault(1);

            if ((entA == null) && (entB == null))
                throw new InvalidOperationException();

            void Process(Entity ent, int x, int y, int d) {
                if (frame == 0) {
                    ent.Model.Translation = s.Player.Model.Translation;
                    ent.WalkmeshTri = s.Player.WalkmeshTri;
                    ent.Model.Visible = true;

                    float rotation = (float)(Math.Atan2(x - s.Player.Model.Translation.X, -(y - s.Player.Model.Translation.Y)) * 180 / Math.PI);
                    ent.Model.Rotation = new Vector3(0, 0, rotation);
                    ent.Model.PlayAnimation(1, true, 1f, null); //TODO - should be run, depending on speed

                } else if (frame == speed) {
                    s.DropToWalkmesh(ent, new Vector2(x, y), ent.WalkmeshTri); //TODO - if it's blocked, this won't be right. But that probably shouldn't happen?
                    float rotation = 360f * d / 255f;
                    ent.Model.Rotation = new Vector3(0, 0, rotation);
                    ent.Model.PlayAnimation(0, true, 1f, null);
                } else {
                    var target = Vector2.Lerp(s.Player.Model.Translation.XY(), new Vector2(x, y), 1f * frame / speed);
                    s.TryWalk(ent, new Vector3(target.X, target.Y, 0), false);
                }
            }

            if (entA != null)
                Process(entA,
                    s.Game.Memory.Read(bankAXY >> 4, xa), s.Game.Memory.Read(bankAXY & 0xf, ya),
                    s.Game.Memory.Read(bankADBX >> 4, da));
            if (entB != null)
                Process(entB,
                    s.Game.Memory.Read(bankADBX & 0xf, ya), s.Game.Memory.Read(bankBYD >> 4, yb),
                    s.Game.Memory.Read(bankBYD & 0xf, db));

            if (frame < speed) {
                f.ResumeState = ++frame;
                return OpResult.Restart;
            } else
                return OpResult.Continue;

        }

        private class JoinState {
            public int Frame;
            public Vector2 EntAStart, EntBStart;
        }

        public static OpResult JOIN(Fiber f, Entity e, FieldScreen s) {
            byte speed = f.ReadU8();

            var entities = s.Entities
                .Where(e => e.Character != null)
                .Where(e => e.Character != s.Player.Character);

            Entity entA = entities.ElementAtOrDefault(0),
                entB = entities.ElementAtOrDefault(1);

            if ((entA == null) && (entB == null))
                throw new InvalidOperationException();

            JoinState state;
            if (f.ResumeState == null) {
                f.ResumeState = state = new JoinState {
                    Frame = 0,
                    EntAStart = entA == null ? Vector2.Zero : entA.Model.Translation.XY(),
                    EntBStart = entB == null ? Vector2.Zero : entB.Model.Translation.XY(),
                };
            } else
                state = (JoinState)f.ResumeState;

            void Process(Entity ent, Vector2 start) {
                if (state.Frame == 0) {
                    float rotation = (float)(Math.Atan2(s.Player.Model.Translation.X - ent.Model.Translation.X, -(s.Player.Model.Translation.Y - ent.Model.Translation.Y)) * 180 / Math.PI);
                    ent.Model.Rotation = new Vector3(0, 0, rotation);
                    ent.Model.PlayAnimation(1, true, 1f, null); //TODO - should be run, depending on speed
                } else if (state.Frame == speed) {
                    ent.Model.Translation = s.Player.Model.Translation;
                    ent.Model.Visible = false;
                } else {
                    var target = Vector2.Lerp(start, s.Player.Model.Translation.XY(), 1f * state.Frame / speed);
                    s.TryWalk(ent, new Vector3(target.X, target.Y, 0), false);
                }
            }

            if (entA != null)
                Process(entA, state.EntAStart);
            if (entB != null)
                Process(entB, state.EntBStart);

            if (state.Frame < speed) {
                state.Frame++;
                return OpResult.Restart;
            } else
                return OpResult.Continue;

        }

        public static OpResult PDIRA(Fiber f, Entity e, FieldScreen s) {
            byte chr = f.ReadU8();
            var target = s.Entities
                .Where(e => e.Character != null)
                .FirstOrDefault(e => e.Character.CharIndex == chr) 
                ?? s.Player;
            float r = (float)Math.Atan2(target.Model.Translation.Y - e.Model.Translation.Y, target.Model.Translation.X - e.Model.Translation.X);
            e.Model.Rotation = new Vector3(0, 0, r * (float)Math.PI / 180f);
            return OpResult.Continue;
        }
        public static OpResult DIR(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8(), parm = f.ReadU8();
            float rotation = 360f * s.Game.Memory.Read(bank, parm) / 255f;
            e.Model.Rotation = new Vector3(0, 0, rotation);
            return OpResult.Continue;
        }
        public static OpResult XYZI(Fiber f, Entity e, FieldScreen s) {
            byte banks1 = f.ReadU8(), banks2 = f.ReadU8();
            short px = f.ReadS16(), py = f.ReadS16(), pz = f.ReadS16();
            ushort ptri = f.ReadU16();

            int x = s.Game.Memory.Read(banks1 >> 4, px),
                y = s.Game.Memory.Read(banks1 & 0xf, py),
                z = s.Game.Memory.Read(banks2 >> 4, pz),
                tri = s.Game.Memory.Read(banks2 & 0xf, ptri);

            e.WalkmeshTri = tri;
            e.Model.Translation = new Vector3(x, y, z);
            System.Diagnostics.Debug.WriteLine($"VM:XYZI moving {e.Name} to {e.Model.Translation} wmtri {tri}");

            return OpResult.Continue;
        }

        private static OpResult DoMove(Fiber f, Entity e, FieldScreen s, bool doAnimation) {
            byte banks = f.ReadU8();
            short sx = f.ReadS16(), sy = f.ReadS16();
            int x = s.Game.Memory.Read(banks >> 4, sx),
                y = s.Game.Memory.Read(banks & 0xf, sy);

            var remaining = new Vector2(x, y) - e.Model.Translation.XY();

            e.Model.Rotation = e.Model.Rotation.WithZ((float)(Math.Atan2(remaining.X, -remaining.Y) * 180f / Math.PI));

            if (doAnimation && (f.ResumeState == null) && (e.Model.AnimationState.Animation != 1))
                f.ResumeState = e.Model.AnimationState;

            if (remaining.Length() <= (e.MoveSpeed * 4)) {
                if (s.TryWalk(e, new Vector3(x, y, e.Model.Translation.Z), true)) {
                    if (f.ResumeState != null)
                        e.Model.AnimationState = (AnimationState)f.ResumeState;
                    return OpResult.Continue;
                } else
                    return OpResult.Restart;
                //TODO: Do we need to stop animating?
            } else {
                remaining.Normalize();
                remaining *= e.MoveSpeed * 4;
                s.TryWalk(e, e.Model.Translation + new Vector3(remaining.X, remaining.Y, 0), true);
                if (doAnimation && (e.Model.AnimationState.Animation != 1))
                    e.Model.PlayAnimation(1, true, 1f, null);
                return OpResult.Restart;
            }
        }

        public static OpResult MOVE(Fiber f, Entity e, FieldScreen s) {
            return DoMove(f, e, s, true);
        }
        public static OpResult FMOVE(Fiber f, Entity e, FieldScreen s) {
            return DoMove(f, e, s, false);
        }

        public static OpResult LINON(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            e.Line.Active = parm != 0;
            return OpResult.Continue;
        }

        public static OpResult UC(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            s.SetPlayerControls(parm == 0);
            return OpResult.Continue;
        }

        public static OpResult TLKON(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            if (parm != 1)
                e.Flags |= EntityFlags.CanTalk;
            else
                e.Flags &= ~EntityFlags.CanTalk;
            return OpResult.Continue;
        }

        public static OpResult SOLID(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            if (parm != 1)
                e.Flags |= EntityFlags.CanCollide;
            else
                e.Flags &= ~EntityFlags.CanCollide;
            return OpResult.Continue;
        }

        public static OpResult VISI(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            e.Model.Visible = parm != 0;
            return OpResult.Continue;
        }

        public static OpResult CANIM2(Fiber f, Entity e, FieldScreen s) {
            byte anim = f.ReadU8(), first = f.ReadU8(), last = f.ReadU8(), speed = f.ReadU8();
            f.Pause();
            var state = e.Model.AnimationState;
            f.OtherState["AnimPlaying"] = true;
            Action onComplete = () => {
                f.Resume();
                e.Model.AnimationState = state;
                f.OtherState["AnimPlaying"] = false;
            };
            e.Model.PlayAnimation(anim, false, 1f / speed, onComplete, first, last); //TODO is this speed even vaguely correct?
            return OpResult.Continue;
        }
        public static OpResult ANIME1(Fiber f, Entity e, FieldScreen s) {
            byte anim = f.ReadU8(), speed = f.ReadU8();
            f.Pause();
            var state = e.Model.AnimationState;
            f.OtherState["AnimPlaying"] = true;
            Action onComplete = () => {
                f.Resume();
                e.Model.AnimationState = state;
                f.OtherState["AnimPlaying"] = false;
            };
            e.Model.PlayAnimation(anim, false, 1f / speed, onComplete); //TODO is this speed even vaguely correct?
            return OpResult.Continue;
        }
        public static OpResult ANIME2(Fiber f, Entity e, FieldScreen s) {
            byte anim = f.ReadU8(), speed = f.ReadU8();
            var state = e.Model.AnimationState;
            f.OtherState["AnimPlaying"] = true;
            Action onComplete = () => {
                e.Model.AnimationState = state;
                f.OtherState["AnimPlaying"] = false;
            };
            e.Model.PlayAnimation(anim, false, 1f / speed, onComplete); 
            return OpResult.Continue;
        }
        public static OpResult ANIM_2(Fiber f, Entity e, FieldScreen s) {
            byte anim = f.ReadU8(), speed = f.ReadU8();
            f.Pause();
            f.OtherState["AnimPlaying"] = true;
            e.Model.PlayAnimation(anim, false, 1f / speed, () => {
                f.Resume();
                f.OtherState["AnimPlaying"] = false;
            }); //TODO is this speed even vaguely correct?
            return OpResult.Continue;
        }
        public static OpResult CANM_2(Fiber f, Entity e, FieldScreen s) {
            byte anim = f.ReadU8(), fstart = f.ReadU8(), fend = f.ReadU8(), speed = f.ReadU8();
            f.Pause();
            f.OtherState["AnimPlaying"] = true;
            e.Model.PlayAnimation(anim, false, 1f / speed, () => {
                f.Resume();
                f.OtherState["AnimPlaying"] = false;
            }, fstart, fend); 
            return OpResult.Continue;
        }
        public static OpResult DFANM(Fiber f, Entity e, FieldScreen s) {
            byte anim = f.ReadU8(), speed = f.ReadU8();
            e.Model.PlayAnimation(anim, true, 1f / speed, null); //TODO is this speed even vaguely correct?
            //TODO - not setting AnimPlaying, is that reasonable?
            return OpResult.Continue;
        }
        public static OpResult ASPED(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8();
            ushort parm = f.ReadU16();
            int speed = s.Game.Memory.Read(bank, parm);
            e.Model.GlobalAnimationSpeed = speed / 16f; //TODO is this even vaguely close
            return OpResult.Continue;
        }
        public static OpResult MSPED(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8();
            ushort parm = f.ReadU16();
            e.MoveSpeed = s.Game.Memory.Read(bank, parm) / 1024f;
            return OpResult.Continue;
        }

        private static OpResult DoTurn(Entity e, float rotation, float rotationSteps, byte rotateDir, byte rotateType) {
            float rotationAmount = rotationSteps == 0 ? 360f : 360f * rotationSteps / 255f;

            if (rotateDir > 2)
                rotateDir = 2; //TODO - it's 10 in elevtr1, so we can expect to see values other than 0/1/2, but how to treat them?

            float ccwAmount = rotation > e.Model.Rotation.Z ? (e.Model.Rotation.Z + 360 - rotation) : e.Model.Rotation.Z - rotation,
                cwAmount = rotation < e.Model.Rotation.Z ? (rotation + 360 - e.Model.Rotation.Z) : rotation - e.Model.Rotation.Z;
            if (rotateDir == 2) {
                if (cwAmount > ccwAmount)
                    rotateDir = 1;
                else
                    rotateDir = 0;
            }

            float remaining;
            if (rotateDir == 0)
                remaining = cwAmount;
            else
                remaining = ccwAmount;

            if (remaining <= rotationAmount) {
                e.Model.Rotation = e.Model.Rotation.WithZ(rotation);
                return OpResult.Continue;
            } else {
                if (rotateDir == 0)
                    e.Model.Rotation = e.Model.Rotation.WithZ((e.Model.Rotation.Z + rotationAmount) % 360);
                else
                    e.Model.Rotation = e.Model.Rotation.WithZ((e.Model.Rotation.Z + 360 - rotationAmount) % 360);
                return OpResult.Restart;
            }
        }

        public static OpResult TURNGEN(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8(), parm = f.ReadU8(),
                rotateDir = f.ReadU8(), steps = f.ReadU8(), rotateType = f.ReadU8(); //TODO!
            byte bRotation = (byte)s.Game.Memory.Read(bank, parm);
            float rotation = 360f * bRotation / 255f; //TODO?

            return DoTurn(e, rotation, steps, rotateDir, rotateType);
        }

        public static OpResult TURA(Fiber f, Entity e, FieldScreen s) {
            byte targetEntityID = f.ReadU8();
            byte rotateDir = f.ReadU8(), steps = f.ReadU8();

            var target = s.Entities[targetEntityID];
            float rotation = (float)(Math.Atan2(target.Model.Translation.X - e.Model.Translation.X, -(target.Model.Translation.Y - e.Model.Translation.Y)) * 180 / Math.PI);
            //TODO - we should calculate this *once* and not on every restart

            return DoTurn(e, rotation, steps, (byte)rotateDir, 2);
        }

        public static OpResult PTURA(Fiber f, Entity e, FieldScreen s) {
            byte targetParty = f.ReadU8(), steps = f.ReadU8(), rotateDir = f.ReadU8();

            var target = s.Entities.Find(e => e.Character == s.Game.SaveData.Party[targetParty]);
            float rotation = (float)(Math.Atan2(target.Model.Translation.X - e.Model.Translation.X, -(target.Model.Translation.Y - e.Model.Translation.Y)) * 180 / Math.PI);
            //TODO - we should calculate this *once* and not on every restart

            return DoTurn(e, rotation, steps, (byte)rotateDir, 2);
        }

        public static OpResult TALKR(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8(), parm = f.ReadU8();
            e.TalkDistance = s.Game.Memory.Read(bank, parm);
            return OpResult.Continue;
        }
        public static OpResult ANIMW(Fiber f, Entity e, FieldScreen s) {
            if ((bool)f.OtherState["AnimPlaying"])
                return OpResult.Restart;
            else
                return OpResult.Continue;
        }

        public static OpResult SLIDR(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8(), parm = f.ReadU8();
            e.CollideDistance = s.Game.Memory.Read(bank, parm);
            return OpResult.Continue;
        }

        public static OpResult LINE(Fiber f, Entity e, FieldScreen s) {
            e.Line = new FieldLine {
                P0 = new Vector3(f.ReadS16(), f.ReadS16(), f.ReadS16()),
                P1 = new Vector3(f.ReadS16(), f.ReadS16(), f.ReadS16()),
            };
            return OpResult.Continue;
        }

        public static OpResult GETAI(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8(), entity = f.ReadU8(), address = f.ReadU8();
            ushort value = (ushort)s.Entities[entity].WalkmeshTri;
            s.Game.Memory.Write(bank, address, value);
            return OpResult.Continue;
        }
        public static OpResult GETAXY(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), entity = f.ReadU8(), xaddr = f.ReadU8(), yaddr = f.ReadU8();
            ushort value = (ushort)s.Entities[entity].Model.Translation.X;
            s.Game.Memory.Write(banks & 0xf, xaddr, value);
            value = (ushort)s.Entities[entity].Model.Translation.Y;
            s.Game.Memory.Write(banks >> 4, yaddr, value);
            return OpResult.Continue;
        }

        public static OpResult OFST(Fiber f, Entity e, FieldScreen s) {
            byte bankxy = f.ReadU8(), bankzs = f.ReadU8(), type = f.ReadU8();
            short x = f.ReadS16(), y = f.ReadS16(), z = f.ReadS16(), speed = f.ReadS16();
            x = (short)s.Game.Memory.Read(bankxy & 0xf, x);
            y = (short)s.Game.Memory.Read(bankxy >> 4, y);
            z = (short)s.Game.Memory.Read(bankzs & 0xf, z);
            speed = (short)s.Game.Memory.Read(bankzs >> 4, speed);

            var start = e.Model.Translation2;
            var end = new Vector3(x, y, z);

            f.OtherState["OFST"] = true;

            s.StartProcess(frame => {
                float progress;
                switch (type) {
                    case 0: //instant
                        progress = 1f;
                        break;
                    case 1: //linear
                        progress = 1f * (frame / 2) / speed;
                        break;
                    case 2: //ease in/out
                        progress = Easings.QuadraticInOut(1f * (frame / 2) / speed);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                if (progress >= 1f) {
                    e.Model.Translation2 = end;
                    f.OtherState["OFST"] = false;
                    return true;
                } else {
                    e.Model.Translation2 = Vector3.Lerp(start, end, progress);
                    return false;
                }
            });

            return OpResult.Continue;
        }
        public static OpResult OFSTW(Fiber f, Entity e, FieldScreen s) {
            if (f.OtherState.TryGetValue("OFST", out object v) && (bool)v)
                return OpResult.Continue;
            else
                return OpResult.Restart;
        }

        public static OpResult KAWAI(Fiber f, Entity e, FieldScreen s) {
            byte len = f.ReadU8(), op = f.ReadU8();
            byte[] data = Enumerable.Range(0, len - 3).Select(_ => f.ReadU8()).ToArray();

            switch(op) {
                case 0xD: //SHINE
                    //VERY TODO!
                    break;
                
                default:
                    throw new NotImplementedException();
            }

            return OpResult.Continue;
        }

    }

    internal static class PartyInventory {
        public static OpResult PRTYE(Fiber f, Entity e, FieldScreen s) {
            foreach (var chr in s.Game.SaveData.Characters)
                if (chr != null)
                    chr.Flags &= ~CharFlags.ANY_PARTY_SLOT;

            void DoSet(byte which, CharFlags slot) {
                //Actually should be checking for 0xfe, but this is equivalent...
                if (which < s.Game.SaveData.Characters.Count)
                    s.Game.SaveData.Characters[which].Flags |= slot;
            }

            DoSet(f.ReadU8(), CharFlags.Party1);
            DoSet(f.ReadU8(), CharFlags.Party2);
            DoSet(f.ReadU8(), CharFlags.Party3);

            return OpResult.Continue;
        }

        public static OpResult MMBLK(Fiber f, Entity e, FieldScreen s) {
            byte charID = f.ReadU8();
            s.Game.SaveData.Characters[charID].Flags |= CharFlags.Locked;
            return OpResult.Continue;
        }

        public static OpResult STITM(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8();
            ushort item = f.ReadU16();
            byte qty = f.ReadU8();
            s.Game.SaveData.GiveInventoryItem(InventoryItemKind.Item, s.Game.Memory.Read(banks & 0xf, item), s.Game.Memory.Read(banks >> 4, qty));
            return OpResult.Continue;
        }
    }

    internal static class WindowMenu {
        public static OpResult MPNAM(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            string name = s.FieldDialog.Dialogs[parm];
            s.Game.SaveData.Location = name;
            return OpResult.Continue;
        }

        public static OpResult WINDOW(Fiber f, Entity e, FieldScreen s) {
            byte id = f.ReadU8();
            ushort x = f.ReadU16(), y = f.ReadU16(), w = f.ReadU16(), h = f.ReadU16();
            s.Dialog.SetupWindow(id, x, y, w, h);
            /* TODO
Adjustments are made for windows that are either too close an edge, or the width / height is too big for the screen. Eg:

// Adjust window position if too close to an edge
const MIN_WINDOW_DISTANCE = 8 // Looks about right
if (x < MIN_WINDOW_DISTANCE) { x = MIN_WINDOW_DISTANCE }
if (y < MIN_WINDOW_DISTANCE) { y = MIN_WINDOW_DISTANCE }
if (x + w + MIN_WINDOW_DISTANCE > GAME_WIDTH) { x = GAME_WIDTH - w - MIN_WINDOW_DISTANCE }
if (y + h + MIN_WINDOW_DISTANCE > GAME_HEIGHT) { y = GAME_HEIGHT - h - MIN_WINDOW_DISTANCE }
             */
            return OpResult.Continue;
        }

        public static OpResult WREST(Fiber f, Entity e, FieldScreen s) {
            byte id = f.ReadU8();
            s.Dialog.ResetWindow(id);
            return OpResult.Continue;
        }
        public static OpResult WCLSE(Fiber f, Entity e, FieldScreen s) {
            byte id = f.ReadU8();
            s.Dialog.ResetWindow(id);
            return OpResult.Continue;
        }
        public static OpResult WSIZW(Fiber f, Entity e, FieldScreen s) {
            byte id = f.ReadU8();
            ushort x = f.ReadU16(), y = f.ReadU16(),
                w = f.ReadU16(), h = f.ReadU16();

            s.Dialog.SetupWindow(id, x, y, w, h);

            return OpResult.Continue;
        }
        public static OpResult ASK(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8(), win = f.ReadU8(), msg = f.ReadU8(),
                firstChoice = f.ReadU8(), lastChoice = f.ReadU8(),
                addr = f.ReadU8();

            f.Pause();
            s.Dialog.Ask(win, s.FieldDialog.Dialogs[msg], Enumerable.Range(firstChoice, lastChoice - firstChoice + 1), ch => {
                s.Game.Memory.Write(bank, addr, (ushort)ch);
                f.Resume();
            });

            return OpResult.Continue;
        }

        public static OpResult MESSAGE(Fiber f, Entity e, FieldScreen s) {
            byte id = f.ReadU8(), dlg = f.ReadU8();
            f.Pause();
            s.Dialog.Show(id, s.FieldDialog.Dialogs[dlg], f.Resume);

            return OpResult.Continue;
        }

        public static OpResult MENU2(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            if (parm == 0)
                s.Options |= FieldOptions.MenuEnabled;
            else
                s.Options &= ~FieldOptions.MenuEnabled;
            return OpResult.Continue;
        }

        public static OpResult MENU(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8(), menu = f.ReadU8(), parm = f.ReadU8();
            int parmValue = s.Game.Memory.Read(bank, parm);

            switch (menu) {
                case 0x6:
                    if (parmValue < 10)
                        s.Game.PushScreen(new UI.Layout.LayoutScreen("Name", parm: parmValue));
                    else
                        throw new NotImplementedException();
                    break;
                case 0x9:
                    s.Game.PushScreen(new UI.Layout.LayoutScreen("MainMenu"));
                    break;
                case 0xE:
                    s.Game.PushScreen(new UI.Layout.LayoutScreen("SaveMenu"));
                    break;

                default:
                    throw new NotImplementedException();
            }

            return OpResult.Continue;
        }

    }

    internal static class CameraAudioVideo {

        //TODO store in external file?
        private static readonly string[] _trackNames = new[] {
            "ERROR", "ERROR", "oa", "ob", "dun2", "guitar2", "fanfare", "makoro", "bat",
            "fiddle", "kurai", "chu", "ketc", "earis", "ta", "tb", "sato",
            "parade", "comical", "yume", "mati", "sido", "siera", "walz", "corneo",
            "horror", "canyon", "red", "seto", "ayasi", "sinra", "sinraslo", "dokubo",
            "bokujo", "tm", "tifa", "costa", "rocket", "earislo", "chase", "rukei",
            "cephiros", "barret", "corel", "boo", "elec", "rhythm", "fan2", "hiku",
            "cannon", "date", "cintro", "cinco", "chu2", "yufi", "aseri", "gold1",
            "mura1", "yado", "over2", "crwin", "crlost", "odds", "geki", "junon",
            "tender", "wind", "vincent", "bee", "jukai", "sadbar", "aseri2", "kita",
            "sid2", "sadsid", "iseki", "hen", "utai", "snow", "yufi2", "mekyu",
            "condor", "lb2", "gun", "weapon", "pj", "sea", "ld", "lb1",
            "sensui", "ro", "jyro", "nointro", "riku", "si", "mogu", "pre",
            "fin", "heart", "roll"
        };

        public static OpResult MUSIC(Fiber f, Entity e, FieldScreen s) {
            byte track = f.ReadU8();
            if (!s.Options.HasFlag(FieldOptions.MusicLocked))
                s.Game.Audio.PlayMusic(_trackNames[s.FieldDialog.AkaoMusicIDs[track]]);
            return OpResult.Continue;
        }
        public static OpResult BMUSC(Fiber f, Entity e, FieldScreen s) {
            byte track = f.ReadU8();
            s.BattleOptions.OverrideMusic = _trackNames[s.FieldDialog.AkaoMusicIDs[track]];
            return OpResult.Continue;
        }
        public static OpResult FMUSC(Fiber f, Entity e, FieldScreen s) {
            byte track = f.ReadU8();
            s.BattleOptions.PostBattleMusic = _trackNames[s.FieldDialog.AkaoMusicIDs[track]];
            return OpResult.Continue;
        }
        public static OpResult MULCK(Fiber f, Entity e, FieldScreen s) {
            byte locked = f.ReadU8();
            if (locked != 0)
                s.Options |= FieldOptions.MusicLocked;
            else
                s.Options &= ~FieldOptions.MusicLocked;
            return OpResult.Continue;
        }

        private static OpResult DoAKAO(Fiber f, Entity e, FieldScreen s, byte op, int parm1, int parm2, int parm3, int parm4, int parm5) {
            switch (op) {
                case 0xc0:
                    s.Game.Audio.SetMusicVolume((byte)parm1);
                    break;

                case 0xc8:
                case 0xc9:
                case 0xca:
                    //Music pan - Not implemented in original FF7 (but maybe we could?)
                    break;
                
                case 0xf0:
                    if (!s.Options.HasFlag(FieldOptions.MusicLocked))
                        s.Game.Audio.StopMusic();
                    break;

                default: //TODO - all ops!
                    throw new NotImplementedException();
            }

            return OpResult.Continue;
        }

        public static OpResult AKAO(Fiber f, Entity e, FieldScreen s) {
            byte bank12 = f.ReadU8(), bank34 = f.ReadU8(), bank5 = f.ReadU8(),
                op = f.ReadU8(), p1 = f.ReadU8();
            ushort p2 = f.ReadU16(), p3 = f.ReadU16(), p4 = f.ReadU16(), p5 = f.ReadU16();

            int parm1 = s.Game.Memory.Read(bank12 >> 4, p1),
                parm2 = s.Game.Memory.Read(bank12 & 0xf, p2),
                parm3 = s.Game.Memory.Read(bank34 >> 4, p3),
                parm4 = s.Game.Memory.Read(bank34 & 0xf, p4),
                parm5 = s.Game.Memory.Read(bank5 >> 4, p5);

            return DoAKAO(f, e, s, op, parm1, parm2, parm3, parm4, parm5);
        }
        public static OpResult AKAO2(Fiber f, Entity e, FieldScreen s) {
            byte bank12 = f.ReadU8(), bank34 = f.ReadU8(), bank5 = f.ReadU8(),
                op = f.ReadU8();
            ushort p1 = f.ReadU16(), p2 = f.ReadU16(), p3 = f.ReadU16(), p4 = f.ReadU16(), p5 = f.ReadU16();

            int parm1 = s.Game.Memory.Read(bank12 >> 4, p1),
                parm2 = s.Game.Memory.Read(bank12 & 0xf, p2),
                parm3 = s.Game.Memory.Read(bank34 >> 4, p3),
                parm4 = s.Game.Memory.Read(bank34 & 0xf, p4),
                parm5 = s.Game.Memory.Read(bank5 >> 4, p5);

            return DoAKAO(f, e, s, op, parm1, parm2, parm3, parm4, parm5);
        }


        public static OpResult SOUND(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8();
            ushort ssound = f.ReadU16();
            byte span = f.ReadU8();
            int sound = s.Game.Memory.Read(banks & 0xf, ssound) - 1;
            int pan = s.Game.Memory.Read(banks >> 4, span);
            if (sound < 0)
                s.Game.Audio.StopLoopingSfx();
            else
                s.Game.Audio.PlaySfx(sound, 1f, (pan - 64) / 64);
            return OpResult.Continue;
        }

        public static OpResult PMVIE(Fiber f, Entity e, FieldScreen s) {
            byte which = f.ReadU8();
            s.Movie.Prepare(which);
            return OpResult.Continue;
        }
        public static OpResult MOVIE(Fiber f, Entity e, FieldScreen s) {
            f.Pause();
            s.Movie.Play(f.Resume);
            return OpResult.Continue;
        }
        public static OpResult MVIEF(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8(), addr = f.ReadU8();
            s.Game.Memory.Write(bank, addr, (ushort)Math.Max(0, s.Movie.Frame));
            return OpResult.Continue;
        }


        public static OpResult SCRLW(Fiber f, Entity e, FieldScreen s) {
            if (s.Options.HasFlag(FieldOptions.CameraIsAsyncScrolling))
                return OpResult.Restart;
            else
                return OpResult.Continue;
        }

        public static OpResult SCR2D(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8();
            short sx = f.ReadS16(), sy = f.ReadS16();
            int x = s.Game.Memory.Read(banks & 0xf, sx),
                y = s.Game.Memory.Read(banks >> 4, sy);

            s.BGScroll(x, y, false);
            return OpResult.Continue;
        }

        public static OpResult SCRCC(Fiber f, Entity e, FieldScreen s) {
            if (s.Player == null) {
                s.WhenPlayerSet += () => {
                    var pos = s.ModelToBGPosition(s.Player.Model.Translation);
                    s.BGScroll(pos.X, pos.Y, true);
                };
            } else {
                var pos = s.ModelToBGPosition(s.Player.Model.Translation);
                s.BGScroll(pos.X, pos.Y, true);
            }
            return OpResult.Continue;
        }

        private class ScrollState {
            public Vector2 Start;
            public int Frame;
        }
        public static OpResult SCR2DC(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), bankS = f.ReadU8();
            short sx = f.ReadS16(), sy = f.ReadS16(), ss = f.ReadS16();
            int x = s.Game.Memory.Read(banks & 0xf, sx),
                y = s.Game.Memory.Read(banks >> 4, sy),
                speed = s.Game.Memory.Read(bankS, ss);

            ScrollState state;
            if (f.ResumeState == null) {
                var scroll = s.GetBGScroll();
                state = new ScrollState {
                    Start = new Vector2(scroll.x, scroll.y),
                };
                f.ResumeState = state;
            } else
                state = (ScrollState)f.ResumeState;
            
            Vector2 end = new Vector2(x, y);
            var progress = Easings.QuadraticInOut(1f * state.Frame / speed); //TODO - is interpreting speed as framecount vaguely correct?

            if (progress >= 1f) {
                s.BGScroll(x, y, false);
                return OpResult.Continue;
            } else {
                var pos = state.Start + (end - state.Start) * progress;
                s.BGScroll(pos.X, pos.Y, false);
                state.Frame++;
                return OpResult.Restart;
            }
        }

        public static OpResult SCRLC(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8();
            byte sspeed = f.ReadU8();
            byte unknown = f.ReadU8(), type = f.ReadU8();
            int speed = s.Game.Memory.Read(bank, sspeed);

            Easing easing;
            switch (type) {
                case 2:
                    easing = Easings.Linear;
                    break;
                case 3:
                    easing = Easings.QuadraticInOut;
                    break;
                default:
                    easing = null;
                    break;
            }

            if (easing != null) {
                (int curX, int curY) = s.GetBGScroll();
                s.Options &= ~FieldOptions.CameraTracksPlayer;
                s.Options |= FieldOptions.CameraIsAsyncScrolling;
                s.StartProcess(frame => {
                    float progress = easing(1f * frame / speed);
                    var target = s.ClampBGScrollToViewport(s.ModelToBGPosition(s.Player.Model.Translation));

                    if (progress >= 1f) {
                        s.BGScroll(target.X, target.Y, true);
                        s.Options &= ~FieldOptions.CameraIsAsyncScrolling;
                        return true;
                    } else {
                        s.BGScroll(
                            curX + (target.X - curX) * progress,
                            curY + (target.Y - curY) * progress,
                            true
                        );
                        return false;
                    }
                });
            }

            return OpResult.Continue;
        }

        public static OpResult SCR2DL(Fiber f, Entity e, FieldScreen s) {
            byte banks1 = f.ReadU8(), banks2 = f.ReadU8();
            short sx = f.ReadS16(), sy = f.ReadS16();
            ushort sspeed = f.ReadU16();
            int x = s.Game.Memory.Read(banks1 & 0xf, sx),
                y = s.Game.Memory.Read(banks1 >> 4, sy),
                speed = s.Game.Memory.Read(banks2 >> 4, sspeed);

            (int curX, int curY) = s.GetBGScroll();

            int numFrames = sspeed * 60 / 32;

            f.Pause();
            s.StartProcess(frame => {
                float progress = 1f * frame / numFrames;
                s.BGScroll(curX + (x - curX) * progress, curY + (y - curY) * progress, false);

                if (progress >= 1) {
                    f.Resume();
                    return true;
                } else
                    return false;
            });

            return OpResult.Continue;
        }

        private static OpResult DoFade(Fiber f, Entity e, FieldScreen s, byte bankRG, byte bankB, 
            byte r, byte g, byte b, byte frames, byte fadeType, byte adjust) {

            int cR = (byte)s.Game.Memory.Read(bankRG >> 4, r),
                 cG = (byte)s.Game.Memory.Read(bankRG & 0xf, g),
                 cB = (byte)s.Game.Memory.Read(bankB, b);

            Color cStandard = new Color(cR, cG, cB, 0xff),
                cInverse = new Color(0xff - cR, 0xff - cG, 0xff - cB, 0xff),
                cInverse4 = new Color(4 * (0xff - cR), 4 * (0xff - cG), 4 * (0xff - cB), 0xff);

            switch (fadeType) {
                case 0: //NFADE type 0
                case 4: //FADE type 4 - both basically the same...?
                    s.Overlay.Fade(0, BlendState.Additive, Color.Black, Color.Black, null);
                    return OpResult.Continue;
                default: //TODO - other types!
                    throw new NotImplementedException();
            }
        }

        public static OpResult NFADE(Fiber f, Entity e, FieldScreen s) {
            byte bankRG = f.ReadU8(), bankB = f.ReadU8(), fadeType = f.ReadU8(), 
                r = f.ReadU8(), g = f.ReadU8(), b = f.ReadU8(),
                speed = f.ReadU8(), adjust = f.ReadU8();

            return DoFade(f, e, s, bankRG, bankB, r, g, b, speed, fadeType, adjust);
        }
        public static OpResult FADE(Fiber f, Entity e, FieldScreen s) {
            byte bankRG = f.ReadU8(), bankB = f.ReadU8(), r = f.ReadU8(), g = f.ReadU8(), b = f.ReadU8(),
                speed = f.ReadU8(), fadeType = f.ReadU8(), adjust = f.ReadU8();

            return DoFade(f, e, s, bankRG, bankB, r, g, b, (byte)(240 / speed), fadeType, adjust);
        }

    }

    internal static class Maths {

        private static Random _random = new();

        public static OpResult RANDOM(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8(), dest = f.ReadU8();
            byte value = (byte)_random.Next(256);
            s.Game.Memory.Write(bank, dest, value);
            return OpResult.Continue;
        }

        public static OpResult SETBYTE(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), dest = f.ReadU8(), src = f.ReadU8();
            int value = s.Game.Memory.Read(banks & 0xf, src);
            s.Game.Memory.Write(banks >> 4, dest, (byte)value);
            return OpResult.Continue;
        }
        public static OpResult SETWORD(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), dest = f.ReadU8();
            ushort src = f.ReadU16();
            int value = s.Game.Memory.Read(banks & 0xf, src);
            s.Game.Memory.Write(banks >> 4, dest, (ushort)value);
            return OpResult.Continue;
        }
        public static OpResult BITOFF(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), dest = f.ReadU8(), src = f.ReadU8();
            int bit = s.Game.Memory.Read(banks & 0xf, src);
            int current = s.Game.Memory.Read(banks >> 4, dest);
            s.Game.Memory.Write(banks >> 4, dest, (byte)(current & ~(1 << bit)));
            return OpResult.Continue;
        }
        public static OpResult BITON(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), dest = f.ReadU8(), src = f.ReadU8();
            int bit = s.Game.Memory.Read(banks & 0xf, src);
            int current = s.Game.Memory.Read(banks >> 4, dest);
            
            s.Game.Memory.Write(banks >> 4, dest, (byte)(current | (1 << bit)));
            return OpResult.Continue;
        }
        public static OpResult AND2(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), dest = f.ReadU8();
            ushort src = f.ReadU16();
            int value = s.Game.Memory.Read(banks & 0xf, src);
            int current = s.Game.Memory.Read(banks >> 4, dest);
            s.Game.Memory.Write(banks >> 4, dest, (ushort)(current & value));
            return OpResult.Continue;
        }
        public static OpResult OR2(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), dest = f.ReadU8();
            ushort src = f.ReadU16();
            int value = s.Game.Memory.Read(banks & 0xf, src);
            int current = s.Game.Memory.Read(banks >> 4, dest);
            s.Game.Memory.Write(banks >> 4, dest, (ushort)(current | value));
            return OpResult.Continue;
        }
    }

    public static class SystemControl {
        public static OpResult BTLON(Fiber f, Entity e, FieldScreen s) {
            s.BattleOptions.BattlesEnabled = f.ReadU8() == 0;
            return OpResult.Continue;
        }

        public static OpResult BTLMD(Fiber f, Entity e, FieldScreen s) {
            s.BattleOptions.Flags = (Battle.BattleFlags)f.ReadU16();
            return OpResult.Continue;
        }

        public static OpResult BATTLE(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8();
            ushort id = f.ReadU16();
            s.TriggerBattle(s.Game.Memory.Read(bank, id));
            return OpResult.Continue;
        }

        public static OpResult PMJMP(Fiber f, Entity e, FieldScreen s) {
            ushort id = f.ReadU16();
            var cached = s.Game.Singleton(() => new CachedField());
            cached.Load(s.Game, id);
            return OpResult.Continue;
        }
        public static OpResult MAPJUMP(Fiber f, Entity e, FieldScreen s) {
            short id = f.ReadS16(), x = f.ReadS16(), y = f.ReadS16();
            ushort tri = f.ReadU16();
            byte rotation = f.ReadU8();

            var destination = new Ficedula.FF7.Field.FieldDestination {
                DestinationFieldID = id,
                X = x, Y = y,
                Triangle = tri,
                Orientation = rotation,
            };
            s.FadeOut(() => {
                s.Game.ChangeScreen(s, new FieldScreen(destination));
            });

            return OpResult.Continue;
        }
    }
}
