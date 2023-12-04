// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using LiteNetLib.Utils;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Net {
    public class WMScreenMessage : ChangeScreenMessage {
        public float AvatarX { get; set; }
        public float AvatarY { get; set; }
        public string Avatar { get; set; }
        public override Screen GetScreen() {
            return new WorldMap.WMScreen(AvatarX, AvatarY, Avatar);
        }

        public override void Load(NetDataReader reader) {
            AvatarX = reader.GetFloat();
            AvatarY = reader.GetFloat();
            Avatar = reader.GetString();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(AvatarX);
            writer.Put(AvatarY);
            writer.Put(Avatar);
        }
    }

    public class WMStatusMessage : ServerMessage {
        public string AvatarAnimation { get; set; }
        public Vector3 AvatarPosition { get; set; }
        public Vector3 AvatarRotation { get; set; }
        public Vector3 CameraOffset { get; set; }
        public bool PanMode { get; set; }

        public override void Load(NetDataReader reader) {
            AvatarAnimation = reader.GetString();
            AvatarPosition = reader.GetVec3();
            AvatarRotation = reader.GetVec3();
            CameraOffset = reader.GetVec3();
            PanMode = reader.GetBool();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(AvatarAnimation);
            writer.Put(AvatarPosition);
            writer.Put(AvatarRotation);
            writer.Put(CameraOffset);
            writer.Put(PanMode);
        }
    }
}
