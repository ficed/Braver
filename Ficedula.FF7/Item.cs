// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7.Battle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7 {
    public class Item {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ushort CameraMovementID { get; set; }
        public EquipRestrictions Restrictions { get; set; }
        public TargettingFlags TargettingFlags { get; set; }
        public byte AttackEffectID { get; set; }
        public byte DamageFormula { get; set; }
        public byte Power { get; set; }
        public AttackCondition AttackCondition { get; set; }
        public AttackStatusType StatusType { get; set; }
        public byte StatusChance { get; set; }
        public Statuses Statuses { get; set; }
        public byte AdditionalEffects { get; set; }
        public byte AdditionalEffectsModifier { get; set; }
        public Elements Elements { get; set; }
        public ushort AttackFlags { get; set; }
    }

    public class ItemCollection {
        private List<Item> _items = new();

        public IReadOnlyList<Item> Items => _items.AsReadOnly();

        public ItemCollection(Kernel kernel) {
            var descriptions = new KernelText(kernel.Sections.ElementAt(11));
            var names = new KernelText(kernel.Sections.ElementAt(19));

            var data = new MemoryStream(kernel.Sections.ElementAt(4));

            int index = 0;
            while (data.Position < data.Length) {
                Item item = new Item {
                    Name = names.Get(index),
                    Description = descriptions.Get(index),
                    ID = index,
                };
                index++;

                data.ReadI32();
                data.ReadI32();
                item.CameraMovementID = data.ReadU16();
                item.Restrictions = (EquipRestrictions)(~data.ReadU16() & 0x7);
                item.TargettingFlags = (TargettingFlags)data.ReadU8();
                item.AttackEffectID = data.ReadU8();
                item.DamageFormula = data.ReadU8();
                item.Power = data.ReadU8();
                item.AttackCondition = (AttackCondition)data.ReadU8();
                byte chance = data.ReadU8();
                item.StatusChance = (byte)(chance & 0x3f);
                if ((chance & 0x80) != 0)
                    item.StatusType = AttackStatusType.Toggle;
                else if ((chance & 0x40) != 0)
                    item.StatusType = AttackStatusType.Cure;
                else
                    item.StatusType = AttackStatusType.Inflict;
                item.AdditionalEffects = data.ReadU8();
                item.AdditionalEffectsModifier = data.ReadU8();
                item.Statuses = (Statuses)data.ReadI32();
                if (chance == 0xff)
                    item.Statuses = Statuses.None;
                item.Elements = (Elements)data.ReadU16();
                item.AttackFlags = data.ReadU16();

                _items.Add(item);
            }
        }
    }
}
