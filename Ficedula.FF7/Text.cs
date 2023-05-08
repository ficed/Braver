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

namespace Ficedula.FF7 {
    public static class Text {
        private static char[] _translate = new[] {
            ' ', '!', '"', '#', '$', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/',
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ':', ';', '<', '=', '>', '?',
            '@', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O',
            'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '[', '\\', ']', '^', '_',
            '`', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o',
            'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '{', ' ', '}', '~', ' ',
            '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬',
            '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬',
            '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬',
            '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬',
            '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '…', '¬', '¬', '¬', '¬', '¬', '¬',
            '¬', '¬', '“', '”', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬',
            '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬',
            ' ', '¬', '\xE020', '\xE021', '\xE022', '\xE023', '\xE024', '\xE025', '\xE026', '\xE027', '\xE028', '\xE029', '\xE030', '¬', '¬', '¬',
            '\xE000', '\t', '\xE003', '\xE001', '\xE002', '¬', '¬', '\r', '\xC', '\xE005', '\xE006', '\xE007', '\xE008', '\xE009', '\xE00A', '\xE00B',
            '\xE00C', '\xE00D', '\xE00E', '\xE00F', '\xE010', '\xE011', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '¬', '\xE012', '\xE013',
        };

        public static string Convert(byte[] input, int offset, int? length = null) {
            char[] c = Enumerable.Range(offset, length ?? input.Length - offset)
                .Select(i => _translate[input[i]])
                .ToArray();
            return new string(c);
        }

        public static string Expand(string input, string[] charNames, string[] partyNames) {
            StringBuilder sb = new StringBuilder();
            foreach (char c in input) {
                switch (c) {
                    case '\xE001':
                        sb.Append(".\""); break;
                    case '\xE002':
                        sb.Append("...\""); break;
                    case '\xE003':
                        sb.Append(", "); break;
                    case '\xE006':
                    case '\xE007':
                    case '\xE008':
                    case '\xE009':
                    case '\xE00A':
                    case '\xE00B':
                    case '\xE00C':
                    case '\xE00D':
                    case '\xE00E':
                        sb.Append(charNames[c - 0xE006]); break;
                    case '\xE00F':
                    case '\xE010':
                    case '\xE011':
                        sb.Append(partyNames[c - 0xE00F]); break;
                    default:
                        sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
