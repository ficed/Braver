// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Battle;
using Ficedula;
using Ficedula.FF7;
using LiteNetLib.Utils;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Braver.Net {

    public class SwirlMessage : ChangeScreenMessage {
        public override Screen GetScreen() {
            return new Battle.Swirl();
        }

        public override void Load(NetDataReader reader) {
            //
        }

        public override void Save(NetDataWriter writer) {
            //
        }
    }

    public class BattleScreenMessage : ChangeScreenMessage {

        public int BattleID { get; set; }

        public override Screen GetScreen() {
            return new Battle.ClientBattleScreen(BattleID);
        }

        public override void Load(NetDataReader reader) {
            BattleID = reader.GetInt();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(BattleID);
        }
    }

    public class SetBattleCameraMessage : ServerMessage {
        public Ficedula.FF7.Battle.BattleCamera Camera { get; set; }

        public override void Load(NetDataReader reader) {
            var ms = new MemoryStream(reader.GetBytesWithLength());
            Camera = new Ficedula.FF7.Battle.BattleCamera(ms);
        }

        public override void Save(NetDataWriter writer) {
            var ms = new MemoryStream();
            Camera.Save(ms);
            writer.PutBytesWithLength(ms.ToArray());
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

    public class ClientMenuItem : ICharacterAction, IMenuSource {
        public Ability? Ability { get; set; }
        public string Name { get; set; }
        public TargettingFlags TargetFlags { get; set; }
        public int? Annotation { get; set; }

        public List<ClientMenuItem> SubItems { get; set; } = new();

        IEnumerable<ICharacterAction> IMenuSource.Actions => SubItems ?? Enumerable.Empty<ICharacterAction>();

        public ClientMenuItem() { }
        public ClientMenuItem(CharacterAction action) {
            Ability = action.Ability;
            Name = action.Name;
            TargetFlags = action.TargetFlags;
            Annotation = action.Annotation; //TODO - won't live update!
            if (action.SubMenu != null)
                SubItems = action.SubMenu.Select(a => new ClientMenuItem(a)).ToList();
        }

        public ClientMenuItem(CharacterActionItem action) {
            Ability = action.Ability;
            Name = action.Name;
            TargetFlags = action.TargetFlags;
            Annotation = action.Annotation?.Invoke(); //TODO!            
        }
    }

    public class CharacterReadyMessage : ServerMessage, IMenuSource {
        public List<ClientMenuItem> Actions { get; set; } = new();
        public int CharIndex { get; set; }

        IEnumerable<ICharacterAction> IMenuSource.Actions => Actions;

        public override void Load(NetDataReader reader) {
            CharIndex = reader.GetInt();
            Actions = reader.GetStringArray()
                .Select(s => Serialisation.Deserialise<ClientMenuItem>(s))
                .ToList();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(CharIndex);
            writer.PutArray(
                Actions
                .Select(a => Serialisation.SerialiseString(a))
                .ToArray()
            );
        }
    }

    public class TargetOption {
        public List<int> TargetIDs { get; set; }
        public bool MustTargetWholeGroup { get; set; }
        public bool IsDefault { get; set; }
        public int? SingleTarget { get; set; }
        public int? DefaultSingleTarget { get; set; }
    }

    public class TargetOptionsMessage : ServerMessage {
        public List<TargetOption> Options { get; set; } = new();
        public Ability Ability { get; set; }

        public override void Load(NetDataReader reader) {
            Ability = Serialisation.Deserialise<Ability>(reader.GetString());
            Options = reader.GetStringArray()
                .Select(s => Serialisation.Deserialise<TargetOption>(s)) 
                .ToList();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(Serialisation.SerialiseString(Ability));
            writer.PutArray(Options.Select(o => Serialisation.SerialiseString(o)).ToArray());
        }
    }


    #region Client Messages

    public class CycleBattleMenuMessage : ClientMessage {
        public int CurrentCharIndex { get; set; }

        public override void Load(NetDataReader reader) {
            CurrentCharIndex = reader.GetInt();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(CurrentCharIndex);
        }
    }

    public class GetTargetOptionsMessage : ClientMessage {
        public Ability Ability { get; set; }
        public int SourceCharIndex { get; set; }
        public TargettingFlags TargettingFlags { get; set; }

        public override void Load(NetDataReader reader) {
            SourceCharIndex = reader.GetInt();
            TargettingFlags = (TargettingFlags)reader.GetInt();
            Ability = Serialisation.Deserialise<Ability>(reader.GetString());
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(SourceCharIndex);
            writer.Put((int)TargettingFlags);
            writer.Put(Serialisation.SerialiseString(Ability));
        }
    }

    public class QueueActionMessage : ClientMessage {
        public int SourceCharIndex { get; set; }
        public Ability Ability { get; set; }
        public List<int> TargetIDs { get; set; }
        public string Name { get; set; }

        public override void Load(NetDataReader reader) {
            SourceCharIndex = reader.GetInt();
            Name = reader.GetString();
            TargetIDs = reader.GetIntArray().ToList();
            Ability = Serialisation.Deserialise<Ability>(reader.GetString());
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(SourceCharIndex);
            writer.Put(Name);
            writer.PutArray(TargetIDs.ToArray());
            writer.Put(Serialisation.SerialiseString(Ability));
        }
    }

    #endregion
}
