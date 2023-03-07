// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {

    public class BuyItem {
        public Ficedula.FF7.ShopItem Item { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Cost { get; set; }
    }

    public class Shop : LayoutModel {

        public Label lDescription, lBuy, lSell, lExit;
        public Box bMenu, bBuyItem;
        public List lbBuy;

        public override bool IsRazorModel => true;

        public Ficedula.FF7.Shop ShopData { get; private set; }
        public List<BuyItem> Items { get; } = new();
        public int BuyQuantity { get; private set; }
        public int TotalCost => Items[_buyIndex].Cost * BuyQuantity;
        public BuyItem CurrentBuyItem => Items[_buyIndex];

        public bool ShowBuyItems => (FocusGroup != null) && ((FocusGroup == lbBuy) || (FocusGroup == bBuyItem));
        public bool ShowBuyTotal => (FocusGroup != null) && (FocusGroup == bBuyItem);
        public bool ShowDescription => (FocusGroup != null) && (FocusGroup != bMenu);

        private int _buyIndex;


        public override void Created(FGame g, LayoutScreen screen) {
            base.Created(g, screen);
            if (ShopData == null) {
                var shops = _game.Singleton<Shops>();
                var materias = _game.Singleton<Materias>();
                ShopData = shops.Data.Shops[(int)screen.Param];
                foreach (var item in ShopData.Items) {
                    switch (item.Kind) {
                        case Ficedula.FF7.ShopItemKind.Item:
                            var inv = ItemMenu.GetInventory(Game, item.ItemID);
                            Items.Add(new BuyItem {
                                Item = item,
                                Name = inv.Item,
                                Description = inv.Description,
                                Cost = shops.Data.ItemCosts[item.ItemID],
                            });
                            break;
                        case Ficedula.FF7.ShopItemKind.Materia:
                            Items.Add(new BuyItem {
                                Item = item,
                                Name = materias[item.ItemID].Name,
                                Description = materias[item.ItemID].Description,
                                Cost = shops.Data.MateriaCosts[item.ItemID],
                            });
                            break;
                    }
                }
            }
        }

        protected override void OnInit() {
            base.OnInit();
            if (FocusGroup == null) {
                PushFocus(bMenu, lBuy);
            }
        }

        public void BuyItemFocussed(Group G) {
            _buyIndex = int.Parse(G.ID.Substring(4));
            _screen.Reload();
        }

        public void BuyItemSelected(Group G) {
            _buyIndex = int.Parse(G.ID.Substring(4));
            BuyQuantity = 1;
            PushFocus(bBuyItem, null);
            _screen.Reload();
        }

        public void MenuSelected(Label L) {
            if (L == lBuy) {
                PushFocus(lbBuy, lbBuy.Children[0]);
                _screen.Reload();
            } else if (L == lSell) {

            } else if (L == lExit) {
                _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
                InputEnabled = false;
                _screen.FadeOut(() => _game.PopScreen(_screen));
            }
        }

        public override bool ProcessInput(InputState input) {
            if (FocusGroup == bBuyItem) {
                if (input.IsRepeating(InputKey.Right)) {
                    BuyQuantity = Math.Min(BuyQuantity + 1, 99);
                    _screen.Reload();
                } else if (input.IsRepeating(InputKey.Left)) {
                    BuyQuantity = Math.Max(1, BuyQuantity - 1);
                    _screen.Reload();
                } else if (input.IsJustDown(InputKey.Cancel)) {
                    PopFocus();
                    _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
                    _screen.Reload();
                } else if (input.IsJustDown(InputKey.OK)) {
                    if (TotalCost <= Game.SaveData.Gil) {
                        switch (Items[_buyIndex].Item.Kind) {
                            case Ficedula.FF7.ShopItemKind.Item:
                                Game.SaveData.GiveInventoryItem(Items[_buyIndex].Item.ItemID, BuyQuantity);
                                break;
                            case Ficedula.FF7.ShopItemKind.Materia:
                                foreach (int _ in Enumerable.Range(0, BuyQuantity))
                                    Game.SaveData.GiveMateria(new OwnedMateria { MateriaID = Items[_buyIndex].Item.ItemID });
                                break;
                        }
                        Game.SaveData.Gil -= TotalCost;
                        _game.Audio.PlaySfx(Sfx.BuyItem, 1f, 0f);
                        PopFocus();
                        _screen.Reload();
                    } else {
                        _game.Audio.PlaySfx(Sfx.Invalid, 1f, 0f);
                    }
                }
                return true;
            }
            return base.ProcessInput(input);
        }

        public override void CancelPressed() {
            if (FocusGroup == bMenu) {
                _game.Audio.PlaySfx(Sfx.Cancel, 1f, 0f);
                InputEnabled = false;
                _screen.FadeOut(() => _game.PopScreen(_screen));
            } else {
                base.CancelPressed();
                _screen.Reload();
            }
        }

    }
}
