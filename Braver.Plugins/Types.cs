// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Plugins {
    public interface ITexLayer {
        Texture2D Tex { get; set; }
        VertexPositionTexture[] Verts { get; set; }
        IEnumerable<Ficedula.FF7.Field.Sprite> Sprites { get; set; }
        List<uint[]> Data { get; set; }
        Ficedula.FF7.Field.BlendType Blend { get; set; }
        int Parameter { get; set; }
        int Mask { get; set; }
        int OffsetX { get; set; }
        int OffsetY { get; set; }
        bool FixedZ { get; set; }
    }
}
