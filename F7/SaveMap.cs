namespace Braver {
   public class SaveMap {
       private VMM _memory;
       public SaveMap(VMM memory) { _memory = memory; }

		public ushort PPV { get => (ushort)_memory.Read(2, 0x00); set => _memory.Write(2, 0x00, (ushort)value); }
		public ushort NumBattlesFought { get => (ushort)_memory.Read(2, 0x18); set => _memory.Write(2, 0x18, (ushort)value); }
		public ushort NumEscapes { get => (ushort)_memory.Read(2, 0x1a); set => _memory.Write(2, 0x1a, (ushort)value); }
		public MenuMask MenuHidden { get => (MenuMask)_memory.Read(2, 0x1c); set => _memory.Write(2, 0x1c, (ushort)value); }
		public MenuMask MenuLocked { get => (MenuMask)_memory.Read(2, 0x1e); set => _memory.Write(2, 0x1e, (ushort)value); }
   }
}
