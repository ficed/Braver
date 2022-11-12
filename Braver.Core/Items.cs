using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {
    public class Item : CacheItem {

        public string Name { get; private set; }
        public string Description { get; private set; }

        public override void Init(BGame g, int index) {
            var text = g.Singleton<GameText>(() => new GameText(g));
            Name = text.Get(19, index);
            Description = text.Get(11, index);
        }
    }

    public class KeyItem : CacheItem {

        public string Name { get; private set; }
        public string Description { get; private set; }

        public override void Init(BGame g, int index) {
            var text = g.Singleton<GameText>(() => new GameText(g));
            Name = text.Get(24, index);
            Description = text.Get(16, index);
        }
    }
}
