using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Battle {

    public interface IInProgress {
        bool Step(GameTime elapsed); //return true if done
        bool IsIndefinite { get; }
    }

    public class ActionInProgress {

        private Dictionary<int?, List<IInProgress>> _items = new();
        private string _name;

        public bool IsComplete => !_items.Keys.Any(phase => phase != null);

        public ActionInProgress(string name) {
            _name = name;
            System.Diagnostics.Trace.WriteLine($"Starting new action {name}");
        }

        public void Add(int? phase, IInProgress inProgress) {
            if (!_items.TryGetValue(phase, out var list))
                _items[phase] = list = new List<IInProgress>();
            list.Add(inProgress);
        }

        public void Step(GameTime elapsed) {
            if (IsComplete) return;

            int phase = _items
                .Keys
                .Where(phase => phase != null)
                .Select(phase => phase.Value)
                .Min();

            var lists = _items
                .Where(kv => (kv.Key == null) || (kv.Key == phase))
                .Select(kv => kv.Value);

            foreach(var list in lists) {
                for (int i = list.Count - 1; i >= 0; i--) {
                    if (list[i].Step(elapsed) && !list[i].IsIndefinite)
                        list.RemoveAt(i);
                }
            }

            var toRemove = _items
                .Where(kv => !kv.Value.Any())
                .Select(kv => kv.Key)
                .ToArray();
            foreach(var key in toRemove)
                _items.Remove(key);
        }

        public override string ToString() => _name;
    }

    public class BattleTitle : IInProgress {
        private string _title;
        private int _frame;
        private int? _frames;
        private UI.UIBatch _ui;
        private float _alpha;

        public bool IsIndefinite => _frames == null;

        public BattleTitle(string title, int? frames, UI.UIBatch ui, float alpha) {
            _title = title;
            _frames = frames;
            _ui = ui;
            _alpha = alpha;
        }

        public bool Step(GameTime elapsed) {
            _ui.DrawBox(new Rectangle(0, 0, 1280, 55), 0.97f, _alpha);
            _ui.DrawText("main", _title, 640, 15, 0.98f, Color.White, UI.Alignment.Center);

            return _frame++ >= _frames.GetValueOrDefault();
        }
    }

    public class BattleResultText : IInProgress {
        private int _frame, _frames;
        private UI.UIBatch _ui;
        private Color _color;
        private Func<Vector2> _start;
        private Vector2 _movement;
        private string _text;
        public bool IsIndefinite => false;

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

        public bool IsIndefinite => false;

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
