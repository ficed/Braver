using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Braver.Field {
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
            public Action<int> OnChoice;
            public int Choice;
            public int[] ChoiceLines;
        }

        private List<Window> _windows = Enumerable.Range(0, 10).Select(_ => new Window()).ToList();
        private FGame _game;
        private UI.UIBatch _ui;

        public bool IsActive => _windows.Any(w => w.State != WindowState.Hidden);

        public Dialog(FGame g, GraphicsDevice graphics) {
            _game = g;
            _ui = new UI.UIBatch(graphics, g);
        }


        public void CloseWindow(int window) {
            _windows[window].State = WindowState.Hiding; //TODO - is this right, or just insta-hide it?
            _windows[window].FrameProgress = 0;
        }
        public void ResetWindow(int window) {
            SetupWindow(window, 5, 5, 0x130, 0x45);
            _windows[window].State = WindowState.Hidden;
        }
        public void SetupWindow(int window, int x, int y, int width, int height) {
            _windows[window].X = x * 3 + 160;
            _windows[window].Y = y * 3;
            _windows[window].Width = width * 3 / 2 + 8; //expand size to account for border...? 
            _windows[window].Height = height * 3 / 2 + 8;
        }

        private void PrepareWindow(int window, string text) {
            var chars = _game.SaveData.Characters.Select(c => c?.Name).ToArray();
            var party = _game.SaveData.Party.Select(c => c?.Name).ToArray();

            _windows[window].Text = text.Split('\xC')
                .Select(line => Ficedula.FF7.Text.Expand(line, chars, party))
                .ToArray();
            _windows[window].FrameProgress = _windows[window].ScreenProgress = 0;
            _windows[window].State = WindowState.Expanding;
        }

        public void Show(int window, string text, Action onClosed) {
            PrepareWindow(window, text);
            _windows[window].OnClosed = onClosed;
            _windows[window].OnChoice = null;
            _windows[window].ChoiceLines = null;
        }
        public void Ask(int window, string text, IEnumerable<int> choices, Action<int> onChoice) {
            PrepareWindow(window, text);
            _windows[window].ChoiceLines = choices.ToArray();
            _windows[window].OnClosed = null;
            _windows[window].OnChoice = onChoice;
            _windows[window].Choice = 0;
        }

        public void ProcessInput(InputState input) { 
            foreach(var window in _windows) {
                switch (window.State) {
                    case WindowState.Displaying:
                        if (input.IsJustDown(InputKey.OK)) {
                            _game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                            window.FrameProgress = 9999;
                        }
                        break;

                    case WindowState.Wait:
                        if (input.IsJustDown(InputKey.OK)) {
                            _game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                            if (window.ScreenProgress < (window.Text.Length - 1)) { //next screen
                                window.ScreenProgress++;
                                window.FrameProgress = 0;
                                window.State = WindowState.Displaying;
                            } else { //we're done
                                window.State = WindowState.Hiding;
                                window.FrameProgress = 0;
                            }
                        } else if (input.IsJustDown(InputKey.Down)) {
                            if (window.ChoiceLines != null) {
                                window.Choice = (window.Choice + 1) % window.ChoiceLines.Length;
                                _game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                            }
                        } else if (input.IsJustDown(InputKey.Up)) {
                            if (window.ChoiceLines != null) {
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
                int y = w.Y + 10;
                float tz = NextZ();
                int lineCount = 0;
                foreach (string line in w.Text[w.ScreenProgress].Split('\r')) {
                    string s = count < line.Length ? line.Substring(0, count) : line;
                    _ui.DrawText("main", s, w.X + 10, y, tz, Color.White);

                    if (w.ChoiceLines != null) {
                        if (w.ChoiceLines[0] == lineCount)
                            count = 99999;
                        if (lineCount == w.ChoiceLines[w.Choice])
                            _ui.DrawImage("pointer", w.X + 10, y, NextZ(), UI.Alignment.Right);
                    }

                    count -= s.Length;
                    if (count <= 0)
                        break;
                    y += 25;
                    lineCount++;
                }
            }

            foreach (var window in _windows) {
                switch (window.State) {
                    case WindowState.Expanding:
                        float rW = MIN_SIZE + (window.Width - MIN_SIZE) * 1f * window.FrameProgress / EXPAND_HIDE_FRAMES,
                            rH = MIN_SIZE + (window.Height - MIN_SIZE) * 1f * window.FrameProgress / EXPAND_HIDE_FRAMES;
                        _ui.DrawBox(new Rectangle(window.X, window.Y, (int)rW, (int)rH), NextZ());
                        break;
                    case WindowState.Hiding:
                        rW = MIN_SIZE + (window.Width - MIN_SIZE) * (1f - 1f * window.FrameProgress / EXPAND_HIDE_FRAMES);
                        rH = MIN_SIZE + (window.Height - MIN_SIZE) * (1f - 1f * window.FrameProgress / EXPAND_HIDE_FRAMES);
                        _ui.DrawBox(new Rectangle(window.X, window.Y, (int)rW, (int)rH), NextZ());
                        break;
                    case WindowState.Displaying:
                        _ui.DrawBox(new Rectangle(window.X, window.Y, window.Width, window.Height), NextZ());
                        int chars = window.FrameProgress / 4;
                        DrawText(window, ref chars);
                        if (chars > 0)
                            window.State = WindowState.Wait;
                        break;
                    case WindowState.Wait:
                        _ui.DrawBox(new Rectangle(window.X, window.Y, window.Width, window.Height), NextZ());
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
