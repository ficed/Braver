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

namespace Ficedula.FF7.WorldMap {
    public class Messages {
        private List<string> _messages = new();

        public int Count => _messages.Count;
        public string Get(int index) => _messages[index];

        public Messages(Stream source) {
            var offsets = Enumerable.Range(0, source.ReadI16())
                .Select(_ => source.ReadI16())
                .ToArray();

            foreach(int i in Enumerable.Range(0, offsets.Length)) {
                source.Position = offsets[i];
                byte[] data;
                if (i < (offsets.Length - 1)) {
                    data = new byte[offsets[i + 1] - offsets[i]];
                    source.ReadExactly(data, 0, data.Length);
                } else {
                    data = new byte[source.Length - offsets[i]];
                    source.ReadExactly(data, 0, data.Length);
                    //Trim off trailing zeroes
                    data = data
                        .Reverse()
                        .SkipWhile(b => b == 0)
                        .Reverse()
                        .ToArray();
                }
                _messages.Add(Text.Convert(data, 0).Trim('\xE013')); //TODO - control code might be needed?
            }
        }

    }
}
