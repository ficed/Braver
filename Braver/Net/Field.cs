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

namespace Braver.Net {

    public class FieldScreenMessage : ChangeScreenMessage {

        public Ficedula.FF7.Field.FieldDestination Destination { get; set; }

        public override Screen GetScreen() {
            return new Field.FieldScreen(Destination);
        }

        public override void Load(NetDataReader reader) {
            Destination = new Ficedula.FF7.Field.FieldDestination {
                X = reader.GetShort(),
                Y = reader.GetShort(),
                Triangle = reader.GetUShort(),
                DestinationFieldID = reader.GetShort(),
                Orientation = reader.GetByte(),
            };
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(Destination.X);
            writer.Put(Destination.Y);
            writer.Put(Destination.Triangle);
            writer.Put(Destination.DestinationFieldID);
            writer.Put(Destination.Orientation);
        }
    }

    public class FieldBGScrollMessage : ServerMessage {
        public float X { get; set; }
        public float Y { get; set; }

        public override void Load(NetDataReader reader) {
            X = reader.GetFloat();
            Y = reader.GetFloat();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(X);
            writer.Put(Y);
        }
    }

    public class FieldBGMessage : ServerMessage {
        public int Parm { get; set; }
        public int Value { get; set; }

        public override void Load(NetDataReader reader) {
            Parm = reader.GetInt();
            Value = reader.GetInt();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(Parm);
            writer.Put(Value);
        }
    }

    public class FieldEntityModelMessage : ServerMessage {
        public int EntityID { get; set; }
        public int ModelID { get; set; }

        public override void Load(NetDataReader reader) {
            EntityID = reader.GetInt();
            ModelID = reader.GetInt();
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(EntityID);
            writer.Put(ModelID);
        }
    }

    public class FieldModelMessage : ServerMessage {
        public int ModelID { get; set; }
        public bool? Visible { get; set; }
        public Vector3? Translation { get; set; }
        public Vector3? Translation2 { get; set; }
        public Vector3? Rotation { get; set; }
        public Vector3? Rotation2 { get; set; }
        public float? Scale { get; set; }
        public Field.AnimationState AnimationState { get; set; }
        public Vector3? AmbientLightColour { get; set; }
        public bool? ShineEffect { get; set; }
        public bool? EyeAnimation { get; set; }
        public float? GlobalAnimationSpeed { get; set; }

        public override void Load(NetDataReader reader) {
            ModelID = reader.GetInt();
            foreach (int index in SetBits(reader.GetInt())) {
                switch (index) {
                    case 0:
                        Visible = reader.GetBool(); break;
                    case 1:
                        Translation = reader.GetVec3(); break;
                    case 2:
                        Translation2 = reader.GetVec3(); break;
                    case 3:
                        Rotation = reader.GetVec3(); break;
                    case 4:
                        Rotation2 = reader.GetVec3(); break;
                    case 5:
                        Scale = reader.GetFloat(); break;
                    case 6:
                        AnimationState = new Field.AnimationState {
                            Animation = reader.GetInt(),
                            AnimationLoop = reader.GetBool(),
                            AnimationSpeed = reader.GetFloat(),
                            EndFrame = Utils.MapToNull(reader.GetInt(), -1),
                            Frame = reader.GetInt(),
                            StartFrame = reader.GetInt(),
                        };
                        break;
                    case 7:
                        AmbientLightColour = reader.GetVec3(); break;
                    case 8:
                        ShineEffect = reader.GetBool(); break;
                    case 9:
                        EyeAnimation = reader.GetBool(); break;
                    case 10:
                        GlobalAnimationSpeed = reader.GetFloat(); break;
                }

            }
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(ModelID);
            writer.Put(GenerateMask(
                Visible.HasValue, Translation.HasValue, Translation2.HasValue,
                Rotation.HasValue, Rotation2.HasValue, Scale.HasValue, AnimationState != null,
                AmbientLightColour.HasValue, ShineEffect.HasValue,
                EyeAnimation.HasValue, GlobalAnimationSpeed.HasValue
            ));
            if (Visible.HasValue)
                writer.Put(Visible.Value);
            if (Translation.HasValue)
                writer.Put(Translation.Value);
            if (Translation2.HasValue)
                writer.Put(Translation2.Value);
            if (Rotation.HasValue)
                writer.Put(Rotation.Value);
            if (Rotation2.HasValue)
                writer.Put(Rotation2.Value);
            if (Scale.HasValue)
                writer.Put(Scale.Value);
            if (AnimationState != null) {
                writer.Put(AnimationState.Animation);
                writer.Put(AnimationState.AnimationLoop);
                writer.Put(AnimationState.AnimationSpeed);
                writer.Put(AnimationState.EndFrame ?? -1);
                writer.Put(AnimationState.Frame);
                writer.Put(AnimationState.StartFrame);
            }
            if (AmbientLightColour.HasValue)
                writer.Put(AmbientLightColour.Value);
            if (ShineEffect.HasValue)
                writer.Put(ShineEffect.Value);
            if (EyeAnimation.HasValue)
                writer.Put(EyeAnimation.Value);
            if (GlobalAnimationSpeed.HasValue)
                writer.Put(GlobalAnimationSpeed.Value);

        }
    }
}
