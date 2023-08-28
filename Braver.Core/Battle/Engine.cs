// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;

namespace Braver.Battle {

    public enum BattleStatus {
        InProgress,
        PlayerWin,
        EnemyWin,
    }

    public enum ActionPriority {
        Normal,
        Limit,
        Counter,
    }

    public interface IInProgress {
        string Description { get; }
        bool Step(int frames); //return true if done
        bool IsIndefinite { get; }
    }

    public class QueuedAction {
        public ICombatant Source { get; private set; }
        public Ability Ability { get; private set; }
        public ICombatant[] Targets { get; private set; }
        public ActionPriority Priority { get; private set; }
        public string Name { get; private set; }
        public List<byte[]> QueuedText { get; private set; } = new();

        public Action AfterAction { get; set; }

        public QueuedAction(ICombatant source, Ability ability, ICombatant[] targets, ActionPriority priority, string name) {
            Source = source;
            Ability = ability;
            Targets = targets;
            Priority = priority;
            Name = name;

            Trace.Assert(Targets.Any());
            Trace.Assert(Source != null);
        }   
    }

    public class Engine {
        private int _speedValue;
        private List<ICombatant> _combatants;
        private Random _r = new();

        private Dictionary<ActionPriority, Queue<QueuedAction>> _queuedActions = new Dictionary<ActionPriority, Queue<QueuedAction>> {
            [ActionPriority.Counter] = new Queue<QueuedAction>(),
            [ActionPriority.Limit] = new Queue<QueuedAction>(),
            [ActionPriority.Normal] = new Queue<QueuedAction>(),
        };

        public bool AnyQueuedCounters => _queuedActions[ActionPriority.Counter].Any();

        public Timer GTimer { get; }
        public BGame Game { get; private set; }
        public Action<ICombatant> ReadyForAction { get; set; }
        public Action<QueuedAction> ActionQueued { get; set; }

        public int Random(int max) => _r.Next(max);

        public IReadOnlyList<ICombatant> Combatants => _combatants.AsReadOnly();
        public IEnumerable<ICombatant> ActiveCombatants => _combatants.Where(c => c != null);

        public BattleStatus Status {
            get {
                var alive = ActiveCombatants.Where(c => c.IsAlive());
                if (alive.All(c => c.IsPlayer))
                    return BattleStatus.PlayerWin;
                if (alive.All(c => !c.IsPlayer))
                    return BattleStatus.EnemyWin;
                return BattleStatus.InProgress;
            }
        }

        public Engine(int battleSpeed, IEnumerable<ICombatant> combatants, BGame game, AICallbacks callbacks) {
            Game = game;

            _speedValue = 32768 / (120 + battleSpeed * 15 / 8);

            GTimer = new Timer(_speedValue, 8192, 0);

            _combatants = combatants.ToList();

            var players = ActiveCombatants.Where(c => c.IsPlayer);

            int normalSpeed = (int)Math.Ceiling(players.Average(c => c.BaseStats.Dex)) + 50; 


            foreach(var comb in ActiveCombatants) {
                comb.VTimer = new Timer(_speedValue * 2, 8192, 0);
                comb.CTimer = new Timer(136, 8192, 0);

                int ttinc;
                if (comb.IsPlayer) {
                    ttinc = (comb.ModifiedStats().Dex + 50) * (_speedValue * 2) / normalSpeed;
                } else {
                    ttinc = comb.ModifiedStats().Dex * (_speedValue * 2) / normalSpeed;
                }
                comb.TTimer = new Timer(ttinc, 65535, _r.Next(0, 32767), false);
                comb.TTimer.On(
                    1, () => {
                        ReadyForAction?.Invoke(comb);
                        comb.TakeAction();
                    }, true);
                comb.Init(this, callbacks);
            }

            //TODO: Implement battle type (side attack, etc) on TTimer
        }



        public void Tick() {
            GTimer.Tick();
            foreach(var comb in ActiveCombatants) {
                if (comb.HP > 0) { //TODO - valid check, other statuses, ...?
                    comb.VTimer.Tick();
                    comb.CTimer.Tick();
                    comb.TTimer.Tick();
                }
            }
        }

        public void QueueAction(QueuedAction action) {
            _queuedActions[action.Priority].Enqueue(action);
            ActionQueued?.Invoke(action);
        }

        public bool ExecuteNextAction(out QueuedAction action, out AbilityResult[] results) {
            results = null;
            if (!_queuedActions[ActionPriority.Counter].TryDequeue(out action))
                if (!_queuedActions[ActionPriority.Limit].TryDequeue(out action))
                    if (!_queuedActions[ActionPriority.Normal].TryDequeue(out action))
                        return false;

            results = ApplyAbility(
                action.Source,
                action.Ability,
                action.Targets
            ).ToArray();
            action.AfterAction?.Invoke();
            return true;
        }

