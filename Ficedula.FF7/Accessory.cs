using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7 {

    public enum AccessoryEffect {
        None = 0xff,
        Haste = 0,
        Berserk = 1,
        Curse = 2,
        Reflect = 3,
        IncreaseSteal = 4,
        IncreaseManipulate = 5,
        Barriers = 6,
        //TODO- Cat's Bell? How is that indicated?
    }

    public class Accessory : EquipItem {
        public EquipElement ElementEffect { get; set; }
        public Elements Elements { get; set; }
        public AccessoryEffect AccessoryEffect { get; set; }
        public Statuses ProtectStatuses { get; set; }
    }

    public class AccessoryCollection {
        private List<Accessory> _accessories = new();

        public IReadOnlyList<Accessory> Accessories => _accessories.AsReadOnly();

        public AccessoryCollection(Kernel kernel) {
            var descriptions = new KernelText(kernel.Sections.ElementAt(14));
            var names = new KernelText(kernel.Sections.ElementAt(22));

            var data = new MemoryStream(kernel.Sections.ElementAt(7));

            int index = 0;
            while (data.Position < data.Length) {
                Accessory accessory = new Accessory {
                    Name = names.Get(index),
                    Description = descriptions.Get(index),
                    ID = index,
                };
                index++;

                ushort stats = data.ReadU16(), values = data.ReadU16();
                foreach(int _ in Enumerable.Range(0, 2)) {
                    switch (stats & 0xff) {
                        case 0:
                            accessory.StrBonus = values & 0xff;
                            break;
                        case 1:
                            accessory.VitBonus = values & 0xff;
                            break;
                        case 2:
                            accessory.MagBonus = values & 0xff;
                            break;
                        case 3:
                            accessory.SprBonus = values & 0xff;
                            break;
                        case 4:
                            accessory.DexBonus = values & 0xff;
                            break;
                        case 5:
                            accessory.LckBonus = values & 0xff;
                            break;
                    }
                    stats >>= 8;
                    values >>= 8;
                }

                accessory.ElementEffect = (EquipElement)data.ReadU8();
                accessory.AccessoryEffect = (AccessoryEffect)data.ReadU8();
                accessory.Elements = (Elements)data.ReadU16();
                accessory.ProtectStatuses = (Statuses)data.ReadU32();
                accessory.EquippableOn = data.ReadU16();
                accessory.Restrictions = (EquipRestrictions)(~data.ReadU16() & 0x7);

                _accessories.Add(accessory);
            }
        }
    }
}
