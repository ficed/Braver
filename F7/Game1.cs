using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace F7 {
    public class Game1 : Game {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

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

        private Screen _screen;
        private FGame _g;

        protected override void LoadContent() {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _g = new FGame(@"C:\games\ff7\data", @"C:\Users\ficed\Projects\F7\data");
            _g.NewGame();
            //_screen = new TestScreen(_g, GraphicsDevice);
            _screen = new Field.FieldScreen("mrkt2", _g, GraphicsDevice);
        }

        private static Dictionary<Keys, InputKey> _keyMap = new Dictionary<Keys, InputKey> {
            [Keys.W] = InputKey.Up,
            [Keys.S] = InputKey.Down,
            [Keys.A] = InputKey.Left,
            [Keys.D] = InputKey.Right,
            [Keys.Enter] = InputKey.OK,
            [Keys.Space] = InputKey.Cancel,
            [Keys.F1] = InputKey.Start,
            [Keys.F2] = InputKey.Select,
        };

        private InputState _input = new();

        protected override void Update(GameTime gameTime) {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            var keyState = Keyboard.GetState();
            foreach(var key in _keyMap.Keys) {
                var ik = _keyMap[key];
                bool down = keyState.IsKeyDown(key);
                if (down) {
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

            _screen.ProcessInput(_input);

            // TODO: Add your update logic here

            base.Update(gameTime);

            _screen.Step(gameTime);
        }

        protected override void Draw(GameTime gameTime) {
            GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1f, 0);

            base.Draw(gameTime);

            _screen.Render();
        }
    }
}