// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Plugins.Field {
    public interface IFieldLocation : IPluginInstance {
        void Step();
        void EntityMoved(IFieldEntity entity, bool isRunning, Vector3 from, Vector3 to);
    }
}
