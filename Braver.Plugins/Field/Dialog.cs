// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Plugins.Field {
    public interface IDialog : IPluginInstance {
        void Showing(int window, int tag, IEnumerable<string> text);
        void Asking(int window, int tag, IEnumerable<string> text, IEnumerable<int> choiceLines);
        void Dialog(int tag, int index, string dialog);
        void ChoiceSelected(IEnumerable<string> choices, int selected);
        void ChoiceMade(int window, int choice);
    }
}
