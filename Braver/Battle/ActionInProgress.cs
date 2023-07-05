// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Braver.Plugins.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Battle {

    public class ActionInProgress {

        private Dictionary<int?, List<IInProgress>> _items = new();
        private string _name;
        private Dictionary<IInProgress, int> _frames = new();
        private PluginInstances _plugins;

        public bool IsComplete => !_items.Keys.Any(phase => phase != null);

        public ActionInProgress(string name, PluginInstances plugins) {
            _name = name;
            _plugins = plugins;
            System.Diagnostics.Trace.WriteLine($"Starting new action {name}");
            _plugins.Call<UISystem>(ui => ui.BattleActionStarted(name));
        }

        public void Add(int? phase, IInProgress inProgress) {
            if (!_items.TryGetValue(phase, out var list))
                _items[phase] = list = new List<IInProgress>();
            list.Add(inProgress);
        }

        public void Step() {
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
                    if (!_frames.TryGetValue(list[i], out int frame)) {
                        frame = 0;
                        _plugins.Call<UISystem>(ui => ui.BattleActionResult(list[i]));
                    }
                    if (list[i].Step(frame) && !list[i].IsIndefinite)
                        list.RemoveAt(i);
                    else
                        _frames[list[i]] = frame + 1;   
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
        private int? _frames;
        private UI.UIBatch _ui;
        private float _alpha;
        private bool _announce;

        public bool IsIndefinite => _frames == null;

        public string Description => _announce ? _title : null;

        public BattleTitle(string title, int? frames, UI.UIBatch ui, float alpha, bool announce) {
            _title = title;
            _frames = frames;
            _ui = ui;
            _alpha = alpha;
            _announce = announce; 
        }

        public bool Step(int frame) {
            _ui.DrawBox(new Rectangle(0, 0, 1280, 55), 0.97f, _alpha);
            _ui.DrawText("main", _title, 640, 15, 0.98f, Color.White, UI.Alignment.Center);

            return frame >= _frames.GetValueOrDefault();
        }
    }

    public class BattleResultText : IInProgress {
        private int _frames;
        private UI.UIBatch _ui;
        private Color _color;
        private Func<Vector2> _start;
        private Vector2 _movement;
        private string _text;
        public bool IsIndefinite => false;
        public string Description { get; private set; }

        public BattleResultText(UI.UIBatch ui, string text, Color color, Func<Vector2> start, Vector2 movement, int frames, string description) {
            _ui = ui;
            _color = color;
            _text = text;
            _start = start;
            _movement = movement;
            _frames = frames;
            Description = description;
        }

        public bool Step(int frame) {
            var pos = _start() + _movement * frame;
            _ui.DrawText("batm", _text, (int)pos.X, (int)pos.Y, 0.96f, _color, UI.Alignment.Center);
            return frame++ >= _frames;
        }
    }

    public class EnemyDeath : IInProgress {
        private int _frames;
        private Model _model;

        public bool IsIndefinite => false;

        public string Description { get; private set; }

        public EnemyDeath(int frames, ICombatant combatant, Model model) {
            _frames = frames;
            _model = model;
            Description = combatant.Name + " died";
        }

        public bool Step(int frame) {
            if (frame < _frames) {
                _model.DeathFade = 0.33f - (0.33f * frame / _frames);
            } else {
                _model.DeathFade = null;
                _model.Visible = false;
            }
            return frame >= _frames;
        }
    }
}
