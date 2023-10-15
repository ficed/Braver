// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7.Field;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Field {
    public class FieldUI {

        private UI.UIBatch _ui;

        public FieldUI(FGame g, GraphicsDevice graphics) {
            _ui = new UI.UIBatch(graphics, g);
        }

        public void Render() {
            _ui.Render();
        }

        int _frame = 0;
        public void Step(FieldScreen field) {
            _ui.Reset();

            var bgOffset = field.GetBGScroll();

            var gateways = field.TriggersAndGateways.Gateways
                .Where(g => g.ShowArrow)
                .Where(g => g.V0 != g.V1);

            float playerHeight = 0;

            if ((field.Player != null) && field.Options.HasFlag(FieldOptions.ShowPlayerHand)) {
                playerHeight = (field.Player.Model.MaxBounds.Y - field.Player.Model.MinBounds.Y) * field.Player.Model.Scale;
                var bg = field.ModelToBGPosition(field.Player.Model.Translation + new Vector3(0, 0, playerHeight) * 1.25f);
                _ui.DrawImage(
                    $"pointer_above",
                    (int)(bg.X - bgOffset.X) * -3 + 640, 360 - (int)(bg.Y - bgOffset.Y) * 3, 0.9f,
                    alignment: UI.Alignment.Center
                );
            }

            if (field.Options.HasFlag(FieldOptions.GatewaysEnabled)) {
                foreach (var arrow in gateways) {
                    var bg = field.ModelToBGPosition((arrow.V0.ToX() + arrow.V1.ToX()) * 0.5f + new Vector3(0, 0, playerHeight));
                    _ui.DrawImage(
                        $"anim_arrow_{(_frame / 12) % 5}",
                        (int)(bg.X - bgOffset.X) * -3 + 640, 360 - (int)(bg.Y - bgOffset.Y) * 3, 0.9f,
                        alignment: UI.Alignment.Center, color: Color.Red
                    );
                }
            }

            foreach (var arrow in field.TriggersAndGateways.Arrows.Where(a => a.Type != ArrowType.Disabled)) {
                var bg = field.ModelToBGPosition(arrow.Position.ToX());
                _ui.DrawImage(
                    $"anim_arrow_{(_frame / 12) % 5}",
                    (int)(bg.X - bgOffset.X) * -3 + 640, 360 - (int)(bg.Y - bgOffset.Y) * 3, 0.9f,
                    alignment: UI.Alignment.Center, 
                    color: arrow.Type == ArrowType.Red ? Color.Red : Color.Green
                );

            }

            _frame++;
        }
    }
}
