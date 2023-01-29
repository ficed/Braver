using Braver.Battle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {
    public class BattleXPAP : LayoutModel {
        //TODO - animate numbers, display messages, ...

        public override bool IsRazorModel => true;

        public int XP { get; set; }
        public int AP { get; set; }

        public override void Created(FGame g, LayoutScreen screen) {
            base.Created(g, screen);
            var results = (BattleResults)screen.Param;
            
            XP = results.XP;
            AP = results.AP;
        }

        public override bool ProcessInput(InputState input) {
            
            if (input.IsJustDown(InputKey.OK)) {
                Game.PopScreen(_screen);
                return true;
            }

            return false;
        }
    }
}
