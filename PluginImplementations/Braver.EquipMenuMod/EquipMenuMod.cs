// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Braver.Plugins.UI;

namespace Braver.EquipMenuMod {
    public class EquipMenuModPlugin : Plugin {
        public override string Name => "Sample Equip Menu Mod";

        public override Version Version => new Version(0, 0, 1);

        public override object ConfigObject => null;

        public override IEnumerable<IPluginInstance> Get(string context, Type t) {
            if (context == "EquipMenu")
                yield return new EquipMenuDetails();
        }

        public override IEnumerable<Type> GetPluginInstances() {
            yield return typeof(IUI);
        }

        public override void Init(BGame game) {
            //TODO - change this for a better way of registering data sources alongside plugins
            string folder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            game.AddDataSource("Layout", new FileDataSource(Path.Combine(folder, "Layout")));
        }
    }

    public class EquipMenuDetails : IUI {
        private ILayoutScreen _screen;
        private IComponent _ui;
        public void Init(ILayoutScreen screen) {
            _screen = screen;
            Reloaded();
        }

        public void Menu(IEnumerable<string> items, int selected, object container) {
            //
        }

        public bool PreInput(InputState input) {
            if (input.IsJustDown(InputKey.Select) && (_ui == null)) {
                dynamic selected = _screen.Model.FocusedWeapon;
                if (selected != null) {
                    var data = new Model {
                        Text = selected.Description,
                        Image = "logo_buster",
                    };
                    _ui = _screen.Load("EquipMenuMod", data);
                    (_screen.Root as IContainer).Children.Add(_ui);
                    return true;
                }
            } else if (input.IsJustDown(InputKey.Cancel) && (_ui != null)) {
                (_screen.Root as IContainer).Children.Remove(_ui);
                _ui = null;
                return true;
            }
            return false;
        }

        public class Model {
            public string Text { get; set; }
            public string Image { get; set; }
        }

        public void Reloaded() {
        }
    }
}