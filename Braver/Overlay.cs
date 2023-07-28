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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {
    public class Overlay {
        private UIBatch _ui;
        private FGame _game;
        private GraphicsDevice _graphics;

        public Overlay(GraphicsDevice graphics, FGame g) {
            _game = g;
            _graphics = graphics;
            _ui = new UIBatch(graphics, g) {
                WarnAboutUnrecognisedCharacters = false,
            };

            Trace.Listeners.Add(new WarningListener(this));
        }

        private class WarningListener : TraceListener {
            private Overlay _owner;

            public WarningListener(Overlay owner) {
                _owner = owner;
            }

            public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message) {
                if (eventType == TraceEventType.Warning) {
                    lock (_owner._warnings)
                        _owner._warnings.Enqueue(new WarningMessage { Message = message });
                }
            }

            public override void Write(string message) {
                //
            }

            public override void WriteLine(string message) {
                //
            }
        }

        private class WarningMessage {
            public int Countdown { get; set; } = 120;
            public string Message { get; set; }
        }

        private Queue<WarningMessage> _warnings = new Queue<WarningMessage>();

        public void Render() {
            lock (_warnings) {
                if (_warnings.Any()) {
                    using (var state = new GraphicsState(_graphics, depthStencilState: DepthStencilState.None)) {
                        _ui.Reset();

                        int y = 5;
                        foreach(var warning in _warnings) {
                            _ui.DrawText("main", warning.Message, 10, y, 0.99f, Color.Yellow);
                            y += 30;
                            warning.Countdown--;
                        }

                        _ui.Render();

                        while (_warnings.Any() && (_warnings.Peek().Countdown <= 0))
                            _warnings.Dequeue();
                    }
                }
            }
        }
    }
}
