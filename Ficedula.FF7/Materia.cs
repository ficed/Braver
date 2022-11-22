using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
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
        public string Description { get; private set; }
        public MateriaEquipEffect EquipEffect { get; private set; }

        protected abstract void DoInit(byte subType, IEnumerable<byte> attrs);

        public void Init(string name, string description, MateriaEquipEffect equipEffect, 
            IEnumerable<int> apLevels,
            byte subType, IEnumerable<byte> attrs) {
            Name = name;
            Description = description; 
            EquipEffect = equipEffect;
            _apLevels = apLevels.ToList();
            DoInit(subType, attrs);
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
        protected override void DoInit(byte subType, IEnumerable<byte> attrs) {
            _magics = Enumerable.Range(0, 53).Cast<int?>().ToList();
        }
    }

    public class MagicMateria : Materia {
        protected List<int?> _magics;

        public IReadOnlyList<int?> Magics => _magics.AsReadOnly();

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
                        names.Get(index), descriptions.Get(index),
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
            Register<AttackCommandMateria>(0x2);
            Register<WCommandMateria>(0x3);
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
