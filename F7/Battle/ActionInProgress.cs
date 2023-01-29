using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Battle {

    public interface IInProgress {
        bool Step(GameTime elapsed); //return true if done
    }

    public class ActionInProgress {

        private List<IInProgress> _items = new();
        private string _name;

        public ActionInProgress(string name) {
            _name = name;
            System.Diagnostics.Debug.WriteLine($"Starting new action {name}");
        }

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

        public override string ToString() => _name;
    }

    public class BattleTitle : IInProgress {
        private string _title;
        private int _frame, _frames;
        private UI.UIBatch _ui;
        private float _alpha;

        public BattleTitle(string title, int frames, UI.UIBatch ui, float alpha) {
            _title = title;
            _frames = frames;
            _ui = ui;
            _alpha = alpha;
        }

        public bool Step(GameTime elapsed) {
            _ui.DrawBox(new Rectangle(0, 0, 1280, 55), 0.97f, _alpha);
            _ui.DrawText("main", _title, 640, 15, 0.98f, Color.White, UI.Alignment.Center);

            return _frame++ >= _frames;
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
            _ui.DrawText("batm", _text, (int)pos.X, (int)pos.Y, 0.96f, _color, UI.Alignment.Center);
            return _frame++ >= _frames;
        }
    }

    public class EnemyDeath : IInProgress {
        private int _frame, _frames;
        private Model _model;

        public EnemyDeath(int frames, Model model) {
            _frames = frames;
            _model = model;
        }

        public bool Step(GameTime elapsed) {
            if (_frame < _frames) {
                _model.DeathFade = 0.33f - (0.33f * _frame / _frames);
            } else {
                _model.DeathFade = null;
                _model.Visible = false;
            }
            return _frame++ >= _frames;
        }
    }
}
