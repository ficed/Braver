using Ficedula.FF7;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {
    public class EquipMenu : LayoutModel {

        public Label lWeapon, lArmour, lAccessory,
            lWeaponText, lArmourText, lAccessoryText,
            lDescription,
            lAttackFrom, lAttackPCFrom, lDefenseFrom, lDefensePCFrom, lMAttackFrom, lMDefFrom, lMDefPCFrom,
            lAttackTo, lAttackPCTo, lDefenseTo, lDefensePCTo, lMAttackTo, lMDefTo, lMDefPCTo;

        public Group gMenu, gStats;
        public List lbWeapons, lbArmour, lbAccessories;

        public override bool IsRazorModel => true;

        public Character Character => _game.SaveData.Party[(int)_screen.Param];
        public Weapon Weapon => Character.GetWeapon(_game);
        public Armour Armour => Character.GetArmour(_game);
        public Accessory Accessory => Character.GetAccessory(_game);

        public List<Weapon> AvailableWeapons { get; } = new();
        public List<Armour> AvailableArmour { get; } = new();
        public List<Accessory> AvailableAccessories { get; } = new();

        public override void Created(FGame g, LayoutScreen screen) {
            base.Created(g, screen);

            var kernel = _game.Singleton(() => new Kernel(_game.Open("kernel", "kernel.bin")));
            var weapons = _game.Singleton(() => new WeaponCollection(kernel));
            var armours = _game.Singleton(() => new ArmourCollection(kernel));
            var accessories = _game.Singleton(() => new AccessoryCollection(kernel));

            AvailableWeapons.Clear();
            AvailableWeapons.AddRange(
                g.SaveData.Inventory
                .Where(inv => inv.Kind == InventoryItemKind.Weapon)
                .Select(inv => inv.ItemID)
                .Concat(new[] { Character.EquipWeapon })
                .Where(id => id >= 0)
                .Distinct()
                .OrderBy(i => i)
                .Select(i => weapons.Weapons[i])
            );

            AvailableArmour.Clear();
            AvailableArmour.AddRange(
                g.SaveData.Inventory
                .Where(inv => inv.Kind == InventoryItemKind.Armour)
                .Select(inv => inv.ItemID)
                .Concat(new[] { Character.EquipArmour })
                .Where(id => id >= 0)
                .Distinct()
                .OrderBy(i => i)
                .Select(i => armours.Armour[i])
            );

            AvailableAccessories.Clear();
            AvailableAccessories.AddRange(
                g.SaveData.Inventory
                .Where(inv => inv.Kind == InventoryItemKind.Accessory)
                .Select(inv => inv.ItemID)
                .Concat(new[] { Character.EquipAccessory })
                .Where(id => id >= 0)
                .Distinct()
                .OrderBy(i => i)
                .Select(i => accessories.Accessories[i])
            );
        }

        protected override void OnInit() {
            base.OnInit();
            if (Focus == null) {
                PushFocus(gMenu, lWeapon);
            }
        }

        public override void CancelPressed() {
            if (FocusGroup == gMenu) {
                _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
                InputEnabled = false;
                _screen.FadeOut(() => _game.PopScreen(_screen));
            } else
                base.CancelPressed();
        }       

        public void MenuSelected(Label selected) {
            if (selected == lWeapon) {
                if (AvailableWeapons.Any()) {
                    PushFocus(lbWeapons, lbWeapons.Children[0]);
                } else
                    _game.Audio.PlaySfx(Sfx.Invalid, 1f, 0f);
            } else if (selected == lArmour) {
                if (AvailableArmour.Any()) {
                    PushFocus(lbArmour, lbArmour.Children[0]);
                } else
                    _game.Audio.PlaySfx(Sfx.Invalid, 1f, 0f);
            } else if (selected == lAccessory) {
                if (AvailableAccessories.Any()) {
                    PushFocus(lbAccessories, lbAccessories.Children[0]);
                } else
                    _game.Audio.PlaySfx(Sfx.Invalid, 1f, 0f);
            }

            UpdateControls();
        }

        private void UpdateControls() {
            lbWeapons.Visible = FocusGroup == lbWeapons;
            lbArmour.Visible = FocusGroup == lbArmour;
            lbAccessories.Visible = FocusGroup == lbAccessories;
            if (FocusGroup == gMenu)
                lDescription.Text = string.Empty;
        }

        private void SetLabels(int oldvalue, int? newvalue, Label lOld, Label lNew) {
            lOld.Text = oldvalue.ToString();
            if ((newvalue == null) || (newvalue.Value == oldvalue)) {
                lNew.Color = Color.White;
                lNew.Text = oldvalue.ToString();
            } else {
                lNew.Color = newvalue.Value < oldvalue ? Color.Red : Color.Yellow;
                lNew.Text = newvalue.Value.ToString();
            }
        }

        private void ResetAllLabels() {
            SetLabels(Character.Strength + Weapon.AttackStrength, null, lAttackFrom, lAttackTo);
            SetLabels(Weapon.HitChance, null, lAttackPCFrom, lAttackPCTo);
            SetLabels(Character.Vitality + Armour.Defense, null, lDefenseFrom, lDefenseTo);
            SetLabels(Character.Dexterity / 4 + Armour.DefensePercent, null, lDefensePCFrom, lDefensePCTo);
            SetLabels(Character.Spirit, null, lMAttackFrom, lMAttackTo);
            SetLabels(Character.Spirit + Armour.MDefense, null, lMDefFrom, lMDefTo);
            SetLabels(Armour.MDefensePercent, null, lMDefPCFrom, lMDefPCTo);
        }

        public void WeaponFocussed() {
            var selected = AvailableWeapons[lbWeapons.GetSelectedIndex(this)];
            lDescription.Text = selected.Description;
            ResetAllLabels();
            SetLabels(Character.Strength + Weapon.AttackStrength, Character.Strength + selected.AttackStrength, lAttackFrom, lAttackTo);
            SetLabels(Weapon.HitChance, selected.HitChance, lAttackPCFrom, lAttackPCTo);
        }

        public void ArmourFocussed() {
            var selected = AvailableArmour[lbArmour.GetSelectedIndex(this)];
            lDescription.Text = selected.Description;
            ResetAllLabels();
            SetLabels(Character.Vitality + Armour.Defense, Character.Vitality + selected.Defense, lDefenseFrom, lDefenseTo);
            SetLabels(Character.Dexterity / 4 + Armour.DefensePercent, Character.Dexterity / 4 + selected.DefensePercent, lDefensePCFrom, lDefensePCTo);
            SetLabels(Character.Spirit + Armour.MDefense, Character.Spirit + selected.MDefense, lMDefFrom, lMDefTo);
        }

        public void AccessoryFocussed() {
            //TODO - Str/Vit/Spr bonuses would affect things!
            lDescription.Text = AvailableAccessories[lbAccessories.GetSelectedIndex(this)].Description;
            ResetAllLabels();
        }

        public void WeaponSelected(Label L) {
            PopFocus();
            int id = int.Parse(L.ID.Substring(6));
            if (id != Character.EquipWeapon) {
                _game.SaveData.GiveInventoryItem(InventoryItemKind.Weapon, Character.EquipWeapon);
                _game.SaveData.TakeInventoryItem(InventoryItemKind.Weapon, id);
                Character.EquipWeapon = id;
                _screen.Reload();
            }
        }

        public void ArmourSelected(Label L) {
            PopFocus();
            int id = int.Parse(L.ID.Substring(6));
            if (id != Character.EquipArmour) {
                _game.SaveData.GiveInventoryItem(InventoryItemKind.Armour, Character.EquipArmour);
                _game.SaveData.TakeInventoryItem(InventoryItemKind.Armour, id);
                Character.EquipArmour = id;
                _screen.Reload();
            }
        }

        public void AccessorySelected(Label L) {
            PopFocus();
            int id = int.Parse(L.ID.Substring(9));
            if (id != Character.EquipAccessory) {
                _game.SaveData.GiveInventoryItem(InventoryItemKind.Accessory, Character.EquipAccessory);
                _game.SaveData.TakeInventoryItem(InventoryItemKind.Accessory, id);
                Character.EquipAccessory = id;
                _screen.Reload();
            }
        }

        public override bool ProcessInput(InputState input) {
            int charIndex = (int)_screen.Param;
            if (input.IsJustDown(InputKey.PanLeft)) {
                charIndex = (charIndex + _game.SaveData.Party.Length - 1) % _game.SaveData.Party.Length;
                _game.PopScreen(_screen);
                _game.PushScreen(new LayoutScreen("EquipMenu", parm: charIndex));
                return true;
            } else if (input.IsJustDown(InputKey.PanRight)) {
                charIndex = (charIndex + _game.SaveData.Party.Length - 1) % _game.SaveData.Party.Length;
                _game.PopScreen(_screen);
                _game.PushScreen(new LayoutScreen("EquipMenu", parm: charIndex));
                return true;
            } else
                return base.ProcessInput(input);
        }
    }
}
