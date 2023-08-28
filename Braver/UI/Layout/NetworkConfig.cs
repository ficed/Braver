// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.UI.Layout {

    public class NetworkConfigCharacterMap {
        public Character Character { get; set; }
        public Net.NetPlayer NetPlayer { get; set; }

        public string PlayerName => NetPlayer?.Name ?? "(Host)";
    }

    public class NetworkConfig : LayoutModel {
        public override bool IsRazorModel => true;

        public List<NetworkConfigCharacterMap> CharacterMap { get; } = new();

        public Box bPlayerPopup;
        public Label lAddPlayer;
        public Group gRoot;

        private void FixupConfig() {
            CharacterMap.Clear();

            CharacterMap.AddRange(
                _game.SaveData.Characters
                .Where(c => c != null)
                .Select(chr => new NetworkConfigCharacterMap {
                    Character = chr,
                    NetPlayer = _game.NetConfig.Players.SingleOrDefault(id => id.ID == _game.NetConfig.CharacterMap.SingleOrDefault(m => m.CharIndex == chr.CharIndex)?.PlayerID)
                })
            );

        }

        public void SaveDefaults() {
            Game.SaveDefaultNetworkConfig();
        }

        public void AddPlayer() {
            var player = new NetPlayer();
            int i = 1;
            do {
                player.Name = "Player " + i;
                i++;
            } while (_game.NetConfig.Players.Any(p => p.Name == player.Name));

            _game.NetConfig.Players.Add(player);
            _screen.Reload();
        }

        public void DeletePlayer(Label sender) {
            Guid id = Guid.Parse(sender.ID.Substring(3));
            _game.NetConfig.Players.RemoveAll(p => p.ID == id);
            _screen.Reload();
        }

        public override void Created(FGame g, LayoutScreen screen) {
            base.Created(g, screen);
            FixupConfig();
        }

        protected override void OnInit() {
            base.OnInit();
            FixupConfig();

            if (Focus == null)
                PushFocus(gRoot, lAddPlayer);
        }

        public void RoleSelected(Label sender) {
            Guid id = Guid.Parse(sender.ID.Substring(4));
            var player = Game.NetConfig.Players.Single(p => p.ID == id);
            var options = Enum.GetValues<PlayerRole>().ToList();
            player.Role = options[(options.IndexOf(player.Role) + 1) % options.Count];
            _screen.Reload();
        }

        public override void CancelPressed() {
            if (FocusGroup == gRoot) {
                _screen.FadeOut(() => _game.PopScreen(_screen));
            } else {
                base.CancelPressed();
                bPlayerPopup.Visible = FocusGroup == bPlayerPopup;
            }
        }

        private Character _mapping;
        public void MapCharacter(Label sender) {
            int index = int.Parse(sender.ID.Substring(3));
            _mapping = Game.SaveData.Characters
                .Where(c => c != null)
                .Single(c => c.CharIndex == index);
            bPlayerPopup.Visible = true;
            PushFocus(bPlayerPopup, bPlayerPopup.FocussableChildren().First().Component);
        }

        public void SelectPlayer(Label sender) {
            Guid id = Guid.Parse(sender.ID.Substring(6));

            var map = Game.NetConfig
                .CharacterMap
                .Single(m => m.CharIndex == _mapping.CharIndex);
            map.PlayerID = id;

            PopFocus();
            _screen.Reload();
        }
    }
}
