// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

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
        public float ZNear { get; set; } = -1;
        public float ZFar { get; set; } = 1;

        public override Matrix Projection => Matrix.CreateOrthographicOffCenter(
                    CenterX - Width / 2, CenterX + Width / 2,
                    CenterY - Height / 2, CenterY + Height / 2,
                    ZNear, ZFar
                );
        public override Matrix View => Matrix.Identity;

        public View2D Clone() {
            return new View2D {
                Width = Width,
                Height = Height,
                CenterX = CenterX,
                CenterY = CenterY,
                ZNear = ZNear,
                ZFar = ZFar,
            };
        }

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

        public float FOV { get; set; } = 90f;
        public Vector2 ScreenOffset { get; set; }

        public PerspView3D Clone() {
            return new PerspView3D {
                AspectRatio = AspectRatio, 
                ZNear = ZNear,
                ZFar = ZFar,
                CameraPosition = CameraPosition,
                CameraUp = CameraUp,
                CameraForwards = CameraForwards,
                FOV = FOV,
                ScreenOffset = ScreenOffset,
            };
        }

        public override Matrix Projection => Matrix.CreatePerspectiveFieldOfView(
                                                FOV * (float)Math.PI / 180, AspectRatio, ZNear, ZFar
                                             )
                                           * Matrix.CreateTranslation(ScreenOffset.X, ScreenOffset.Y, 0);

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

        public Vector3 ProjectTo2D(Vector3 pos3D) {
            var pos = Vector4.Transform(pos3D, View * Projection);
            pos /= pos.W;
            return new Vector3(
                (pos.X + 1) * 1280f / 2f,
                720f - (pos.Y + 1) * 720f / 2f,
                pos.Z
            );
        }

        public override string ToString() {
            return $"Persp Z-range {ZNear}:{ZFar} Pos {CameraPosition} Fwd {CameraForwards} Up {CameraUp}";
        }

    }
}


