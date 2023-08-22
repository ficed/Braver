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

namespace Braver.UI.Layout {
    public class ClientScreen : Screen, Net.IListen<Net.UIStateMessage> {
        private UIBatch _ui;
        private Color _clearColor;
        private string _description;
        public override Color ClearColor => _clearColor;
        public override string Description => _description;
        public bool UpdateInBackground { get; set; }

        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);
            _ui = new UIBatch(graphics, g);
            g.Net.Listen<Net.UIStateMessage>(this);
        }

        public void Received(Net.UIStateMessage message) {
            if (UpdateInBackground || (Game.Screen == this)) { //TODO - this will work for now but is a bit hacky?
                _clearColor = new Color(message.ClearColour);
                _ui.LoadState(message.State);
                _description = message.Description;
            }
        }

        protected override void DoRender() {
            _ui.Render();
        }

        protected override void DoStep(GameTime elapsed) {
        }
    }
}
