using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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


    public class FieldModelMessage : NetMessage {
        public int EntityID { get; set; }
        public bool? Visible { get; set; }
        public Vector3? Translation { get; set; }
        public Vector3? Rotation { get; set; }
        public Field.AnimationState AnimationState { get; set; }

        public override void Load(NetDataReader reader) {
            EntityID = reader.GetInt();
            foreach(int index in SetBits(reader.GetInt())) {
                switch (index) {
                    case 0:
                        Visible = reader.GetBool(); break;
                    case 1:
                        Translation = reader.GetVec3(); break;
                    case 2:
                        Rotation = reader.GetVec3(); break;
                    case 3:
                        AnimationState = new Field.AnimationState {
                            Animation = reader.GetInt(),
                            AnimationLoop = reader.GetBool(),
                            AnimationSpeed = reader.GetFloat(),
                            EndFrame = reader.GetInt(),
                            Frame = reader.GetInt(),
                            StartFrame = reader.GetInt(),
                        };
                        break;
                }
            }
        }

        public override void Save(NetDataWriter writer) {
            writer.Put(EntityID);
            writer.Put(GenerateMask(Visible.HasValue, Translation.HasValue, Rotation.HasValue, AnimationState != null));
            if (Visible.HasValue)
                writer.Put(Visible.Value);
            if (Translation.HasValue)
                writer.Put(Translation.Value);
            if (Rotation.HasValue)
                writer.Put(Rotation.Value);
            if (AnimationState != null) {
                writer.Put(AnimationState.Animation);
                writer.Put(AnimationState.AnimationLoop);
                writer.Put(AnimationState.AnimationSpeed);
                writer.Put(AnimationState.EndFrame);
                writer.Put(AnimationState.Frame);
                writer.Put(AnimationState.StartFrame);
            }
        }
    }
}
