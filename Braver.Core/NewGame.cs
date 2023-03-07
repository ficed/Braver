// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {
    internal static class NewGame {

        private static string[] _charIDs = new[] {
            "cloud", "barret", "tifa", "aeris", "red13", "yuffie", "caitsith", "vincent", "cid",
        };

        public static void Init(BGame game) {
            game.Memory.ResetAll();

            var kernel = game.Singleton<KernelCache>();
            var data = new MemoryStream(kernel.Kernel.Sections[3]);
            foreach(int c in Enumerable.Range(0, 9)) {
                var chr = LoadChar(data);
                chr.ID = _charIDs[c];
                chr.Recalculate(game);
                game.SaveData.Characters.Add(chr);
            }

            data.Position = 0x4f8 - 0x54;
            game.SaveData.Characters[data.ReadU8()].Flags |= CharFlags.Party1;
            game.SaveData.Characters[data.ReadU8()].Flags |= CharFlags.Party2;
            game.SaveData.Characters[data.ReadU8()].Flags |= CharFlags.Party3;

            data.Position = 0x4fc - 0x54;
            foreach(int _ in Enumerable.Range(0, 320)) {
                ushort value = data.ReadU16();
                int index = value & 0x1ff, qty = value >> 9;
                if (qty > 0)
                    game.SaveData.Inventory.Add(new InventoryItem {
                        ItemID = index,
                        Quantity = qty,
                    });
            }

            data.Position = 0x77c - 0x54;
            foreach(int _ in Enumerable.Range(0, 200)) {
                int value = data.ReadI32();
                if ((value & 0xff) != 0xff) {
                    game.SaveData.MateriaStock.Add(new OwnedMateria {
                        MateriaID = value & 0xff,
                        AP = value >> 8,
                    });
                }
            }

            data.Position = 0xb7c - 0x54;
            game.SaveData.Gil = data.ReadI32();

            //Can rely on md1stin to init everything else that needs it?!

            game.SaveMap.MenuLocked = MenuMask.Save;
            //Apparently we can't rely on md1stin to set this up?
        }

        private static Character LoadChar(Stream s) {
            var c = new Character();
            c.CharIndex = s.ReadU8();
            c.Level = s.ReadU8();
            c.Strength = s.ReadU8();
            c.Vitality = s.ReadU8();
            c.Magic = s.ReadU8();
            c.Spirit = s.ReadU8();
            c.Dexterity = s.ReadU8();
            c.Luck = s.ReadU8();
            c.StrBonus = s.ReadU8();
            c.VitBonus = s.ReadU8();
            c.MagBonus = s.ReadU8();
            c.SprBonus = s.ReadU8();
            c.DexBonus = s.ReadU8();
            c.LckBonus = s.ReadU8();
            c.LimitLevel = s.ReadU8();
            c.LimitBar = s.ReadU8();
            c.Name = Text.Convert(s.ReadBytes(12), 0).TrimEnd('\xE013');
            c.EquipWeapon = (sbyte)s.ReadU8();
            c.EquipArmour = (sbyte)s.ReadU8();
            c.EquipAccessory = (sbyte)s.ReadU8();
            c.Statuses = (Statuses)s.ReadU8();
            if (s.ReadU8() == 0xfe)
                c.Flags |= CharFlags.BackRow;
            s.ReadU8(); //TODO XPTNL bar
            
            ushort limits = s.ReadU16();
            if ((limits & 0x1) != 0)
                c.LimitBreaks |= LimitBreaks.Limit1_1;
            if ((limits & 0x2) != 0)
                c.LimitBreaks |= LimitBreaks.Limit1_2;
            if ((limits & 0x8) != 0)
                c.LimitBreaks |= LimitBreaks.Limit2_1;
            if ((limits & 0x10) != 0)
                c.LimitBreaks |= LimitBreaks.Limit2_2;
            if ((limits & 0x40) != 0)
                c.LimitBreaks |= LimitBreaks.Limit3_1;
            if ((limits & 0x80) != 0)
                c.LimitBreaks |= LimitBreaks.Limit3_2;
            if ((limits & 0x200) != 0)
                c.LimitBreaks |= LimitBreaks.Limit4_1;

            c.NumKills = s.ReadU16();
            c.UsedLimit1_1 = s.ReadU16();   
            c.UsedLimit2_1 = s.ReadU16();
            c.UsedLimit3_1 = s.ReadU16();
            
            c.CurrentHP = s.ReadU16();
            c.BaseHP = s.ReadU16();
            c.CurrentMP = s.ReadU16();
            c.BaseMP = s.ReadU16();

            s.ReadI32();

            c.MaxHP = s.ReadU16();
            c.MaxMP = s.ReadU16();

            c.XP = s.ReadI32();

            void DoMateria(List<OwnedMateria> list) {
                int value = s.ReadI32();
                if ((value & 0xff) != 255) {
                    list.Add(new OwnedMateria {
                        MateriaID = value & 0xff,
                        AP = value >> 8,
                    });
                }
            }
            foreach (int _ in Enumerable.Range(0, 8))
                DoMateria(c.WeaponMateria);
            foreach (int _ in Enumerable.Range(0, 8))
                DoMateria(c.ArmourMateria);

            c.XPTNL = s.ReadI32();

            return c;
        }
    }
}
