// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7 {

    public class MateriaEquipEffect {
        public int Strength { get; set; }
        public int Vitality { get; set; }
        public int Magic { get; set; }
        public int Spirit { get; set; }
        public int Dexterity { get; set; }
        public int Luck { get; set; }
        public int MaxHP { get; set; }
        public int MaxMP { get; set; }

        private MateriaEquipEffect() { }

        public static MateriaEquipEffect ByIndex(int index) => _byIndex[index];

        private static MateriaEquipEffect[] _byIndex = new[] {
            new MateriaEquipEffect { },
            new MateriaEquipEffect { Strength = -2, Vitality = -1, Magic = +2, Spirit = +1, MaxHP = -5, MaxMP = +5 },
            new MateriaEquipEffect { Strength = -4, Vitality = -2, Magic = +4, Spirit = +2, MaxHP = -10, MaxMP = +10 },
            new MateriaEquipEffect { Dexterity = +2, Luck = -2 },
            new MateriaEquipEffect { Strength = -1, Vitality = -1, Magic = +1, Spirit = +1 },
            new MateriaEquipEffect { Strength = +1, Vitality = +1, Magic = -1, Spirit = -1 },
            new MateriaEquipEffect { Vitality = +1 },
            new MateriaEquipEffect { Luck = +1 },
            new MateriaEquipEffect { Luck = -1 },
            new MateriaEquipEffect { Dexterity = -2 },
            new MateriaEquipEffect { Dexterity = +2 },
            new MateriaEquipEffect { Strength = -1, Magic = +1, MaxHP = -2, MaxMP = +2 },
            new MateriaEquipEffect { Magic = +1, MaxHP = -2, MaxMP = +2 },
            new MateriaEquipEffect { Magic = +1, Spirit = +1, MaxHP = -5, MaxMP = +5 },
            new MateriaEquipEffect { Magic = +2, Spirit = +2, MaxHP = -10, MaxMP = +10 },
            new MateriaEquipEffect { Magic = +4, Spirit = +4, MaxHP = -10, MaxMP = +15 },
            new MateriaEquipEffect { Magic = +8, Spirit = +8, MaxHP = -10, MaxMP = +20 },
            new MateriaEquipEffect { },
            new MateriaEquipEffect { },
            new MateriaEquipEffect { },
            new MateriaEquipEffect { },
            new MateriaEquipEffect { Strength = +1, Vitality = +2, Magic = +4, Spirit = +8, Dexterity = +16, Luck = +32, MaxHP = +64, MaxMP = +128 },
        };
    }

    public abstract class Materia {

        private List<int> _apLevels;

        public IReadOnlyList<int> APLevels => _apLevels.AsReadOnly();
        public string Name { get; private set; }
        public int ID { get; private set; }
        public string Description { get; private set; }
        public MateriaEquipEffect EquipEffect { get; private set; }

        protected abstract void DoInit(byte subType, IEnumerable<byte> attrs);

        public void Init(string name, string description, int id, MateriaEquipEffect equipEffect,
            IEnumerable<int> apLevels,
            byte subType, IEnumerable<byte> attrs) {
            Name = name;
            ID = id;
            Description = description;
            EquipEffect = equipEffect;
            _apLevels = apLevels.ToList();
            DoInit(subType, attrs);
        }
    }

    public enum SupportMateriaKind {
        All = 0x51,
        CommandCounter = 0x54,
        MagicCounter = 0x55,
        SneakAttack = 0x56,
        FinalAttack = 0x57,
        MPTurbo = 0x58,
        MPAbsorb = 0x59,
        HPAbsorb = 0x5A,
        AddedCut = 0x5C,
        StealAsWell = 0x5D,
        Elemental = 0x5E,
        AddedEffect = 0x5F,
        QuadraMagic = 0x63,
    }

    public class SupportMateria : Materia {
        public SupportMateriaKind Kind { get; private set; }

        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            Kind = (SupportMateriaKind)attrs.First();
        }

        public override string ToString() {
            return $"Apply effect {Kind} to linked materia";
        }
    }

    public enum IndependentMateriaKind {
        Underwater,
        HP_MP_Swap,
        LongRange,
        XPPlus,

        CounterAttack,
        Cover,

        StrengthPlus,
        VitalityPlus,
        MagicPlus,
        SpiritPlus,
        SpeedPlus,
        LuckPlus,
        AttackPlus,
        DefensePlus,
        MaxHPPlus,
        MaxMPPlus,

        GilPlus,
        EnemyAway,
        EnemyLure,
        ChocoboLure,
        PreEmptive,

        MegaAll,
    }

    public abstract class IndependentMateria : Materia {
        public IndependentMateriaKind Kind { get; protected set; }

        public override string ToString() {
            return $"Independent effect {Kind}";
        }
    }

    public class IndependentMateria1 : IndependentMateria {
        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            switch (attrs.First()) {
                case 0:
                    Kind = IndependentMateriaKind.GilPlus;
                    break;
                case 1:
                    Kind = IndependentMateriaKind.EnemyLure;//TODO or away!
                    break;
                case 2:
                    Kind = IndependentMateriaKind.ChocoboLure;
                    break;
                case 3:
                    Kind = IndependentMateriaKind.PreEmptive;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
    public class IndependentMateria4 : IndependentMateria {
        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            Kind = IndependentMateriaKind.MegaAll;
        }
    }
    public class IndependentMateria0 : IndependentMateria {

        public List<int> Amounts { get; private set; }

        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            switch (subType) {
                case 0x0:
                    switch (attrs.First()) {
                        case 0xC:
                            Kind = IndependentMateriaKind.Underwater;
                            break;
                        case 0x62:
                            Kind = IndependentMateriaKind.HP_MP_Swap;
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                    break;
                case 0x2:
                    switch (attrs.First()) {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                        case 6:
                        case 7:
                        case 8:
                        case 9:
                            Kind = IndependentMateriaKind.StrengthPlus + attrs.First();
                            break;

                        case 11:
                            Kind = IndependentMateriaKind.Cover;
                            break;

                        case 0x53:
                            Kind = IndependentMateriaKind.CounterAttack;
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    Amounts = attrs.Skip(1).Select(b => (int)b).ToList();
                    break;
                case 0x3:
                    Kind = IndependentMateriaKind.LongRange;
                    break;
                case 0x4:
                    Kind = IndependentMateriaKind.XPPlus;
                    break;
            }
        }
    }




    public abstract class CommandMateria : Materia {
        public int? RemoveCommand { get; protected set; }
        public List<int> Commands { get; protected set; }
        public override string ToString() => $"{Name} - grants commands {string.Join("/", Commands)} removes {RemoveCommand}";
    }

    public class MasterCommandMateria : CommandMateria {
        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            Commands = new List<int> { 
                5, 6, 7, 9, 10, 11, 12,
            };
        }
    }

    public class ESkillCommandMateria : CommandMateria {
        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            Commands = new List<int> { 13 };
        }
    }

    public class AttackCommandMateria : CommandMateria {

        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            RemoveCommand = 1;
            Commands = attrs
                .TakeWhile(b => b != 0xff)
                .Select(b => (int)b)
                .ToList();
        }
    }

    public class WCommandMateria : CommandMateria {
        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            switch (attrs.First()) {
                case 0x15:
                    RemoveCommand = 2;
                    Commands = new List<int> { 21 };
                    break;
                case 0x16:
                    RemoveCommand = 3;
                    Commands = new List<int> { 22 };
                    break;
                case 0x17:
                    RemoveCommand = 4;
                    Commands = new List<int> { 23 };
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }

    public class StandardCommandMateria : CommandMateria {
        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            Commands = attrs
                .TakeWhile(b => b != 0xff)
                .Select(b => (int)b)
                .ToList();
        }
    }

    public class MasterSummonMateria : SummonMateria {
        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            _summons = Enumerable.Range(56, 16).ToList();
        }
    }

    public class SummonMateria : Materia {
        protected List<int> _summons;
        public IReadOnlyList<int> Summons => _summons.AsReadOnly();

        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            _summons = new List<int> { attrs.First() };
        }

        public override string ToString() => $"{Name} - grants summons {string.Join("/", Summons)}";
    }

    public class MasterMagicMateria : MagicMateria {

        public override IEnumerable<int> GrantedAtLevel(int level) => _magics.Select(m => m.Value);

        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            _magics = Enumerable.Range(0, 53).Cast<int?>().ToList();
        }
    }

    public class MagicMateria : Materia {
        protected List<int?> _magics;

        public IReadOnlyList<int?> Magics => _magics.AsReadOnly();

        public virtual IEnumerable<int> GrantedAtLevel(int level) => Magics
            .Take(level)
            .Where(m => m.HasValue)
            .Select(m => m.Value);

        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            _magics = attrs
                .Reverse()
                .SkipWhile(b => b == 0xff)
                .Reverse()
                .Select(b => b == 0xff ? (int?)null : (int)b)
                .ToList();
        }

        public override string ToString() => $"{Name} - grants magic {string.Join("/", Magics)}";
    }

    public class MateriaCollection {

        private Dictionary<int, Materia> _materia = new();

        public IReadOnlyDictionary<int, Materia> Item => _materia;

        public MateriaCollection(Kernel kernel) {
            var descriptions = new KernelText(kernel.Sections.ElementAt(15));
            var names = new KernelText(kernel.Sections.ElementAt(23));

            var data = new MemoryStream(kernel.Sections.ElementAt(8));

            int index = 0;
            while(data.Position < data.Length) {
                ushort[] apLimits = Enumerable.Range(0, 4)
                    .Select(_ => data.ReadU16())
                    .ToArray();

                byte equipEffect = data.ReadU8();
                uint statusElement = data.ReadU32();
                byte materiaType = data.ReadU8();
                byte[] attrs = Enumerable.Range(0, 6)
                    .Select(_ => data.ReadU8())
                    .ToArray();

                if (_byType.TryGetValue(materiaType & 0xf, out var create)) {
                    var materia = create();
                    materia.Init(
                        names.Get(index), descriptions.Get(index), index,
                        MateriaEquipEffect.ByIndex(equipEffect),
                        apLimits.TakeWhile(u16 => u16 != 0xffff).Select(u16 => (int)u16),
                        (byte)(materiaType >> 4), attrs
                    );
                    _materia[index] = materia;
                }

                index++;
            }
        }

        private static Dictionary<int, Func<Materia>> _byType = new();

        private static void Register<T>(byte type) where T : Materia, new() {
            _byType[type] = () => new T();
        }

        static MateriaCollection() {
            Register<IndependentMateria0>(0x0);
            Register<IndependentMateria1>(0x1);
            Register<AttackCommandMateria>(0x2);
            Register<WCommandMateria>(0x3);
            Register<IndependentMateria4>(0x4);
            Register<SupportMateria>(0x5);
            Register<StandardCommandMateria>(0x6);
            Register<ESkillCommandMateria>(0x7);
            Register<MasterCommandMateria>(0x8);
            Register<MagicMateria>(0x9);
            Register<MasterMagicMateria>(0xA);
            Register<SummonMateria>(0xB);
            Register<MasterSummonMateria>(0xC);
        }
    }


}