        private static Statuses _autoHitStatuses = Statuses.Death | Statuses.Sleep |
            Statuses.Confusion | Statuses.Stop | Statuses.Petrify | Statuses.Paralysed; 

        private AbilityResult ApplyAbilityOneTarget(ICombatant source, Ability ability, ICombatant target, bool isMultiTarget) {
            if (ability.InflictStatus != 0) {
                if (ability.Power == 0)
                    if ((ability.InflictStatus & target.ImmuneStatuses) == ability.InflictStatus)
                        return new AbilityResult {
                            Target = target,
                            Hit = false,
                        };
                //If fully immune to all statuses, and no power (=>damage), then ability cannot do anything and always misses
            }

            HashSet<ElementResistance> resistances = new HashSet<ElementResistance>(
                ability.Elements
                .Split()
                .Where(e => target.Elements.ContainsKey(e))
                .Select(e => target.Elements[e])
                .Distinct()
            );

            bool deathWeakness = false, recovery = false;

            if (ability.Elements.Split().Any(e => target.Elements.TryGetValue(e, out var resist) && resist == ElementResistance.Absorb)) {
                var temp = ability.InflictStatus;
                ability.InflictStatus = ability.RemoveStatus;
                ability.RemoveStatus = temp;

                if ((ability.InflictStatus & Statuses.Death) != 0) {
                    deathWeakness = true;
                    ability.InflictStatus &= ~Statuses.Death;
                }

                if ((ability.RemoveStatus & Statuses.Death) != 0) {
                    recovery = true;
                    ability.RemoveStatus &= ~Statuses.Death;
                }
            }

            if (deathWeakness) {
                switch (ability.Formula) {
                    case AttackFormula.MaxHPPercent:
                    case AttackFormula.HPPercent:
                        if (_r.Next(100) > ability.Power) {
                            deathWeakness = false;
                            //TODO is now immune?
                        }
                        break;
                }
            }

            if (ability.Power == 0) {
                if (resistances.Contains(ElementResistance.Weak)) {
                    ability.PAtPercent *= 2;
                    ability.MAtPercent *= 2;
                }
                if (resistances.Contains(ElementResistance.Resist)) {
                    ability.PAtPercent /= 2;
                    ability.MAtPercent /= 2;
                }
            }

            var sourceStats = source.ModifiedStats();
            var targetStats = target.ModifiedStats();

            bool didHit;
            bool isCritical = ability.AutoCritical;

            if (ability.IsPhysical) {
                int hit = (sourceStats.Dex / 4) + ability.PAtPercent + sourceStats.DfPC - targetStats.DfPC;

                if ((source.Statuses & Statuses.Fury) != 0)
                    hit = hit * 7 / 10;

                if (deathWeakness || resistances.Contains(ElementResistance.AutoHit) ||
                    resistances.Contains(ElementResistance.Immune) ||
                    resistances.Contains(ElementResistance.Absorb) ||
                    ((target.Statuses & _autoHitStatuses) != 0) ||
                    ((target.Statuses & Statuses.Manipulate) != 0)
                ) {
                    hit = 255;
                }

                if (hit < 1) hit = 1;

                int L = _r.Next(100);
                if (L < (sourceStats.Lck / 4))
                    hit = 255;
                else if (L < ((targetStats.Lck / 4) - (sourceStats.Lck / 4)))
                    hit = 0;

                int rh = _r.Next(65536) * 99 / 65535 + 1;
                didHit = rh < hit;

                if (didHit) {
                    int crit;
                    if ((source.Statuses & Statuses.LuckyGirl) != 0) {
                        crit = 255;
                    } else {
                        crit = (sourceStats.Lck + sourceStats.Level - targetStats.Level) / 4 + sourceStats.CriticalChance;
                    }

                    int rc = _r.Next(65536) * 99 / 65535 + 1;
                    if (rc < crit)
                        isCritical = true;
                }
            } else {
                int hit = ability.MAtPercent;

                if (deathWeakness || resistances.Contains(ElementResistance.AutoHit) ||
                    resistances.Contains(ElementResistance.Immune) ||
                    resistances.Contains(ElementResistance.Absorb)
                )
                    hit = 255;

                if (ability.IsReflectable && ((target.Statuses & Statuses.Reflect) != 0))
                    hit = 255;

                if (ability.InflictStatus == Statuses.None)
                    if ((target.Statuses & _autoHitStatuses) != 0)
                        hit = 255;

                if (hit == 255)
                    didHit = true;
                else {
                    hit = hit * 7 / 10;

                    int rmd = _r.Next(100);
                    if (rmd < targetStats.MDPC)
                        didHit = false;
                    else {
                        hit = hit + sourceStats.Level - targetStats.Level / 2 - 1;
                        int rh = _r.Next(100);
                        didHit = rh < hit;
                    }
                }

            }


            //Now check whether status effects apply
            if (didHit) {
                Statuses inflictStatus = Statuses.None, cureStatus = Statuses.None;
                if ((ability.InflictStatus != 0) || (ability.ToggleStatus != 0) || (ability.RemoveStatus != 0)) {
                    int statusHit = ability.StatusChance;
                    bool didStatusHit;

                    if ((ability.InflictStatus | ability.ToggleStatus | ability.RemoveStatus) == Statuses.Frog)
                        if ((target.Statuses & Statuses.Frog) != 0)
                            statusHit = 255;

                    if ((ability.InflictStatus | ability.ToggleStatus | ability.RemoveStatus) == Statuses.Small)
                        if ((target.Statuses & Statuses.Small) != 0)
                            statusHit = 255;

                    if (((ability.InflictStatus | ability.ToggleStatus | ability.RemoveStatus) & (Statuses.Haste | Statuses.Berserk | Statuses.Shield)) != 0)
                        if (target.IsPlayer)
                            statusHit = 255;

                    statusHit = (statusHit * (10 + ability.MPTurboLevel)) / 10;

                    if (statusHit >= 100)
                        didStatusHit = true;
                    else {
                        if (isMultiTarget && !ability.NoSplit)
                            statusHit = statusHit * 2 / 3;

                        didStatusHit = _r.Next(100) < statusHit;
                    }

                    if (didStatusHit) {
                        inflictStatus = ability.InflictStatus | (ability.ToggleStatus & ~target.Statuses);
                        cureStatus = ability.RemoveStatus | (ability.ToggleStatus & target.Statuses);
                    } 
                }

                int damage;

                void DoCritical() {
                    if (isCritical)
                        damage *= 2;
                }
                void DoBerserk() {
                    if (source.Statuses.HasFlag(Statuses.Berserk))
                        damage = damage * 3 / 2;
                }
                void RowCheck() {
                    if (source.IsBackRow || target.IsBackRow)
                        if (!ability.LongRange)
                            damage /= 2;
                }
                void DoDefend() {
                    if (target.IsDefending)
                        damage /= 2;
                }
                void DoBackAttack() {
                    //TODO
                }
                void DoFrog() {
                    if (source.Statuses.HasFlag(Statuses.Frog))
                        damage /= 4;
                }
                void DoSadness() {
                    if (target.Statuses.HasFlag(Statuses.Sadness))
                        damage = damage * 7 / 10;
                }
                void DoSplitQuadra() {
                    if (ability.IsQuadraMagic)
                        damage /= 2;
                    else if (isMultiTarget && !ability.NoSplit)
                        damage = damage * 2 / 3;
                }
                void DoBarriers() {
                    if (ability.IsPhysical && target.Statuses.HasFlag(Statuses.Barrier))
                        damage /= 2;
                    else if (ability.IsMagical && target.Statuses.HasFlag(Statuses.MBarrier))
                        damage /= 2;
                }
                void DoMPTurbo() {
                    damage = (damage * (10 + ability.MPTurboLevel)) / 10;
                }
                void DoMini() {
                    if (source.Statuses.HasFlag(Statuses.Small))
                        damage = 0;
                }
                void DoRV() {
                    damage = damage * (3841 + _r.Next(256)) / 4096;
                    if (damage == 0)
                        damage = 1;
                }


                switch (ability.Formula) {
                    case AttackFormula.Physical:
                        damage = sourceStats.Att +
                            ((sourceStats.Att + sourceStats.Level) / 32) * ((sourceStats.Att + sourceStats.Level) / 32);
                        damage = (ability.Power * (512 - targetStats.Def) * damage) / (16 * 512);
                        DoCritical();
                        DoBerserk();
                        RowCheck();
                        DoDefend();
                        DoBackAttack();
                        DoFrog();
                        DoSadness();
                        DoSplitQuadra();
                        DoBarriers();
                        DoMPTurbo();
                        DoMini();
                        DoRV();
                        break;

                    case AttackFormula.Magical:
                        damage = 6 * (sourceStats.MAt + sourceStats.Level);
                        damage = (ability.Power * (512 - targetStats.MDf) * damage) / (16 * 512);
                        DoSadness();
                        DoSplitQuadra();
                        DoBarriers();
                        DoMPTurbo();
                        DoRV();
                        break;

                    case AttackFormula.Cure:
                        damage = 6 * (sourceStats.MAt + sourceStats.Level);
                        damage = damage + 22 * ability.Power;
                        DoSplitQuadra();
                        DoBarriers();
                        DoMPTurbo();
                        DoRV();
                        break;

                    case AttackFormula.Item:
                        damage = 16 * ability.Power;
                        damage = damage * (512 - targetStats.Def) / 512;
                        DoRV();
                        break;

                    case AttackFormula.HPPercent:
                        if (ability.DamageMP)
                            damage = target.MP * ability.Power / 32;
                        else
                            damage = target.HP * ability.Power / 32;
                        DoSplitQuadra();
                        break;
                    case AttackFormula.MaxHPPercent:
                        if (ability.DamageMP)
                            damage = target.MaxMP * ability.Power / 32;
                        else
                            damage = target.MaxHP * ability.Power / 32;
                        DoSplitQuadra();
                        break;

                    case AttackFormula.Fixed:
                        damage = ability.Power * 20;
                        break;

                    case AttackFormula.Recovery:
                        damage = 0;
                        break;

                    default:
                    case AttackFormula.Custom:
                        throw new NotImplementedException();
                }

                //TODO Lucky 7s

                bool isImmune = false, isRestore = ability.IsRestore;

                if (ability.IsPhysical && target.PhysicalImmune)
                    isImmune = true;
                else if (ability.IsMagical && target.MagicalImmune)
                    isImmune = true;

                if (resistances.Contains(ElementResistance.Absorb))
                    isRestore = !isRestore;
                else {
                    if (resistances.Contains(ElementResistance.Weak))
                        damage *= 2;
                    if (resistances.Contains(ElementResistance.Resist))
                        damage = (damage + 1) / 2;
                }

                if (deathWeakness) {
                    if (target.Statuses.HasFlag(Statuses.Death))
                        return new AbilityResult {
                            Target = target,
                            Hit = false,
                        };
                    else
                        return new AbilityResult {
                            Target = target,
                            Hit = true,
                            InflictStatus = Statuses.Death,
                        };
                }

                if (resistances.Contains(ElementResistance.Recovery)) {
                    recovery = true;
                }

                if (resistances.Contains(ElementResistance.Immune)) {
                    ability.InflictStatus = 0;
                    damage = 0;
                }

                if (damage > 9999)
                    damage = 9999;
                if (ability.DamageMP && (damage > 999))
                    damage = 999;
                //TODO HP<->MP

                if (target.Statuses.HasFlag(Statuses.Peerless))
                    damage = 0;
                if (target.Statuses.HasFlag(Statuses.Petrify))
                    damage = 0;

                if (recovery) {
                    return new AbilityResult {
                        Target = target,
                        Hit = true,
                        Recovery = true,
                    };
                }

                if (isRestore)
                    damage = -damage;

                return new AbilityResult {
                    Target = target,
                    Hit = true,
                    MPDamage = ability.DamageMP ? -damage : 0,
                    HPDamage = ability.DamageMP ? 0 : -damage,
                    InflictStatus = inflictStatus,
                    RemoveStatus = cureStatus,
                };

            } else {
                return new AbilityResult {
                    Target = target,
                    Hit = false,                    
                    InflictStatus = ability.InflictStatus | (ability.ToggleStatus & ~target.Statuses),
                    RemoveStatus = ability.RemoveStatus | (ability.ToggleStatus & target.Statuses),
                };
            }

        }

