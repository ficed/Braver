// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Braver.UI;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Braver.Battle {

    public enum EffectCommand {
        PreloadSprite,
        PreloadSummon,
        PreloadSfx,

        Sprite,
        Sfx,
        Camera,
        Anim,
        Resume,
        Wait,
        Pause,
        Result,

        DisplayText,

        DeathFade,
        ApplyResults,
    }

    public interface IWaitableEffect {
        bool IsComplete { get; }
    }

    public struct ImmediatelyCompleteEffect : IWaitableEffect {
        public bool IsComplete => true;
    }
    public struct CallbackWaitEffect : IWaitableEffect {
        public Func<bool> CheckComplete { get; set; }
        public bool IsComplete => CheckComplete();
    }

    public class EffectTemplate : IBraverTemplateModel {
        private AbilityResult[] _results;
        private FGame _game;
        private QueuedAction _action;

        public IEnumerable<AbilityResult> Results => _results;
        public QueuedAction Action => _action;

        FGame IBraverTemplateModel.Game => _game;
        string IBraverTemplateModel.SourceCategory => "battle";

        string IBraverTemplateModel.SourceExtension => ".effect";

        public EffectTemplate(AbilityResult[] results, FGame game, QueuedAction action) {
            _results = results;
            _game = game;
            _action = action;
        }

        public string Expand(byte[] text) {
            var chars = _game.SaveData.Characters.Select(c => c?.Name).ToArray();
            var party = _game.SaveData.Party.Select(c => c?.Name).ToArray();
            return Ficedula.FF7.Text.Expand(Ficedula.FF7.Text.Convert(text, 0), chars, party);
        }
    }

    public class BattleEffectManager {

        private static Dictionary<EffectCommand, Func<BattleEffectManager, IEnumerable<string>, IWaitableEffect>> _executor = new();
        private Dictionary<string, List<IWaitableEffect>> _waitable = new(StringComparer.InvariantCultureIgnoreCase);
        private List<IInProgress> _ongoing = new();

        private static IWaitableEffect _immediatelyComplete = new ImmediatelyCompleteEffect();


        static BattleEffectManager() {
            _executor[EffectCommand.Sfx] = (effect, parms) => effect.CmdSfx(parms);
            _executor[EffectCommand.PreloadSfx] = (effect, parms) => effect.CmdPreloadSfx(parms);
            _executor[EffectCommand.Wait] = (effect, parms) => effect.CmdWait(parms);
            _executor[EffectCommand.Pause] = (effect, parms) => effect.CmdPause(parms);
            _executor[EffectCommand.PreloadSprite] = (effect, parms) => effect.CmdPreloadSprite(parms);
            _executor[EffectCommand.Sprite] = (effect, parms) => effect.CmdSprite(parms);
            _executor[EffectCommand.Anim] = (effect, parms) => effect.CmdAnim(parms);
            _executor[EffectCommand.Resume] = (effect, parms) => effect.CmdResume(parms);
            _executor[EffectCommand.Result] = (effect, parms) => effect.CmdResult(parms);
            _executor[EffectCommand.DeathFade] = (effect, parms) => effect.CmdDeathFade(parms);
            _executor[EffectCommand.ApplyResults] = (effect, parms) => effect.CmdApplyResults(parms);
            _executor[EffectCommand.DisplayText] = (effect, parms) => effect.CmdDisplayText(parms);
            _executor[EffectCommand.Camera] = (effect, parms) => effect.CmdCamera(parms);
        }

        private IWaitableEffect WaitableFromInProgress(IEnumerable<IInProgress> effects) {
            _ongoing.AddRange(effects);
            return new CallbackWaitEffect {
                CheckComplete = () => effects.All(p => p.IsComplete)
            };
        }
        private IWaitableEffect WaitableFromInProgress(params IInProgress[] effects) {
            return WaitableFromInProgress(effects.AsEnumerable());
        }

        private IWaitableEffect CmdResult(IEnumerable<string> parms) {
            var result = _results.Single(r => r.Target.ID.ToString() == parms.ElementAt(0));

            IInProgress effect = null;

            if (result.Hit) {
                if (result.HPDamage != 0) {
                    effect = new BattleResultText(
                        _screen.MenuUI, Math.Abs(result.HPDamage).ToString(), result.HPDamage < 0 ? Color.White : Color.Green,
                        () => _screen.GetModelScreenPos(result.Target).XY(), new Vector2(0, -1),
                        60,
                        $"{result.Target.Name} {Math.Abs(result.HPDamage)} {(result.HPDamage >= 0 ? "healing" : "damage")}"
                    );
                }
                if (result.MPDamage != 0) {
                    effect = new BattleResultText(
                        _screen.MenuUI, $"{Math.Abs(result.MPDamage)} {Font.BATTLE_MP}", result.MPDamage < 0 ? Color.White : Color.Green,
                        () => _screen.GetModelScreenPos(result.Target).XY(), new Vector2(0, -1),
                        60,
                        $"{result.Target.Name} {Math.Abs(result.HPDamage)} MP {(result.HPDamage >= 0 ? "healing" : "damage")}"
                    );
                }
                //TODO anything else?!?!
            } else {
                effect = new BattleResultText(
                    _screen.MenuUI, Font.BATTLE_MISS.ToString(), Color.White,
                    () => _screen.GetModelScreenPos(result.Target).XY(), new Vector2(0, -1),
                    60,
                    $"{result.Target.Name} missed"
                );
            }

            if (effect != null)
                return WaitableFromInProgress(effect);
            else
                return _immediatelyComplete;
        }

        private IWaitableEffect CmdDeathFade(IEnumerable<string> parms) {
            var who = _models[parms.ElementAt(0)];
            return WaitableFromInProgress(new EnemyDeath(60, who, _screen.Renderer.Models[who]));
        }

        private IWaitableEffect CmdDisplayText(IEnumerable<string> parms) {
            string text = string.Join(" ", parms);
            return WaitableFromInProgress(new BattleTitle(text, 60, _screen.MenuUI, 1f, true));
        }

        private IWaitableEffect CmdApplyResults(IEnumerable<string> parms) {
            foreach (var result in _results) {
                result.Apply(_action);
                _screen.UpdateVisualState(result.Target);
            }
            return _immediatelyComplete;
        }

        private IWaitableEffect CmdResume(IEnumerable<string> parms) {
            var who = _models[parms.ElementAt(0)];
            var exec = _anims[who];
            switch (exec.WaitingFor) {
                case AnimScriptExecutor.WaitingForKind.Action:
                    exec.Resume();
                    return _immediatelyComplete;
                default:
                    throw new NotImplementedException();
            }
        }
        private IWaitableEffect CmdAnim(IEnumerable<string> parms) {
            var who = _models[parms.ElementAt(0)];
            var model = _screen.Renderer.Models[who];
            int anim = int.Parse(parms.ElementAt(1));

            var exec = _anims[who] = new AnimScriptExecutor(
                who, _screen,
                new Ficedula.FF7.Battle.AnimationScriptDecoder(model.AnimationScript.Scripts[anim])
            );

            return new CallbackWaitEffect {
                CheckComplete = () => (_anims[who] != exec) || exec.IsComplete || (exec.WaitingFor == AnimScriptExecutor.WaitingForKind.Action)
            };
        }

        private IWaitableEffect CmdSprite(IEnumerable<string> parms) {
            var sprite = _spritesByAlias[parms.First()];
            var target = _models[parms.ElementAt(1)];
            var model = _screen.Renderer.Models[target];
            var offset = GraphicsUtil.Parse3(parms.ElementAtOrDefault(2) ?? "0/0/0");
            var instance = _screen.Renderer.Sprites.Add(sprite, () => _screen.GetModelScreenPos(target, offset));
            return new CallbackWaitEffect {
                CheckComplete = () => !instance.IsActive
            };
        }

        private IWaitableEffect CmdPreloadSprite(IEnumerable<string> parms) {
            //TODO actually background load!
            var sprite = _screen.Sprites.Get(parms.ElementAt(1), parms.Skip(2));
            _spritesByAlias[parms.ElementAt(0)] = sprite;
            return _immediatelyComplete;
        }

        private IWaitableEffect CmdPause(IEnumerable<string> parms) {
            int completeFrames = _game.GameTimeFrames + int.Parse(parms.First());
            return new CallbackWaitEffect {
                CheckComplete = () => _game.GameTimeFrames >= completeFrames
            };
        }

        private IWaitableEffect CmdCamera(IEnumerable<string> parms) {
            int id;
            var source = _models["source"];
            var targets = _models.Values.Where(c => c != source).Select(c => _screen.Renderer.Models[c]);

            if (parms.First().Equals("auto")) {
                id = (targets.Count() > 1 ? _action.Ability.MultiTargetCamera : _action.Ability.SingleTargetCamera) ?? 0;
            } else
                id = int.Parse(parms.First());

            bool isDone = false;
            _screen.CameraController.Execute(
                id,
                _screen.Renderer.Models[source],
                targets,
                () => isDone = true
            );
            return new CallbackWaitEffect {
                CheckComplete = () => isDone
            };
        }

        private IWaitableEffect CmdSfx(IEnumerable<string> parms) {
            int id = int.Parse(parms.First());
            //TODO - 3d position
            _game.Audio.PlaySfx(id, 1f, 0f);
            return _immediatelyComplete;
            //TODO - wait for sfx completion?
        }
        private IWaitableEffect CmdPreloadSfx(IEnumerable<string> parms) {
            int id = int.Parse(parms.First());
            _game.Audio.Precache(id, false);
            return _immediatelyComplete;
            //TODO - wait for completion?
        }

        private IWaitableEffect CmdWait(IEnumerable<string> parms) {
            if (parms.First().Equals("All", StringComparison.InvariantCultureIgnoreCase)) {
                var allWaiters = _waitable.Values.SelectMany(L => L);
                if (allWaiters.Any(w => !w.IsComplete))
                    return null;
                _waitable.Clear();
                return _immediatelyComplete;
            } else {
                var waiters = parms
                    .Select(id => _waitable.GetValueOrDefault(id))
                    .Where(L => L != null)
                    .SelectMany(L => L);
                if (waiters.Any(w => !w.IsComplete))
                    return null;
                return _immediatelyComplete;
            }
        }


        private Dictionary<string, ICombatant> _models = new(StringComparer.InvariantCultureIgnoreCase);
        private Dictionary<ICombatant, AnimScriptExecutor> _anims = new();
        private List<string> _effect;
        private int _ip;
        private FGame _game;
        private Dictionary<string, LoadedSprite> _spritesByAlias = new(StringComparer.InvariantCultureIgnoreCase);
        private RealBattleScreen _screen;
        private QueuedAction _action;
        private AbilityResult[] _results;

        public bool IsComplete => _ip >= _effect.Count;

        public BattleEffectManager(FGame g, RealBattleScreen screen, 
            QueuedAction action, AbilityResult[] results, string effect) {
            _game = g;
            _screen = screen;
            _action = action;
            _results = results;

            var cache = g.Singleton(() => new RazorLayoutCache(g));
            var model = new EffectTemplate(results, g, action);
            _effect = cache.Compile("battle", effect + ".effect", false)
                .Run(template => template.Model = model)
                .Split('\r', '\n')
                .ToList();

            _models["source"] = action.Source;
            foreach (var result in results)
                _models[result.Target.ID.ToString()] = result.Target;
        }

        public void Render() {
            foreach (var anim in _anims.Values.Where(a => a != null))
                anim.Render();

        }

        public void Step() {
            foreach (var anim in _anims.Values.Where(a => a != null))
                anim.Step();
            foreach (var effect in _ongoing)
                effect.FrameStep();

            while(_ip < _effect.Count) {
                string cmd = _effect[_ip].Trim();
                if (string.IsNullOrWhiteSpace(cmd) || cmd.StartsWith('#')) {
                    _ip++;
                    continue;
                }
                var parts = cmd.Split(null)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
                int index = 0;
                string waitID;
                if (parts[index].StartsWith('!')) 
                    waitID = parts[index++];
                else
                    waitID = null;

                var effectCmd = Enum.Parse<EffectCommand>(parts[index++]);
                var result = _executor[effectCmd](this, parts.Skip(index));
                if (result != null) {
                    _ip++;
                    if (waitID != null) {
                        if (!_waitable.TryGetValue(waitID, out var list))
                            _waitable[waitID] = list = new List<IWaitableEffect>();
                        list.Add(result);
                    }
                } else
                    return;
            }

            if (IsComplete) {
                foreach (var effect in _ongoing.Where(p => !p.IsComplete))
                    effect.Cancel();
            }
        }

    }
}
/*
 * Prefix any command with !waitID to make it waitable later
 * 
 * PRELOADSPRITE <alias> <spritefile> <tex> <tex> <tex>...
 * PRELOADSUMMON <alias> <summoncode>
 * PRELOADSFX <numberOrName>
 * 
 * SPRITE <alias> <combatantID> <xyzOffset>
 * SFX <numberOrName> <positionAtCombatantID>
 * CAMERA <id>|auto
 * ANIM <combatantID> <animNumber>
 * RESUME <combatantID>
 * WAIT <id> <id> <id>...
 * WAIT ALL
 * PAUSE <duration>
 * RESULT <combatantID>
 * 
 */