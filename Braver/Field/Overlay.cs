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

namespace Braver.Field {
    public class Overlay {

        private Color _color = Color.Black;
        private SpriteBatch _spriteBatch;
        private BlendState _blend;
        private Texture2D _tex;
        private Rectangle _rect;

        private Action _onComplete;
        private int _progress, _duration;
        private Color _cFrom, _cTo;

        public bool HasTriggered { get; private set; }
        public bool IsFading => _progress < _duration;

        public Overlay(FGame g, GraphicsDevice graphics) {
            _spriteBatch = new SpriteBatch(graphics);
            g.Singleton(() => new CompositeImages(graphics, g)).Find("white", out _tex, out _rect, out _);
        }

        public void Fade(int frames, BlendState blend, Color cFrom, Color cTo, Action onComplete) {
            _color = _cFrom = cFrom;
            _cTo = cTo;
            _onComplete = onComplete;
            _progress = 0;
            _duration = frames;
            _blend = blend;
            HasTriggered = true;
        }

        public void Render() {
            if ((_color.PackedValue & 0xffffff) != 0) {
                _spriteBatch.Begin(blendState: _blend, depthStencilState: DepthStencilState.None);
                _spriteBatch.Draw(_tex, new Rectangle(0, 0, 9000, 9000), _rect, _color);
                _spriteBatch.End();
            }
        }

        public void Step() {
            if (_progress == _duration) {
                _color = _cTo;
                _onComplete?.Invoke();
            } else {
                _progress++;
                _color = Color.Lerp(_cFrom, _cTo, 1f * _progress / _duration);
            }
        }
    }
}
