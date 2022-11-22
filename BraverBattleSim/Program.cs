// Usage: BraverBattleSim [DataFolder] [EncounterNumber] [SaveDataFile]
using Braver;
using Braver.Battle;

Console.WriteLine("Braver Battle Sim");

var scene = Ficedula.FF7.Battle.SceneDecoder.Decode(File.OpenRead(Path.Combine(args[0], "battle", "scene.bin")))
    .ElementAt(int.Parse(args[1]));

var saveData = Serialisation.Deserialise<SaveData>(File.OpenRead(args[2]));

var chars = saveData
    .Party
    .Select(c => new CharacterCombatant(c));

var enemies = scene
    .Enemies
    .GroupBy(ei => ei.Enemy.ID)
    .SelectMany(group => group.Select((ei, index) => new EnemyCombatant(ei, group.Count() == 1 ? null : index)));

var engine = new Braver.Battle.Engine(128, chars.Concat<ICombatant>(enemies));

while (true) {
    engine.Tick();
    var ready = engine.Combatants.Where(c => c.TTimer.IsFull);
    if (ready.Any()) {
        Console.WriteLine($"At {engine.GTimer.Ticks} gticks, {string.Join(", ", ready.Select(c => c.Name))} ready to act");
        foreach(var enemy in ready.OfType<EnemyCombatant>()) {
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
            }
        }
    }

}