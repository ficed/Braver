using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Field {
    public class Shake {

        private struct ShakeKind {
            public int XDuration, YDuration, XDistance, YDistance;
            public float CurrentXTarget, CurrentYTarget;
        }

        private int _frame;
        private ShakeKind _kind, _next;
        private float _xOffset, _yOffset;

        private static float[] _randomAmplitude = new[] {
           -1f, 1.14f, -1.34f, 0.61f, -0.49f, 0.64f, -0.25f, 0.47f, -1.20f, -0.01f,
           1.14f, -0.86f, 0.81f, -0.58f, 0.68f, -1.31f, 0.44f, -0.91f, 0.03f, -0.97f,
           1.25f, -0.76f, 0.48f, -0.99f, 1.34f, -0.85f, 0.55f, -0.65f, 0.07f, -0.83f
        };
        private int _randomOffset;

        public void Queue(int xDuration, int yDuration, int xDistance, int yDistance) {
            _next = new ShakeKind {
                XDuration = xDuration,
                YDuration = yDuration,
                XDistance = xDistance,
                YDistance = yDistance
            };
        }

        public void Apply(View2D view2D, PerspView3D view3D) {
            view2D.CenterX += _xOffset;
            view2D.CenterY += _yOffset;

            view3D.ScreenOffset += new Vector2(
                -_xOffset * 3f / 1280, -_yOffset * 3f / 720
            );
        }

        public void Step() {
            if ((_kind.XDuration == 0) && (_kind.YDuration == 0)) {
                _xOffset = _yOffset = 0;
                _frame = 0;
                _kind = _next;
                return;
            }

            if ((_frame % Math.Max(_kind.XDuration, _kind.YDuration)) == 0) {
                _kind = _next;
            }

            if (_kind.XDistance != 0) {
                if ((_frame % _kind.XDuration) == 0)
                    _kind.CurrentXTarget = _kind.XDistance * 0.5f * _randomAmplitude[_randomOffset++ % _randomAmplitude.Length];
            } else
                _kind.CurrentXTarget = 0;

            if (_kind.YDistance != 0) {
                if ((_frame % _kind.YDuration) == 0)
                    _kind.CurrentYTarget = _kind.YDistance * 0.5f * _randomAmplitude[_randomOffset++ % _randomAmplitude.Length];
            } else
                _kind.CurrentYTarget = 0;

            float GetOffset(float distance, int duration) {
                if (distance == 0) return 0;

                float progress = 1f * (_frame % duration) / duration;
                if (progress > 0.5f)
                    progress = 1f - Easings.QuadraticInOut(2f * (1f - progress));
                else
                    progress = Easings.QuadraticInOut(progress * 2);
                return distance * progress;
            }

            _xOffset = GetOffset(_kind.CurrentXTarget, _kind.XDuration);
            _yOffset = GetOffset(_kind.CurrentYTarget, _kind.YDuration);

            _frame++;
        }
    }
}
