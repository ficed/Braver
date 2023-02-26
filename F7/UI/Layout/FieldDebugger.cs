using Braver.Field;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.UI.Layout {

    public class FieldDebugger : LayoutModel {

        public List lbEntities, lbFibers;


        private FieldScreen _field;

        public override bool IsRazorModel => true;

        public IEnumerable<Entity> Entities => _field.Entities;

        public Entity CurrentEntity { get; private set; }
        public Fiber CurrentFiber { get; private set; }

        public List<Ficedula.FF7.Field.DisassembledOp> Disassembly { get; } = new();

        public override void Created(FGame g, LayoutScreen screen) {
            base.Created(g, screen);
            _field = (FieldScreen)screen.Param;
        }

        protected override void OnInit() {
            base.OnInit();
            if (FocusGroup == null) {
                PushFocus(lbEntities, lbEntities.Children[0]);
            }
        }

        public void FiberFocussed(Label L) {
            CurrentFiber = CurrentEntity.DebugFibers.Single(f => L.ID == ("Fiber" + f.Priority));
            int offset = Math.Max(0, CurrentFiber.IP);
            byte[] script = _field.FieldDialog.ScriptBytecode
                .Skip(offset)
                .ToArray();
            Disassembly.Clear();
            Disassembly.AddRange(Ficedula.FF7.Field.VMOpcodes.Disassemble(script, offset)); 
            _screen.Reload();
        }

        public void EntitySelected(Label L) {
            CurrentEntity = Entities.Single(e => L.ID == ("Entity" + e.Name));
            CurrentFiber = CurrentEntity.DebugFibers.First();
            _screen.Reload();
            PushFocus(lbFibers, lbFibers.Children[0]);
        }

        public override void CancelPressed() {
            if (FocusGroup == lbEntities) {
                _screen.FadeOut(() => Game.PopScreen(_screen));
            } else
                base.CancelPressed();
        }
    }
}
