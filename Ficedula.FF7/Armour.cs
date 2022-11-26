using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7 {
    
    public enum EquipElement {
        Absorb = 0,
        Nullify = 1,
        Halve = 2,
        None = 0xff,
    }

    public enum MateriaSlotKind {
        Single,
        Linked,
    }

    [Flags]
    public enum EquipRestrictions {
        None = 0,
        CanBeSold = 0x1,
        CanUseInBattle = 0x2,
        CanUseInMenu = 0x4,
        CanThrow = 0x8,
    }

    public class Armour : MateriaItem {
        public EquipElement ElementEffect { get; set; }
        public Elements Elements { get; set; }
        public byte Defense { get; set; }
        public byte MDefense { get; set; }
        public byte DefensePercent { get; set; }
        public byte MDefensePercent { get; set; }
        public Statuses StatusDefense { get; set; }

    }

    public class ArmourCollection {

        private List<Armour> _armour = new();

        public IReadOnlyList<Armour> Armour => _armour.AsReadOnly();

        public ArmourCollection(Kernel kernel) {
            var descriptions = new KernelText(kernel.Sections.ElementAt(13));
            var names = new KernelText(kernel.Sections.ElementAt(21));

            var data = new MemoryStream(kernel.Sections.ElementAt(6));

            int index = 0;
            while (data.Position < data.Length) {
                data.ReadU8();
                Armour armour = new Armour {
                    Name = names.Get(index),
                    Description = descriptions.Get(index),
                    ID = index,
                };
                index++;
                armour.ElementEffect = (EquipElement)data.ReadU8();
                armour.Defense = data.ReadU8();
                armour.MDefense = data.ReadU8();
                armour.DefensePercent = data.ReadU8();
                armour.MDefensePercent = data.ReadU8();
                byte status = data.ReadU8();
                armour.StatusDefense = status == 0xff ? Statuses.None : (Statuses)(1 << status);
                data.ReadU16();
                foreach(int _ in Enumerable.Range(0, 8)) {
                    switch (data.ReadU8()) {
                        case 1:
                        case 5:
                            armour.MateriaSlots.Add(MateriaSlotKind.Single);
                            break;
                        case 2:
                        case 3:
                        case 6:
                        case 7:
                            armour.MateriaSlots.Add(MateriaSlotKind.Linked);
                            break;
                    }
                }
                armour.Growth = data.ReadU8();
                if (armour.Growth > 3) armour.Growth = 1;
                armour.EquippableOn = data.ReadU16();
                armour.Elements = (Elements)data.ReadU16();
                data.ReadU16();
                switch (data.ReadI32()) {
                    case 0:
                        armour.StrBonus = data.ReadI32();
                        break;
                    case 1:
                        armour.VitBonus = data.ReadI32();
                        break;
                    case 2:
                        armour.MagBonus = data.ReadI32();
                        break;
                    case 3:
                        armour.SprBonus = data.ReadI32();
                        break;
                    case 4:
                        armour.DexBonus = data.ReadI32();
                        break;
                    case 5:
                        armour.LckBonus = data.ReadI32();
                        break;
                    default:
                        data.ReadI32();
                        break;
                }
                armour.Restrictions = (EquipRestrictions)(~data.ReadU16() & 0x7);
                data.ReadU16();

                _armour.Add(armour);
            }
        }
    }
}
