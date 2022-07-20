using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace F7 {
    public abstract class Viewer {
        public abstract Matrix Projection { get; }
        public abstract Matrix View { get; }
    }

    public class View2D : Viewer {
        public int Width { get; set; }
        public int Height { get; set; }
        public int CenterX { get; set; }
        public int CenterY { get; set; }

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
            CameraPosition, CameraPosition + CameraPosition, CameraUp
        );
    }

    public class OrthoView3D : View3D {

        public float Width { get; set; }
        public float Height { get; set; }

        public override Matrix Projection => Matrix.CreateOrthographicOffCenter(
            -Width/2, Width/2, -Height/2, Height/2, ZNear, ZFar
        );
    }

    public class PerspView3D : View3D {

        public override Matrix Projection => Matrix.CreatePerspectiveFieldOfView(
            90f * (float)Math.PI / 180, AspectRatio, ZNear, ZFar
        );
    }
}
