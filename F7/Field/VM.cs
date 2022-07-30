using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public void Run(bool isInit = false) {
            _inInit = isInit;
            while (Active) {
                int opIP = _ip;
                OpCode op = (OpCode)ReadU8();
                switch (VM.Execute(op, this, _entity, _screen)) {
                    case OpResult.Continue:
                        OpcodeAttempts = 0;
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
            Register(typeof(Audio));
            Register(typeof(WindowMenu));
            Register(typeof(FieldModels));
            Register(typeof(Maths));
        }

        public static OpResult Execute(OpCode op, Fiber f, Entity e, FieldScreen s) {
            if (_executors[(int)op] == null) {
                if (ErrorOnUnknown)
                    throw new F7Exception($"Cannot execute opcode {op}");
                f.Stop();
                f.Jump(f.IP - 1); //So if we retry, the opcode is actually retried [and will fail again] rather than trying the next operand byte as an opcode
                System.Diagnostics.Debug.WriteLine($"Aborting script due to unrecognised opcode {op}");
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
                    match = (val2 & (1 << val2)) != 0; break;
                case 0xA:
                    match = ~(val2 & (1 << val2)) != 0; break;
                default:
                    throw new F7Exception($"Unrecognised comparison {comparison}");
            }

            if (match)
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
        public static OpResult CC(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            s.Player = s.Entities[parm].Model; //TODO: also center screen etc.
            return OpResult.Continue;
        }

        public static OpResult CHAR(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            int modelIndex = s.GetNextModelIndex();
            if (parm != modelIndex)
                System.Diagnostics.Debug.WriteLine($"CHAR opcode - parameter {parm} did not match auto-assign ID {modelIndex}");
            e.Model = s.FieldModels[modelIndex];
            return OpResult.Continue;
        }

        public static OpResult PC(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            e.Character = s.Game.SaveData.Characters[parm];
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

        public static OpResult TLKON(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            if (parm != 0)
                e.Flags |= EntityFlags.CanTalk;
            else
                e.Flags &= ~EntityFlags.CanTalk;
            return OpResult.Continue;
        }

        public static OpResult SOLID(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            if (parm != 0)
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

        public static OpResult ANIME1(Fiber f, Entity e, FieldScreen s) {
            byte anim = f.ReadU8(), speed = f.ReadU8();
            f.Pause();
            var state = e.Model.AnimationState;
            Action onComplete = () => {
                f.Resume();
                e.Model.AnimationState = state;
            };
            e.Model.PlayAnimation(anim, true, 1f / speed, onComplete); //TODO is this speed even vaguely correct?
            return OpResult.Continue;
        }
        public static OpResult ANIM_2(Fiber f, Entity e, FieldScreen s) {
            byte anim = f.ReadU8(), speed = f.ReadU8();
            f.Pause();
            e.Model.PlayAnimation(anim, true, 1f / speed, f.Resume); //TODO is this speed even vaguely correct?
            return OpResult.Continue;
        }
        public static OpResult DFANM(Fiber f, Entity e, FieldScreen s) {
            byte anim = f.ReadU8(), speed = f.ReadU8();
            e.Model.PlayAnimation(anim, true, 1f / speed, null); //TODO is this speed even vaguely correct?
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
            e.MoveSpeed = s.Game.Memory.Read(bank, parm);
            return OpResult.Continue;
        }

        public static OpResult TALKR(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8(), parm = f.ReadU8();
            e.TalkDistance = s.Game.Memory.Read(bank, parm);
            return OpResult.Continue;
        }
        public static OpResult ANIMW(Fiber f, Entity e, FieldScreen s) {
            f.Pause();
            e.Model.AnimationState.AnimationComplete += f.Resume;
            return OpResult.Continue;
        }

        public static OpResult SLIDR(Fiber f, Entity e, FieldScreen s) {
            byte bank = f.ReadU8(), parm = f.ReadU8();
            e.CollideDistance = s.Game.Memory.Read(bank, parm);
            return OpResult.Continue;
        }
    }

    internal static class WindowMenu {
        public static OpResult MPNAM(Fiber f, Entity e, FieldScreen s) {
            byte parm = f.ReadU8();
            string name = s.Dialog.Dialogs[parm];
            s.Game.SaveData.Location = name;
            return OpResult.Continue;
        }

    }

    internal static class Audio {

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
            //TODO!!!!
            //s.Game.Audio.PlayMusic(_trackNames[s.Script.MusicIDs.ElementAt(track)], false);
            return OpResult.Continue;
        }
    }

    internal static class Maths {
        public static OpResult SETBYTE(Fiber f, Entity e, FieldScreen s) {
            byte banks = f.ReadU8(), dest = f.ReadU8(), src = f.ReadU8();
            int value = s.Game.Memory.Read(banks & 0xf, src);
            s.Game.Memory.Write(banks >> 4, dest, (byte)value);
            return OpResult.Continue;
        }
    }
}