        public IEnumerable<AbilityResult> ApplyAbility(ICombatant source, Ability ability, IEnumerable<ICombatant> targets) {
            return targets.Select(t => ApplyAbilityOneTarget(source, ability, t, targets.Count() > 1));
        }

    }



    public class AbilityResult {
        public ICombatant Target { get; set; }
        public bool Hit { get; set; }
        public Statuses InflictStatus { get; set; }
        public Statuses RemoveStatus { get; set; }
        public bool Recovery { get; set; }
        public int HPDamage { get; set; }
        public int MPDamage { get; set; }

        public void Apply(QueuedAction action) {
            if (Hit) {
                Target.Statuses |= InflictStatus;
                Target.Statuses &= ~RemoveStatus;
                Target.LastAttacker = action.Source;
                if (action.Ability.IsPhysical)
                    Target.LastPhysicalAttacker = action.Source;
                if (action.Ability.IsMagical)
                    Target.LastMagicAttacker = action.Source;

                //TODO Recovery
                if (HPDamage != 0)
                    Target.HP = Math.Min(Target.MaxHP, Math.Max(0, Target.HP + HPDamage));
                if (MPDamage != 0)
                    Target.MP = Math.Min(Target.MaxMP, Math.Max(0, Target.MP + MPDamage));

                Target.Hit(action);
            }
        }
    }
}
