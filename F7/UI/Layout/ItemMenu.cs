using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {
	internal class ItemMenu : LayoutModel {

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


        public void ItemFocussed() {
			var item = _game.CacheItem<Item>(_game.SaveData.Inventory[lbItems.GetSelectedIndex(this)].ItemID);
			lDescription.Text = item.Description;
		}

        public void KeyItemFocussed() {
			var keyItem = _game.CacheItem<KeyItem>(_game.SaveData.KeyItems[lbKeyItems.GetSelectedIndex(this)]);
			lDescription.Text = keyItem.Description;
		}

    }
}
