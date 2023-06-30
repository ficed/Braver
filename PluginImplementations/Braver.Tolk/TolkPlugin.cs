// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Plugins;
using Braver.Plugins.UI;
using System.Windows.Forms;

namespace Braver.Tolk {

    public class TolkConfig {
        public bool EnableSAPI { get; set; } = true;
    }

    public class TolkPlugin : Plugin {
        private TolkConfig _config = new();

        public override string Name => "Tolk Text To Speech Plugin";
        public override Version Version => new Version(0, 0, 1);
        public override object ConfigObject => _config;

        public override IPluginInstance Get(string context, Type t) {
            return new TolkInstance(_config);
        }

        public override IEnumerable<Type> GetPluginInstances() {
            return new Type[] { typeof(UISystem) };
        }

        public override void Init(BGame game) {
            //
        }
    }

    public class TolkInstance : UISystem {

        public TolkInstance(TolkConfig config) {
            DavyKager.Tolk.TrySAPI(config.EnableSAPI);
            DavyKager.Tolk.Load();
        }

        public void ActiveScreenChanged(IScreen screen) {
            DavyKager.Tolk.Speak(screen.Description, true);
        }

        public void Choices(IEnumerable<string> choices, int selected) {
            DavyKager.Tolk.Speak(
                $"Choice {choices.ElementAtOrDefault(selected)}, {selected + 1} of {choices.Count()}",
                false
            );
        }

        public void Dialog(string dialog) {
            DavyKager.Tolk.Speak(dialog, false);
        }

        public void Menu(IEnumerable<string> items, int selected) {
            DavyKager.Tolk.Speak(
                $"Menu {items.ElementAtOrDefault(selected)}, {selected + 1} of {items.Count()}",
                false
            );
        }
    }
}