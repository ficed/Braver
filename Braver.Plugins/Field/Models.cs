// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Braver.Plugins.Field {

    public interface IModelLoader : IPluginInstance {
        FieldModelRenderer Load(BGame game, string category, string hrc);
    }

    public abstract class FieldModelRenderer {
        public abstract Vector3 MinBounds { get; }
        public abstract Vector3 MaxBounds { get; }
        public abstract int AnimationCount { get; }

        public abstract void Init(BGame game, GraphicsDevice graphics, string category, string hrc,
            IEnumerable<string> animations,
            uint? globalLightColour = null,
            uint? light1Colour = null, Vector3? light1Pos = null,
            uint? light2Colour = null, Vector3? light2Pos = null,
            uint? light3Colour = null, Vector3? light3Pos = null
        );
        public abstract int GetFrameCount(int anim);
        public abstract void Render(Vector3 modelPosition, Matrix view, Matrix projection, Matrix transform, int animation, int frame, bool eyeBlink, bool transparentGroups);
        public abstract void FrameStep();
        public abstract void ConfigureLighting(Vector3 ambient, bool shineEffect);

    }
}
