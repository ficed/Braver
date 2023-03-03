// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

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
