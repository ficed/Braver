using Ficedula.FF7;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {
  
    public class KeyItem {
        public string Name { get; init; }
        public string Description { get; init; }
    }

    public class KernelCache : Cacheable {
        public Kernel Kernel { get; private set; }

        public override void Init(BGame g) {
            Kernel = new Kernel(g.Open("kernel", "kernel.bin"));
        }
    }

    public class KeyItems : Cacheable {

        private List<KeyItem> _keyitems = new();

        public IReadOnlyList<KeyItem> Items => _keyitems.AsReadOnly();

        public override void Init(BGame g) {
            var kernel = g.Singleton<KernelCache>();
            var names = new KernelText(kernel.Kernel.Sections[24]);
            var descriptions = new KernelText(kernel.Kernel.Sections[16]);

            _keyitems = Enumerable.Range(0, names.Count)
                .Select(i => new KeyItem {
                    Name = names.Get(i),
                    Description = descriptions.Get(i),
                })
                .ToList();
        }

    }

    public abstract class CachedText : Cacheable {

        private KernelText _names, _descriptions;

        public (string Name, string Description) this[int index] => (_names.Get(index), _descriptions.Get(index));
        public int Count => _names.Count;

        protected void Init(BGame g, int nameSection, int descriptionSection) {
            var kernel = g.Singleton<KernelCache>();
            _names = new KernelText(kernel.Kernel.Sections[nameSection]);
            _descriptions = new KernelText(kernel.Kernel.Sections[descriptionSection]);
        }
    }

    public class MagicText : CachedText {
        public override void Init(BGame g) {
            base.Init(g, 18, 10);
        }
    }

    public class Items : Cacheable {
        private ItemCollection _items;

        public Item this[int index] => _items.Items[index];
        public int Count => _items.Items.Count;

        public override void Init(BGame g) {
            var kernel = g.Singleton<KernelCache>();
            _items = new ItemCollection(kernel.Kernel);
        }
    }

    public class Weapons : Cacheable {
        private WeaponCollection _weapons;

        public Weapon this[int index] => _weapons.Weapons[index];
        public int Count => _weapons.Weapons.Count;

        public override void Init(BGame g) {
            var kernel = g.Singleton<KernelCache>();
            _weapons = new WeaponCollection(kernel.Kernel);
        }
    }

    public class Armours : Cacheable {
        private ArmourCollection _armours;

        public Armour this[int index] => _armours.Armour[index];
        public int Count => _armours.Armour.Count;

        public override void Init(BGame g) {
            var kernel = g.Singleton<KernelCache>();
            _armours = new ArmourCollection(kernel.Kernel);
        }
    }

    public class Accessories : Cacheable {
        private AccessoryCollection _accessories;

        public Accessory this[int index] => _accessories.Accessories[index];
        public int Count => _accessories.Accessories.Count;

        public override void Init(BGame g) {
            var kernel = g.Singleton<KernelCache>();
            _accessories = new AccessoryCollection(kernel.Kernel);
        }
    }

    public class Materias : Cacheable {
        private MateriaCollection _materia;
        public Materia this[int index] => _materia.Item[index];
        public int Count => _materia.Item.Count;

        public override void Init(BGame g) {
            var kernel = g.Singleton<KernelCache>();
            _materia = new MateriaCollection(kernel.Kernel);
        }
    }
}
