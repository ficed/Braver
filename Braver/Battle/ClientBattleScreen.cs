// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpDX.X3DAudio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Battle {
    public class ClientBattleScreen : Screen, Net.IListen<AddBattleModelMessage>, Net.IListen<SetBattleCameraMessage> {
        public override Color ClearColor => Color.Black;
        public override string Description => "";

        private BattleRenderer<int> _renderer;
        private int _formationID;


        public ClientBattleScreen(int formationID) {
            _formationID = formationID;
        }

        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);

            var scene = g.Singleton(() => new BattleSceneCache(g)).Scenes[_formationID];

            var ui = new UI.Layout.ClientScreen {
                UpdateInBackground = true,
            };
            ui.Init(g, graphics);
            _renderer = new BattleRenderer<int>(g, graphics, ui);
            _renderer.LoadBackground(scene.LocationID);
            g.Net.Listen<Net.AddBattleModelMessage>(this);
            g.Net.Listen<Net.SetBattleCameraMessage>(this);
        }

        protected override void DoRender() {
            _renderer.Render();
        }

        protected override void DoStep(GameTime elapsed) {
            _renderer.Step(elapsed);
        }

        public void Received(AddBattleModelMessage message) {
            var model = Model.LoadBattleModel(Graphics, Game, message.Code);
            model.Translation = message.Position;
            model.Scale = 1;
            if (message.Position.Z < 0)
                model.Rotation = new Vector3(0, 180, 0);
            _renderer.Models.Add(message.ID, model);
        }

        public void Received(SetBattleCameraMessage message) {
            _renderer.SetCamera(message.Camera);
        }
    }
}
