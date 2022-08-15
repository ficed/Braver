using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RazorEngineCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Braver.UI.Layout {
    [XmlInclude(typeof(Box)), XmlInclude(typeof(Label)), XmlInclude(typeof(Gauge))]
    [XmlInclude(typeof(Group)), XmlInclude(typeof(Image))]
    public abstract class Component {

        private static Dictionary<string, Color> _colors = new Dictionary<string, Color>(StringComparer.InvariantCultureIgnoreCase);

        protected Color GetColor(string colorName, ref Color? storage) {
            storage ??= _colors[colorName];
            return storage.GetValueOrDefault();
        }

        static Component() {
            foreach(var prop in typeof(Color).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)) {
                _colors[prop.Name] = (Color)prop.GetValue(null);
            }
        }


        [XmlAttribute]
        public int X { get; set; }
        [XmlAttribute]
        public int Y { get; set; }
        [XmlAttribute]
        public string ID { get; set; }
        [XmlAttribute]
        public bool Visible { get; set; } = true;


        [XmlAttribute]
        public string Click { get; set; }
        [XmlIgnore]
        public virtual Action OnClick { get; set; }

        [XmlIgnore]
        public Container Parent { get; internal set; }

        public Point GetAbsolutePosition() {
            int x = X, y = Y;
            Container c = Parent;
            while (c != null) {
                x += c.X;
                y += c.Y;
                c = c.Parent;
            }
            return new Point(x, y);
        }

        public abstract void Draw(UIBatch ui, int offsetX, int offsetY, Func<float> getZ);
    }

    public abstract class SizedComponent : Component {
        [XmlAttribute]
        public int W { get; set; }
        [XmlAttribute]
        public int H { get; set; }

    }

    public abstract class Container : SizedComponent {
        [XmlElement("Component")]
        public List<Component> Children { get; set; } = new();

        public override void Draw(UIBatch ui, int offsetX, int offsetY, Func<float> getZ) {
            foreach (var child in Children.Where(c => c.Visible))
                child.Draw(ui, offsetX + X, offsetY + Y, getZ);
        }
    }

    public class Box : Container {
        public override void Draw(UIBatch ui, int offsetX, int offsetY, Func<float> getZ) {
            ui.DrawBox(new Rectangle(offsetX + X, offsetY + Y, W, H), getZ());
            base.Draw(ui, offsetX, offsetY, getZ);
        }
    }

    public class Group : Container { }

    public class Label : Component {
        [XmlText]
        public string Text { get; set; }
        [XmlAttribute]
        public string Font { get; set; } = "main";

        [XmlAttribute("Color")]
        public string ColorString { get; set; } = "White";
        [XmlAttribute]
        public Alignment Alignment { get; set; }

        private Color? _color;
        [XmlIgnore]
        public Color Color {
            get => Enabled ? GetColor(ColorString, ref _color) : Color.Gray;
            set => _color = value;
        }

        [XmlIgnore]
        public override Action OnClick {
            get => Enabled ? base.OnClick : null; 
            set => base.OnClick = value; 
        }

        [XmlAttribute]
        public bool Enabled { get; set; } = true;

        public override void Draw(UIBatch ui, int offsetX, int offsetY, Func<float> getZ) {
            if (!string.IsNullOrWhiteSpace(Text))
                ui.DrawText(Font, Text, offsetX + X, offsetY + Y, getZ(), Color, Alignment);
        }
    }

    public class Image : Component {
        [XmlText]
        public string ImageName { get; set; }
        [XmlAttribute]
        public float Scale { get; set; } = 1f;

        public override void Draw(UIBatch ui, int offsetX, int offsetY, Func<float> getZ) {
            ui.DrawImage(ImageName, offsetX + X, offsetY + Y, getZ(), Alignment.Left, Scale);
        }
    }

    public enum GaugeStyle {
        HP, MP, Limit
    }
    public class Gauge : SizedComponent {
        [XmlAttribute]
        public float Current { get; set; }
        [XmlAttribute]
        public float Max { get; set; }
        [XmlAttribute]
        public GaugeStyle Style { get; set; }

        public override void Draw(UIBatch ui, int offsetX, int offsetY, Func<float> getZ) {
            switch (Style) {
                case GaugeStyle.Limit:
                    float z1 = getZ();
                    ui.DrawImage(
                        "border_limit_l", X + offsetX, Y + offsetY, z1, new Point(6, H)
                    );
                    ui.DrawImage(
                        "border_limit_c", X + offsetX + 6, Y + offsetY, z1, new Point(W - 14, H)
                    );
                    ui.DrawImage(
                        "border_limit_r", X + offsetX + W - 8, Y + offsetY, z1, new Point(8, H)
                    );
                    ui.DrawImage(
                        "grad_limit", X + offsetX + 6, Y + offsetY + 6, getZ(), new Point((int)((W - 14) * Current / Max), H - 14)
                    );
                    break;

                default:
                    int filled = Max > 0 ? (int)(W * Current / Max) : 0;
                    ui.DrawImage(
                        "grad_" + Style.ToString(), X + offsetX, Y + offsetY, getZ(), new Point(filled, H)
                    );
                    ui.DrawImage(
                        "white", X + filled + offsetX, Y + offsetY, getZ(), new Point(W - filled, H),
                        Alignment.Left, new Color(0xff06043a)
                    );
                    break;
            }
        }
    }



    public class Layout {
        public Component Root { get; set; }
    }

    public abstract class LayoutModel {

        protected Dictionary<string, Component> _components = new Dictionary<string, Component>(StringComparer.InvariantCultureIgnoreCase);
        protected FGame _game;
        protected LayoutScreen _screen;

        public Container FocusGroup => _focus.Any() ? _components[_focus.Peek().GroupID] as Container : null;
        public Component Focus => _focus.Any() ? _components[_focus.Peek().FocusID] : null;
        public Component FlashFocus { get; set; }
        public bool InputEnabled { get; set; } = true;

        private class FocusEntry {
            public string GroupID { get; set; }
            public string FocusID { get; set; }
        }

        private Stack<FocusEntry> _focus = new();

        public virtual void CancelPressed() {
            _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
            PopFocus(); 
        }

        public void PushFocus(Container group, Component focus) {
            _focus.Push(new FocusEntry {
                GroupID = group.ID,
                FocusID = focus.ID,
            });
            FlashFocus = null;
        }
        public void PopFocus() {
            _focus.Pop();
            FlashFocus = null;
        }
        public void ChangeFocus(Component focus) {
            Debug.Assert(focus.Parent == FocusGroup);
            _focus.Peek().FocusID = focus.ID;
        }

        /// <summary>
        /// Process input before the default UI handling takes place
        /// </summary>
        /// <param name="input">Current input state</param>
        /// <returns>true if the input has been handled and default handling should not take place;
        /// false otherwise.</returns>
        public virtual bool ProcessInput(InputState input) {
            return false;
        }


        public virtual void Step() { }
        protected virtual void OnInit() { }

        public void Init(FGame g, Layout layout, LayoutScreen screen) {
            _game = g;
            _screen = screen;

            Type thisType = this.GetType();
            var fields = thisType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .ToDictionary(f => f.Name, f => f, StringComparer.InvariantCultureIgnoreCase);

            void Descend(Component c) {
                if (!string.IsNullOrEmpty(c.ID)) {
                    _components[c.ID] = c;
                    if (fields.TryGetValue(c.ID, out var field))
                        field.SetValue(this, c);
                }

                if (!string.IsNullOrEmpty(c.Click)) {
                    var method = thisType.GetMethod(c.Click);
                    switch (method.GetParameters().Length) {
                        case 0:
                            c.OnClick = () => method.Invoke(this, null);
                            break;
                        case 1:
                            c.OnClick = () => method.Invoke(this, new object[] { c });
                            break;
                    }
                }

                if (c is Container container) {
                    foreach (var child in container.Children) {
                        child.Parent = container;
                        Descend(child);
                    }
                }
            }

            Descend(layout.Root);
            FlashFocus = null;
            OnInit();
        }
    }


    public class BraverTemplate : RazorEngineTemplateBase {
        public new FGame Model { get; set; }

        public string Bool(bool b) {
            return b.ToString().ToLower();
        }
    }
    public class LayoutScreen : Screen {
        private Layout _layout;
        private LayoutModel _model;
        private UIBatch _ui;
        private string _layoutFile;

        private static Task _backgroundLoad;
        public static void BeginBackgroundLoad(FGame g, string layout) {
            var cache = g.Singleton(() => new RazorLayoutCache(g));
            _backgroundLoad = Task.Run(() => cache.Compile(layout, true));
        }

        private class RazorLayoutCache {
            private Dictionary<string, IRazorEngineCompiledTemplate<BraverTemplate>> _templates = new Dictionary<string, IRazorEngineCompiledTemplate<BraverTemplate>>(StringComparer.InvariantCultureIgnoreCase);
            private IRazorEngine _razorEngine = new RazorEngine();
            private FGame _game;

            public RazorLayoutCache(FGame game) {
                _game = game;
            }

            public IRazorEngineCompiledTemplate<BraverTemplate> Compile(string layout, bool forceReload) {
                if (forceReload || !_templates.TryGetValue(layout, out var razor)) {
                    string template = _game.OpenString("layout", layout + ".xml");
                    _templates[layout] = razor = _razorEngine.Compile<BraverTemplate>(template, builder => {
                        builder.AddAssemblyReference(System.Reflection.Assembly.GetExecutingAssembly());
                    });
                }
                return razor;
            }

            public Layout Apply(string layout, bool forceReload) {
                string xml = Compile(layout, forceReload).Run(template => {
                    template.Model = _game;
                });
                return Serialisation.Deserialise<Layout>(xml);
            }
        }

        public override Color ClearColor => Color.Black;

        public LayoutScreen(FGame g, GraphicsDevice graphics, string layout) : base(g, graphics) {
            _layoutFile = layout;
            if (_backgroundLoad != null) {
                _backgroundLoad.Wait();
                _backgroundLoad = null;
            }
            _layout = g.Singleton(() => new RazorLayoutCache(g)).Apply(layout, false);
            _model = Activator.CreateInstance(Type.GetType("Braver.UI.Layout." + layout)) as LayoutModel;
            _model.Init(g, _layout, this);
            _ui = new UIBatch(graphics, g);
        }

        public void Reload(bool forceReload = false) {
            _layout = Game.Singleton(() => new RazorLayoutCache(Game)).Apply(_layoutFile, forceReload);
            _model.Init(Game, _layout, this);
        }

        protected override void DoRender() {
            _ui.Render();
        }

        protected override void DoStep(GameTime elapsed) {
            _ui.Reset();
            float z = 0;
            _layout.Root.Draw(_ui, 0, 0, () => {
                z += UIBatch.Z_ITEM_OFFSET;
                return z;
            });

            _model.Step();

            if (_model.Focus != null) {
                var pos = _model.Focus.GetAbsolutePosition();
                if (_model.Focus is SizedComponent sized)
                    pos.Y += sized.H / 2;
                _ui.DrawImage("pointer", pos.X - 5, pos.Y, z + UIBatch.Z_ITEM_OFFSET, Alignment.Right);
            }
            if (_model.FlashFocus != null) {
                if (((int)(elapsed.TotalGameTime.TotalSeconds * 30) % 2) == 0) {
                    var pos = _model.FlashFocus.GetAbsolutePosition();
                    if (_model.FlashFocus is SizedComponent sized)
                        pos.Y += sized.H / 2;
                    _ui.DrawImage("pointer", pos.X - 5, pos.Y, z + UIBatch.Z_ITEM_OFFSET, Alignment.Right);
                }
            }
        }

        private Component FindNextFocus(Container container, Component current, int ox, int oy) {
            var candidates = container.Children
                .Where(c => c.OnClick != null)
                .Where(c => c != current);

            switch (ox) {
                case 1:
                    candidates = candidates
                        .Where(c => (c.X > current.X) && (Math.Abs(c.Y - current.Y) < Math.Abs(c.X - current.X)));
                    break;
                case -1:
                    candidates = candidates
                        .Where(c => (c.X < current.X) && (Math.Abs(c.Y - current.Y) < Math.Abs(c.X - current.X)));
                    break;
            }
            switch (oy) {
                case 1:
                    candidates = candidates
                        .Where(c => (c.Y > current.Y) && (Math.Abs(c.X - current.X) < Math.Abs(c.Y - current.Y)));
                    break;
                case -1:
                    candidates = candidates
                        .Where(c => (c.Y < current.Y) && (Math.Abs(c.X - current.X) < Math.Abs(c.Y - current.Y)));
                    break;
            }

            var result = candidates
                .OrderBy(c => (new Vector2(c.X, c.Y) - new Vector2(current.X, current.Y)).LengthSquared())
                .FirstOrDefault();

            return result;
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);
            if (!_model.InputEnabled)
                return;
            if (_model.ProcessInput(input))
                return;

            if (_model.Focus != null) {

                if (input.IsJustDown(InputKey.OK)) {
                    Game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                    _model.Focus.OnClick();
                } else {
                    Component next = null;
                    if (input.IsJustDown(InputKey.Up))
                        next = FindNextFocus(_model.FocusGroup, _model.Focus, 0, -1);
                    else if (input.IsJustDown(InputKey.Down))
                        next = FindNextFocus(_model.FocusGroup, _model.Focus, 0, 1);
                    else if (input.IsJustDown(InputKey.Left))
                        next = FindNextFocus(_model.FocusGroup, _model.Focus, -1, 0);
                    else if (input.IsJustDown(InputKey.Right))
                        next = FindNextFocus(_model.FocusGroup, _model.Focus, 1, 0);

                    if (next != null) {
                        _model.ChangeFocus(next);
                        Game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                    } else {

                        if (input.IsJustDown(InputKey.Cancel)) {
                            _model.CancelPressed();
                        }
                    }
                }
            }

            if (input.IsJustDown(InputKey.Debug1)) {
                Reload(true);
            }

        }
    }
}
