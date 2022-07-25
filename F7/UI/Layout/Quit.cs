using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace F7.UI.Layout {
    internal class Quit : LayoutModel {

        public Box Root;
        public Label bYes, bNo;

        protected override void OnInit() {
            base.OnInit();
            PushFocus(Root, bYes);

            _game.Audio.PlayMusic("iseki.ogg");
        }

        public void Yes_Click() {
            InputEnabled = false;
            _screen.FadeOut(() => Environment.Exit(0));
        }

        public void No_Click() {
            _game.PopScreen(_screen);
        }
    }
}
