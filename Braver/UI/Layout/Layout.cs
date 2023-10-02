// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula;
using Braver.Plugins;
using Braver.Plugins.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RazorEngineCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Braver.UI.Layout {
    [XmlInclude(typeof(Box)), XmlInclude(typeof(Label)), XmlInclude(typeof(Gauge))]
    [XmlInclude(typeof(Group)), XmlInclude(typeof(Image)), XmlInclude(typeof(List))]
    public abstract class Component : IComponent {

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
        public string FocusDescription { get; set; }


        [XmlAttribute]
        public string Click { get; set; }
        [XmlIgnore]
        public virtual Action OnClick { get; set; }
        [XmlAttribute]
        public string Focussed { get; set; }
        [XmlIgnore]
        public virtual Action OnFocussed  { get; set; }

        [XmlIgnore]
        public virtual string Description => FocusDescription ?? null;

        [XmlIgnore]
        public Container Parent { get; internal set; }

        [XmlIgnore]
        public Container FocusParent {
            get {
                Container c = Parent;
                while ((c != null) && c.InputPassthrough)
                    c = c.Parent;
                return c;
            }
        }

        [XmlIgnore]
        IContainer IComponent.Parent => Parent;

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

        public abstract void Draw(LayoutModel model, UIBatch ui, int offsetX, int offsetY, Func<float> getZ);
    }

    public abstract class SizedComponent : Component, ISizedComponent {
        [XmlAttribute]
        public int W { get; set; }
        [XmlAttribute]
        public int H { get; set; }

    }

    public class Focussable {
        public Component Component { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    public abstract class Container : SizedComponent, IContainer {
        [XmlElement("Component")]
        public List<Component> Children { get; set; } = new();

        [XmlAttribute]
        public bool InputPassthrough { get; set; }

        private class ChildInterfaceList : IList<IComponent> {
            public IComponent this[int index] { 
                get => UnderlyingList[index]; 
                set => UnderlyingList[index] = value as Component; 
            }

            public List<Component> UnderlyingList { get; set; }

            public int Count => UnderlyingList.Count;

            public bool IsReadOnly => false;

            public void Add(IComponent item) {
                UnderlyingList.Add(item as Component);
            }

            public void Clear() {
                UnderlyingList.Clear();
            }

            public bool Contains(IComponent item) => UnderlyingList.Contains(item);

            public void CopyTo(IComponent[] array, int arrayIndex) {
                throw new NotImplementedException();
            }

            public IEnumerator<IComponent> GetEnumerator() {
                throw new NotImplementedException();
            }

            public int IndexOf(IComponent item) => UnderlyingList.IndexOf(item as Component);

            public void Insert(int index, IComponent item) {
                UnderlyingList.Insert(index, item as Component);
            }

            public bool Remove(IComponent item) => UnderlyingList.Remove(item as Component);

            public void RemoveAt(int index) {
                UnderlyingList.RemoveAt(index);
            }

            IEnumerator IEnumerable.GetEnumerator() => UnderlyingList.GetEnumerator();
        }


        private ChildInterfaceList _ciList;
        [XmlIgnore]
        IList<IComponent> IContainer.Children {
            get {
                _ciList ??= new ChildInterfaceList { UnderlyingList = Children };
                return _ciList;
            }
        }

        public override void Draw(LayoutModel model, UIBatch ui, int offsetX, int offsetY, Func<float> getZ) {
            foreach (var child in Children.Where(c => c.Visible))
                child.Draw(model, ui, offsetX + X, offsetY + Y, getZ);
        }

        public IEnumerable<Focussable> FocussableChildren() {
            foreach (var child in Children.Where(c => (c.OnClick != null) || (c.OnFocussed != null)))
                yield return new Focussable {
                    Component = child,
                    X = child.X,
                    Y = child.Y,
                };
            foreach (var container in Children.OfType<Container>().Where(c => c.InputPassthrough))
                foreach (var gChild in container.FocussableChildren()) {
                    gChild.X += container.X;
                    gChild.Y += container.Y;
                    yield return gChild;
                }
        }
    }

    public class Box : Container {

        public float BackgroundAlpha { get; set; } = 1f;

        public override void Draw(LayoutModel model, UIBatch ui, int offsetX, int offsetY, Func<float> getZ) {
            ui.DrawBox(new Rectangle(offsetX + X, offsetY + Y, W, H), getZ(), BackgroundAlpha);
            base.Draw(model, ui, offsetX, offsetY, getZ);
        }
    }

    public class List : Container {
        [XmlAttribute]
        public int ItemHeight { get; set; } = 30;
        [XmlAttribute]
        public int ItemSpacing { get; set; } = 0;

        public int GetSelectedIndex(LayoutModel model) => Children.IndexOf(model.Focus);

        private int _first = -1;

        private void Rearrange() {
            foreach (int i in Enumerable.Range(0, Children.Count)) {
                Children[i].Visible = (i >= _first) && (i < (_first + H / ItemHeight));
                Children[i].Y = (i - _first) * ItemHeight;
                if (Children[i] is SizedComponent sized)
                    sized.H = ItemHeight - ItemSpacing;
            }
        }

        public override void Draw(LayoutModel model, UIBatch ui, int offsetX, int offsetY, Func<float> getZ) {
            if (_first < 0) {
                _first = 0;
                Rearrange();
            }
            int focus = GetSelectedIndex(model);
            if (focus >= 0) {
                int? newFirst = null;
                if (focus < _first)
                    newFirst = focus;
                else if (focus >= (_first + H / ItemHeight ))
                    newFirst = focus - H / ItemHeight + 1;
                if (newFirst != null) {
                    _first = newFirst.Value;
                    Rearrange();
                }
            }
            base.Draw(model, ui, offsetX, offsetY, getZ);
        }
    }

    public class Group : Container {
        [XmlAttribute("Background")]
        public string BackgroundString { get; set; } = "Black";
        [XmlAttribute]

        private Color? _background;
        [XmlIgnore]
        public Color Background {
            get => GetColor(BackgroundString, ref _background);
            set => _background = value;
        }

        [XmlAttribute]
        public float BackgroundAlpha { get; set; }


        public override void Draw(LayoutModel model, UIBatch ui, int offsetX, int offsetY, Func<float> getZ) {
            if (BackgroundAlpha > 0)
                ui.DrawImage("white", X + offsetX, Y + offsetY, getZ(), new Point(W, H), color: Background.WithAlpha((byte)(BackgroundAlpha * 255)));
            base.Draw(model, ui, offsetX, offsetY, getZ);
        }

    }

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

        [XmlIgnore]
        public override string Description => base.Description ?? Text;

        public override void Draw(LayoutModel model, UIBatch ui, int offsetX, int offsetY, Func<float> getZ) {
            if (!string.IsNullOrWhiteSpace(Text)) {
                int ry = offsetY + Y;
                foreach(string line in Text.Split('\r')) {
                    ui.DrawText(Font, line, offsetX + X, ry, getZ(), Color, Alignment);
                    ry += 25; //TODO?!
                }
            }
        }
    }

    public class Image : Component {
        [XmlText]
        public string ImageName { get; set; }
        [XmlAttribute]
        public float Scale { get; set; } = 1f;

        [XmlAttribute("Color")]
        public string ColorString { get; set; } = "White";
        [XmlAttribute]

        private Color? _color;
        [XmlIgnore]
        public Color Color {
            get => GetColor(ColorString, ref _color);
            set => _color = value;
        }

        public override void Draw(LayoutModel model, UIBatch ui, int offsetX, int offsetY, Func<float> getZ) {
            if (!string.IsNullOrWhiteSpace(ImageName))
                ui.DrawImage(ImageName, offsetX + X, offsetY + Y, getZ(), Alignment.Left, Scale, Color);
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

        public override void Draw(LayoutModel model, UIBatch ui, int offsetX, int offsetY, Func<float> getZ) {
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
        [XmlAttribute]
        public string Description { get; set; }
    }

    public abstract class LayoutModel : IBraverTemplateModel {

        protected Dictionary<string, Component> _components = new Dictionary<string, Component>(StringComparer.InvariantCultureIgnoreCase);
        protected FGame _game;
        protected LayoutScreen _screen;

        public Container FocusGroup => (Container)(_focus.Any() ? _components[_focus.Peek().ContainerID] : null);
        public Component Focus {
            get {
                if (_focus.Any()) {
                    string id = _focus.Peek().FocusID;
                    if (!string.IsNullOrEmpty(id))
                        return _components[id];
                }
                return null;
            }
        }

        public Component FlashFocus { get; set; }
        public bool InputEnabled { get; set; } = true;
        public FGame Game => _game;
        public virtual string Description => null;

        public virtual bool IsRazorModel => false;

        string IBraverTemplateModel.SourceCategory => "layout";
        string IBraverTemplateModel.SourceExtension => "xml";

        private class FocusEntry {
            public string ContainerID { get; set; }
            public string FocusID { get; set; }
        }

        private Stack<FocusEntry> _focus = new();

        public virtual void CancelPressed() {
            _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
            PopFocus(); 
        }

        void FocusUpdated() {
            var f = Focus;
            if (f != null) {
                Focus?.OnFocussed?.Invoke();
                var group = FocusGroup.FocussableChildren().Select(child => child.Component).ToList();
                Game.InvokeOnMainThread(
                    () => _screen.Plugins.Call(ui => ui.Menu(group.Select(c => c.Description), group.IndexOf(f), FocusGroup)),
                    1
                ); //delay so initial announcement happens after loading has finished
            }
        }

        public void PushFocus(Container group, Component focus) {
            _focus.Push(new FocusEntry {
                ContainerID = group.ID,
                FocusID = focus?.ID,
            });
            FlashFocus = null;
            FocusUpdated();
        }
        public void PopFocus() {
            _focus.Pop();
            FlashFocus = null;
            FocusUpdated();
        }
        public void ChangeFocus(Component focus) {
            Trace.Assert(focus.FocusParent == FocusGroup);
            _focus.Peek().FocusID = focus?.ID;
            FocusUpdated();
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

        public virtual void Created(FGame g, LayoutScreen screen) {
            _game = g;
            _screen = screen;
        }

        public void Init(Layout layout) {

            Type thisType = this.GetType();
            var fields = thisType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .ToDictionary(f => f.Name, f => f, StringComparer.InvariantCultureIgnoreCase);

            void Descend(Component c) {
                if (!string.IsNullOrEmpty(c.ID)) {
                    _components[c.ID] = c;
                    if (fields.TryGetValue(c.ID, out var field))
                        field.SetValue(this, c);
                }

                Action GetHandler(string name) {
                    if (!string.IsNullOrEmpty(name)) {
                        var method = thisType.GetMethod(name);
                        switch (method.GetParameters().Length) {
                            case 0:
                                return () => method.Invoke(this, null);
                            case 1:
                                return () => method.Invoke(this, new object[] { c });
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    return null;
                }

                c.OnClick = GetHandler(c.Click);
                c.OnFocussed = GetHandler(c.Focussed);

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

    internal static class LayoutRazor {
        public static Layout ApplyLayout(this RazorLayoutCache cache, string layout, bool forceReload, object model = null) {
            string xml = cache.Compile("layout", layout + ".xml", forceReload).Run(template => {
                template.Model = model ?? cache.Game;
            });
            return Serialisation.Deserialise<Layout>(xml);
        }

    }

    public class LayoutScreen : Screen, ILayoutScreen {
        private Layout _layout;
        private LayoutModel _model;
        private UIBatch _ui;
        private string _layoutFile;

        private static Task _backgroundLoad;
        public static void BeginBackgroundLoad(FGame g, string layout) {
            var cache = g.Singleton(() => new RazorLayoutCache(g));
            _backgroundLoad = Task.Run(() => cache.Compile("layout", layout + ".xml", true));
        }

        public override Color ClearColor => Color.Black;
        public object Param { get; private set; }
        public override string Description => _model.Description ?? _layout.Description ?? "No description";

        public PluginInstances<IUI> Plugins { get; private set; }

        IComponent ILayoutScreen.Root => _layout.Root;
        IContainer ILayoutScreen.FocusGroup => _model.FocusGroup;
        IComponent ILayoutScreen.Focus => _model.Focus;
        dynamic ILayoutScreen.Model => _model;

        private bool _isEmbedded;

        public LayoutScreen(string layout, LayoutModel model = null, object parm = null, bool isEmbedded = false) {
            _layoutFile = layout;
            Param = parm;
            _model = model ?? Activator.CreateInstance(Type.GetType("Braver.UI.Layout." + layout)) as LayoutModel;
            _isEmbedded = isEmbedded;   
        }

        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);
            Plugins = GetPlugins<IUI>(_layoutFile);
            if (!_isEmbedded) g.Net.Send(new Net.UIScreenMessage());
            _model.Created(Game, this);

            if (_backgroundLoad != null) {
                _backgroundLoad.Wait();
                _backgroundLoad = null;
            }
            _layout = g.Singleton(() => new RazorLayoutCache(g)).ApplyLayout(_layoutFile, false, _model.IsRazorModel ? _model : null);
            _model.Init(_layout);
            _ui = new UIBatch(graphics, g);
            if (!_isEmbedded) g.Net.Send(new Net.ScreenReadyMessage());
            Plugins.Call(ui => ui.Init(this));
        }

        public void Reload(bool forceReload = false) {
            _model.Created(Game, this);
            _layout = Game.Singleton(() => new RazorLayoutCache(Game)).ApplyLayout(_layoutFile, forceReload, _model.IsRazorModel ? _model : null);
            _model.Init(_layout);
            Plugins.Call(ui => ui.Reloaded());
        }

        protected override void DoRender() {
            _ui.Render();
        }


        private string _lastState;

        protected override void DoStep(GameTime elapsed) {
            _ui.Reset();
            float z = 0;
            _layout.Root.Draw(_model, _ui, 0, 0, () => {
                z += UIBatch.Z_ITEM_OFFSET;
                return z;
            });

            _model.Step();

            if (_model.Focus != null) {
                var pos = _model.Focus.GetAbsolutePosition();
                if ((_model.Focus is SizedComponent sized) && (sized.H >= 64)) //only if large enough that offsetting makes sense
                    pos.Y += sized.H / 2;
                _ui.DrawImage("pointer", pos.X - 5, pos.Y, z + UIBatch.Z_ITEM_OFFSET, Alignment.Right);
            }
            if (_model.FlashFocus != null) {
                if (((int)(elapsed.TotalGameTime.TotalSeconds * 30) % 2) == 0) {
                    var pos = _model.FlashFocus.GetAbsolutePosition();
                    if ((_model.FlashFocus is SizedComponent sized) && (sized.H >= 64)) //only if large enough that offsetting makes sense
                        pos.Y += sized.H / 2;
                    _ui.DrawImage("pointer", pos.X - 5, pos.Y, z + UIBatch.Z_ITEM_OFFSET, Alignment.Right);
                }
            }

            string state = _ui.SaveState();
            if (!state.Equals(_lastState)) {
                Game.Net.Send(new Net.UIStateMessage {
                    ClearColour = ClearColor.PackedValue,
                    State = state,
                    Description = this.Description,
                });
                _lastState = state;
            }
        }

        private Component FindNextFocus(Container container, Component current, int ox, int oy) {
            var candidates = container.FocussableChildren();
            var currentFocus = candidates.FirstOrDefault(c => c.Component == current);
            candidates = candidates.Where(c => c.Component != current);

            switch (ox) {
                case 1:
                    candidates = candidates
                        .Where(c => (c.X > currentFocus.X) && (Math.Abs(c.Y - currentFocus.Y) < Math.Abs(c.X - currentFocus.X)));
                    break;
                case -1:
                    candidates = candidates
                        .Where(c => (c.X < currentFocus.X) && (Math.Abs(c.Y - currentFocus.Y) < Math.Abs(c.X - currentFocus.X)));
                    break;
            }
            switch (oy) {
                case 1:
                    candidates = candidates
                        .Where(c => (c.Y > currentFocus.Y) && (Math.Abs(c.X - currentFocus.X) < Math.Abs(c.Y - currentFocus.Y)));
                    break;
                case -1:
                    candidates = candidates
                        .Where(c => (c.Y < currentFocus.Y) && (Math.Abs(c.X - currentFocus.X) < Math.Abs(c.Y - currentFocus.Y)));
                    break;
            }

            var result = candidates
                .OrderBy(c => (new Vector2(c.X, c.Y) - new Vector2(currentFocus.X, currentFocus.Y)).LengthSquared())
                .FirstOrDefault();

            return result?.Component;
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);

            if (Plugins.CallAll(ui => ui.PreInput(input)).Any(b => b))
                return;

            if (!_model.InputEnabled)
                return;
            if (_model.ProcessInput(input))
                return;

            if (_model.Focus != null) {

                if (input.IsJustDown(InputKey.OK)) {
                    Game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
                    _model.Focus.OnClick?.Invoke();
                } else {
                    Component next = null;
                    if (input.IsRepeating(InputKey.Up))
                        next = FindNextFocus(_model.FocusGroup, _model.Focus, 0, -1);
                    else if (input.IsRepeating(InputKey.Down))
                        next = FindNextFocus(_model.FocusGroup, _model.Focus, 0, 1);
                    else if (input.IsRepeating(InputKey.Left))
                        next = FindNextFocus(_model.FocusGroup, _model.Focus, -1, 0);
                    else if (input.IsRepeating(InputKey.Right))
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

        IComponent ILayoutScreen.Load(string templateName, object? model) {
            var cache = Game.Singleton(() => new RazorLayoutCache(Game));
            string xml = cache.ApplyPartial("layout", templateName + ".xml", false, model ?? _model);
            return Serialisation.Deserialise<Component>(xml);
        }

        void ILayoutScreen.PushFocus(IContainer group, IComponent focus) {
            _model.PushFocus(group as Container, focus as Component);
        }

        void ILayoutScreen.PopFocus() => _model.PopFocus();

        void ILayoutScreen.ChangeFocus(IComponent focus) => _model.ChangeFocus(focus as Component);
    }
}
