using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7 {

    [Flags]
    public enum TargettingFlags {
        None = 0,
        EnableSelection = 0x1,
        StartOnEnemy = 0x2,
        MultiTargets = 0x4,
        ToggleMultiSingleTarget = 0x8,
        OneRowOnly = 0x10,
        ShortRange = 0x20,
        AllRows = 0x40,
        RandomTarget = 0x80,
    }
  
    public class EquipItem {
        public int ID { get; set; }
        public EquipRestrictions Restrictions { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public int StrBonus { get; set; }
        public int VitBonus { get; set; }
        public int MagBonus { get; set; }
        public int SprBonus { get; set; }
        public int DexBonus { get; set; }
        public int LckBonus { get; set; }

        public int EquippableOn { get; set; } //TODO - enum flags?

    }

    public class MateriaItem : EquipItem {
        public List<MateriaSlotKind> MateriaSlots { get; } = new();
        public int Growth { get; set; }

    }

    public class Weapon : MateriaItem {
        public TargettingFlags TargettingFlags { get; set; }
        public byte DamageFormula { get; set; }
        public byte AttackStrength { get; set; }
        public Statuses Statuses { get; set; }
        public int CriticalChance { get; set; }
        public int HitChance { get; set; }
        public byte AnimationModifier { get; set; }
        public byte AttackModel { get; set; }
        public Elements Elements { get; set; }
        public int HitSoundEffect { get; set; }
        public int CriticalSoundEffect { get; set; }
        public int MissSoundEffect { get; set; }
        public byte ImpactEffectID { get; set; }

    }

    public class WeaponCollection {
        private List<Weapon> _weapons = new();

        public IReadOnlyList<Weapon> Weapons => _weapons.AsReadOnly();

        public WeaponCollection(Kernel kernel) {
            var descriptions = new KernelText(kernel.Sections.ElementAt(12));
            var names = new KernelText(kernel.Sections.ElementAt(20));

            var data = new MemoryStream(kernel.Sections.ElementAt(5));

            int index = 0;
            while (data.Position < data.Length) {
                Weapon weapon = new Weapon {
                    Name = names.Get(index),
                    Description = descriptions.Get(index),
                    ID = index,
                };
                index++;
                weapon.TargettingFlags = (TargettingFlags)data.ReadU8();
                data.ReadU8();
                weapon.DamageFormula = data.ReadU8();
                data.ReadU8();
                weapon.AttackStrength = data.ReadU8();
                byte status = data.ReadU8();
                weapon.Statuses = status == 0xff ? Statuses.None : (Statuses)(1 << status);
                weapon.Growth = data.ReadU8();
                if (weapon.Growth > 3) weapon.Growth = 1;
                weapon.CriticalChance = data.ReadU8();
                weapon.HitChance = data.ReadU8();
                byte model = data.ReadU8();
                weapon.AnimationModifier = (byte)(model >> 4);
                weapon.AttackModel = (byte)(model & 0xf);
                data.ReadU8();
                byte hiSound = data.ReadU8();
                hiSound = 0;
                data.ReadU16();
                weapon.EquippableOn = data.ReadU16();
                weapon.Elements = (Elements)data.ReadU16();
                data.ReadU16();

                switch (data.ReadI32()) {
                    case 0:
                        weapon.StrBonus = data.ReadI32();
                        break;
                    case 1:
                        weapon.VitBonus = data.ReadI32();
                        break;
                    case 2:
                        weapon.MagBonus = data.ReadI32();
                        break;
                    case 3:
                        weapon.SprBonus = data.ReadI32();
                        break;
                    case 4:
                        weapon.DexBonus = data.ReadI32();
                        break;
                    case 5:
                        weapon.LckBonus = data.ReadI32();
                        break;
                    default:
                        data.ReadI32();
                        break;
                }

                foreach (int _ in Enumerable.Range(0, 8)) {
                    switch (data.ReadU8()) {
                        case 1:
                        case 5:
                            weapon.MateriaSlots.Add(MateriaSlotKind.Single);
                            break;
                        case 2:
                        case 3:
                        case 6:
                        case 7:
                            weapon.MateriaSlots.Add(MateriaSlotKind.Linked);
                            break;
                    }
                }

                weapon.HitSoundEffect = (hiSound << 8) | data.ReadU8();
                weapon.CriticalSoundEffect = (hiSound << 8) | data.ReadU8();
                weapon.MissSoundEffect = (hiSound << 8) | data.ReadU8();

                if ((hiSound & 1) != 0)
                    weapon.HitSoundEffect += 254;
                if ((hiSound & 2) != 0)
                    weapon.CriticalSoundEffect += 254;
                if ((hiSound & 4) != 0)
                    weapon.MissSoundEffect += 254;

                weapon.ImpactEffectID = data.ReadU8();
                data.ReadU16();
                weapon.Restrictions = (EquipRestrictions)(~data.ReadU16() & 0xf);

                _weapons.Add(weapon);
            }
        }
  

    }
}
