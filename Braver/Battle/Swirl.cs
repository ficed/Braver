// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Battle {
    public class Swirl : Screen {

        public override bool ShouldClear => false;
        public override Color ClearColor => throw new NotImplementedException();

        private GraphicsDevice _graphics;
        private Texture2D _texture;
        private SpriteBatch _spriteBatch;
        private float _rotation, _zoom;
        private Texture2D _whiteTex;
        private Rectangle _whiteRect;
        private int _frames;

        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);
            _graphics = graphics;
            _spriteBatch = new SpriteBatch(graphics);
            g.Singleton(() => new CompositeImages(graphics, g)).Find("white", out _whiteTex, out _whiteRect, out _);
        }

        protected override void DoRender() {
            if (_texture != null) {

                using (var state = new GraphicsState(_graphics, BlendState.NonPremultiplied, DepthStencilState.None)) {
                    var transform = 
                          Matrix.CreateScale(_zoom)
                        * Matrix.CreateTranslation(-_texture.Width / 2, -_texture.Height / 2, 0)
                        * Matrix.CreateRotationZ(_rotation)
                        * Matrix.CreateTranslation(_texture.Width / 2, _texture.Height / 2, 0);

                    var fsRect = new Rectangle(0, 0, _graphics.Viewport.Width, _graphics.Viewport.Height);

                    _spriteBatch.Begin(transformMatrix: transform);
                    _spriteBatch.Draw(_texture, fsRect, new Color(0x20, 0x20, 0x20, 0x20));
                    _spriteBatch.End();
                    _spriteBatch.Begin();
                    if (_frames >= 64) {
                        _spriteBatch.Draw(_whiteTex, fsRect, _whiteRect, Color.Black.WithAlpha((byte)((_frames - 64) * 8)));
                    }
                    _spriteBatch.End();
                }
            }
        }

        protected override void DoStep(GameTime elapsed) {
            if (_texture == null) {
                _texture = new Texture2D(_graphics, _graphics.Viewport.Width, _graphics.Viewport.Height, false, SurfaceFormat.Color);
                byte[] buffer = new byte[_texture.Width * _texture.Height * 4];
                _graphics.GetBackBufferData(buffer);
                _texture.SetData(buffer);
                _zoom = 1;
                Game.Audio.PlaySfx(Sfx.BattleSwirl, 1f, 0f);
            } else
                _zoom += 0.02f;
            _rotation += (float)(2 * Math.PI / 180);
            _frames++;
            if (_frames >= 96)
                Game.PopScreen(this);
        }
    }
}
