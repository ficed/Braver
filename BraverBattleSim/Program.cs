// Usage: BraverBattleSim [DataFolder] [EncounterNumber] [SaveDataFile]
using Braver;
using Braver.Battle;
using Ficedula.FF7.Battle;
using System;
using System.Numerics;

Console.WriteLine("Braver Battle Sim");

var game = new SimGame(args[0]);
game.Start(args[2]);

var scene = SceneDecoder.Decode(game.Open("battle", "scene.bin"))
    .ElementAt(int.Parse(args[1]));

ICombatant[] combatants = new ICombatant[16];

int index = 0;
foreach (var chr in game.SaveData.Party)
    combatants[index++] = new CharacterCombatant(game, chr);

index = 4;
foreach(var group in scene.Enemies.GroupBy(ei => ei.Enemy.ID)) {
    int c = 0;
    foreach(var enemy in group) {
        combatants[index++] = new EnemyCombatant(enemy, group.Count() == 1 ? null : c++);
    }
}

var callbacks = new SimCallbacks(game.Memory);
var engine = new Engine(128, combatants, game, callbacks);

engine.ReadyForAction += c => Console.WriteLine(c.Name + " is ready for an action.");
engine.ActionQueued += a => Console.WriteLine($"{a.Source} queued action {a.Name} priority {a.Priority}");

while (engine.Status == BattleStatus.InProgress) {
    engine.Tick();
    var ready = engine.ActiveCombatants.OfType<CharacterCombatant>().Where(c => c.ReadyForAction);
    if (ready.Any()) {

        foreach (var chr in ready) {
            Console.WriteLine(chr.Name);
            var ability = MenuChoose(chr);
            Console.WriteLine("Targets:");
            Console.WriteLine(string.Join(" ", engine.ActiveCombatants.Select((comb, index) => $"{(char)('A' + index)}:{comb.Name}")));
            var targets = Console.ReadLine()
                .Trim()
                .ToUpper()
                .Split(' ')
                .Select(s => s[0])
                .Select(c => engine.ActiveCombatants.ElementAt(c - 'A'));

            var q = new QueuedAction(chr, ability.ability, targets.ToArray(), ActionPriority.Normal, ability.name);
            //TODO limit priority
            q.AfterAction = () => {
                chr.TTimer.Reset();
            };
            engine.QueueAction(q);
            chr.ReadyForAction = false;
        }
    }

    bool didAnyActions = false;
    while (engine.ExecuteNextAction(out var action, out var results)) {
        Console.WriteLine($"{action.Source} targetting {string.Join<ICombatant>(",", action.Targets)} with {action.Name}");
        foreach (var result in results) {
            Console.WriteLine($"--Target {result.Target}, hit {result.Hit}, inflict {result.InflictStatus} remove {result.RemoveStatus} recovery {result.Recovery}, damage HP {result.HPDamage} MP {result.MPDamage}");
            result.Apply(action);
        }
        didAnyActions = true;
    }

    if (didAnyActions) {
        foreach (var c in engine.ActiveCombatants) {
            Console.WriteLine($"{c} HP {c.HP}/{c.MaxHP} MP {c.MP}/{c.MaxMP} Status {c.Statuses}");
        }
        Console.WriteLine();
    }
}
Console.WriteLine($"Result: {engine.Status}");

(Ability ability, string name) MenuChoose(CharacterCombatant chr) {
    char c = 'A';
    foreach (var action in chr.Actions) {
        Console.WriteLine($"  {c++}: {action.Name}");
    }
    char choice = Console.ReadLine().Trim().ToUpper().First();
    var chosen = chr.Actions[choice - 'A'];
    if (chosen.Ability != null)
        return (chosen.Ability.Value, chosen.Name);

    c = 'A';
    foreach(var sub in chosen.SubMenu) {
        Console.WriteLine($"    {c++}: {sub.Name}");
    }
    choice = Console.ReadLine().Trim().ToUpper().First();
    var subchosen = chosen.SubMenu[choice - 'A'];
    return (subchosen.Ability, subchosen.Name);
}

public class SimGame : BGame {
    public SimGame(string data) {
        _data["battle"] = new List<DataSource> {
                new LGPDataSource(new Ficedula.FF7.LGPFile(Path.Combine(data, "battle", "battle.lgp"))),
                new FileDataSource(Path.Combine(data, "battle"))
            };
        _data["kernel"] = new List<DataSource> {
                new FileDataSource(Path.Combine(data, "kernel"))
            };
    }

    public void Start(string savegame) {
        SaveData = Serialisation.Deserialise<SaveData>(File.OpenRead(savegame));
    }
}

public class SimCallbacks : AICallbacks {
    public SimCallbacks(VMM vmm) {
        _vmm = vmm;
    }

    public override void DisplayText(byte[] text) {
        //TODO encoding?!?!
        Console.WriteLine(System.Text.Encoding.ASCII.GetString(text));
    }
}