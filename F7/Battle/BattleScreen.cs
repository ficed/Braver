using Braver.UI;
using Braver.UI.Layout;
using Ficedula.FF7;
using Ficedula.FF7.Battle;
using Ficedula.FF7.Field;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;

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

    public class RealBattleScreen : BattleScreen {

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


        private BackgroundKind _backgroundKind;

        private class BackgroundChunk {
            public BasicEffect Effect;
            public int IndexOffset;
            public int VertOffset;
            public int TriCount;
        }

        private List<BackgroundChunk> _backgroundChunks = new();
        private VertexBuffer _vertexBuffer;
        private IndexBuffer _indexBuffer;

        private Dictionary<ICombatant, Model> _models = new();
        private BattleScene _scene;

        private PerspView3D _view;

        private UI.Layout.LayoutScreen _ui;
        private UIHandler _uiHandler;
        private UI.UIBatch _menuUI;
        private Menu _activeMenu;

        private TargetGroup _targets;
        private bool _toggleMultiSingleTarget;

        private ActionInProgress _actionInProgress;
        private Action _actionComplete;

        private Engine _engine;

        private bool _debugCamera = false;

        private void AddModel(string code, Vector3 position, ICombatant combatant) {
            var model = Model.LoadBattleModel(Graphics, Game, code);
            model.Translation = position;
            model.Scale = 1;
            if (position.Z < 0)
                model.Rotation = new Vector3(0, 180, 0);
            //Defaults to animation 0 so no PlayAnimation required
            _models.Add(combatant, model);
        }

        private void UpdateVisualState(ICombatant combatant) {
            var model = _models[combatant];
            switch(combatant) {
                case CharacterCombatant chr:
                    if (chr.HP <= 0)
                        model.PlayAnimation(6, true, 1f, null);
                    else if (chr.HP <= (chr.MaxHP / 4))
                        model.PlayAnimation(1, true, 1f, null);
                    else
                        model.PlayAnimation(chr.IdleBattleAnimation, true, 1f, null);
                    //TODO - much more than this!
                    break;
                case EnemyCombatant enemy:
                    model.PlayAnimation(enemy.IdleBattleAnimation, true, 1f, null);
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
                            results.Items.Add(new InventoryItem { ItemID = itemID, Quantity = 1, Kind = InventoryItemKind.Item });
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

        private void LoadBackground() {
            string prefix = SceneDecoder.LocationIDToFileName(_scene.LocationID);

            string NumToFile(int num) {
                char c1 = (char)('a' + (num / 26)),
                    c2 = (char)('a' + (num % 26));
                return $"{prefix}{c1}{c2}";
            }

            List<Texture2D> texs = new();

            int num = 2; //start with ac for texs
            while (true) {
                using (var stex = Game.TryOpen("battle", NumToFile(num++))) {
                    if (stex == null)
                        break;
                    texs.Add(Graphics.LoadTex(new Ficedula.FF7.TexFile(stex), 0));
                }
            }

            List<VertexPositionColorTexture> verts = new();
            List<int> indices = new();

            List<Ficedula.FF7.PFile> pfiles = new();
            num = 12;
            while (true) {
                using (var sp = Game.TryOpen("battle", NumToFile(num++))) {
                    if (sp == null)
                        break;
                    pfiles.Add(new Ficedula.FF7.PFile(sp));
                }
            }

            foreach (var group in pfiles.SelectMany(p => p.Chunks).GroupBy(c => c.Texture)) {
                var bchunk = new BackgroundChunk {
                    Effect = new BasicEffect(Graphics) {
                        VertexColorEnabled = true,
                        LightingEnabled = false,
                        TextureEnabled = group.Key.HasValue,
                        Texture = texs.ElementAtOrDefault(group.Key.GetValueOrDefault(99999)),
                        World = Matrix.Identity,
                    },
                    IndexOffset = indices.Count,
                    VertOffset = verts.Count,
                    TriCount = group.Sum(c => c.Indices.Count) / 3,
                };
                int vcount = 0;
                foreach (var pchunk in group) {
                    indices.AddRange(pchunk.Indices.Select(i => i + vcount));
                    vcount += pchunk.Verts.Count;
                    verts.AddRange(
                        pchunk.Verts
                        .Select(v => new VertexPositionColorTexture {
                            Position = v.Position.ToX(),
                            Color = new Color(v.Colour),
                            TextureCoordinate = v.TexCoord.ToX(),
                        })
                    );
                }
                _backgroundChunks.Add(bchunk);
            }

            _vertexBuffer = new VertexBuffer(Graphics, typeof(VertexPositionColorTexture), verts.Count, BufferUsage.WriteOnly);
            _vertexBuffer.SetData(verts.ToArray());
            _indexBuffer = new IndexBuffer(Graphics, typeof(int), indices.Count, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices.ToArray());
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
        public override void Init(FGame g, GraphicsDevice graphics) {
            base.Init(g, graphics);
            _scene = g.Singleton(() => new BattleSceneCache(g)).Scenes[_formationID];

            LoadBackground();

            var cam = _scene.Cameras[0];
            _view = new PerspView3D {
                CameraPosition = new Vector3(cam.X, cam.Y, cam.Z),
                CameraForwards = new Vector3(cam.LookAtX - cam.X, cam.LookAtY - cam.Y, cam.LookAtZ - cam.Z),
                CameraUp = -Vector3.UnitY, //TODO!!
                ZNear = 100,
                ZFar = 100000,
                FOV = 51f, //Seems maybe vaguely correct, more or less what Proud Clod uses for its preview...
            };

            InitEngine();

            foreach (var enemy in _scene.Enemies) {
                AddModel(
                    SceneDecoder.ModelIDToFileName(enemy.Enemy.ID),
                    new Vector3(enemy.PositionX, enemy.PositionY, enemy.PositionZ),
                    _engine.Combatants.OfType<EnemyCombatant>().First(ec => ec.Enemy == enemy)
                );
            }

            foreach(var player in Game.SaveData.Party.Zip(_playerPositions)) {
                var comb = _engine.Combatants.OfType<CharacterCombatant>().First(cc => cc.Character == player.First);
                AddModel(player.First.BattleModel, player.Second, comb);
            }

            _uiHandler = new UIHandler(_engine.Combatants.OfType<CharacterCombatant>());
            _ui = new UI.Layout.LayoutScreen("battle", _uiHandler);
            _ui.Init(Game, Graphics);

            _menuUI = new UI.UIBatch(Graphics, Game);

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
            _engine = new Engine(128, combatants, Game, callbacks);

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
                SingleTarget = Targets[0];
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

        private Vector2 GetModelScreenPos(ICombatant combatant) {
            var model = _models[combatant];
            var middle = (model.MaxBounds + model.MinBounds) * 0.5f;
            var screenPos = _view.ProjectTo2D(model.Translation + middle);
            return screenPos.XY();
        }

        protected override void DoStep(GameTime elapsed) {
            foreach (var model in _models.Values) {
                model.FrameStep();
            }
            _engine.Tick();

            if (_activeMenu == null) {
                var chr = ReadyToAct.FirstOrDefault();
                if (chr != null)
                    _activeMenu = new Menu(Game, _menuUI, chr);
            } else if (!_activeMenu.Combatant.ReadyForAction)
                _activeMenu = null;

            _uiHandler.Update(elapsed.TotalGameTime.Milliseconds < 500 ? null : _activeMenu?.Combatant);
            _ui.Step(elapsed);

            _menuUI.Reset();

            _activeMenu?.Step();

            var action = _activeMenu?.SelectedAction;
            if (action == null)
                _targets = null;
            else {
                if (_targets == null)
                    _targets = GetTargetOptions(_activeMenu.Combatant, _activeMenu.SelectedAction.TargetFlags).First(tg => tg.IsDefault);
            }

            if (_targets != null) {
                IEnumerable<ICombatant> targets;
                if (_targets.SingleTarget != null)
                    targets = Enumerable.Repeat(_targets.SingleTarget, 1);
                else if (_activeMenu.SelectedAction.TargetFlags.HasFlag(TargettingFlags.RandomTarget))
                    targets = Enumerable.Repeat(_targets.Targets[((long)elapsed.TotalGameTime.TotalMilliseconds / 100) % _targets.Targets.Length], 1);
                else
                    targets = _targets.Targets;

                foreach(var target in targets) {
                    var screenPos = GetModelScreenPos(target);
                    //TODO clamp to screen, presumably
                    _menuUI.DrawImage("pointer", (int)screenPos.X, (int)screenPos.Y, 0.99f, Alignment.Right);
                }
            }

            if (_actionInProgress != null) {
                _actionInProgress.Step(elapsed);
                if (_actionInProgress.IsComplete) {
                    System.Diagnostics.Debug.WriteLine($"Action {_actionInProgress} now complete");
                    _actionInProgress = null;
                    _actionComplete?.Invoke();
                    _actionComplete = null;
                }
            } else {
                /*
* -If action in progress - continue it!
 * -Or, If any counters queued - execute next counter!
 * -Or, if any enemies now dead - execute death effect
 * -Or, Check for battle end, and if not, execute any other queued action
                  */

                var pendingDeadEnemies = _models
                    .Where(kv => !kv.Key.IsPlayer && !kv.Key.IsAlive())
                    .Select(kv => kv.Value)
                    .Where(m => m.Visible);

                if (_engine.AnyQueuedCounters)
                    DoExecuteNextQueuedAction();
                else if (pendingDeadEnemies.Any()) {
                    _actionInProgress = new ActionInProgress("FadeDeadEnemies");
                    foreach (var enemy in pendingDeadEnemies) {
                        _actionInProgress.Add(1, new EnemyDeath(60, enemy));
                        Game.Audio.PlaySfx(Sfx.EnemyDeath, 1f, 0f); //TODO 3d position?!
                    }
                } else if (CheckForBattleEnd()) {
                    //
                } else
                    DoExecuteNextQueuedAction();
            }
        }

        private void DoExecuteNextQueuedAction() {
            if (_engine.ExecuteNextAction(out var nextAction, out var results)) {
                _actionInProgress = new ActionInProgress($"Action {nextAction.Name} by {nextAction.Source.Name}");
                _actionComplete += () => {
                    foreach (var result in results) {
                        result.Apply(nextAction);
                        UpdateVisualState(result.Target);
                    }
                };

                int phase = 0;

                if (nextAction.QueuedText.Any()) {
                    var chars = Game.SaveData.Characters.Select(c => c?.Name).ToArray();
                    var party = Game.SaveData.Party.Select(c => c?.Name).ToArray();

                    foreach (var text in nextAction.QueuedText) {
                        _actionInProgress.Add(phase++, new BattleTitle(
                            Ficedula.FF7.Text.Expand(Ficedula.FF7.Text.Convert(text, 0), chars, party),
                            60, _menuUI, 1f
                        ));
                    }
                }

                //TODO this isn't always needed
                _actionInProgress.Add(phase, new BattleTitle(
                    nextAction.Name ?? "(Unknown)", 60, _menuUI, 0.75f
                ));

                //TODO all the animations!!
                foreach (var result in results) {
                    if (result.Hit) {
                        if (result.HPDamage != 0) {
                            _actionInProgress.Add(phase, new BattleResultText(
                                _menuUI, Math.Abs(result.HPDamage).ToString(), result.HPDamage < 0 ? Color.White : Color.Green,
                                () => GetModelScreenPos(result.Target), new Vector2(0, -1),
                                60
                            ));
                        }
                        if (result.MPDamage != 0) {
                            _actionInProgress.Add(phase, new BattleResultText(
                                _menuUI, $"{Math.Abs(result.MPDamage)} {Font.BATTLE_MP}", result.MPDamage < 0 ? Color.White : Color.Green,
                                () => GetModelScreenPos(result.Target), new Vector2(0, -1),
                                60
                            ));
                        }
                        //TODO anything else?!?!
                    } else {
                        _actionInProgress.Add(phase, new BattleResultText(
                            _menuUI, Font.BATTLE_MISS.ToString(), Color.White,
                            () => GetModelScreenPos(result.Target), new Vector2(0, -1),
                            60
                        ));
                    }
                }
            }
        }

        protected override void DoRender() {

            Graphics.DepthStencilState = DepthStencilState.Default;
            Graphics.RasterizerState = RasterizerState.CullClockwise;
            Graphics.SamplerStates[0] = SamplerState.LinearWrap;

            Graphics.Indices = _indexBuffer;
            Graphics.SetVertexBuffer(_vertexBuffer);

            foreach(var chunk in _backgroundChunks) {
                chunk.Effect.View = _view.View;
                chunk.Effect.Projection = _view.Projection;
                foreach (var pass in chunk.Effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    Graphics.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList, chunk.VertOffset, chunk.IndexOffset, chunk.TriCount
                    );
                }
            }

            foreach (var model in _models.Values)
                if (model.Visible)
                    model.Render(_view);

            _ui.Render();
            _menuUI.Render();
        }

        private IEnumerable<CharacterCombatant> ReadyToAct => _uiHandler.Combatants.Where(c => c.ReadyForAction);

        public override void ProcessInput(InputState input) {
            base.ProcessInput(input);

            if (input.IsJustDown(InputKey.Debug1))
                _debugCamera = !_debugCamera;

            if (_debugCamera) {
                if (input.IsDown(InputKey.Up))
                    _view.CameraPosition += new Vector3(0, 0, -100);
                if (input.IsDown(InputKey.Down))
                    _view.CameraPosition += new Vector3(0, 0, 100);
                if (input.IsDown(InputKey.Left))
                    _view.CameraPosition += new Vector3(100, 0, 0);
                if (input.IsDown(InputKey.Right))
                    _view.CameraPosition += new Vector3(-100, 0, 0);
            } else if (input.IsJustDown(InputKey.Menu)) {
                var ready = ReadyToAct.ToList();
                int index = ready.IndexOf(_activeMenu?.Combatant);
                if (ready.Any()) {
                    _activeMenu = new Menu(Game, _menuUI, ready[(index + 1) % ready.Count]);
                    _targets = null;
                }
            } else if (_targets != null) {
                bool blip = false;
                if (!_targets.MustTargetWholeGroup) {
                    if (input.IsRepeating(InputKey.Up)) {
                        _targets.SingleTarget = _targets.Targets[(_targets.Targets.IndexOf(_targets.SingleTarget) + _targets.Targets.Length - 1) % _targets.Targets.Length];
                        blip = true;
                    } else if (input.IsRepeating(InputKey.Down)) {
                        _targets.SingleTarget = _targets.Targets[(_targets.Targets.IndexOf(_targets.SingleTarget) + 1) % _targets.Targets.Length];
                        blip = true;
                    }
                }

                if (_activeMenu.SelectedAction.TargetFlags.HasFlag(TargettingFlags.ToggleMultiSingleTarget) && input.IsJustDown(InputKey.Select)) {
                    _targets.MustTargetWholeGroup = !_targets.MustTargetWholeGroup;
                    blip = true;
                    if (_targets.MustTargetWholeGroup)
                        _targets.SingleTarget = null;
                    else
                        _targets.SingleTarget = _targets.Targets[0];
                }

                int groupShift = 0;
                if (input.IsRepeating(InputKey.Left))
                    groupShift = -1;
                else if (input.IsRepeating(InputKey.Right))
                    groupShift = 1;

                if (groupShift != 0) {
                    var groups = GetTargetOptions(_activeMenu.Combatant, _activeMenu.SelectedAction.TargetFlags).ToList();
                    int current = groups.FindIndex(g => g.Targets.Any(t => _targets.Targets.Contains(t)));
                    int newIndex = current + groupShift;
                    if ((newIndex >= 0) && (newIndex < groups.Count)) {
                        _targets = groups[newIndex];
                        System.Diagnostics.Debug.WriteLine($"Now targetting {_targets}");
                        blip = true;
                    }
                }

                if (input.IsJustDown(InputKey.OK)) {
                    var source = _activeMenu.Combatant;
                    var q = new QueuedAction(
                        source, _activeMenu.SelectedAction.Ability.Value,
                        _targets.SingleTarget == null ? _targets.Targets : new[] { _targets.SingleTarget },
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
            if (game.DebugOptions.SkipBattleMenu)
                game.PushScreen(new BattleDebug(flags));
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
