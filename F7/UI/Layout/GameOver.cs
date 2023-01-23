using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {
    internal class GameOver : LayoutModel {

        protected override void OnInit() {
            base.OnInit();
            Game.Audio.PlayMusic("over2");
            _screen.FadeIn(null, 90);
        }
    }
}
