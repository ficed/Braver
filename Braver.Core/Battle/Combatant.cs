using Ficedula.FF7;
using Ficedula.FF7.Field;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Battle {

    public struct CombatStats {
        public int Str { get; }
        public int Dex { get; }
        public int Vit { get; }
        public int Mag { get; }
        public int Spr { get; }
        public int Lck { get; }
        public int Level { get; }

        public int CriticalChance { get; }

        public int Att { get; }
        public int AtPC { get; }
        public int Def { get; }
        public int DfPC { get; }
        public int MAt { get; }
        public int MDf { get; }
        public int MDPC { get; }
    }

    public delegate CombatStats StatModifier(CombatStats stats);

    public interface ICombatant {
        public CombatStats BaseStats { get; }

        public int HP { get; set; }
        public int MaxHP { get; set; }
        public int MP { get; set; }
        public int MaxMP { get; set; }

        public Timer VTimer { get; set; }
        public Timer CTimer { get; set; }
        public Timer TTimer { get; set; }

        public int Row { get; set; }
        public bool IsBackRow { get; set; }
        public bool IsDefending { get; set; }

        public bool IsPlayer { get; set; }
        public bool PhysicalImmune { get; set; }
        public bool MagicalImmune { get; set; }

        public List<StatModifier> StatModifiers { get; }

        public Dictionary<Element, ElementResistance> Elements { get; }

        public Statuses ImmuneStatuses { get; }

        public Statuses Statuses { get; set; }
    }

    public static class CombatantUtil {
        public static CombatStats ModifiedStats(this ICombatant combatant) {
            var stats = combatant.BaseStats;
            foreach (var mod in combatant.StatModifiers)
                stats = mod(stats);
            return stats;
            //TODO we could cache these and only recalculate when necessary? If we care?
        }
    }
}
