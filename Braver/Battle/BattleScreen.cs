// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.Net;
using Braver.Plugins;
using Braver.Plugins.UI;
using Braver.UI;
using Braver.UI.Layout;
using Ficedula;
using Ficedula.FF7;
using Ficedula.FF7.Battle;
using Ficedula.FF7.Field;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NAudio.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Battle {

    public class BattleSceneCache {
        public Dictionary<int, BattleScene> Scenes { get; }

        public BattleSceneCache(FGame g) {
            Scenes = SceneDecoder.Decode(g.Open("battle", "scene.bin"))
                .Where(s => s.Enemies.Any())
                .ToDictionary(s => s.FormationID, s => s);
        }
    }

    enum BackgroundKind {
        HorizontalScroll = 0,
        Normal = 1,
        VerticalScroll = 2,
        Lifestream = 3,
        Rotating = 4,
        NormalAgain = 5,
    }

    public class RealBattleScreen : BattleScreen, Net.IClientListen<GetTargetOptionsMessage>,
        Net.IClientListen<QueueActionMessage> {

        private class Callbacks : AICallbacks {
            public Callbacks(VMM vmm) {
                _vmm = vmm;
            }

        }

        public class UIHandler : UI.Layout.LayoutModel {
            public UI.Layout.Label
                lName0, lName1, lName2,
                lHP0, lHP1, lHP2,
                lMaxHP0, lMaxHP1, lMaxHP2,
                lMP0, lMP1, lMP2,
                lMaxMP0, lMaxMP1, lMaxMP2;
            public UI.Layout.Gauge
                gHP0, gHP1, gHP2,
                gMP0, gMP1, gMP2,
                gLimit0, gLimit1, gLimit2,
                gTime0, gTime1, gTime2;

            private List<CharacterCombatant> _combatants;

            public IReadOnlyList<CharacterCombatant> Combatants => _combatants.AsReadOnly();

            public override bool IsRazorModel => true;

            public UIHandler(IEnumerable<CharacterCombatant> combatants) {
                _combatants = combatants.ToList();
            }

            private void DoUpdate(CharacterCombatant combatant,
                UI.Layout.Label lName,
                UI.Layout.Label lHP, UI.Layout.Label lMaxHP, UI.Layout.Label lMP, UI.Layout.Label lMaxMP,
                UI.Layout.Gauge gHP, UI.Layout.Gauge gMP, UI.Layout.Gauge gLimit, UI.Layout.Gauge gTime,
                CharacterCombatant highlight) {
                
                if (combatant == null) return;

                if (combatant == highlight)
                    lName.Color = Color.Gray;
                else
                    lName.Color = Color.White;

                lHP.Text = combatant.Character.CurrentHP.ToString();
                lMP.Text = combatant.Character.CurrentMP.ToString();
                gHP.Current = combatant.Character.CurrentHP;
                gMP.Current = combatant.Character.CurrentMP;
                gLimit.Current = combatant.Character.LimitBar;
                gTime.Current = 255 * combatant.TTimer.Fill;
            }

            public void Update(CharacterCombatant highlight) {
                DoUpdate(_combatants.ElementAtOrDefault(0), lName0, lHP0, lMaxHP0, lMP0, lMaxMP0, gHP0, gMP0, gLimit0, gTime0, highlight);
                DoUpdate(_combatants.ElementAtOrDefault(1), lName1, lHP1, lMaxHP1, lMP1, lMaxMP1, gHP1, gMP1, gLimit1, gTime1, highlight);
                DoUpdate(_combatants.ElementAtOrDefault(2), lName2, lHP2, lMaxHP2, lMP2, lMaxMP2, gHP2, gMP2, gLimit2, gTime2, highlight);
            }
        }

        private BattleScene _scene;

        private UI.UIBatch _menuUI;
        private Menu<CharacterCombatant> _activeMenu;
        private Dictionary<CharacterCombatant, Guid> _menuDisplaying = new();

        private TargetGroup _targets;
        private TargetGroup Targets {
            get => _targets;
            set {
                _targets = value;
                TargetsChanged();
            }
        }

        private bool _toggleMultiSingleTarget;

        private BattleEffectManager _effect;

        private Engine _engine;
        private BattleDebug _debug;
        private PluginInstances<IBattleUI> _plugins;
        private UIHandler _uiHandler;

        private bool _debugCamera = false;

        private void TargetsChanged() {
            IEnumerable<ICombatant> current = null;
            if (_targets != null) {
                if (_targets.SingleTarget != null)
                    current = Enumerable.Repeat(_targets.SingleTarget, 1);
                else
                    current = _targets.Targets;
            }
            _plugins.Call(ui => ui.BattleTargetHighlighted(current));
        }

        public BattleRenderer<ICombatant> Renderer { get; private set; }
        public SpriteManager Sprites { get; private set; }
        public UI.UIBatch MenuUI => _menuUI;

        public override string Description {
            get {
                var enemies = _engine.ActiveCombatants.OfType<EnemyCombatant>();
                return "Battle with " + string.Join(", ", enemies.Select(e => e.Name));
            }
        }

        private int? GetCombatantID(ICombatant combatant) {
            if (combatant == null)
                return null;
            else
                return _engine.Combatants.IndexOf(combatant);
        }

        private void AddModel(string code, Vector3 position, ICombatant combatant) {
            var model = Model.LoadBattleModel(Graphics, Game, code);
            model.Translation = position;
            model.Scale = 1;
            if (position.Z < 0)
                model.Rotation = new Vector3(0, 180, 0);
            //Defaults to animation 0 so no PlayAnimation required
            Renderer.Models.Add(combatant, model);
            Game.Net.Send(new AddBattleModelMessage {
                Code = code,
                Position = position,
                ID = GetCombatantID(combatant).Value,
            });
        }

        public void UpdateVisualState(ICombatant combatant) {
            var model = Renderer.Models[combatant];
            switch(combatant) {
                case CharacterCombatant chr:
                    if (chr.HP <= 0)
                        model.PlayAnimation(6, true, 1f);
                    else if (chr.HP <= (chr.MaxHP / 4))
                        model.PlayAnimation(1, true, 1f);
                    else
                        model.PlayAnimation(chr.IdleBattleAnimation, true, 1f);
                    //TODO - much more than this!
                    break;
                case EnemyCombatant enemy:
                    model.PlayAnimation(enemy.IdleBattleAnimation, true, 1f);
                    break;
            }
        }

        private bool CheckForBattleEnd() {
            if (_engine.AnyQueuedCounters) return false;
            //TODO victory poses!

            BattleResults GetResults() {
                var results = new BattleResults();

                void AddItem(int itemID, byte chance) {
                    if (_engine.Random(64) <= chance) {
                        var existing = results.Items.Find(inv => inv.ItemID == itemID);
                        if (existing == null)
                            results.Items.Add(new InventoryItem { ItemID = itemID, Quantity = 1 });
                        else
                            existing.Quantity++;
                    }
                }

                foreach (var defeated in _engine.Combatants.OfType<EnemyCombatant>().Where(c => !c.IsAlive())) {
                    results.Gil += defeated.Enemy.Enemy.Gil;
                    results.XP += defeated.Enemy.Enemy.Exp;
                    results.AP += defeated.Enemy.Enemy.AP;
                    foreach (var item in defeated.Enemy.Enemy.DropItems)
                        AddItem(item.ItemID, item.Chance);
                }

                return results;
            }

            if (_engine.Combatants.OfType<CharacterCombatant>().All(c => !c.IsAlive()))
                TriggerBattleLose(GetResults());
            else if (_engine.Combatants.OfType<EnemyCombatant>().All(c => !c.IsAlive()))
                TriggerBattleWin(GetResults());
            else
                return false;

            return true;
        }

        private static Vector3[] _playerPositions = new[] {
            new Vector3(-7 * 256, 0, 6 * 256),
            new Vector3(0 * 256, 0, 6 * 256),
            new Vector3(7 * 256, 0, 6 * 256),
        };

        private int _formationID;
        public RealBattleScreen(int formationID, BattleFlags flags) {
            _formationID = formationID;
            _flags = flags;
        }

        private static string[] _charBattleModels = new[] {
            "rt", "sb", "ru", "rv"
        };

        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);
            _scene = g.Singleton(() => new BattleSceneCache(g)).Scenes[_formationID];

            g.Net.Listen<GetTargetOptionsMessage>(this);
            g.Net.Listen<QueueActionMessage>(this);
            g.Net.Send(new Net.BattleScreenMessage { BattleID = _formationID });

            InitEngine();

            _uiHandler = new UIHandler(_engine.Combatants.OfType<CharacterCombatant>());
            var ui = new UI.Layout.LayoutScreen("battle", _uiHandler, isEmbedded: true);
            ui.Init(Game, Graphics);

            Renderer = new BattleRenderer<ICombatant>(g, graphics, ui);
            Renderer.LoadBackground(_scene.LocationID);
            var cam = _scene.Cameras[0];
            Renderer.SetCamera(cam);
            
            foreach (var enemy in _scene.Enemies) {
                AddModel(
                    SceneDecoder.ModelIDToFileName(enemy.Enemy.ID),
                    new Vector3(enemy.PositionX, enemy.PositionY, enemy.PositionZ),
                    _engine.Combatants.OfType<EnemyCombatant>().First(ec => ec.Enemy == enemy)
                );
            }

            foreach(var player in Game.SaveData.Party.Zip(_playerPositions)) {
                var comb = _engine.Combatants.OfType<CharacterCombatant>().First(cc => cc.Character == player.First);
                AddModel(_charBattleModels[player.First.CharIndex], player.Second, comb);
            }

            if (_flags.HasFlag(BattleFlags.BraverDebug))
                _debug = new BattleDebug(Graphics, Game, _engine, this);

            _menuUI = new UI.UIBatch(Graphics, Game);
            _plugins = GetPlugins<IBattleUI>(_formationID.ToString());
            Sprites = new SpriteManager(g, graphics);

            g.Net.Send(new Net.ScreenReadyMessage());
            g.Audio.PlayMusic("bat", true); //TODO!
        }

        private void InitEngine() {

            ICombatant[] combatants = new ICombatant[16];

            int index = 0;
            foreach (var chr in Game.SaveData.Party)
                combatants[index++] = new CharacterCombatant(Game, chr);

            index = 4;
            foreach (var group in _scene.Enemies.GroupBy(ei => ei.Enemy.ID)) {
                int c = 0;
                foreach (var enemy in group) {
                    combatants[index++] = new EnemyCombatant(enemy, group.Count() == 1 ? null : c++);
                }
            }

            var callbacks = new Callbacks(Game.Memory);
            _engine = new Engine(Game.GameOptions.BattleSpeed, combatants, Game, callbacks);

            _engine.ReadyForAction += c => { };
            _engine.ActionQueued += a => { };

        }

        public override Color ClearColor => Color.Black;

        private class TargetGroup {
            public ICombatant[] Targets { get; private set; }
            public bool MustTargetWholeGroup { get; set; }
            public bool IsDefault { get; private set; }
            public ICombatant SingleTarget { get; set; }

            public TargetGroup(ICombatant[] targets, bool mustTargetWholeGroup, bool isDefault = false) {
                Targets = targets;
                MustTargetWholeGroup = mustTargetWholeGroup;
                IsDefault = isDefault;
                if (!MustTargetWholeGroup)
                    SetDefaultSingleTarget();
            }

            public void SetDefaultSingleTarget() {
                SingleTarget = Targets.OrderBy(c => c.IsAlive() ? 0 : 1).FirstOrDefault();
            }

            public override string ToString() {
                string group = string.Join(", ", Targets.Select(c => c.Name));
                if (SingleTarget == null)
                    return group;
                else
                    return $"{SingleTarget.Name} (from group {group})";
            }
        }

        private IEnumerable<TargetGroup> GetTargetOptions(CharacterCombatant source, TargettingFlags flags) {
            if (flags == TargettingFlags.None) {
                yield return new TargetGroup(new[] { source }, true, true);
                yield break;
            }

            var firstEnemyRow = _engine.ActiveCombatants.OfType<EnemyCombatant>().First().Row;

            if (flags.HasFlag(TargettingFlags.OneRowOnly)) { //No options - defaults to defined row
                if (flags.HasFlag(TargettingFlags.StartOnEnemy)) {
                    yield return new TargetGroup(
                        _engine.ActiveCombatants
                        .OfType<EnemyCombatant>()
                        .Where(c => c.Row == firstEnemyRow)
                        .ToArray(),
                        true, true
                    );
                    yield break;
                } else {
                    yield return new TargetGroup(
                        _engine.ActiveCombatants
                        .OfType<CharacterCombatant>()
                        .Where(c => c.Row == source.Row)
                        .ToArray(),
                        true, true
                    );
                    yield break;
                }
            }

            if (flags.HasFlag(TargettingFlags.AllRows)) {
                yield return new TargetGroup(_engine.ActiveCombatants.ToArray(), true, true);
                yield break;
            }

            //So, potentially some options to deal with
            bool isMultiTarget = flags.HasFlag(TargettingFlags.MultiTargets) ^ _toggleMultiSingleTarget;
            foreach(var group in _engine.ActiveCombatants.GroupBy(c => c.Row)) {
                bool isDefault;
                if (flags.HasFlag(TargettingFlags.StartOnEnemy))
                    isDefault = (group.First() is EnemyCombatant) && (group.First().Row == firstEnemyRow);
                else
                    isDefault = group.Contains(source);
                yield return new TargetGroup(group.ToArray(), isMultiTarget, isDefault);
            }
        }

        public Vector3 GetModelScreenPos(ICombatant combatant, Vector3? offset = null) {
            var model = Renderer.Models[combatant];
            var middle = (model.MaxBounds + model.MinBounds) * 0.5f;
            if (offset != null)
                middle += offset.Value;
            var screenPos = Renderer.View3D.ProjectTo2D(model.Translation + middle);
            return screenPos;
        }

        private void NextMenu() {
            var ready = ReadyToActForPlayer(Guid.Empty).ToList();
            int index = ready.IndexOf(_activeMenu?.Combatant);
            if (ready.Any()) {
                var menuFor = ready[(index + 1) % ready.Count];
                _plugins.Call(ui => ui.BattleCharacterReady(menuFor));
                _activeMenu = new Menu<CharacterCombatant>(Game, _menuUI, menuFor, _plugins);
                Targets = null;
            } else {
                _activeMenu = null;
            }
        }

        private void CheckClientMenus() {
            foreach(var combatant in _uiHandler.Combatants) {
                Guid playerID = ControllingPlayer(combatant);
                if (combatant.ReadyForAction) {
                    if (!_menuDisplaying.ContainsKey(combatant) && (playerID != Guid.Empty)) {
                        _menuDisplaying[combatant] = playerID;
                        Game.Net.SendTo(new CharacterReadyMessage {
                            CharIndex = combatant.Character.CharIndex,
                            Actions = combatant.Actions.Select(a => new ClientMenuItem(a)).ToList(),
                        }, playerID);
                    }
                } else {
                    _menuDisplaying.Remove(combatant);
                }
            }
        }

        private void EngineTick(GameTime elapsed) {
            _engine.Tick();

            if (_activeMenu == null)
                NextMenu();
            CheckClientMenus();

            _uiHandler.Update(_activeMenu?.Combatant);

            _activeMenu?.Step();

            var action = _activeMenu?.SelectedAction;
            if (action == null)
                Targets = null;
            else {
                if (Targets == null)
                    Targets = GetTargetOptions(_activeMenu.Combatant, _activeMenu.SelectedAction.TargetFlags).First(tg => tg.IsDefault);
            }

            if (Targets != null) {
                IEnumerable<ICombatant> targets;
                if (Targets.SingleTarget != null)
                    targets = Enumerable.Repeat(Targets.SingleTarget, 1);
                else if (_activeMenu.SelectedAction.TargetFlags.HasFlag(TargettingFlags.RandomTarget))
                    targets = Enumerable.Repeat(Targets.Targets[((long)elapsed.TotalGameTime.TotalMilliseconds / 100) % Targets.Targets.Length], 1);
                else
                    targets = Targets.Targets;

                foreach (var target in targets) {
                    var screenPos = GetModelScreenPos(target);
                    //TODO clamp to screen, presumably
                    _menuUI.DrawImage("pointer", (int)screenPos.X, (int)screenPos.Y, 0.99f, Alignment.Right);
                }
            }

            if (_effect != null) {
                _effect.Step();
                if (_effect.IsComplete) {
                    System.Diagnostics.Trace.WriteLine($"Action {_effect} now complete");
                    _effect = null;
                }
            } else {
                /*
* -If action in progress - continue it!
 * -Or, If any counters queued - execute next counter!
 * -Or, if any enemies now dead - execute death effect
 * -Or, Check for battle end, and if not, execute any other queued action
                  */

                var pendingDeadEnemies = Renderer.Models
                    .Where(kv => !kv.Key.IsPlayer && !kv.Key.IsAlive())
                    .Where(kv => kv.Value.Visible);

                if (_engine.AnyQueuedCounters)
                    DoExecuteNextQueuedAction();
                else if (pendingDeadEnemies.Any()) {
                    _effect = new BattleEffectManager(
                        Game, this, null,
                        pendingDeadEnemies.Select(e => new AbilityResult { Target = e.Key }).ToArray(),
                        "FadeDeadEnemies"
                    );
                } else if (CheckForBattleEnd()) {
                    //
                } else
                    DoExecuteNextQueuedAction();
            }
        }

        protected override void DoStep(GameTime elapsed) {
            _menuUI.Reset();

            if (_debug != null) {
                _debug.Step();
            } else {
                EngineTick(elapsed);
            }
            Renderer.Step(elapsed);
        }

        private void DoExecuteNextQueuedAction() {
            if (_engine.ExecuteNextAction(out var nextAction, out var results)) {
                _effect = new BattleEffectManager(Game, this, nextAction, results, nextAction.Name /*VERY TODO*/);
            }
        }

        protected override void DoRender() {
            Renderer.Render();
            _menuUI.Render();
            _debug?.Render();
        }

        private Guid ControllingPlayer(CharacterCombatant combatant) {
            var map = Game.NetConfig
                .CharacterMap
                .SingleOrDefault(m => m.CharIndex == combatant.Character.CharIndex);
            return map?.PlayerID ?? Guid.Empty;
        }

        private IEnumerable<CharacterCombatant> ReadyToActForPlayer(Guid playerID) {
            return _uiHandler.Combatants
                .Where(c => c.ReadyForAction)
                .Where(c => ControllingPlayer(c) == playerID);
        }

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);

            _debug?.ProcessInput(input);

            if (input.IsJustDown(InputKey.Debug1))
                _debugCamera = !_debugCamera;

            if (_debugCamera) {
                if (input.IsDown(InputKey.Up))
                    Renderer.View3D.CameraPosition += new Vector3(0, 0, -100);
                if (input.IsDown(InputKey.Down))
                    Renderer.View3D.CameraPosition += new Vector3(0, 0, 100);
                if (input.IsDown(InputKey.Left))
                    Renderer.View3D.CameraPosition += new Vector3(100, 0, 0);
                if (input.IsDown(InputKey.Right))
                    Renderer.View3D.CameraPosition += new Vector3(-100, 0, 0);
            } else if (input.IsJustDown(InputKey.Menu)) {
                NextMenu();
            } else if (Targets != null) {
                bool blip = false;
                if (!Targets.MustTargetWholeGroup) {
                    if (input.IsRepeating(InputKey.Up)) {
                        Targets.SingleTarget = Targets.Targets[(Targets.Targets.IndexOf(Targets.SingleTarget) + Targets.Targets.Length - 1) % Targets.Targets.Length];
                        blip = true;
                        TargetsChanged();
                    } else if (input.IsRepeating(InputKey.Down)) {
                        Targets.SingleTarget = Targets.Targets[(Targets.Targets.IndexOf(Targets.SingleTarget) + 1) % Targets.Targets.Length];
                        blip = true;
                        TargetsChanged();
                    }
                }

                if (_activeMenu.SelectedAction.TargetFlags.HasFlag(TargettingFlags.ToggleMultiSingleTarget) && input.IsJustDown(InputKey.Select)) {
                    Targets.MustTargetWholeGroup = !Targets.MustTargetWholeGroup;
                    blip = true;
                    if (Targets.MustTargetWholeGroup)
                        Targets.SingleTarget = null;
                    else
                        Targets.SetDefaultSingleTarget();
                    TargetsChanged();
                }

                int groupShift = 0;
                if (input.IsRepeating(InputKey.Left))
                    groupShift = -1;
                else if (input.IsRepeating(InputKey.Right))
                    groupShift = 1;

                if (groupShift != 0) {
                    var groups = GetTargetOptions(_activeMenu.Combatant, _activeMenu.SelectedAction.TargetFlags).ToList();
                    int current = groups.FindIndex(g => g.Targets.Any(t => Targets.Targets.Contains(t)));
                    int newIndex = current + groupShift;
                    if ((newIndex >= 0) && (newIndex < groups.Count)) {
                        Targets = groups[newIndex];
                        System.Diagnostics.Trace.WriteLine($"Now targetting {Targets}");
                        blip = true;
                    }
                }

                if (input.IsJustDown(InputKey.OK)) {
                    var source = _activeMenu.Combatant;
                    var q = new QueuedAction(
                        source, _activeMenu.SelectedAction.Ability.Value,
                        Targets.SingleTarget == null ? Targets.Targets : new[] { Targets.SingleTarget },
                        ActionPriority.Normal, _activeMenu.SelectedAction.Name
                    );
                    //TODO limit priority
                    q.AfterAction = () => {
                        source.TTimer.Reset();
                    };
                    _engine.QueueAction(q);
                    source.ReadyForAction = false;
                    _activeMenu = null;
                } else if (input.IsJustDown(InputKey.Cancel))
                    _activeMenu.ProcessInput(input);

                if (blip)
                    Game.Audio.PlaySfx(Sfx.Cursor, 1f, 0f);
            } else
                _activeMenu?.ProcessInput(input);
        }

        public void Received(GetTargetOptionsMessage message, Guid playerID) {
            var source = _uiHandler.Combatants
                .Single(c => c.Character.CharIndex == message.SourceCharIndex);
            if (source.ReadyForAction) {
                Game.Net.SendTo(new TargetOptionsMessage {
                    Ability = message.Ability,
                    Options = GetTargetOptions(source, message.TargettingFlags)
                        .Select(group => new TargetOption {
                            IsDefault = group.IsDefault,
                            MustTargetWholeGroup = group.MustTargetWholeGroup,
                            TargetIDs = group.Targets.Select(c => GetCombatantID(c).Value).ToList(),
                            SingleTarget = GetCombatantID(group.SingleTarget),
                            DefaultSingleTarget = GetCombatantID(group.Targets.OrderBy(c => c.IsAlive() ? 0 : 1).FirstOrDefault())
                        })
                        .ToList()
                }, playerID);                
            }
        }

        public void Received(QueueActionMessage message, Guid playerID) {
            var source = _uiHandler.Combatants
                .Single(c => c.Character.CharIndex == message.SourceCharIndex);

            var q = new QueuedAction(
                source, message.Ability,
                message.TargetIDs.Select(index => _engine.Combatants[index]).ToArray(),
                ActionPriority.Normal, message.Name
            );
            //TODO limit priority
            q.AfterAction = () => {
                source.TTimer.Reset();
            };
            _engine.QueueAction(q);
            source.ReadyForAction = false;
        }
    }

    public abstract class BattleScreen : Screen {
        
        protected BattleFlags _flags;

        protected void TriggerBattleWin(BattleResults results) {
            Game.PopScreen(this);
            if (!_flags.HasFlag(BattleFlags.NoVictoryMusic))
                Game.Audio.PlayMusic("fan2");
            if (!_flags.HasFlag(BattleFlags.NoVictoryScreens)) {
                Game.PushScreen(new LayoutScreen("BattleGilItems", parm: results));
                Game.PushScreen(new LayoutScreen("BattleXPAP", parm: results));
            }
        }
        protected void TriggerBattleLose(BattleResults results) {
            if (_flags.HasFlag(BattleFlags.NoGameOver)) {
                Game.PopScreen(this); //TODO - XP/AP, or must always be skipped in this case?
            } else {
                Game.PushScreen(new UI.Layout.LayoutScreen("GameOver"));
            }
        }

        public static void Launch(FGame game, int battleID, BattleFlags flags) {
            if (game.GameOptions.SkipBattleMenu)
                game.PushScreen(new BattleSkipScreen(flags));
            else {
                game.PushScreen(new RealBattleScreen(battleID, flags));
                game.PushScreen(new Swirl());
            }
        }

        public static void Launch(FGame game, EncounterTable encounters, BattleFlags flags, Random r) {
            int preemptive = 16;
            //TODO - preemptive materia!
            bool isPreemptive = r.Next(256) < preemptive;

            bool ambushAlert = false; //TODO!

            int specialChance = r.Next(64);
            foreach(var enc in encounters.SpecialEncounters) {
                int frequency = enc.Frequency;
                if (ambushAlert && (enc != encounters.SideAttack))
                    frequency /= 2;
                if (specialChance < enc.Frequency) {
                    game.SaveData.LastRandomBattleID = enc.EncounterID;
                    Launch(game, enc.EncounterID, flags); 
                    return;
                }
                specialChance -= enc.Frequency;
            }

            Encounter PickEncounter() {
                int num = r.Next(64);
                foreach(var enc in encounters.StandardEncounters) {
                    if (num < enc.Frequency)
                        return enc;
                    num -= enc.Frequency;
                }
                throw new InvalidOperationException();
            }

            var which = PickEncounter();
            if (which.EncounterID == game.SaveData.LastRandomBattleID)
                which = PickEncounter();
            game.SaveData.LastRandomBattleID = which.EncounterID;
            Launch(game, which.EncounterID, flags);
        }
    }

    public class BattleResults {
        public int AP { get; set; }
        public int XP { get; set; }
        public int Gil { get; set; }
        public List<InventoryItem> Items { get; set; } = new();
    }

    [Flags]
    public enum BattleFlags {
        None = 0,
        TimedBattle = 0x2,
        Preemptive = 0x4,
        NoEscape = 0x8,
        NoVictoryMusic = 0x20,
        BattleArena = 0x40,
        NoVictoryScreens = 0x80,
        NoGameOver = 0x100,

        BraverDebug = 0x10000000,
    }
}

/*
 * Character battle animations?
0=default
1=neardeath
2=victory
3=changerow(fwd?)
4=changerow(back?)
5=block?
6=dead
7=runaway
8=cover
9=useitem
10=alsouseitem??
11=cast
12=castinprogress?
13=castend?
14=injured
*/
