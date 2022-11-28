// Usage: BraverBattleSim [DataFolder] [EncounterNumber] [SaveDataFile]
using Braver;
using Braver.Battle;
using Ficedula.FF7.Battle;
using System;

Console.WriteLine("Braver Battle Sim");

var game = new SimGame(args[0]);
game.Start(args[2]);

var scene = Ficedula.FF7.Battle.SceneDecoder.Decode(game.Open("battle", "scene.bin"))
    .ElementAt(int.Parse(args[1]));

var chars = game.SaveData
    .Party
    .Select(c => new CharacterCombatant(game, c));

var enemies = scene
    .Enemies
    .GroupBy(ei => ei.Enemy.ID)
    .SelectMany(group => group.Select((ei, index) => new EnemyCombatant(ei, group.Count() == 1 ? null : index)));

var engine = new Engine(128, chars.Concat<ICombatant>(enemies));

while (true) {
    engine.Tick();
    var ready = engine.Combatants.Where(c => c.TTimer.IsFull);
    if (ready.Any()) {
        Console.WriteLine($"At {engine.GTimer.Ticks} gticks, {string.Join(", ", ready.Select(c => c.Name))} ready to act");

        foreach(var chr in ready.OfType<CharacterCombatant>()) {
            Console.WriteLine(chr.Name);
            var ability = MenuChoose(chr);
            Console.WriteLine("Targets:");
            Console.WriteLine(string.Join(" ", engine.Combatants.Select((comb, index) => $"{(char)('A' + index)}:{comb.Name}")));
            var targets = Console.ReadLine()
                .Trim()
                .ToUpper()
                .Split(' ')
                .Select(s => s[0])
                .Select(c => engine.Combatants.ElementAt(c - 'A'));
            var results = engine.ApplyAbility(
                chr,
                ability,
                targets
            );
            foreach (var result in results) {
                Console.WriteLine($"--Target {result.Target}, hit {result.Hit}, inflict {result.InflictStatus} remove {result.RemoveStatus} recovery {result.Recovery}, damage HP {result.HPDamage} MP {result.MPDamage}");
                result.Apply();
            }
            chr.TTimer.Reset();
        }

        foreach (var enemy in ready.OfType<EnemyCombatant>()) {
            //TODO AI
            var action = enemy.Enemy.Enemy.Actions[0];
            var target = engine.Combatants
                .OfType<CharacterCombatant>()
                .First();

            Console.WriteLine($"Enemy {enemy.Name} targetting {target.Name} with {action.Attack.Name}");

            var results = engine.ApplyAbility(
                enemy,
                action.Attack.ToAbility(enemy),
                new[] { target }
            );
            foreach(var result in results) {
                Console.WriteLine($"--Target {result.Target}, hit {result.Hit}, inflict {result.InflictStatus} remove {result.RemoveStatus} recovery {result.Recovery}, damage HP {result.HPDamage} MP {result.MPDamage}");
                result.Apply();
            }
            enemy.TTimer.Reset();
        }

        foreach(var c in engine.Combatants) {
            Console.WriteLine($"{c.Name} HP {c.HP}/{c.MaxHP} MP {c.MP}/{c.MaxMP} Status {c.Statuses}");
        }
        Console.WriteLine();
    }

}

Ability MenuChoose(CharacterCombatant chr) {
    char c = 'A';
    foreach (var action in chr.Actions) {
        Console.WriteLine($"  {c++}: {action.Name}");
    }
    char choice = Console.ReadLine().Trim().ToUpper().First();
    var chosen = chr.Actions[choice - 'A'];
    if (chosen.Ability != null)
        return chosen.Ability.Value;

    c = 'A';
    foreach(var sub in chosen.SubMenu) {
        Console.WriteLine($"    {c++}: {sub.Name}");
    }
    choice = Console.ReadLine().Trim().ToUpper().First();
    var subchosen = chosen.SubMenu[choice - 'A'];
    return subchosen.Ability;
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
