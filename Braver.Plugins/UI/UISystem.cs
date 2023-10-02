// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Battle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Braver.Plugins.UI {

    public static class ComponentUtil {
        public static IComponent? Find(this IComponent component, string id) {
            if (id.Equals(component?.ID, StringComparison.InvariantCultureIgnoreCase))
                return component;
            if (component is IContainer container) {
                foreach(var child in container.Children) {
                    var found = child.Find(id);
                    if (found != null) return found;    
                }
            }
            return null;
        }

        public static T? Find<T>(this IComponent component, string id) where T : IComponent {
            return (T?)component.Find(id);
        }
    }

    public interface IComponent {
        string ID { get; }
        int X { get; set; }
        int Y { get; set; }
        bool Visible { get; set; }
        IContainer Parent { get; }
    }

    public interface ISizedComponent : IComponent {
        int W { get; set; }
        int H { get; set; }
    }

    public interface IContainer : IComponent {
        IList<IComponent> Children { get; }
    }

    public interface ILayoutScreen {
        IComponent Root { get; }
        IComponent Load(string templateName, object? model);
        IContainer FocusGroup { get; }
        IComponent Focus { get; }
        dynamic Model { get; }
        void PushFocus(IContainer group, IComponent focus);
        void PopFocus();
        void ChangeFocus(IComponent focus);
    }


    public interface ISystem : IPluginInstance {
        void ActiveScreenChanged(IScreen screen);
    }

    public interface IUI : IPluginInstance {
        void Menu(IEnumerable<string> items, int selected, object container);
        void Init(ILayoutScreen screen);
        void Reloaded();
        bool PreInput(InputState input);
    }

    public interface IBattleUI : IPluginInstance {
        void Menu(IEnumerable<string> items, int selected, object container);
        void BattleCharacterReady(ICombatant character);
        void BattleTargetHighlighted(IEnumerable<ICombatant> targets);
        void BattleActionStarted(string action);
        //void BattleActionResult(IInProgress result);
    }
}
