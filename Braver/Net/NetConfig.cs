// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.UI.Layout;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Net {

    public enum PlayerRole {
        Player,
        Viewer,
    }

    public class NetPlayer {

        private static string NewKey() {

            const string KEY_CHARS = "QWRTYPSDFGHJKLZXCVBNM";

            var r = new Random();
            return new string(
                Enumerable.Range(0, 8)
                .Select(_ => KEY_CHARS[r.Next(KEY_CHARS.Length)])
                .ToArray()
            );
        }

        public Guid ID { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string Key { get; set; } = NewKey();
        public PlayerRole Role { get; set; }
    }

    public class NetCharacterMap {
        public int CharIndex { get; set; }
        public Guid PlayerID { get; set; }

        public static readonly Guid AutoAssign = Guid.Parse("8A09B5AD-65FE-4F43-80CC-A068F9D05A91");
    }

    public class NetConfig {
        public List<NetPlayer> Players { get; set; } = new();
        public List<NetCharacterMap> CharacterMap { get; set; } = new();

        public void Fixup(FGame game) {
            game.NetConfig.CharacterMap.RemoveAll(m => !game.NetConfig.Players.Any(p => p.ID == m.PlayerID));

            var toAdd = game.SaveData.Characters
                .Where(c => c != null)
                .Where(c => !game.NetConfig.CharacterMap.Any(m => m.CharIndex == c.CharIndex))
                .ToArray();
            foreach (var add in toAdd)
                game.NetConfig.CharacterMap.Add(new NetCharacterMap {
                    CharIndex = add.CharIndex,
                });
        }
    }
}
