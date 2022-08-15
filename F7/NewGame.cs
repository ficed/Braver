using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {
    internal static class NewGame {
        public static void Init(FGame game) {
            game.SaveMap.MenuLocked = MenuMask.PHS | MenuMask.Save;
            game.SaveMap.MenuHidden = MenuMask.PHS;
            game.SaveMap.PPV = 0;
        }
    }
}
