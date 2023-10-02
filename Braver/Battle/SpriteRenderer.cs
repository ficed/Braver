// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Battle {

    public class LoadedSprite {

        private Dictionary<(int page, int palette), Texture2D> _textures = new();
        private Ficedula.FF7.Battle.Sprite _sprite;

        public LoadedSprite(FGame g, GraphicsDevice graphics, string spriteFile, IEnumerable<string> textures) {

            using (var s = g.Open("battle", spriteFile))
                _sprite = new Ficedula.FF7.Battle.Sprite(s);

            int basePage = _sprite
                .Frames
                .SelectMany(f => f.Draws)
                .Select(d => d.TexturePage)
                .Min();

            var required = _sprite
                .Frames
                .SelectMany(f => f.Draws)
                .Select(d => new { Page = d.TexturePage, Palette = d.Flags >> 8 })
                .Distinct();

            var texFiles = textures
                .Select(fn => {
                    using (var s = g.Open("battle", fn))
                        return new Ficedula.FF7.TexFile(s);
                })
                .ToArray();

            foreach(var reqTexture in required) {
                var tex = graphics.LoadTex(texFiles[reqTexture.Page - basePage], reqTexture.Palette);
                _textures[(reqTexture.Page, reqTexture.Palette)] = tex;
            }
        }

        public class Instance {
            private int _progress;
            private Dictionary<int, int> _active = new Dictionary<int, int>(); //Frame->Remaining
            private LoadedSprite _sprite;

            public bool IsActive => _active.Any();

            public Instance(LoadedSprite sprite) {
                _sprite = sprite;
                FrameStep();
            }

            public void FrameStep() {
                foreach (int activeFrame in _active.Keys.ToArray()) {
                    if (_active[activeFrame] == 1)
                        _active.Remove(activeFrame);
                    else
                        _active[activeFrame]--;
                }

                if ((_progress % 4) == 0) {
                    int frame = _progress / 4;
                    if (frame < _sprite._sprite.Frames.Count) {
                        int duration = (_sprite._sprite.Frames[frame].Unknown & 0x80) == 0 ? 4 : 8; //VERY TODO
                        _active[frame] = duration;
                    }
                }
                _progress++;
            }

            public void Draw(SpriteBatch spriteBatch, Ficedula.FF7.BlendType blend, Vector3 pos) {
                if (blend != Ficedula.FF7.BlendType.Additive) return; //VERY TODO

                foreach(int activeFrame in _active.Keys) {
                    foreach (var draw in _sprite._sprite.Frames[activeFrame].Draws) {
                        var tex = _sprite._textures[(draw.TexturePage, draw.Flags >> 8)];
                        spriteBatch.Draw(
                            tex,
                            new Rectangle((int)pos.X + draw.X, (int)pos.Y + draw.Y, draw.Width1, draw.Height1),
                            new Rectangle(draw.SrcX, draw.SrcY, draw.Width1, draw.Height1),
                            Color.White, 0, Vector2.Zero, SpriteEffects.None,
                            pos.Z
                        );
                    }
                }
            }

        }

    }

    public class SpriteManager {
        private Dictionary<string, LoadedSprite> _sprites = new(StringComparer.InvariantCultureIgnoreCase);

        private FGame _game;
        private GraphicsDevice _graphics;
        
        public SpriteManager(FGame game, GraphicsDevice graphics) {
            _game = game;
            _graphics = graphics;
        }

        public LoadedSprite Get(string spriteFile, IEnumerable<string> textures) {
            string key = spriteFile + "," + string.Join(",", textures);
            if (!_sprites.TryGetValue(key, out var sprite))
                _sprites[key] = sprite = new LoadedSprite(_game, _graphics, spriteFile, textures);
            return sprite;
        }
    }

    public class SpriteRenderer {

        private List<(LoadedSprite.Instance sprite, Func<Vector3> pos)> _sprites = new();
        private SpriteBatch _spriteBatch;
        private GraphicsDevice _graphics;

        public SpriteRenderer(GraphicsDevice graphics) {
            _spriteBatch = new SpriteBatch(graphics);
            _graphics = graphics;
        }

        public LoadedSprite.Instance Add(LoadedSprite sprite,  Func<Vector3> getPos) {
            var instance = new LoadedSprite.Instance(sprite);
            _sprites.Add((instance, getPos));
            return instance;
        }

        public void FrameStep() {
            for(int i = _sprites.Count - 1; i >= 0; i--) {
                _sprites[i].sprite.FrameStep();
                if (!_sprites[i].sprite.IsActive)
                    _sprites.RemoveAt(i);
            }
        }

        public void Render() {
            float scale = _graphics.Viewport.Width / 1280f;
            using (var state = new GraphicsState(_graphics, forceSaveAll: true)) {
                foreach (var blend in Enum.GetValues<Ficedula.FF7.BlendType>()) {
                    BlendState blendState;
                    switch (blend) {
                        case Ficedula.FF7.BlendType.Blend:
                            blendState = BlendState.AlphaBlend;
                            break;
                        case Ficedula.FF7.BlendType.Additive:
                            blendState = BlendState.Additive;
                            break;
                        case Ficedula.FF7.BlendType.Subtractive:
                            blendState = GraphicsUtil.BlendSubtractive;
                            break;
                        case Ficedula.FF7.BlendType.QuarterAdd:
                            blendState = GraphicsUtil.BlendQuarterAdd;
                            break;
                        default:
                            continue;
                    }
                    _spriteBatch.Begin(
                        blendState: blendState,
                        depthStencilState: DepthStencilState.None,
                        transformMatrix: Matrix.CreateScale(scale, scale, 1f)
                    );
                    foreach (var instance in _sprites) {
                        instance.sprite.Draw(_spriteBatch, blend, instance.pos());
                    }
                    _spriteBatch.End();
                }
            }
        }
    }
}
