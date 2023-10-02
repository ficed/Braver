// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula;
using Braver.Plugins;
using Braver.Plugins.Field;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Field {

    [Flags]
    public enum DialogOptions {
        None = 0,
        Transparent = 0x1,
        NoBorder = 0x2,
        IsPermanent = 0x4,
    }

    public enum DialogVariable {
        None,
        Timer,
        Counter,
    }

    public class Dialog {

        public enum WindowState {
            Hidden,
            Expanding,
            Displaying,
            Wait,
            Hiding,
        }

        private const int MIN_SIZE = 16;
        private const int EXPAND_HIDE_FRAMES = 30;

        public class Window {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public DialogOptions Options { get; set; }

            public string[] Text;
            public WindowState State = WindowState.Hidden;
            public int FrameProgress, ScreenProgress, Tag;
            public Action OnClosed;
            public Action<int?> OnChoice;
            public int Choice;
            public int[] ChoiceLines;
            public DialogVariable Variable { get; set; }
            public int VariableX { get; set; }
            public int VariableY { get; set; }
            public int LineScroll;
            public int TextPause; //# of frames left to wait for an embedded text pause to complete

            public UI.Layout.Container Visual;
            private UI.Layout.Label lText, lVariable;
            private UI.Layout.Image iPointer;

            public void Reset(FGame game) {
                var cache = game.Singleton(() => new RazorLayoutCache(game));
                string xml = cache.ApplyPartial("layout", "dialog.xml", false, this);
                Visual = Serialisation.Deserialise<UI.Layout.Component>(xml) as UI.Layout.Container;
                lText = Visual.Children.Single(c => c.ID == nameof(lText)) as UI.Layout.Label;
                lVariable = Visual.Children.SingleOrDefault(c => c.ID == nameof(lVariable)) as UI.Layout.Label;
                iPointer = Visual.Children.Single(c => c.ID == nameof(iPointer)) as UI.Layout.Image;
                iPointer.Visible = false;
            }

            public void Render(FGame game, UI.UIBatch ui, Func<float> nextZ) {

                void DrawText(ref int count) {
                    int y = Y + 10 - LineScroll;
                    float tz = nextZ();
                    int lineCount = 0;

                    switch (Variable) {
                        case DialogVariable.None:
                            break;

                        case DialogVariable.Timer:
                            int secs = game.CounterSeconds % 60,
                                mins = game.CounterSeconds / 60;
                            if (mins > 99) mins = 99;
                            string timeText = $"{mins:00}{((secs % 2) == 0 ? ':' : ';')}{secs:00}";
                            lVariable.Text = timeText;
                            //TODO adjust Y in case of variable and text
                            break;

                        default:
                            throw new NotSupportedException();
                    }

                    var toRender = new List<string>();

                    foreach (string line in Text[ScreenProgress].Split('\r')) {
                        string s = count < line.Length ? line.Substring(0, count) : line;

                        //TODO - smooth scrolling, maybe scissor clip out the text within the box...
                        if (y > (Y + Height - 25)) {
                            LineScroll += 25;
                            count = 0;
                            break;
                        }
                        if (y > Y)
                            toRender.Add(s);

                        if (ReadyForChoice) {
                            if (ChoiceLines[0] == lineCount)
                                count = 99999;
                            if (lineCount == ChoiceLines[Choice]) {
                                iPointer.Visible = true;
                                iPointer.Y = y;
                            }
                        } else
                            iPointer.Visible = false;

                        if ((s.Length > 0) && (s.Last() == '\xE030')) {
                            //pause opcode
                            TextPause = 10;
                        }

                        count -= s.Length;
                        if (count <= 0)
                            break;
                        y += 25;
                        lineCount++;
                    }
                    lText.Text = string.Join("\r", toRender);
                }

                switch (State) {
                    case WindowState.Expanding:
                        float rW = MIN_SIZE + (Width - MIN_SIZE) * 1f * FrameProgress / EXPAND_HIDE_FRAMES,
                            rH = MIN_SIZE + (Height - MIN_SIZE) * 1f * FrameProgress / EXPAND_HIDE_FRAMES;

                        Visual.W = (int)rW;
                        Visual.H = (int)rH;

                        break;
                    case WindowState.Hiding:
                        rW = MIN_SIZE + (Width - MIN_SIZE) * (1f - 1f * FrameProgress / EXPAND_HIDE_FRAMES);
                        rH = MIN_SIZE + (Height - MIN_SIZE) * (1f - 1f * FrameProgress / EXPAND_HIDE_FRAMES);
                        Visual.W = (int)rW;
                        Visual.H = (int)rH;
                        lText.Text = string.Empty;
                        break;
                    case WindowState.Displaying:
                        Visual.W = Width;
                        Visual.H = Height;
                        int chars = FrameProgress / 4;
                        DrawText(ref chars);
                        if (chars > 0)
                            State = WindowState.Wait;
                        break;
                    case WindowState.Wait:
                        Visual.W = Width;
                        Visual.H = Height;
                        int i = 99999;
                        DrawText(ref i);
                        break;
                    
                    case WindowState.Hidden:
                        return;
                }
                Visual.Draw(null, ui, 0, 0, nextZ);
            }

            public bool ReadyForChoice => (ChoiceLines != null) && (ScreenProgress == (Text.Length - 1));

            public void StateChanged(PluginInstances<IDialog> plugins) {
                plugins.Call(ui => ui.Dialog(Tag, ScreenProgress, Text[ScreenProgress]));
                if (ReadyForChoice)
                    ChoiceChanged(plugins);
            }

            public void ChoiceChanged(PluginInstances<IDialog> plugins) {
                var lines = Text[ScreenProgress].Split('\r');
                var choices = ChoiceLines.Select(i => lines[i]);
                plugins.Call(ui => ui.ChoiceSelected(choices, Choice));
            }
        }

        private List<Window> _windows = Enumerable.Range(0, 10).Select(_ => new Window()).ToList();
        private FGame _game;
        private PluginInstances<IDialog> _plugins;
        private UI.UIBatch _ui;

        public bool IsActive => _windows.Any(w => (w.State != WindowState.Hidden) && !w.Options.HasFlag(DialogOptions.IsPermanent));

        public Dialog(FGame g, PluginInstances<IDialog> plugins, GraphicsDevice graphics) {
            _game = g;
            _plugins = plugins;
            _ui = new UI.UIBatch(graphics, g);
        }


        public void CloseWindow(int window) {
            _windows[window].State = WindowState.Hiding; //TODO - is this right, or just insta-hide it?
            _windows[window].FrameProgress = 0;
        }
        public void ResetWindow(int window) {
            var win = _windows[window];
            win.OnClosed?.Invoke();
            win.OnChoice?.Invoke(null);
            SetupWindow(window, 5, 5, 0x130, 0x45);
            win.State = WindowState.Hidden;
            win.Options = DialogOptions.None;
            win.Variable = DialogVariable.None;
        }
        public void SetupWindow(int window, int x, int y, int width, int height) {
            var win = _windows[window];
            win.X = x * 3 + 160;
            win.Y = y * 3;
            win.Width = width * 3 / 2 + 8; //expand size to account for border...? 
            win.Height = height * 3 / 2 + 8;
            win.OnClosed = null;
            win.TextPause = 0;
        }
        public void SetOptions(int window, DialogOptions options) {
            _windows[window].Options = options;
        }
        public void SetVariable(int window, DialogVariable variable, int x, int y) {
            _windows[window].Variable = variable;
            _windows[window].VariableX = x;
            _windows[window].VariableY = y;
        }

        private void PrepareWindow(int window, string text, int tag) {
            var chars = _game.SaveData.Characters.Select(c => c?.Name).ToArray();
            var party = _game.SaveData.Party.Select(c => c?.Name).ToArray();
            var win = _windows[window];
            win.Text = text.Split('\xC')
                .Select(line => Ficedula.FF7.Text.Expand(line, chars, party))
                .ToArray();
            win.FrameProgress = win.ScreenProgress = win.LineScroll = 0;
            win.Tag = tag;

            if (win.Options.HasFlag(DialogOptions.NoBorder))
                win.State = WindowState.Displaying;
            else
                win.State = WindowState.Expanding;

            if ((win.Width == 0) && (win.Height == 0)) {
                win.Width = win.Text
                    .SelectMany(s => s.Split('\r'))
                    .Select(s => _ui.TextWidth("main", s)).Max() + 24;
                win.Height = win.Text
                    .Select(line => line.Split('\r').Length)
                    .Max() * 25 + 16;
            }
            if ((win.X == 0) && (win.Y == 0)) {
                win.X = 640 - win.Width / 2;
                win.Y = 720 - win.Height;
            }

            win.Reset(_game);
            win.StateChanged(_plugins);
        }

        public void Show(int window, int tag, string text, Action onClosed) {
            PrepareWindow(window, text, tag);
            _windows[window].OnClosed = onClosed;
            _windows[window].OnChoice = null;
            _windows[window].ChoiceLines = null;
            _plugins.Call(dlg => dlg.Showing(window, tag, _windows[window].Text));
        }
        public void Ask(int window, int tag, string text, IEnumerable<int> choices, Action<int?> onChoice) {
            PrepareWindow(window, text, tag);
            _windows[window].ChoiceLines = choices.ToArray();
            _windows[window].OnClosed = null;
            _windows[window].OnChoice = onChoice;
            _windows[window].Choice = 0;
            _plugins.Call(dlg => dlg.Asking(window, tag, _windows[window].Text, _windows[window].ChoiceLines));
        }

        public void ProcessInput(InputState input) { 
            foreach(var window in _windows.Reverse<Window>()) { 
                switch (window.State) {
                    case WindowState.Displaying:
                        if (input.IsJustDown(InputKey.OK)) {
                            _game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                            window.FrameProgress = 9999;
                        }
                        break;

                    case WindowState.Wait:
                        if (input.IsJustDown(InputKey.OK)) {
                            if (window.ScreenProgress < (window.Text.Length - 1)) { //next screen
                                window.ScreenProgress++;
                                window.StateChanged(_plugins);
                                window.LineScroll = window.FrameProgress = 0;                                
                                window.State = WindowState.Displaying;
                                _game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                            } else { //we're done
                                if (!window.Options.HasFlag(DialogOptions.IsPermanent)) {
                                    window.State = WindowState.Hiding;
                                    window.FrameProgress = 0;
                                    _game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                                }
                            }
                        } else if (input.IsJustDown(InputKey.Down)) {
                            if (window.ReadyForChoice) {
                                window.Choice = (window.Choice + 1) % window.ChoiceLines.Length;
                                window.ChoiceChanged(_plugins);
                                _game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                            }
                        } else if (input.IsJustDown(InputKey.Up)) {
                            if (window.ReadyForChoice) {
                                window.Choice = (window.Choice + window.ChoiceLines.Length + 1) % window.ChoiceLines.Length;
                                window.ChoiceChanged(_plugins);
                                _game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                            }
                        }
                        break;
                }
            }
        }

        public void Step() {
            foreach (var window in _windows) {
                switch (window.State) {
                    case WindowState.Expanding:
                    case WindowState.Hiding:
                        if (window.FrameProgress < EXPAND_HIDE_FRAMES)
                            window.FrameProgress++;
                        else if (window.State == WindowState.Expanding) {
                            window.FrameProgress = 0;
                            window.State = WindowState.Displaying;
                        } else {
                            window.State = WindowState.Hidden;
                            window.OnClosed?.Invoke();
                            window.OnChoice?.Invoke(window.ChoiceLines[window.Choice]);
                        }
                        break;

                    case WindowState.Displaying:
                        if (window.TextPause > 0)
                            window.TextPause--;
                        else
                            window.FrameProgress++;
                        break;
                }
            }

            _ui.Reset();
            float z = 0;
            float NextZ() {
                z += UI.UIBatch.Z_ITEM_OFFSET;
                return z;
            }


            foreach (var window in _windows.Reverse<Window>()) { //lower ID windows should appear on top of higher IDs {
                window.Render(_game, _ui, NextZ);
            }
        }

        public void Render() {
            _ui.Render();
        }
    }
}
