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

namespace Ficedula.FF7 {
    public class TexFile {

        public List<uint[]> Palettes { get; } = new();
        public List<byte[]> Pixels { get; }

        public int Width => Pixels[0].Length;
        public int Height => Pixels.Count;

        public TexFile(Stream source) {
            source.Position = 0x30;
            int numPalettes = source.ReadI32();
            int colours = source.ReadI32();

            source.Position = 0x3C;
            int width = source.ReadI32();
            int height = source.ReadI32();

            source.Position = 0x58;
            int paletteSize = source.ReadI32() * 4;

            foreach(int p in Enumerable.Range(0, numPalettes)) {
                source.Position = 0xEC + colours * 4 * p;
                Palettes.Add(
                    Enumerable.Range(0, colours)
                    .Select(_ => Utils.BSwap(source.ReadU32()))
                    .ToArray()
                );
            }

            source.Position = 0xEC + paletteSize;
            Pixels = Enumerable.Range(0, height)
                .Select(_ =>
                    Enumerable.Range(0, width)
                    .Select(__ => source.ReadU8())
                    .ToArray()
                )
                .ToList();
        }

        public List<uint[]> ApplyPalette(int which) {
            var palette = Palettes[which];
            return Pixels
                .Select(row => row.Select(b => palette[b]).ToArray())
                .ToList();
        }
    }
}
