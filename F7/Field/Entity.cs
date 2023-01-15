using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Field {
    [Flags]
    public enum EntityFlags {
        None = 0,
        CanTalk = 0x1,
        CanCollide = 0x2,
    }

    public class Entity {

        public static bool DEBUG_OUT = true;

        private Ficedula.FF7.Field.Entity _entity;
        private Fiber[] _priorities;

        public string Name => _entity.Name;
        public FieldModel Model { get; set; }
        public FieldLine Line { get; set; }
        public Character Character { get; set; }
        public EntityFlags Flags { get; set; }
        public float TalkDistance { get; set; }
        public float CollideDistance { get; set; }
        public float MoveSpeed { get; set; }
        public int WalkmeshTri { get; set; }

        public HashSet<Entity> CollidingWith { get; } = new();
        public HashSet<Entity> LinesCollidingWith { get; } = new();

        public Entity(Ficedula.FF7.Field.Entity entity, FieldScreen screen) {
            _entity = entity;
            _priorities = Enumerable.Range(0, 8)
                .Select(_ => new Fiber(this, screen, screen.FieldDialog.ScriptBytecode))
                .ToArray();
            Flags = EntityFlags.CanTalk | EntityFlags.CanCollide;
            MoveSpeed = 1f;
        }

        public bool Call(int priority, int script, Action onComplete) {
            if (_priorities[priority].Active)
                return false;

            _priorities[priority].OnStop = onComplete;
            _priorities[priority].Start(_entity.Scripts[script]);
            return true;
        }

        public void Run(int maxOps, bool isInit = false) {
            int priority = 7;
            foreach (var fiber in _priorities.Reverse()) {
                if (fiber.InProgress) {
                    if (DEBUG_OUT)
                        System.Diagnostics.Debug.WriteLine($"Entity {Name} running script from IP {fiber.IP} priority {priority}");
                    fiber.Run(maxOps, isInit);
                    if (isInit) fiber.Resume();
                    break;
                }
                priority--;
            }
        }

        public override string ToString() => $"Entity {Name}";

    }
}
