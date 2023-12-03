// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ficedula.FF7.WorldMap {

    public class AreaEncounters {
        public EncounterTable Area0 { get; private set; }
        public EncounterTable Area1 { get; private set; }
        public EncounterTable Area2 { get; private set; }
        public EncounterTable Area3 { get; private set; }

        public IEnumerable<EncounterTable> Areas {
            get {
                yield return Area0;
                yield return Area1;
                yield return Area2;
                yield return Area3;
            }
        }

        public AreaEncounters(Stream source) {
            Area0 = new EncounterTable(source);
            Area1 = new EncounterTable(source);
            Area2 = new EncounterTable(source);
            Area3 = new EncounterTable(source);
        }
    }

    public class Encounter {
        public byte BattleID { get; private set; }
        public byte Chance { get; private set; }

        public bool IsValid => BattleID > 0 && Chance > 0;

        public Encounter(Stream source) {
            BattleID = source.ReadU8();
            Chance = source.ReadU8();
        }
    }

    public class EncounterTable {
        public bool Enabled { get; private set; }
        public byte EncounterRate { get; private set; }

        public List<Encounter> NormalEncounters { get; private set; }
        public List<Encounter> SpecialEncounters { get; private set; }
        public List<Encounter> ChocoboEncounters { get; private set; }

        public EncounterTable(Stream source) {
            Enabled = source.ReadU8() != 0;
            EncounterRate = source.ReadU8();

            NormalEncounters = Enumerable.Range(0, 6)
                .Select(_ => new Encounter(source))
                .Where(e => e.IsValid)
                .ToList();
            SpecialEncounters = Enumerable.Range(0, 4)
                .Select(_ => new Encounter(source))
                .Where(e => e.IsValid)
                .ToList();
            ChocoboEncounters = Enumerable.Range(0, 5)
                .Select(_ => new Encounter(source))
                .Where(e => e.IsValid)
                .ToList();
        }
    }

    //Decodes the encounter tables found in enc_w.bin, which provide a list of possible encounters
    //(along with encounter rates and chances for each individual encounter) for each of the 16
    //possible world map areas
    public class EncounterTables {

        public List<AreaEncounters> Areas { get; private set; }

        public EncounterTables(Stream source) {
            //Skip header - not decoded it yet, but unclear if we actually need any data from it?
            source.Position = 0xA0;

            //File always contains 16 areas; seems like that's hardcoded in FF7
            //(Map data contains 17 areas, but the last one is sea, so it seems like only the first 16
            //areas can actually have encounters)
            Areas = Enumerable.Range(0, 16)
                .Select(_ => new AreaEncounters(source))
                .ToList();
        }
    }

    //Decodes the data table which states for each of the 16 world map areas, which walkmap statuses map
    //to which encounter table.

    public class WalkMapArea {
        private List<byte> _walkmapTypes;

        public WalkMapArea(Stream source) {
            _walkmapTypes = Enumerable.Range(0, 4)
                .Select(_ => source.ReadU8())
                .ToList();
        }

        public int? TableForWalkmapType(byte walkmapType) {
            foreach (int i in Enumerable.Range(0, _walkmapTypes.Count))
                if (walkmapType == _walkmapTypes[i])
                    return i;
            return null;
        }
    }

    public class WalkMapEncounterTable {
        public List<WalkMapArea> WalkmapAreas { get; private set;}

        public WalkMapEncounterTable(Stream source) {
            WalkmapAreas = Enumerable.Range(0, 16)
                .Select(_ => new WalkMapArea(source))
                .ToList();
        }
    }
}