// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

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

		public static (string Item, string Description) GetInventoryByIndex(FGame game, int index) {
			return GetInventory(game, game.SaveData.Inventory[index]);
		}
		public static (string Item, string Description) GetInventory(FGame game, InventoryItem inv) {
			return GetInventory(game, inv.ItemID);
		}
		public static (string Item, string Description) GetInventory(FGame game, int itemID) {
			if (itemID < InventoryItem.ITEM_ID_CUTOFF) {
				var item = game.Singleton<Items>()[itemID];
				return (item.Name, item.Description);
			} else if (itemID < InventoryItem.WEAPON_ID_CUTOFF) {
				var weapon = game.Singleton<Weapons>()[itemID - 128];
				return (weapon.Name, weapon.Description);
			} else if (itemID < InventoryItem.ARMOUR_ID_CUTOFF) {
				var armour = game.Singleton<Armours>()[itemID - 256];
				return (armour.Name, armour.Description);
			} else if (itemID < InventoryItem.ACCESSORY_ID_CUTOFF) {
				var accessory = game.Singleton<Accessories>()[itemID - 288];
				return (accessory.Name, accessory.Description);
			} else
				throw new NotImplementedException();
		}


        public void ItemFocussed() {
			lDescription.Text = GetInventoryByIndex(_game, lbItems.GetSelectedIndex(this)).Description;
		}

        public void KeyItemFocussed() {
			var keyItem = _game.Singleton<KeyItems>().Items[_game.SaveData.KeyItems[lbKeyItems.GetSelectedIndex(this)]];
			lDescription.Text = keyItem.Description;
		}

    }
}
