// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Field;
using Ficedula.FF7.Field;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Braver.Field {

    public static class FieldUtil {
        public static Vector3 ToX(this FieldVertex v) {
            return new Vector3(v.X, v.Y, v.Z);
        }

        public static Vector3 GetMiddlePoint(this WalkmeshTriangle tri) {
            return (tri.V0.ToX() + tri.V1.ToX() + tri.V2.ToX()) / 3;
        }
    }

    public class FocusState {
        public string TargetName { get; set; }
        public Vector3 TargetPosition { get; set; }
        public int WalkmeshDistance { get; set; }
        public List<int> WalkmeshTriPoints { get; set; }
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
        int FieldID { get; }
        string FieldFile { get; }
        public FieldOptions Options { get; }
        Vector3 PlayerPosition { get; }
        IReadOnlyList<WalkmeshTriangle> Walkmesh { get; }
        FocusState GetFocusState();
        Vector2 Transform(Vector3 position);
    }

    public interface IFieldLocation : IPluginInstance {
        void Init(IField field);
        void Step();
        void Suspended();
        void EntityMoved(IFieldEntity entity, bool isRunning, Vector3 from, Vector3 to);
        void FocusChanged();
    }
}
