using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {
    internal static class NewGame {
        public static void Init(BGame game) {
            game.Memory.ResetAll();
            //Can rely on md1stin to init everything that needs it?!
        }
    }
}
