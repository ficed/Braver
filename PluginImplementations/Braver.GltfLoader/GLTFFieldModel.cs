// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Braver.Plugins.Field;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;

namespace Braver.Field {

    public class GltfConfig { }

    public class GltfPlugin : Plugin {
        private GltfConfig _config = new GltfConfig();
        public override string Name => "GLTF Model Loader";
        public override Version Version => new Version(0, 0, 1);
        public override object ConfigObject => _config;

        public override IEnumerable<IPluginInstance> Get(string context, Type t) {
            if (t == typeof(IModelLoader)) {
                yield return new GltfModelLoader();
            }
        }

        public override IEnumerable<Type> GetPluginInstances() {
            yield return typeof(IModelLoader);
        }

        public override void Init(BGame game) {
            //
        }
    }

    public class GltfModelLoader : IModelLoader, IDisposable {

        private List<GLTFFieldModel> _models = new();

        public void Dispose() {
            foreach (var model in _models)
                model.Dispose();
            _models.Clear();
        }

        public Plugins.Field.FieldModelRenderer Load(BGame game, string category, string hrc) {
            using (var s = game.TryOpen(category, Path.ChangeExtension(hrc, ".glb"))) {
                if (s != null) {
                    var model = new GLTFFieldModel();
                    _models.Add(model);
                    return model;
                }
            }
            return null;
        }

    }

    internal class GLTFFieldModel : Plugins.Field.FieldModelRenderer, IDisposable {

        private SharpGLTF.Runtime.MonoGameDeviceContent<SharpGLTF.Runtime.MonoGameModelTemplate> _content;
        private SharpGLTF.Runtime.MonoGameModelInstance _model;
        private Vector3 _minBounds, _maxBounds;
        private Vector3? _light1Pos, _light2Pos, _light3Pos;
        private bool _shineEffect;
        private int _shineRotation;

        private List<int> _animIndices;

        public override Vector3 MinBounds => _minBounds;
        public override Vector3 MaxBounds => _maxBounds;
        public override int AnimationCount => _animIndices.Count;

        private void UpdateEffects(Action<SkinnedEffect> updater) {
            foreach (var effect in _model.Template.Effects.OfType<SkinnedEffect>())
                updater(effect);
        }

        public override void ConfigureLighting(Vector3 ambient, bool shineEffect) {
            _shineEffect = shineEffect;
            UpdateEffects(fx => fx.AmbientLightColor = ambient);
        }

        public override void FrameStep() {
            _shineRotation++;
        }

        public override int GetFrameCount(int anim) {
            return (int)Math.Ceiling(_model.Controller.Armature.AnimationTracks[_animIndices[anim]].Duration * 30); //TODO - 30fps???
        }

        public override void Init(BGame game, GraphicsDevice graphics, string category, string hrc, IEnumerable<string> animations, uint? globalLightColour = null, uint? light1Colour = null, Vector3? light1Pos = null, uint? light2Colour = null, Vector3? light2Pos = null, uint? light3Colour = null, Vector3? light3Pos = null) {
            using(var s = game.Open(category, Path.ChangeExtension(hrc, ".glb"))) {
                var model = SharpGLTF.Schema2.ModelRoot.ReadGLB(s);
                _content = SharpGLTF.Runtime.MonoGameModelTemplate.CreateDeviceModel(graphics, model);
                _model = _content.Instance.CreateInstance();

                var maxVector = Vector3.One;
                maxVector.Normalize();
                _maxBounds = _content.Instance.Bounds.Center + maxVector * _content.Instance.Bounds.Radius;
                _minBounds = _content.Instance.Bounds.Center - maxVector * _content.Instance.Bounds.Radius;

                var allAnims = _model.Controller.Armature.AnimationTracks.ToList();
                _animIndices = animations
                    .Select(anim => allAnims.FindIndex(a => a.Name == Path.GetFileNameWithoutExtension(anim)))
                    .ToList();
            }

            if (globalLightColour != null) {
                _light1Pos = light1Pos.Value;
                _light2Pos = light2Pos.Value;
                _light3Pos = light3Pos.Value;

                var ambient = new Color(globalLightColour.Value).ToVector3();
                UpdateEffects(fx => {
                    fx.AmbientLightColor = ambient;
                    fx.DirectionalLight0.Enabled = fx.DirectionalLight1.Enabled =
                        fx.DirectionalLight2.Enabled = true;
                    fx.DirectionalLight0.DiffuseColor = new Color(light1Colour.Value).ToVector3();
                    fx.DirectionalLight1.DiffuseColor = new Color(light2Colour.Value).ToVector3();
                    fx.DirectionalLight2.DiffuseColor = new Color(light3Colour.Value).ToVector3();
                });


            } else {
                UpdateEffects(fx => fx.AmbientLightColor = Vector3.One);
            }
        }

        public override void Render(Vector3 modelPosition, Matrix view, Matrix projection, Matrix transform, int animation, int frame, bool eyeBlink, bool transparentGroups) {
            
            if (_light1Pos != null) {
                Matrix lightRotate;
                int r = (_shineRotation * 6) % 720;
                if (_shineEffect && (r < 360))
                    lightRotate = Matrix.CreateRotationZ(r * (float)Math.PI / 180);
                else
                    lightRotate = Matrix.Identity;

                Vector3 GetLightDirection(Vector3? pos) {
                    var direction = modelPosition - pos.Value;
                    direction.Normalize();
                    return Vector3.Transform(direction, lightRotate);
                }

                Vector3 dir1 = GetLightDirection(_light1Pos),
                    dir2 = GetLightDirection(_light2Pos),
                    dir3 = GetLightDirection(_light3Pos);

                UpdateEffects(fx => {
                    fx.DirectionalLight0.SpecularColor = _shineEffect ? Vector3.One : Vector3.Zero;
                    fx.DirectionalLight0.Direction = dir1;
                    fx.DirectionalLight1.Direction = dir2;
                    fx.DirectionalLight2.Direction = dir3;
                });
            }


            _model.Controller.Armature.SetAnimationFrame(
                _animIndices[animation],
                frame / 30f //TODO?!
            );            

            transform = Matrix.CreateRotationZ((float)Math.PI) * transform;
            //_model.Template._Meshes[0].Effects.First().
            _model.Draw(projection, view, transform);
        }

        public void Dispose() {
            _content.Dispose();
        }
    }
}
