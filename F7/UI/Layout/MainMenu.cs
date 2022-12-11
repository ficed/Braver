using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {
    internal class MainMenu : LayoutModel {

        public Box Chars, Menu;
        public Group Char0, Char1, Char2;

        public Label lItem, lMagic, lMateria, lEquip, lStatus, lOrder, lLimit,
            lConfig, lPHS, lSave, lQuit;

        public Label lTimeHrs, lTimeC1, lTimeMins, lTimeC2, lTimeSecs;

        protected override void OnInit() {
            base.OnInit();
            if (Focus == null) {
                PushFocus(Menu, lItem);
            }
        }

        public override void Step() {
            base.Step();
            lTimeHrs.Text = (_game.SaveData.GameTimeSeconds / (60 * 60)).ToString();
            lTimeMins.Text = ((_game.SaveData.GameTimeSeconds / 60) % 60).ToString("00");
            lTimeSecs.Text = (_game.SaveData.GameTimeSeconds % 60).ToString("00");
            lTimeC1.Color = ((lTimeMins.Text == "00") && (lTimeSecs.Text == "00")) ? Color.Gray : Color.White;
            lTimeC2.Color = lTimeSecs.Text == "00" ? Color.Gray : Color.White;
        }

        public override void CancelPressed() {
            if (FocusGroup == Menu) {
                _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
                InputEnabled = false;
                _screen.FadeOut(() => _game.PopScreen(_screen));
            } else
                base.CancelPressed();
        }

        public void MenuSelected(Label selected) {
            if (selected == lOrder) {
                PushFocus(Chars, Char0);
            } else if (selected == lItem) {
                _game.PushScreen(new LayoutScreen("ItemMenu"));
            } else if (selected == lEquip) {
                _game.PushScreen(new LayoutScreen("EquipMenu", parm: 0));
            } else if (selected == lMateria) {
                _game.PushScreen(new LayoutScreen("MateriaMenu", parm: 0));
            } else if (selected == lSave) {
                _game.PushScreen(new LayoutScreen("SaveMenu"));
            } else if (selected == lQuit) {
                _game.PushScreen(new LayoutScreen("Quit"));
            }
        }

        public void SelectChar(Group selected) {
            var groups = new List<Group> { Char0, Char1, Char2 };
            if (FlashFocus == null) {
                FlashFocus = selected;
            } else if (FlashFocus == selected) {
                Character chr = _game.SaveData.Party[groups.IndexOf(selected)];
                chr.Flags ^= CharFlags.BackRow;
                FlashFocus = null;
                _screen.Reload();
            } else {
                int from = groups.IndexOf(FlashFocus as Group), to = groups.IndexOf(selected);
                Character cFrom = _game.SaveData.Party[from],
                    cTo = _game.SaveData.Party[to];
                CharFlags fSlot = cFrom.Flags & CharFlags.ANY_PARTY_SLOT,
                    tSlot = cTo.Flags & CharFlags.ANY_PARTY_SLOT;
                cFrom.Flags = (cFrom.Flags & ~CharFlags.ANY_PARTY_SLOT) | tSlot;
                cTo.Flags = (cTo.Flags & ~CharFlags.ANY_PARTY_SLOT) | fSlot;
                FlashFocus = null;
                _screen.Reload();
            }
        }
    }
}
