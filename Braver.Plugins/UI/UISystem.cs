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
using System.Threading.Tasks;

namespace Braver.Plugins.UI {

    public interface UISystem : IPluginInstance {

        void ActiveScreenChanged(IScreen screen);

        void Dialog(string dialog);
        void Choices(IEnumerable<string> choices, int selected);

        void Menu(IEnumerable<string> items, int selected, object container);

        void BattleCharacterReady(ICombatant character);
        void BattleTargetHighlighted(IEnumerable<ICombatant> targets);
        void BattleActionStarted(string action);
        void BattleActionResult(IInProgress result);
    }
}
