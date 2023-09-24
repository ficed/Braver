// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Braver.Plugins.Field;
using Ficedula.FF7;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using ZstdSharp.Unsafe;

namespace Braver.Field {

    public class AnimationState {
        public float AnimationSpeed { get; set; }
        public int Animation { get; set; }
        public int Frame { get; set; }
        public bool AnimationLoop { get; set; }
        public int StartFrame { get; set; }
        public int? EndFrame { get; set; }
        public int CompletionCount { get; set; }
    }

    public class FieldModel {

        private const int EYE_BLINK_PERIOD = 93;

        private Vector3 _rotation, _rotation2, _translation, _translation2;
        private float _scale;
        private bool _visible = true, _shineEffect, _eyeAnimation = true;
        private Vector3 _ambientLightColour;
        private AnimationState _animationState;
        private int _modelID;
        private float _globalAnimationSpeed = 1f;
        private Plugins.Field.FieldModelRenderer _renderer;

        private void DoSetNet(Action<Net.FieldModelMessage> setNet) {
            var msg = new Net.FieldModelMessage {
                ModelID = _modelID
            };
            setNet(msg);
            _game.Net.Send(msg);
        }
        private void DoSetNet<T>(ref T field, T value, Action<Net.FieldModelMessage> setNet) {
            field = value;
            var msg = new Net.FieldModelMessage {
                ModelID = _modelID
            };
            setNet(msg);
            _game.Net.Send(msg);
        }

        public Vector3 Rotation2 {
            get => _rotation2;
            set => DoSetNet(ref _rotation2, value, msg => msg.Rotation2 = value);
        }
        public Vector3 Rotation {
            get => _rotation;
            set => DoSetNet(ref _rotation, value, msg => msg.Rotation = value);
        }
        public Vector3 Translation {
            get => _translation;
            set {
                DoSetNet(ref _translation, value, msg => msg.Translation = value);
            }
        }
        public Vector3 Translation2 {
            get => _translation2;
            set => DoSetNet(ref _translation2, value, msg => msg.Translation2 = value);
        }
        public float Scale {
            get => _scale;
            set => DoSetNet(ref _scale, value, msg => msg.Scale = value);
        }
        public bool Visible {
            get => _visible;
            set => DoSetNet(ref _visible, value, msg => msg.Visible = value);
        }
        public AnimationState AnimationState {
            get => _animationState;
            set {
                if (value == null) System.Diagnostics.Debugger.Break();
                DoSetNet(ref _animationState, value, msg => msg.AnimationState = value);
            }
        }

        public Vector3 AmbientLightColour {
            get => _ambientLightColour;
            set {
                DoSetNet(ref _ambientLightColour, value, msg => msg.AmbientLightColour = value);
                _renderer.ConfigureLighting(_ambientLightColour, _shineEffect);
            }
        }

        public bool ShineEffect {
            get => _shineEffect;
            set {
                DoSetNet(ref _shineEffect, value, msg => msg.ShineEffect = value);
                _renderer.ConfigureLighting(_ambientLightColour, _shineEffect);
            }
        }

        public bool EyeAnimation {
            get => _eyeAnimation;
            set {
                DoSetNet(ref _eyeAnimation, value, msg => msg.EyeAnimation = value);
            }
        }

        public float GlobalAnimationSpeed {
            get => _globalAnimationSpeed;
            set {
                DoSetNet(ref _globalAnimationSpeed, value, msg => msg.GlobalAnimationSpeed = value);
            }
        }

        public int AnimationCount => _renderer.AnimationCount;
        public Vector3 MinBounds => _renderer.MinBounds;
        public Vector3 MaxBounds => _renderer.MaxBounds;
        public bool ZUp { get; set; } = true;

        private GraphicsDevice _graphics;
        private FGame _game;

