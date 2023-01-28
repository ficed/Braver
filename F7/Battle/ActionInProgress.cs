using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Battle {

    public interface IInProgress {
        bool Step(GameTime elapsed); //return true if done
    }

    public class ActionInProgress {

        private List<IInProgress> _items = new();

        public void Add(IInProgress inProgress) {
            _items.Add(inProgress);
        }

        public bool Step(GameTime elapsed) {
            for(int i = _items.Count - 1; i >= 0; i--) {
                if (_items[i].Step(elapsed))
                    _items.RemoveAt(i);
            }
            return _items.Count == 0;
        }
    }

    public class BattleResultText : IInProgress {
        private int _frame, _frames;
        private UI.UIBatch _ui;
        private Color _color;
        private Func<Vector2> _start;
        private Vector2 _movement;
        private string _text;

        public BattleResultText(UI.UIBatch ui, string text, Color color, Func<Vector2> start, Vector2 movement, int frames) {
            _ui = ui;
            _color = color;
            _text = text;
            _start = start;
            _movement = movement;
            _frames = frames;
        }

        public bool Step(GameTime elapsed) {
            var pos = _start() + _movement * _frame;
            _ui.DrawText("batm", _text, (int)pos.X, (int)pos.Y, 0.98f, _color, UI.Alignment.Center);
            return _frame++ >= _frames;
        }
    }
}
