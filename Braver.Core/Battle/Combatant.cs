using Ficedula.FF7;
using Ficedula.FF7.Battle;
using Ficedula.FF7.Field;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace Braver.Battle {

    public struct CombatStats {
        public int Dex { get; set; }
        public int Lck { get; set; }
        public int Level { get; set; }

        public int CriticalChance { get; set; }

        public int Att { get; set; }
        public int Def { get; set; }
        public int DfPC { get; set; }
        public int MAt { get; set; }
        public int MDf { get; set; }
        public int MDPC { get; set; }
    }

    public delegate CombatStats StatModifier(CombatStats stats);

    public interface ICombatant {
        public CombatStats BaseStats { get; }

        public string Name { get; }

        public int HP { get; set; }
        public int MaxHP { get; }
        public int MP { get; set; }
        public int MaxMP { get; }
        public int Level { get; }

        public Timer VTimer { get; set; }
        public Timer CTimer { get; set; }
        public Timer TTimer { get; set; }

        public int Row { get; set; }
        public bool IsBackRow { get; set; }
        public bool IsDefending { get; set; }

        public bool IsPlayer { get; }
        public bool PhysicalImmune { get; set; }
        public bool MagicalImmune { get; set; }

        public ICombatant LastAttacker { get; set; }
        public ICombatant LastPhysicalAttacker { get; set; }
        public ICombatant LastMagicAttacker { get; set; }

        public List<StatModifier> StatModifiers { get; }

        public Dictionary<Element, ElementResistance> Elements { get; }

        public Statuses ImmuneStatuses { get; }

        public Statuses Statuses { get; set; }

        void Init(Engine engine, AICallbacks callbacks);
        void TakeAction();
        void Hit(QueuedAction hitBy);
    }

    public class CharacterActionItem {
        public int ID { get; set; }
        public Ability Ability { get; set; }
        public string Name { get; set; }
    }

    public class CharacterAction {
        public Ability? Ability { get; set; }
        public string Name { get; set; }
        public List<CharacterActionItem> SubMenu { get; set; }
    }

    public class CharacterCombatant : ICombatant {

        private Character _char;
        private CombatStats _stats;

        public string Name => _char.Name;
        public Character Character => _char;

        public List<CharacterAction> Actions { get; } = new();

        public CharacterCombatant(BGame g, Character chr) {
            _char = chr;

            var weapon = chr.GetWeapon(g);
            var armour = chr.GetArmour(g);
            var accessory = chr.GetAccessory(g);

            _stats = new CombatStats {
                Dex = chr.Dexterity,
                Lck = chr.Luck,
                Level = chr.Level,
                CriticalChance = weapon.CriticalChance,
                Att = chr.Strength + (weapon?.AttackStrength ?? 0),
                Def = chr.Vitality + (armour?.Defense ?? 0),
                DfPC = chr.Dexterity / 4 + (armour?.DefensePercent ?? 0),
                MAt = chr.Spirit,
                MDf = chr.Spirit + (armour?.MDefense ?? 0),
                MDPC = armour?.MDefensePercent ?? 0,
            };

            Actions.Add(new CharacterAction {
                Name = "Attack",
                Ability = new Ability {
                    PAtPercent = (byte)weapon.HitChance,
                    Power = (byte)(chr.Strength + weapon.AttackStrength),
                    IsPhysical = true,
                    Elements = new HashSet<Element>(weapon.Elements.Split()),
                    LongRange = !weapon.TargettingFlags.HasFlag(TargettingFlags.ShortRange),
                    InflictStatus = weapon.Statuses,
                    Formula = AttackFormula.Physical, //TODO                    
                }
            });

            var kernel = g.Singleton<KernelCache>();
            var attacks = g.Singleton<Attacks>();
            var materia = chr.EquippedMateria(g);
            var grantedMagic = materia
                .Where(m => m.Materia is MagicMateria)
                .SelectMany(m => (m.Materia as MagicMateria).GrantedAtLevel(m.Level))
                .Distinct()
                .OrderBy(m => m);

            if (grantedMagic.Any()) {
                var mText = new KernelText(kernel.Kernel.Sections[18]);
                var magic = new CharacterAction {
                    Name = "Magic",
                    SubMenu = new List<CharacterActionItem>()
                };
                Actions.Add(magic);
                foreach(int m in grantedMagic) {
                    magic.SubMenu.Add(new CharacterActionItem {
                        ID = m,
                        Ability = attacks[m].ToAbility(this),
                        Name = mText.Get(m),
                    });
                }
            }

            Actions.Add(new CharacterAction {
                Name = "Item",
                SubMenu = g.SaveData
                    .Inventory
                    .Where(inv => inv.Kind == InventoryItemKind.Item)
                    .Select(inv => g.Singleton<Items>()[inv.ItemID])
                    .Where(item => item.Restrictions.HasFlag(EquipRestrictions.CanUseInBattle))
                    .Select(item => {
                        return new CharacterActionItem {
                            ID = item.ID,
                            Ability = new Ability {
                                Power = item.Power,
                                Elements = new HashSet<Element>(item.Elements.Split()),
                                StatusChance = item.StatusChance,
                                InflictStatus = item.StatusType == AttackStatusType.Inflict ? item.Statuses : Statuses.None,
                                RemoveStatus = item.StatusType == AttackStatusType.Cure ? item.Statuses : Statuses.None,
                                ToggleStatus = item.StatusType == AttackStatusType.Toggle ? item.Statuses : Statuses.None,                                
                            }
                        };
                    })
                    .ToList()
            });
        }

        public CombatStats BaseStats => _stats;

        public int HP { 
            get => _char.CurrentHP;
            set => _char.CurrentHP = value; 
        }
        public int MaxHP { get => _char.MaxHP; }
        public int MP {
            get => _char.CurrentMP;
            set => _char.CurrentMP = value;
        }
        public int MaxMP { get => _char.MaxMP; }

        public int Level => _char.Level;

        public Timer VTimer { get; set; }
        public Timer CTimer { get; set; }
        public Timer TTimer { get; set; }
        public int Row { get; set; }
        public bool IsBackRow { get; set; }
        public bool IsDefending { get; set; }
        public bool IsPlayer => true;
        public bool PhysicalImmune { get; set; }
        public bool MagicalImmune { get; set; }
        public ICombatant LastAttacker { get; set; }
        public ICombatant LastPhysicalAttacker { get; set; }
        public ICombatant LastMagicAttacker { get; set; }

        public List<StatModifier> StatModifiers { get; } = new();

        public Dictionary<Element, ElementResistance> Elements { get; private set; } = new();

        public Statuses ImmuneStatuses => Statuses.None; //TODO!!!

        public Statuses Statuses { get; set; }
        public bool ReadyForAction { get; set; }

        public override string ToString() => Name;

        public void Init(Engine engine, AICallbacks callbacks) {
            //
        }

        public void TakeAction() {
            ReadyForAction = true;
        }

        public void Hit(QueuedAction hitBy) {
            //TODO
        }
    }

    public class EnemyCombatant : ICombatant {
        private EnemyInstance _enemy;
        private int _currentHP, _currentMP;
        private CombatStats _stats;

        public string Name { get; private set; }

        public EnemyInstance Enemy => _enemy;
        public AI AI { get; set; }

        public EnemyCombatant(EnemyInstance enemy, int? indexInGroup) {
            _enemy = enemy;
            _currentHP = enemy.Enemy.HP;
            _currentMP = enemy.Enemy.MP;
            Row = enemy.Row;
            IsBackRow = Row > 0;
            if (indexInGroup != null)
                Name = _enemy.Enemy.Name + " " + (char)('A' + indexInGroup.Value);
            else
                Name = _enemy.Enemy.Name;

            _stats = new CombatStats {
                Att = enemy.Enemy.Attack,
                Dex = enemy.Enemy.Dexterity,
                Lck = enemy.Enemy.Luck,
                Level = enemy.Enemy.Level,
                Def = enemy.Enemy.Defense * 2, //Hmm
                MDf = enemy.Enemy.MDef * 2, //Hmm
                MAt = enemy.Enemy.MAttackPercent,
                DfPC = enemy.Enemy.DefPercent,                
            };
        }

        public CombatStats BaseStats => _stats;

        public int HP { 
            get => _currentHP;
            set => _currentHP = value; 
        }
        public int MaxHP => _enemy.Enemy.HP;

        public int MP {
            get => _currentMP;
            set => _currentMP = value;
        }
        public int MaxMP => _enemy.Enemy.MP;

        public Timer VTimer { get; set; }
        public Timer CTimer { get; set; }
        public Timer TTimer { get; set; }
        public int Row { get; set; }
        public bool IsBackRow { get; set; }
        public bool IsDefending { get; set; }
        public ICombatant LastAttacker { get; set; }
        public ICombatant LastPhysicalAttacker { get; set; }
        public ICombatant LastMagicAttacker { get; set; }

        public bool IsPlayer => false;
        public int Level => _enemy.Enemy.Level;

        public bool PhysicalImmune { get; set; }
        public bool MagicalImmune { get; set; }

        public List<StatModifier> StatModifiers { get; } = new();

        public Dictionary<Element, ElementResistance> Elements { get; } = new();

        public Statuses ImmuneStatuses => ~_enemy.Enemy.AllowedStatuses;

        public Statuses Statuses { get; set; }
        public override string ToString() => Name;

        private Engine _engine;
        public void Init(Engine engine, AICallbacks callbacks) {
            _engine = engine;
            AI = new AI(Enemy.Enemy.AI, new CombatantMemory(engine, this), callbacks);
            AI.Run(AIScriptFunction.PreBattle);
        }

        private QueuedAction RunAIAndQueueAction(AIScriptFunction function, ActionPriority priority) {
            AI.Memory.ResetRegion2(_engine.Game.SaveData.Gil);
            AI.Run(function);

            if (AI.ActionID.HasValue) {
                var action = Enemy.Enemy.Actions.First(a => a.ActionID == AI.ActionID);
                var targets = Utils.IndicesOfSetBits(AI.Memory.Read2(0x070))
                    .Select(i => _engine.Combatants[i]);
                var q = new QueuedAction(
                    this, action.Attack.ToAbility(this), targets.ToArray(),
                    priority, action.Attack.Name
                );
                _engine.QueueAction(q);
                return q;
            } else
                return null;
        }

        public void TakeAction() {
            var action = RunAIAndQueueAction(AIScriptFunction.Main, ActionPriority.Normal);
            if (action != null)
                action.AfterAction = () => TTimer.Reset();
            else //Guess we chose not to do anything, so just reset our TTimer now and try again next time!
                TTimer.Reset();
        }

        public void Hit(QueuedAction hitBy) {
            if (hitBy.Priority != ActionPriority.Counter) {
                Console.WriteLine($"Enemy {Name} was hit, last attacked {LastAttacker} physical {LastPhysicalAttacker} magic {LastMagicAttacker}");
                RunAIAndQueueAction(AIScriptFunction.GeneralCounter, ActionPriority.Counter);
                AI.Run(AIScriptFunction.GeneralCounter);
                if (hitBy.Ability.IsPhysical)
                    RunAIAndQueueAction(AIScriptFunction.PhysicalCounter, ActionPriority.Counter);
                if (hitBy.Ability.IsMagical)
                    RunAIAndQueueAction(AIScriptFunction.MagicCounter, ActionPriority.Counter);
            }
        }
    }

    public static class CombatantUtil {

        public static bool IsAlive(this ICombatant combatant) {
            return combatant.HP > 0; //TODO???
        }

        public static CombatStats ModifiedStats(this ICombatant combatant) {
            var stats = combatant.BaseStats;
            foreach (var mod in combatant.StatModifiers)
                stats = mod(stats);
            return stats;
            //TODO we could cache these and only recalculate when necessary? If we care?
        }
    }
}
