using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {
    public abstract class Viewer {
        public abstract Matrix Projection { get; }
        public abstract Matrix View { get; }
    }

    public class View2D : Viewer {
        public float Width { get; set; }
        public float Height { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }

        public override Matrix Projection => Matrix.CreateOrthographicOffCenter(
                    CenterX - Width / 2, CenterX + Width / 2,
                    CenterY - Height / 2, CenterY + Height / 2,
                    -1, 1
                );
        public override Matrix View => Matrix.Identity;
    }

    public abstract class View3D : Viewer {
        public float AspectRatio { get; set; } = 1280f / 720f;
        public float ZNear { get; set; } = 10f;
        public float ZFar { get; set; } = 10000f;
        public Vector3 CameraPosition { get; set; }
        public Vector3 CameraUp { get; set; }
        public Vector3 CameraForwards { get; set; }

        public override Matrix View => Matrix.CreateLookAt(
            CameraPosition, CameraPosition + CameraForwards, CameraUp
        );
    }

    public class OrthoView3D : View3D {

        public float Width { get; set; }
        public float Height { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }

        public override Matrix Projection => Matrix.CreateOrthographicOffCenter(
                    CenterX - Width / 2, CenterX + Width / 2,
                    CenterY - Height / 2, CenterY + Height / 2,
                    ZNear, ZFar
                );

        public override string ToString() {
            return $"Ortho Z-range {ZNear}:{ZFar} W/H {Width}/{Height} Center {CenterX}/{CenterY} Pos {CameraPosition} Fwd {CameraForwards} Up {CameraUp}";
        }
    }

    public class PerspView3D : View3D {

        public PerspView3D Clone() {
            return new PerspView3D {
                AspectRatio = AspectRatio, 
                ZNear = ZNear,
                ZFar = ZFar,
                CameraPosition = CameraPosition,
                CameraUp = CameraUp,
                CameraForwards = CameraForwards,
            };
        }

        public override Matrix Projection => Matrix.CreatePerspectiveFieldOfView(
            90f * (float)Math.PI / 180, AspectRatio, ZNear, ZFar
        );

        public PerspView3D Blend(PerspView3D other, float factor) {
            return new PerspView3D {
                AspectRatio = other.AspectRatio, //not going to change anyway...?
                ZNear = ZNear * (1 - factor) + other.ZNear * factor,
                ZFar = ZFar * (1 - factor) + other.ZFar * factor,
                CameraPosition = CameraPosition * (1 - factor) + other.CameraPosition * factor,
                CameraUp = CameraUp * (1 - factor) + other.CameraUp * factor,
                CameraForwards = CameraForwards * (1 - factor) + other.CameraForwards * factor,
            };
        }

        public override string ToString() {
            return $"Persp Z-range {ZNear}:{ZFar} Pos {CameraPosition} Fwd {CameraForwards} Up {CameraUp}";
        }

    }
}

