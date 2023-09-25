// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7.Battle {

	public class SpriteDraw {
		public int Flags { get; set; }
		public short X { get; set; }
        public short Y { get; set; }
        public short SrcX { get; set; }
        public short SrcY { get; set; }
        public short TexturePage { get; set; }
        public short Unknown { get; set; }
		public byte Width1 { get; set; }
        public byte Width2 { get; set; }
        public byte Height1 { get; set; }
        public byte Height2 { get; set; }

		public SpriteDraw(Stream s) {
			Flags = s.ReadI32();
			X = s.ReadI16();
            Y = s.ReadI16();
            SrcX = s.ReadI16();
            SrcY = s.ReadI16();
            TexturePage = s.ReadI16();
            Unknown = s.ReadI16();
			Width1 = s.ReadU8();
            Width2 = s.ReadU8();
            Height1 = s.ReadU8();
            Height2 = s.ReadU8();
        }
    }
    public class SpriteFrame {
		public short Unknown { get; set; }
		public List<SpriteDraw> Draws { get; set; } 

		public SpriteFrame(Stream s) {
			Unknown = s.ReadI16();
			Draws = Enumerable.Range(0, s.ReadI16())
				.Select(_ => new SpriteDraw(s))
				.ToList();	
		}
	}

    public class Sprite {
        public byte FileType { get; set; }
        public byte Version { get; set; }
		public short Unknown2 { get; set; }
		public List<SpriteFrame> Frames { get; set; }

		public Sprite(Stream s) {
			FileType = s.ReadU8();
			Version = s.ReadU8();
			Unknown2 = s.ReadI16();

			Frames = Enumerable.Range(0, s.ReadI32())
				.Select(_ => new SpriteFrame(s))
				.ToList();
		}
    }
}
