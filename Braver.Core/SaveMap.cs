// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

namespace Braver {
   public class SaveMap {
       private VMM _memory;
       public SaveMap(VMM memory) { _memory = memory; }

		public ushort PPV { get => (ushort)_memory.Read(2, 0x00); set => _memory.Write(2, 0x00, (ushort)value); }
		public byte GameTimeHours { get => (byte)_memory.Read(1, 0x10); set => _memory.Write(1, 0x10, (byte)value); }
		public byte GameTimeMinutes { get => (byte)_memory.Read(1, 0x11); set => _memory.Write(1, 0x11, (byte)value); }
		public byte GameTimeSeconds { get => (byte)_memory.Read(1, 0x12); set => _memory.Write(1, 0x12, (byte)value); }
		public byte GameTimeFrames { get => (byte)_memory.Read(1, 0x13); set => _memory.Write(1, 0x13, (byte)value); }
		public byte CounterHours { get => (byte)_memory.Read(1, 0x14); set => _memory.Write(1, 0x14, (byte)value); }
		public byte CounterMinutes { get => (byte)_memory.Read(1, 0x15); set => _memory.Write(1, 0x15, (byte)value); }
		public byte CounterSeconds { get => (byte)_memory.Read(1, 0x16); set => _memory.Write(1, 0x16, (byte)value); }
		public byte CounterFrames { get => (byte)_memory.Read(1, 0x17); set => _memory.Write(1, 0x17, (byte)value); }
		public ushort NumBattlesFought { get => (ushort)_memory.Read(2, 0x18); set => _memory.Write(2, 0x18, (ushort)value); }
		public ushort NumEscapes { get => (ushort)_memory.Read(2, 0x1a); set => _memory.Write(2, 0x1a, (ushort)value); }
		public MenuMask MenuVisible { get => (MenuMask)_memory.Read(2, 0x1c); set => _memory.Write(2, 0x1c, (ushort)value); }
		public MenuMask MenuLocked { get => (MenuMask)_memory.Read(2, 0x1e); set => _memory.Write(2, 0x1e, (ushort)value); }
   }
}
