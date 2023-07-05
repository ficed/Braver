// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Field;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Field {

    public class FocusState {
        public string TargetName { get; set; }
        public Vector3 TargetPosition { get; set; }
        public int WalkmeshDistance { get; set; }
    }

    [Flags]
    public enum FieldOptions {
        None = 0,
        PlayerControls = 0x1,
        //LinesActive = 0x2,
        MenuEnabled = 0x4,
        CameraTracksPlayer = 0x8,
        CameraIsAsyncScrolling = 0x10,
        MusicLocked = 0x20,
        UseMovieCam = 0x40,
        GatewaysEnabled = 0x80,
        ShowPlayerHand = 0x100,

        NoScripts = 0x1000,

        DEFAULT = PlayerControls | MenuEnabled | CameraTracksPlayer | UseMovieCam | GatewaysEnabled | ShowPlayerHand,
    }

}

namespace Braver.Plugins.Field {
    public interface IField {
        Vector3 PlayerPosition { get; }
        FocusState GetFocusState();
        public FieldOptions Options { get; }
    }

    public interface IFieldLocation : IPluginInstance {
        void Step(IField field);
        void Suspended();
        void EntityMoved(IFieldEntity entity, bool isRunning, Vector3 from, Vector3 to);
        void FocusChanged();
    }
}