        //TODO dedupe textures
        public FieldModel(GraphicsDevice graphics, FGame g, int modelID, string hrc, IEnumerable<string> animations, 
            PluginInstances<IModelLoader> loaders,
            string category = "field", uint? globalLightColour = null,
            uint? light1Colour = null, Vector3? light1Pos = null,
            uint? light2Colour = null, Vector3? light2Pos = null,
            uint? light3Colour = null, Vector3? light3Pos = null
            ) {
            _graphics = graphics;
            _game = g;
            _modelID = modelID;

            _eyeFrame = new Random(hrc.GetHashCode()).Next(EYE_BLINK_PERIOD);

            _renderer = loaders.Call(loader => loader.Load(g, category, hrc));
            _renderer.Init(
                g, graphics, category, hrc, animations,
                globalLightColour, light1Colour, light1Pos,
                light2Colour, light2Pos, light3Colour, light3Pos
            );

            PlayAnimation(0, true, 1f);
            System.Diagnostics.Trace.WriteLine($"Model {hrc} with min bounds {_renderer.MinBounds}, max {_renderer.MaxBounds}");
        }

        public bool IntersectsLine(Vector3 p0, Vector3 p1, float intersectDistance) {
            if ((Translation.Z - 5) > Math.Max(p0.Z, p1.Z)) return false; //TODO - close enough for now? ;)
            float entHeight = (MaxBounds.Y - MinBounds.Y) * Scale;
                        if ((Translation.Z + entHeight + 5) < Math.Min(p0.Z, p1.Z)) return false;

            return GraphicsUtil.LineCircleIntersect(p0.XY(), p1.XY(), Translation.XY(), intersectDistance);
        }

        public void Render(Viewer viewer, bool transparentGroups) {
            var transform = Matrix.CreateRotationX((ZUp ? -90 : 0) * (float)Math.PI / 180)
                    * Matrix.CreateRotationZ((Rotation.Z + Rotation2.Z) * (float)Math.PI / 180)
                    * Matrix.CreateRotationX((Rotation.X + Rotation2.X) * (float)Math.PI / 180)
                    * Matrix.CreateRotationY((Rotation.Y + Rotation2.Y) * (float)Math.PI / 180)
                    * Matrix.CreateScale(Scale, Scale, Scale)
                    * Matrix.CreateTranslation(Translation + Translation2);
            bool eyeBlink = EyeAnimation && ((_eyeFrame % EYE_BLINK_PERIOD) == 0);
            _renderer.Render(Translation, viewer.View, viewer.Projection, transform,
                AnimationState.Animation, AnimationState.Frame,
                eyeBlink, transparentGroups);
        }

        private float _animCountdown;
        private int _eyeFrame;

        public void FrameStep() {
            _eyeFrame++;
            _renderer.FrameStep();
            _animCountdown -= AnimationState.AnimationSpeed * GlobalAnimationSpeed;
            if (_animCountdown <= 0) {
                _animCountdown = 1;
                int actualEnd = AnimationState.EndFrame ?? _renderer.GetFrameCount(AnimationState.Animation) - 1;
                if (AnimationState.Frame == actualEnd) {
                    if (AnimationState.AnimationLoop) {
                        AnimationState.Frame = AnimationState.StartFrame;
                        AnimationState.CompletionCount++;
                    } else
                        AnimationState.CompletionCount = 1;
                } else {
                    AnimationState.Frame++;
                }
            }
        }

        public void PlayAnimation(int animation, bool loop, float speed, int startFrame = 0, int? endFrame = null) {
            if ((endFrame ?? 0) >= _renderer.GetFrameCount(animation)) {
                System.Diagnostics.Trace.WriteLine($"Clamping out of range animation frames {endFrame}->{_renderer.GetFrameCount(animation) - 1}");
                endFrame = _renderer.GetFrameCount(animation) - 1;
            }
            AnimationState = new AnimationState {
                Animation = animation,
                AnimationLoop = loop,
                AnimationSpeed = speed,
                StartFrame = startFrame,
                Frame = startFrame,
                EndFrame = endFrame,
            }; 
            _animCountdown = 1;
        }

    }
}
