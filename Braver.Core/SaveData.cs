// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Braver {

    public class InventoryItem {
        public int ItemID { get; set; }
        public int Quantity { get; set; }

        public const int ITEM_ID_CUTOFF = 128;
        public const int WEAPON_ID_CUTOFF = 256;
        public const int ARMOUR_ID_CUTOFF = 288;
        public const int ACCESSORY_ID_CUTOFF = 320;
    }

    [Flags]
    public enum MenuMask {
        None = 0,
        Item = 0x1,
        Magic = 0x2,
        Materia = 0x4,
        Equip = 0x8,
        Status = 0x10,
        Order = 0x20,
        Limit = 0x40,
        Config = 0x80,
        PHS = 0x100,
        Save = 0x200,
    }

    public enum Module {
        WorldMap,
        Field,
    }

    [Flags]
    public enum CharFlags {
        None = 0,
        Sadness = 0x1,
        Fury = 0x2,
        BackRow = 0x4,

        Party1 = 0x10,
        Party2 = 0x20,
        Party3 = 0x40,

        Available = 0x100,
        Locked = 0x200,

        ANY_PARTY_SLOT = Party1 | Party2 | Party3,
    }

    [Flags]
    public enum LimitBreaks {
        None = 0, //Not really valid
        Limit1_1 = 0x1,
        Limit1_2 = 0x2,
        Limit2_1 = 0x4,
        Limit2_2 = 0x8,
        Limit3_1 = 0x10,
        Limit3_2 = 0x20,
        Limit4_1 = 0x40,
        Limit4_2 = 0x80,
    }

    public enum ElementResistance {
        None,
        
        Death,
        AutoHit,
        Weak,
        
        Resist,
        Immune,
        Absorb,
        Recovery,

    }

    public class OwnedMateria {
        public int MateriaID { get; set; }
        public int AP { get; set; }
    }

    public class Character {
        public int CharIndex { get; set; }
        public string ID { get; set; }
        public int Level { get; set; }

        //Base values
        public int BaseStrength { get; set; }
        public int BaseVitality { get; set; }
        public int BaseMagic { get; set; }
        public int BaseSpirit { get; set; }
        public int BaseDexterity { get; set; }
        public int BaseLuck { get; set; }

        //Bonuses from sources
        public int StrBonus { get; set; }
        public int VitBonus { get; set; }
        public int MagBonus { get; set; }
        public int SprBonus { get; set; }
        public int DexBonus { get; set; }
        public int LckBonus { get; set; }

        //Final values
        public int Strength { get; set; }
        public int Vitality { get; set; }
        public int Magic { get; set; }
        public int Spirit { get; set; }
        public int Dexterity { get; set; }
        public int Luck { get; set; }

        public int LimitLevel { get; set; }
        public int LimitBar { get; set; }

        public string Name { get; set; }

        public int EquipWeapon { get; set; }
        public int EquipArmour { get; set; }
        public int EquipAccessory { get; set; }

        public CharFlags Flags { get; set; }
        public LimitBreaks LimitBreaks { get; set; }
        public int NumKills { get; set; }
        public int UsedLimit1_1 { get; set; }
        public int UsedLimit2_1 { get; set; }
        public int UsedLimit3_1 { get; set; }

        public int CurrentHP { get; set; }
        public int BaseHP { get; set; }
        public int MaxHP { get; set; }
        public int CurrentMP { get; set; }
        public int BaseMP { get; set; }
        public int MaxMP { get; set; }
        public int XP { get; set; }
        public int XPTNL { get; set; }

        public Statuses Statuses { get; set; }

        public List<OwnedMateria> WeaponMateria { get; set; } = new();
        public List<OwnedMateria> ArmourMateria { get; set; } = new();

        [XmlIgnore]
        public float LevelProgress => 0.7f; //TODO!!!

        [XmlIgnore]
        public bool IsBackRow => Flags.HasFlag(CharFlags.BackRow);

        [XmlIgnore]
        public int PartyIndex {
            get {
                switch(Flags & CharFlags.ANY_PARTY_SLOT) {
                    case CharFlags.Party1:  return 0;
                    case CharFlags.Party2:  return 1;
                    case CharFlags.Party3:  return 2;
                    default:                return -1;
                }
            }
        }

        public IEnumerable<(Materia Materia, int AP, int Level)> EquippedMateria(BGame game) {
            var materias = game.Singleton<Materias>();
            foreach(var mat in WeaponMateria.Concat(ArmourMateria).Where(m => m != null)) {
                var materia = materias[mat.MateriaID];
                int level = Enumerable.Range(0, materia.APLevels.Count)
                    .Where(lvl => materia.APLevels[lvl] <= mat.AP)
                    .Count() + 1;
                yield return (materia, mat.AP, level);
            }
        }

        public Weapon GetWeapon(BGame game) {
            if (EquipWeapon < 0) return null;
            var w = game.Singleton<Weapons>()[EquipWeapon];
            while (WeaponMateria.Count < w.MateriaSlots.Count)
                WeaponMateria.Add(null);
            while (WeaponMateria.Count > w.MateriaSlots.Count) {
                game.SaveData.MateriaStock.Add(WeaponMateria[w.MateriaSlots.Count]);
                WeaponMateria.RemoveAt(w.MateriaSlots.Count);
            }
            return w;
        }
        public Armour GetArmour(BGame game) {
            if (EquipArmour < 0) return null;
            var a = game.Singleton<Armours>()[EquipArmour];
            while (ArmourMateria.Count < a.MateriaSlots.Count)
                ArmourMateria.Add(null);
            while (ArmourMateria.Count > a.MateriaSlots.Count) {
                game.SaveData.MateriaStock.Add(ArmourMateria[a.MateriaSlots.Count]);
                ArmourMateria.RemoveAt(a.MateriaSlots.Count);
            }
            return a;
        }
        public Accessory GetAccessory(BGame game) {
            return EquipAccessory < 0 ? null : game.Singleton<Accessories>()[EquipAccessory];
        }

        public void Recalculate(BGame game) {

            Strength = BaseStrength + StrBonus;
            Vitality = BaseVitality + VitBonus;
            Magic = BaseMagic + MagBonus;
            Spirit = BaseSpirit + SprBonus;
            Dexterity = BaseDexterity + DexBonus;
            Luck = BaseLuck + LckBonus;

            void Apply(EquipItem equip) {
                if (equip == null) return;
                Strength += equip.StrBonus;
                Vitality += equip.VitBonus;
                Magic += equip.MagBonus;
                Spirit += equip.SprBonus;
                Dexterity += equip.DexBonus;
                Luck += equip.LckBonus;
            }

            Apply(GetWeapon(game));
            Apply(GetArmour(game));
            Apply(GetAccessory(game));

            MaxHP = BaseHP;
            MaxMP = BaseMP;

            foreach(var materia in EquippedMateria(game)) {
                Strength += materia.Materia.EquipEffect.Strength;
                Vitality += materia.Materia.EquipEffect.Vitality;
                Magic += materia.Materia.EquipEffect.Magic;
                Spirit += materia.Materia.EquipEffect.Spirit;
                Dexterity += materia.Materia.EquipEffect.Dexterity;
                Luck += materia.Materia.EquipEffect.Luck;

                MaxHP = MaxHP * (100 + materia.Materia.EquipEffect.MaxHP) / 100;
                MaxMP = MaxMP * (100 + materia.Materia.EquipEffect.MaxMP) / 100;

                if (materia.Materia is IndependentMateria0 im0) {
                    switch (im0.Kind) {
                        case IndependentMateriaKind.StrengthPlus:
                            Strength += im0.Amounts[materia.Level];
                            break;
                        case IndependentMateriaKind.VitalityPlus:
                            Vitality += im0.Amounts[materia.Level];
                            break;
                        case IndependentMateriaKind.MagicPlus:
                            Magic += im0.Amounts[materia.Level];
                            break;
                        case IndependentMateriaKind.SpiritPlus:
                            Spirit += im0.Amounts[materia.Level];
                            break;
                        case IndependentMateriaKind.SpeedPlus:
                            Dexterity += im0.Amounts[materia.Level];
                            break;
                        case IndependentMateriaKind.LuckPlus:
                            Luck += im0.Amounts[materia.Level];
                            break;
                        case IndependentMateriaKind.MaxHPPlus:
                            MaxHP = MaxHP * (100 + im0.Amounts[materia.Level]) / 100;
                            break;
                        case IndependentMateriaKind.MaxMPPlus:
                            MaxMP = MaxMP * (100 + im0.Amounts[materia.Level]) / 100;
                            break;
                    }
                }
            }

            CurrentHP = Math.Min(CurrentHP, MaxHP);
            CurrentMP = Math.Min(CurrentMP, MaxMP);
        }
    }

    public class SaveData {

        private IEnumerable<Character> CharactersInParty() {
            var p1 = Characters.Where(c => c != null && c.Flags.HasFlag(CharFlags.Party1)).FirstOrDefault();
            if (p1 != null) yield return p1;
            var p2 = Characters.Where(c => c != null && c.Flags.HasFlag(CharFlags.Party2)).FirstOrDefault();
            if (p2 != null) yield return p2;
            var p3 = Characters.Where(c => c != null && c.Flags.HasFlag(CharFlags.Party3)).FirstOrDefault();
            if (p3 != null) yield return p3;
        }

        public List<Character> Characters { get; set; } = new();

        public List<InventoryItem> Inventory { get; set; } = new();
        public List<int> KeyItems { get; set; } = new();
        public List<OwnedMateria> MateriaStock { get; set; } = new();

        public int FieldAvatarCharID { get; set; }
        public string WorldMapAvatar { get; set; }
        public float WorldMapX { get; set; }
        public float WorldMapY { get; set; }

        public Ficedula.FF7.Field.FieldDestination FieldDestination { get; set; }
        public Module Module { get; set; }

        public int FieldDangerCounter { get; set; }

        public int LastRandomBattleID { get; set; }

        public string Location { get; set; }
        public int Gil { get; set; }


        [XmlIgnore]
        public Character[] Party {
            get => CharactersInParty().ToArray(); //Array so we can interop
            set {
                foreach (var chr in Characters)
                    if (chr != null)
                        chr.Flags &= ~CharFlags.ANY_PARTY_SLOT;

                var members = value.Where(c => c != null);
                if (members.Count() > 0)
                    members.ElementAt(0).Flags |= CharFlags.Party1;
                if (members.Count() > 1)
                    members.ElementAt(1).Flags |= CharFlags.Party2;
                if (members.Count() > 2)
                    members.ElementAt(2).Flags |= CharFlags.Party3;
            }
        }

        public void CleanUp() {
            Characters.Sort((c1, c2) => (c1?.CharIndex ?? 999).CompareTo(c2?.CharIndex ?? 999));
            foreach (int i in Enumerable.Range(0, 8)) {
                while (Characters.Count <= i)
                    Characters.Add(null);
                while ((Characters[i] != null) && (Characters[i].CharIndex > i))
                    Characters.Insert(i, null);
            }

            int lastValid = MateriaStock.FindLastIndex(m => m != null);
            if (lastValid < (MateriaStock.Count - 1))
                MateriaStock.RemoveRange(lastValid + 1, MateriaStock.Count - lastValid - 1);
        }

        public void GiveMateria(OwnedMateria materia) {
            int space = MateriaStock.IndexOf(null);
            if (space >= 0)
                MateriaStock[space] = materia;
            else
                MateriaStock.Add(materia);
        }
        public bool GiveInventoryItem(int id, int quantity = 1) {
            var entry = Inventory.Find(inv => inv.ItemID == id);
            if (entry != null) {
                if ((entry.Quantity + quantity) > 99) {
                    entry.Quantity = 99;
                    return false;
                }
                entry.Quantity += quantity;
            } else {
                entry = new InventoryItem { ItemID = id, Quantity = quantity };
                int index = Inventory.FindIndex(inv => inv.ItemID == -1);
                if (index >= 0)
                    Inventory[index] = entry;
                else
                    Inventory.Add(entry);
            }
            return true;
        }

        public bool TakeInventoryItem(int id, int quantity = 1, bool takePartial = false) {
            var entry = Inventory.Find(inv => inv.ItemID == id);
            if (entry == null) return false;
            if (entry.Quantity < quantity) {
                if (!takePartial) return false;
                entry.Quantity = 0;
            } else
                entry.Quantity -= quantity;

            if (entry.Quantity <= 0) {
                entry.ItemID = -1;
                while (Inventory.Last().ItemID == -1)
                    Inventory.RemoveAt(Inventory.Count - 1);
            }
            return true;
        }
    }
}
