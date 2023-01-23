using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Battle {
    public class BattleDebug : BattleScreen {
        public override Color ClearColor => Color.Black;

        private UI.UIBatch _ui;

        public BattleDebug(BattleFlags flags) {
            _flags = flags;
        }

        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);
            _ui = new UI.UIBatch(graphics, g);
        }

        protected override void DoRender() {
            _ui.Reset();
            _ui.DrawText("main", "Up: Win battle", 600, 100, 0.1f, Color.White);
            _ui.DrawText("main", "Down: Lose battle", 600, 130, 0.1f, Color.White);
            _ui.Render();
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);
            if (input.IsJustDown(InputKey.Up))
                TriggerBattleWin();
            else if (input.IsJustDown(InputKey.Down))
                TriggerBattleLose();
        }

        protected override void DoStep(GameTime elapsed) {
        }
    }
}
