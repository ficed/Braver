using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7 {

    public enum ShopItemKind {
        Item,
        Materia,
    }
    public class ShopItem {
        public ShopItemKind Kind { get; set; }
        public int ItemID { get; set; }
    }
    public class Shop {
        public byte TextIndex { get; init; }
        public short NameIndex { get; init; }
        public List<ShopItem> Items { get; } = new();
    }

    public class ShopData {

        private List<Shop> _shops = new();

        public IReadOnlyList<Shop> Shops => _shops.AsReadOnly();

        public ShopData(Stream s) {
            while (true) {
                byte text = s.ReadU8(), dummy = s.ReadU8();
                short name = s.ReadI16();
                if ((text == 0) && (name == 0)) break;

                var shop = new Shop {
                    TextIndex = text,
                    NameIndex = name,
                };
                _shops.Add(shop);

                foreach(int i in Enumerable.Range(0, 10)) {
                    short kind = s.ReadI16(), dummy2 = s.ReadI16();
                    int index = s.ReadI32();
                    if ((i == 0) || (kind != 0) || (dummy2 != 0) || (index != 0)) {
                        shop.Items.Add(new ShopItem {
                            Kind = (ShopItemKind)kind,
                            ItemID = index,
                        });
                    }
                }
            }
        }
    }
}
