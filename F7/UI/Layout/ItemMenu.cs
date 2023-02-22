using Ficedula.FF7;
using SharpDX.MediaFoundation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {
	public class ItemMenu : LayoutModel {

		public Box Chars, Menu, Arrange;
		public List lbItems, lbKeyItems;
        public Group Char0, Char1, Char2;

		public Label lDescription, lUse, lArrange, lKey,
			lCustomise, lField, lBattle, lThrow, lType, lName, lMost, lLeast;

        protected override void OnInit() {
			base.OnInit();
			if (Focus == null) {
				PushFocus(Menu, lUse);
			}
		}

		public override void CancelPressed() {
			if (FocusGroup == Menu) {
				_game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
				InputEnabled = false;
				_screen.FadeOut(() => _game.PopScreen(_screen));
			} else
				base.CancelPressed();
			Arrange.Visible = FocusGroup == Arrange;
		}

		public void MenuSelected(Label selected) {
			if (selected == lKey) {
				lbItems.Visible = false;
				lbKeyItems.Visible = true;
				if (lbKeyItems.Children.Any())
					PushFocus(lbKeyItems, lbKeyItems.Children[0]);
			} else {
                lbItems.Visible = true;
                lbKeyItems.Visible = false;
				if (selected == lUse) {
					if (lbItems.Children.Any())
						PushFocus(lbItems, lbItems.Children[0]);
				} else if (selected == lArrange) {
					Arrange.Visible = true;
					PushFocus(Arrange, Arrange.Children[0]);
				}
            }
        }

		public void SelectChar(Group selected) {
		}

		public void ItemSelected(Group selected) {
		}

		public void ArrangeSelected(Label selected) {

		}

		public static (string Item, string Description) GetInventory(FGame game, int index) {
			return GetInventory(game, game.SaveData.Inventory[index]);
		}
		public static (string Item, string Description) GetInventory(FGame game, InventoryItem inv) {
			return GetInventory(game, inv.Kind, inv.ItemID);
		}
        public static (string Item, string Description) GetInventory(FGame game, InventoryItemKind kind, int itemID) {
                switch (kind) {
				case InventoryItemKind.Item:
					var item = game.Singleton<Items>()[itemID];
					return (item.Name, item.Description);
				case InventoryItemKind.Weapon:
					var weapon = game.Singleton<Weapons>()[itemID];
					return (weapon.Name, weapon.Description);
				case InventoryItemKind.Armour:
					var armour = game.Singleton<Armours>()[itemID];
					return (armour.Name, armour.Description);
				case InventoryItemKind.Accessory:
					var accessory = game.Singleton<Accessories>()[itemID];
                    return (accessory.Name, accessory.Description);
				default:
					throw new NotImplementedException();
            }
        }


        public void ItemFocussed() {
			lDescription.Text = GetInventory(_game, lbItems.GetSelectedIndex(this)).Description;
		}

        public void KeyItemFocussed() {
			var keyItem = _game.Singleton<KeyItems>().Items[_game.SaveData.KeyItems[lbKeyItems.GetSelectedIndex(this)]];
			lDescription.Text = keyItem.Description;
		}

    }
}
