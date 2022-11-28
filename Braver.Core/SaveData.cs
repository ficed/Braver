using Ficedula.FF7;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Braver {

    public enum InventoryItemKind {
        Item,
        Weapon,
        Armour,
        Accessory,
        Blank,
    }

    public class InventoryItem {
        public InventoryItemKind Kind { get; set; }
        public int ItemID { get; set; }
        public int Quantity { get; set; }
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
        public int Strength { get; set; }
        public int Vitality { get; set; }
        public int Magic { get; set; }
        public int Spirit { get; set; }
        public int Dexterity { get; set; }
        public int Luck { get; set; }
        public int StrBonus { get; set; }
        public int VitBonus { get; set; }
        public int MagBonus { get; set; }
        public int SprBonus { get; set; }
        public int DexBonus { get; set; }
        public int LckBonus { get; set; }

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

        public List<OwnedMateria> WeaponMateria { get; set; }
        public List<OwnedMateria> ArmourMateria { get; set; }

        public string BattleModel { get; set; }

        [XmlIgnore]
        public float LevelProgress => 0.7f; //TODO!!!

        [XmlIgnore]
        public bool IsBackRow => Flags.HasFlag(CharFlags.BackRow);

        public Weapon GetWeapon(BGame game) {
            return EquipWeapon < 0 ? null : game.Singleton<Weapons>()[EquipWeapon];
        }
        public Armour GetArmour(BGame game) {
            return EquipArmour < 0 ? null : game.Singleton<Armours>()[EquipArmour];
        }
        public Accessory GetAccessory(BGame game) {
            return EquipAccessory < 0 ? null : game.Singleton<Accessories>()[EquipAccessory];
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

        public List<Character> Characters { get; set; }

        public List<InventoryItem> Inventory { get; set; }
        public List<int> KeyItems { get; set; }
        public List<OwnedMateria> MateriaStock { get; set; }

        public string WorldMapAvatar { get; set; }
        public float WorldMapX { get; set; }
        public float WorldMapY { get; set; }

        public Module Module { get; set; }

        public string Location { get; set; }
        public int Gil { get; set; }
        public int GameTimeSeconds { get; set; }

        [XmlIgnore]
        public Character[] Party => CharactersInParty().ToArray(); //Array so we can interop with Lua

        public void Loaded() {
            Characters.Sort((c1, c2) => c1.CharIndex.CompareTo(c2.CharIndex));
            foreach (int i in Enumerable.Range(0, 8)) {
                while (Characters.Count <= i)
                    Characters.Add(null);
                while ((Characters[i] != null) && (Characters[i].CharIndex > i))
                    Characters.Insert(i, null);
            }
        }

        public bool GiveInventoryItem(InventoryItemKind kind, int id, int quantity = 1) {
            var entry = Inventory.Find(inv => (inv.Kind == kind) && (inv.ItemID == id));
            if (entry != null) {
                if ((entry.Quantity + quantity) > 99) {
                    entry.Quantity = 99;
                    return false;
                }
                entry.Quantity += quantity;
            } else {
                entry = new InventoryItem { Kind = kind, ItemID = id, Quantity = quantity };
                int index = Inventory.FindIndex(inv => inv.Kind == InventoryItemKind.Blank);
                if (index >= 0)
                    Inventory[index] = entry;
                else
                    Inventory.Add(entry);
            }
            return true;
        }

        public bool TakeInventoryItem(InventoryItemKind kind, int id, int quantity = 1, bool takePartial = false) {
            var entry = Inventory.Find(inv => (inv.Kind == kind) && (inv.ItemID == id));
            if (entry == null) return false;
            if (entry.Quantity < quantity) {
                if (!takePartial) return false;
                entry.Quantity = 0;
            } else
                entry.Quantity -= quantity;

            if (entry.Quantity <= 0) {
                entry.Kind = InventoryItemKind.Blank;
                while (Inventory.Last().Kind == InventoryItemKind.Blank)
                    Inventory.RemoveAt(Inventory.Count - 1);
            }
            return true;
        }
    }
}
