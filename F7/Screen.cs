using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {

    public abstract class Transition {
        private int _frames, _total;
        
        public Action OnComplete { get; private set; }

        protected Transition(int frames) {
            _total = frames;
        }

        protected abstract void DoRender(SpriteBatch fxBatch, float progress);

        public void Render(SpriteBatch fxBatch) {
            DoRender(fxBatch, 1f * _frames / _total);
        }

        public virtual bool Step() {
            _frames++;
            return _frames > _total;
        }
    }

    public class FadeTransition : Transition {
        private Color _color;
        private byte _from, _to;
        private UI.CompositeImages _images;

        public FadeTransition(int frames, Color color, byte from, byte to, UI.CompositeImages images) : base(frames) {
            _color = color;
            _from = from;
            _to = to;
            _images = images;
        }

        protected override void DoRender(SpriteBatch fxBatch, float progress) {
            fxBatch.Begin(sortMode: SpriteSortMode.Immediate, blendState: BlendState.AlphaBlend);

            _images.Find("white", out var tex, out var source, out bool flip);
            byte alpha = (byte)(_from + (_to - _from) * progress);
            fxBatch.Draw(tex, new Rectangle(0, 0, 9000, 9000), source, _color.WithAlpha(alpha));

            fxBatch.End();
        }
    }

    public abstract class Screen {

        public FGame Game { get; private set; }
        public GraphicsDevice Graphics { get; private set; }
        public bool InputEnabled { get; protected set; } = true;

        public abstract Color ClearColor { get; }

        protected SpriteBatch _fxBatch;
        
        private Transition _transition;
        private Action _transitionAction;

        public virtual void Init(FGame g, GraphicsDevice graphics) {
            Game = g;
            Graphics = graphics;
            _fxBatch = new SpriteBatch(graphics);
        }

        protected abstract void DoStep(GameTime elapsed);
        protected abstract void DoRender();

        public virtual void Reactivated() { }
        public virtual void Dispose() { }

        public void FadeOut(Action then) {
            _transition = new FadeTransition(
                60, Color.Black, 0, 255,
                Game.Singleton(() => new UI.CompositeImages(Graphics, Game))
            );
            _transitionAction = then;
        }
        public void FadeIn(Action then) {
            _transition = new FadeTransition(
                60, Color.Black, 255, 0,
                Game.Singleton(() => new UI.CompositeImages(Graphics, Game))
            );
            _transitionAction = then;
        }

        public void Step(GameTime elapsed) {
            DoStep(elapsed);
            if (_transition != null) {
                if (_transition.Step()) {
                    _transition = null;
                    _transitionAction?.Invoke();
                }
            }
        }

        public void Render() {
            DoRender();
            if (_transition != null)
                _transition.Render(_fxBatch);
        }

        public virtual void ProcessInput(InputState input) { }
    }
}
