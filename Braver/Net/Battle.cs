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
    public class BattleScreenMessage : ChangeScreenMessage {

        public int BattleID { get; set; }

        public override Screen GetScreen() {
            return new Battle.RealBattleScreen(BattleID, Battle.BattleFlags.None);
        }

        public override void Load(NetDataReader reader) {
            BattleID = reader.GetInt();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(BattleID);
        }
    }

    public class AddBattleModelMessage : ServerMessage {
        public string Code { get; set; }
        public Vector3 Position { get; set; }
        public int ID { get; set; }

        public override void Load(NetDataReader reader) {
            Code = reader.GetString();
            Position = reader.GetVec3();
            ID = reader.GetInt();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(Code);
            writer.Put(Position);
            writer.Put(ID);
        }
    }
}
