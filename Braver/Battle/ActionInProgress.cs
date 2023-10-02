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

    public interface IInProgress {
        string Description { get; }
        void FrameStep();
        void Cancel();
        bool IsComplete { get; }
    }

    public abstract class TimedInProgress : IInProgress {
        protected int _frames;
        protected int _frame;
        protected string _description;

        public string Description => _description;
        public bool IsComplete => _frame > _frames;

        protected float Progress => 1f * _frame / _frames;

        protected abstract void DoStep();

        public void FrameStep() {
            if (_frame <= _frames)
                DoStep();
            _frame++;
        }
        public virtual void Cancel() {
            _frame = _frames;
        }
    }

    public class BattleTitle : TimedInProgress {
        private string _title;
        private UI.UIBatch _ui;
        private float _alpha;
        private bool _announce;

        public BattleTitle(string title, int frames, UI.UIBatch ui, float alpha, bool announce) {
            _title = title;
            _frames = frames;
            _ui = ui;
            _alpha = alpha;
            _announce = announce;
            _description = _announce ? _title : null;
        }

        protected override void DoStep() {
            _ui.DrawBox(new Rectangle(0, 0, 1280, 55), 0.97f, _alpha);
            _ui.DrawText("main", _title, 640, 15, 0.98f, Color.White, UI.Alignment.Center);
        }

    }

    public class BattleResultText : TimedInProgress {
        private UI.UIBatch _ui;
        private Color _color;
        private Func<Vector2> _start;
        private Vector2 _movement;
        private string _text;
        public bool IsIndefinite => false;

        public BattleResultText(UI.UIBatch ui, string text, Color color, Func<Vector2> start, Vector2 movement, int frames, string description) {
            _ui = ui;
            _color = color;
            _text = text;
            _start = start;
            _movement = movement;
            _frames = frames;
            _description = description;
        }

        protected override void DoStep() {
            var pos = _start() + _movement * _frame;
            _ui.DrawText("batm", _text, (int)pos.X, (int)pos.Y, 0.96f, _color, UI.Alignment.Center);           
        }
    }

    public class EnemyDeath : TimedInProgress {
        private Model _model;

        public bool IsIndefinite => false;

        public EnemyDeath(int frames, ICombatant combatant, Model model) {
            _frames = frames;
            _model = model;
            _description = combatant.Name + " died";
        }

        protected override void DoStep() {
            _model.DeathFade = 0.33f - (0.33f * _frame / _frames);
        }

        public override void Cancel() {
            base.Cancel();
            _model.DeathFade = null;
            _model.Visible = false;
        }
    }
}
