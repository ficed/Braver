// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Net {

    public class UIScreenMessage : ChangeScreenMessage {
        public override Screen GetScreen() => new UI.Layout.ClientScreen();

        public override void Load(NetDataReader reader) {
            //
        }

        public override void Save(NetDataWriter writer) {
            //
        }
    }

    public class UIStateMessage : ServerMessage {
        public string State { get; set; }
        public uint ClearColour { get; set; }

        public override void Load(NetDataReader reader) {
            ClearColour = reader.GetUInt();
            State = reader.GetString();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(ClearColour);
            writer.Put(State);
        }
    }

}
