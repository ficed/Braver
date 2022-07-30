using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {
    internal class Quit : LayoutModel {

        public Box Root;
        public Label bYes, bNo;

        protected override void OnInit() {
            base.OnInit();
            PushFocus(Root, bYes);
        }

        public void Yes_Click() {
            InputEnabled = false;
            _screen.FadeOut(() => Environment.Exit(0));
        }

        public void No_Click() {
            _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
            _game.PopScreen(_screen);
        }

        public override bool ProcessInput(InputState input) {
            if (input.IsJustDown(InputKey.Cancel)) {
                No_Click();
                return true;
            }

            return base.ProcessInput(input);
        }
    }
}
