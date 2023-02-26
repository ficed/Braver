using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver {
    public class Game1 : Game {
        private GraphicsDeviceManager _graphics;

        public Game1() {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 1280 * 2;
            _graphics.PreferredBackBufferHeight = 720 * 2;
            _graphics.GraphicsProfile = GraphicsProfile.HiDef;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize() {
            // TODO: Add your initialization logic here

            base.Initialize();
        }

        private FGame _g;

        protected override void LoadContent() {

            _g = new FGame(GraphicsDevice);
            _g.NewGame();

            Dictionary<string, string> parms = Environment.GetCommandLineArgs()
                .Select(s => s.Split('='))
                .Where(sa => sa.Length == 2)
                .ToDictionary(sa => sa[0], sa => sa[1], StringComparer.InvariantCultureIgnoreCase);

            if (parms.ContainsKey("host")) {
                _g.ChangeScreen(null, new UI.Splash(parms["host"], int.Parse(parms["port"]), parms["key"]));
            } else {
                _g.ChangeScreen(null, new UI.Splash());
            }
        }

        private static Dictionary<Keys, InputKey> _keyMap = new Dictionary<Keys, InputKey> {
            [Keys.W] = InputKey.Up,
            [Keys.S] = InputKey.Down,
            [Keys.A] = InputKey.Left,
            [Keys.D] = InputKey.Right,
            [Keys.Enter] = InputKey.OK,
            [Keys.Space] = InputKey.Cancel,

            [Keys.OemOpenBrackets] = InputKey.PanLeft,
            [Keys.OemCloseBrackets] = InputKey.PanRight,

            [Keys.F1] = InputKey.Start,
            [Keys.F2] = InputKey.Select,
            [Keys.F3] = InputKey.Menu,
            [Keys.F4] = InputKey.Pause,

            [Keys.F5] = InputKey.Debug1,
            [Keys.F6] = InputKey.Debug2,
            [Keys.F7] = InputKey.Debug3,
            [Keys.F8] = InputKey.Debug4,
            [Keys.F9] = InputKey.Debug5,

            [Keys.F11] = InputKey.DebugOptions,
            [Keys.F12] = InputKey.DebugSpeed,
        };

        private InputState _input = new();

        protected override void Update(GameTime gameTime) {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            HashSet<InputKey> isDown = new();
            void SetInput(InputKey k, bool down) {
                if (down) isDown.Add(k);
            }

            var keyState = Keyboard.GetState();
            foreach(var key in _keyMap.Keys) {
                SetInput(_keyMap[key], keyState.IsKeyDown(key));
            }

            var padState = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.Circular);
            SetInput(InputKey.Down, padState.DPad.Down == ButtonState.Pressed);
            SetInput(InputKey.Left, padState.DPad.Left == ButtonState.Pressed);
            SetInput(InputKey.Right, padState.DPad.Right == ButtonState.Pressed);
            SetInput(InputKey.Up, padState.DPad.Up == ButtonState.Pressed);

            SetInput(InputKey.OK, padState.Buttons.A == ButtonState.Pressed);
            SetInput(InputKey.Cancel, padState.Buttons.B == ButtonState.Pressed);
            SetInput(InputKey.Menu, padState.Buttons.Y == ButtonState.Pressed);
            SetInput(InputKey.PanLeft, padState.Buttons.LeftShoulder == ButtonState.Pressed);
            SetInput(InputKey.PanRight, padState.Buttons.RightShoulder == ButtonState.Pressed);
            SetInput(InputKey.Start, padState.Buttons.Start == ButtonState.Pressed);
            SetInput(InputKey.Select, padState.Buttons.Back == ButtonState.Pressed);

            foreach(var ik in Enum.GetValues<InputKey>()) {
                if (isDown.Contains(ik)) {
                    if (_input.DownFor[ik] > 0)
                        _input.DownFor[ik]++;
                    else
                        _input.DownFor[ik] = 1;
                } else {
                    if (_input.DownFor[ik] > 0)
                        _input.DownFor[ik] = -1;
                    else
                        _input.DownFor[ik] = 0;
                }
            }

            if (_input.IsDown(InputKey.DebugSpeed)) {
                foreach (int _ in Enumerable.Range(0, 4))
                    _g.Step(gameTime, _input);
            } else {
                _g.Step(gameTime, _input);
            }

            if (_input.IsJustDown(InputKey.DebugOptions))
                _g.PushScreen(new UI.Layout.LayoutScreen("Debug"));

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime) {
            if (_g.Screen.ShouldClear)
                GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, _g.Screen.ClearColor, 1f, 0);
            base.Draw(gameTime);
            _g.Screen.Render();
        }
    }
}