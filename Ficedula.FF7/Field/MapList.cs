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

namespace Ficedula.FF7.Field {
    public class MapList {
        private List<string> _names = new();

        public IReadOnlyList<string> Items => _names.AsReadOnly();

        public MapList(Stream s) {
            byte[] buffer = new byte[0x20];
            foreach(int _ in Enumerable.Range(0, s.ReadI16())) {
                s.Read(buffer, 0, buffer.Length);
                _names.Add(Encoding.UTF8.GetString(buffer).TrimEnd('\0'));
            }
        }
    }
}
