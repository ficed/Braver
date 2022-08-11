using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {

    public delegate float Easing(float t);

    public static class Easings {

        private static float _quadraticInOut(float t) {
            if (t <= 0.5f)
                return 2f * t * t;
            t -= 0.5f;
            return 2f * t * (1f - t) + 0.5f;
        }

        public static Easing Linear { get; } = f => f;
        public static Easing QuadraticInOut { get; } = _quadraticInOut;
    }

}
