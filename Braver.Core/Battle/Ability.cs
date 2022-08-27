using Ficedula.FF7;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Battle {

    public enum AttackFormula {
        Physical,
        Magical,
        Cure,
        Item,
        HPPercent,
        MaxHPPercent,
        Fixed,
        Recovery,
        Custom,
    }

    public struct Ability {

        public Statuses InflictStatus { get; set; }
        public Statuses RemoveStatus { get; set; }
        public Statuses ToggleStatus { get; set; }
        public int StatusChance { get; set; }

        public HashSet<Element> Elements { get; set; }

        public int Power { get; set; }
        public AttackFormula Formula { get; set; }

        public int PAtPercent { get; set; }
        public int MAtPercent { get; set; }
        public bool IsPhysical { get; set; }
        public bool IsMagical { get; set; }
        public bool IsReflectable { get; set; }
        public bool AutoCritical { get; set; }
        public bool NoSplit { get; set; }
        public bool LongRange { get; set; }
        public bool IsQuadraMagic { get; set; }
        public bool DamageMP { get; set; }
        public int MPTurboLevel { get; set; }
        public bool IsRestore { get; set; }
    }
}
