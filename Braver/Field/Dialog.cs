// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        private enum WindowState {
            Hidden,
            Expanding,
            Displaying,
            Wait,
            Hiding,
        }

        private const int MIN_SIZE = 16;
        private const int EXPAND_HIDE_FRAMES = 30;

        private class Window {
            public int X, Y, Width, Height;
            public string[] Text;
            public WindowState State = WindowState.Hidden;
            public int FrameProgress, ScreenProgress;
            public Action OnClosed;
            public Action<int?> OnChoice;
            public int Choice;
            public int[] ChoiceLines;
            public DialogOptions Options;
            public DialogVariable Variable;
            public int VariableX, VariableY;
            public int LineScroll;
            public int TextPause; //# of frames left to wait for an embedded text pause to complete

            public bool ReadyForChoice => (ChoiceLines != null) && (ScreenProgress == (Text.Length - 1));
        }

        private List<Window> _windows = Enumerable.Range(0, 10).Select(_ => new Window()).ToList();
        private FGame _game;
        private PluginInstances _plugins;
        private UI.UIBatch _ui;

        public bool IsActive => _windows.Any(w => (w.State != WindowState.Hidden) && !w.Options.HasFlag(DialogOptions.IsPermanent));

        public Dialog(FGame g, PluginInstances plugins, GraphicsDevice graphics) {
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

        private void PrepareWindow(int window, string text) {
            var chars = _game.SaveData.Characters.Select(c => c?.Name).ToArray();
            var party = _game.SaveData.Party.Select(c => c?.Name).ToArray();
            var win = _windows[window];
            win.Text = text.Split('\xC')
                .Select(line => Ficedula.FF7.Text.Expand(line, chars, party))
                .ToArray();
            win.FrameProgress = win.ScreenProgress = win.LineScroll = 0;

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
        }

        public void Show(int window, int tag, string text, Action onClosed) {
            PrepareWindow(window, text);
            _windows[window].OnClosed = onClosed;
            _windows[window].OnChoice = null;
            _windows[window].ChoiceLines = null;
            _plugins.Call<Plugins.Field.IDialog>(dlg => dlg.Showing(window, tag, _windows[window].Text));
        }
        public void Ask(int window, int tag, string text, IEnumerable<int> choices, Action<int?> onChoice) {
            PrepareWindow(window, text);
            _windows[window].ChoiceLines = choices.ToArray();
            _windows[window].OnClosed = null;
            _windows[window].OnChoice = onChoice;
            _windows[window].Choice = 0;
            _plugins.Call<Plugins.Field.IDialog>(dlg => dlg.Asking(window, tag, _windows[window].Text, _windows[window].ChoiceLines));
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
                                _game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                            }
                        } else if (input.IsJustDown(InputKey.Up)) {
                            if (window.ReadyForChoice) {
                                window.Choice = (window.Choice + window.ChoiceLines.Length + 1) % window.ChoiceLines.Length;
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

            void DrawText(Window w, ref int count) {
                int y = w.Y + 10 - w.LineScroll;
                float tz = NextZ();
                int lineCount = 0;

                switch (w.Variable) {
                    case DialogVariable.None:
                        break;

                    case DialogVariable.Timer:
                        int secs = _game.CounterSeconds % 60,
                            mins = _game.CounterSeconds / 60;
                        if (mins > 99) mins = 99;
                        string timeText = $"{mins:00}{((secs % 2) == 0 ? ':' : ';')}{secs:00}";
                        _ui.DrawText("clock", timeText, w.X + w.VariableX, w.Y + w.VariableY, tz, Color.White);
                        //TODO adjust Y in case of variable and text
                        break;

                    default:
                        throw new NotSupportedException();
                }

                foreach (string line in w.Text[w.ScreenProgress].Split('\r')) {
                    string s = count < line.Length ? line.Substring(0, count) : line;

                    //TODO - smooth scrolling, maybe scissor clip out the text within the box...
                    if (y > (w.Y + w.Height - 25)) {
                        w.LineScroll += 25;
                        count = 0;
                        break;
                    }
                    if (y > w.Y)
                        _ui.DrawText("main", s, w.X + 10, y, tz, Color.White);

                    if (w.ReadyForChoice) {
                        if (w.ChoiceLines[0] == lineCount)
                            count = 99999;
                        if (lineCount == w.ChoiceLines[w.Choice])
                            _ui.DrawImage("pointer", w.X + 10, y, NextZ(), UI.Alignment.Right);
                    }

                    if ((s.Length > 0) && (s.Last() == '\xE030')) {
                        //pause opcode
                        w.TextPause = 10;
                    }

                    count -= s.Length;
                    if (count <= 0)
                        break;
                    y += 25;
                    lineCount++;
                }
            }

            foreach (var window in _windows.Reverse<Window>()) { //lower ID windows should appear on top of higher IDs {
                float alpha = window.Options.HasFlag(DialogOptions.Transparent) ? 0.5f : 1f;
                bool box = !window.Options.HasFlag(DialogOptions.NoBorder);
                switch (window.State) {
                    case WindowState.Expanding:
                        float rW = MIN_SIZE + (window.Width - MIN_SIZE) * 1f * window.FrameProgress / EXPAND_HIDE_FRAMES,
                            rH = MIN_SIZE + (window.Height - MIN_SIZE) * 1f * window.FrameProgress / EXPAND_HIDE_FRAMES;
                        if (box)
                            _ui.DrawBox(new Rectangle(window.X, window.Y, (int)rW, (int)rH), NextZ(), alpha);
                        break;
                    case WindowState.Hiding:
                        rW = MIN_SIZE + (window.Width - MIN_SIZE) * (1f - 1f * window.FrameProgress / EXPAND_HIDE_FRAMES);
                        rH = MIN_SIZE + (window.Height - MIN_SIZE) * (1f - 1f * window.FrameProgress / EXPAND_HIDE_FRAMES);
                        if (box)
                            _ui.DrawBox(new Rectangle(window.X, window.Y, (int)rW, (int)rH), NextZ(), alpha);
                        break;
                    case WindowState.Displaying:
                        if (box)
                            _ui.DrawBox(new Rectangle(window.X, window.Y, window.Width, window.Height), NextZ(), alpha);
                        int chars = window.FrameProgress / 4;
                        DrawText(window, ref chars);
                        if (chars > 0)
                            window.State = WindowState.Wait;
                        break;
                    case WindowState.Wait:
                        if (box)
                            _ui.DrawBox(new Rectangle(window.X, window.Y, window.Width, window.Height), NextZ(), alpha);
                        int i = 99999;
                        DrawText(window, ref i);
                        break;
                }
            }
        }

        public void Render() {
            _ui.Render();
        }
    }
}
