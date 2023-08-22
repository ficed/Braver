// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using Ficedula.FF7.Field;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7.Battle {

    public enum BattleLayout : byte {
        Normal = 0,
        Preemptive = 1,
        BackAttack = 2,
        SideAttack = 3,
        PincerAttack = 4,
        PincerAttack2 = 5,
        SideAttack2 = 6,
        SideAttack3 = 7,
        NormalLockFront = 8,
    }

    public class BattleScene {
        //Computed
        public int FormationID { get; set; }

        public short LocationID { get; set; }
        public short? NextBattleID { get; set; }
        public short EscapeCounter { get; set; }
        public List<ushort> NextBattleArenaFormations { get; } = new();
        public ushort EscapableFlag { get; set; } //TODO flags?
        public BattleLayout Layout { get; set; }
        public byte InitialCamera { get; set; }
        public List<BattleCamera> Cameras { get; } = new();

        public List<EnemyInstance> Enemies { get; } = new();

        public byte[] FormationAI { get; set; }

        public BattleScene(Stream s, BattleCamera[] cameras) {
            LocationID = s.ReadI16();
            NextBattleID = s.ReadI16(); //TODO null
            EscapeCounter = s.ReadI16();
            s.ReadI16();
            NextBattleArenaFormations.AddRange(
                Enumerable.Range(0, 4)
                .Select(_ => s.ReadU16())
                .Where(id => id != 0x3E7)
            );
            EscapableFlag = s.ReadU16();
            Layout = (BattleLayout)s.ReadByte();
            InitialCamera = (byte)s.ReadByte();
            Cameras.AddRange(cameras);
        }
    }

    [Flags]
    public enum EnemyFlags {
        None = 0,
        Visible = 0x1,
        SideAttackFacing = 0x2,
        Targettable = 0x8,
        MainScriptActive = 0x10,
    }

    public class EnemyInstance {
        public short PositionX { get; set; }
        public short PositionY { get; set; }
        public short PositionZ { get; set; }
        public short Row { get; set; }
        public ushort CoverFlags { get; set; }
        public EnemyFlags Flags { get; set; }
        public Enemy? Enemy { get; set; }

        public EnemyInstance(Stream s, Enemy[] enemies) {
            short eID = s.ReadI16();
            if (eID != -1)
                Enemy = enemies.First(e => e.ID == eID);

            PositionX = s.ReadI16();
            PositionY = s.ReadI16();
            PositionZ = s.ReadI16();
            Row = s.ReadI16();
            CoverFlags = s.ReadU16();
            s.ReadByte(); s.ReadByte(); s.ReadByte();
            Flags = (EnemyFlags)s.ReadByte();
        }
    }

    public enum ElementRate {
        Death = 0,
        Weak = 2,
        Resist = 4,
        Immune = 5,
        Absorb = 6,
        Recovery = 7,
        None = 0xff
    }

    public class EnemyAction {
        public byte AnimationIndex { get; set; }
        public short ActionID { get; set; }
        public byte CameraSingleTarget { get; set; }
        public byte CameraMultiTarget { get; set; }
        public bool AvailableViaManipulate { get; set; }
        public Attack Attack { get; set; }
    }

    public class EnemyItem {
        public short ItemID { get; set; }
        public byte Chance { get; set; } // / 63
    }

    public class Enemy {
        public short ID { get; set; }
        public string Name { get; set; }
        public byte Level { get; set; }
        public byte Dexterity { get; set; }
        public byte Luck { get; set; }
        public byte DefPercent { get; set; }
        public byte Attack { get; set; }
        public byte Defense { get; set; }
        public byte MAttackPercent { get; set; }
        public byte MDef { get; set; }
        public short MP { get; set; }
        public short AP { get; set; }
        public short? MorphIntoItemID { get; set; }
        public float BackDamageMultiplier { get; set; }
        public int HP { get; set; }
        public int Exp { get; set; }
        public int Gil { get; set; }
        public Statuses AllowedStatuses { get; set; }
        public List<(Element, ElementRate)> ElementResistances { get; } = new();
        public List<(Statuses, ElementRate)> StatusResistances { get; } = new();
        public List<EnemyItem> DropItems { get; } = new();
        public List<EnemyItem> StealItems { get; } = new();
        public List<EnemyAction> Actions { get; } = new();
        public byte[] AI { get; set; }

        public void Load(Stream s) {
            Name = Text.Convert(s.ReadBytes(32).Where(b => b != 0xff).ToArray(), 0).Trim();
            Level = (byte)s.ReadByte();
            Dexterity = (byte)s.ReadByte();
            Luck = (byte)s.ReadByte();
            DefPercent = (byte)s.ReadByte();
            Attack = (byte)s.ReadByte();
            Defense = (byte)s.ReadByte();
            MAttackPercent = (byte)s.ReadByte();
            MDef = (byte)s.ReadByte();

            byte[] elements = s.ReadBytes(8), rates = s.ReadBytes(8);

            foreach(int e in Enumerable.Range(0, 8)) {
                if ((elements[e] != 0xff) && (rates[e] != 0xff)) {
                    if (elements[e] < 0x10)
                        ElementResistances.Add(((Element)elements[e], (ElementRate)rates[e]));
                    else if (elements[e] < 0x40)
                        StatusResistances.Add(((Statuses)(1 << (elements[e] - 0x20)) , (ElementRate)rates[e]));
                }
            }

            Actions.AddRange(
                Enumerable.Range(0, 16)
                .Select(_ => new EnemyAction {
                    AnimationIndex = (byte)s.ReadByte(),
                })
            );
            foreach (int a in Enumerable.Range(0, 16))
                Actions[a].ActionID = s.ReadI16();
            foreach (int a in Enumerable.Range(0, 16)) {
                Actions[a].CameraSingleTarget = (byte)s.ReadByte();
                Actions[a].CameraMultiTarget = (byte)s.ReadByte();
            }

            var itemrates = s.ReadBytes(4);
            var itemids = Enumerable.Range(0, 4).Select(_ => s.ReadI16()).ToArray();

            foreach(int i in Enumerable.Range(0, 4)) {
                if (itemids[i] != -1) {
                    if (itemrates[i] < 0x80)
                        DropItems.Add(new EnemyItem {
                            Chance = itemrates[i],
                            ItemID = itemids[i],
                        });
                    else
                        StealItems.Add(new EnemyItem {
                            Chance = (byte)(itemrates[i] - 0x80),
                            ItemID = itemids[i],
                        });
                }
            }

            foreach(int _ in Enumerable.Range(0, 3)) {
                short mid = s.ReadI16();
                if (mid != -1) {
                    var action = Actions.FirstOrDefault(a => a.ActionID == mid);
                    if (action != null) //TODO - OK? :/
                        action.AvailableViaManipulate = true; 
                }
            }

            s.ReadI16();
            MP = s.ReadI16();
            AP = s.ReadI16();
            MorphIntoItemID = Util.ValueOrNull(s.ReadI16(), (short)-1);
            BackDamageMultiplier = s.ReadByte() / 8f;
            s.ReadByte();
            HP = s.ReadI32();
            Exp = s.ReadI32();
            Gil = s.ReadI32();
            AllowedStatuses = (Statuses)s.ReadI32();
            s.ReadI32();
        }
    }

    public class BattleCamera {
        public short X { get; set; }
        public short Y { get; set; }
        public short Z { get; set; }
        public short LookAtX { get; set; }
        public short LookAtY { get; set; }
        public short LookAtZ { get; set; }

        public BattleCamera(Stream s) {
            X = s.ReadI16();
            Y = s.ReadI16();
            Z = s.ReadI16();
            LookAtX = s.ReadI16();
            LookAtY = s.ReadI16();
            LookAtZ = s.ReadI16();
        }

        public void Save(Stream s) {
            s.WriteI16(X);
            s.WriteI16(Y);
            s.WriteI16(Z);
            s.WriteI16(LookAtX);
            s.WriteI16(LookAtY);
            s.WriteI16(LookAtZ);
        }
    }

    public static class SceneDecoder {

        public static IEnumerable<BattleScene> Decode(Stream s) {
            int sceneCount = 0;
            foreach(int block in Enumerable.Range(0, 256)) {
                s.Position = block * 0x2000;
                int[] offsets = Enumerable.Range(0, 16)
                    .Select(_ => s.ReadI32())
                    .ToArray();

                foreach(int i in Enumerable.Range(0, 16)) {
                    if (offsets[i] < 0)
                        break;

                    s.Position = block * 0x2000 + offsets[i] * 4;
                    int next;
                    if (i == 15)
                        next = 0x2000;
                    else
                        next = Math.Min(offsets[i + 1] * 4, 0x2000);
                    if (next < 0)
                        next = 0x2000;

                    byte[] data = new byte[next - offsets[i] * 4];
                    s.Read(data, 0, data.Length);
                    if (data.Length == 0)
                        continue;
                    if ((data[0] == 0) && (data[1] == 0))
                        continue;

                    var ms = new MemoryStream();
                    new GZipStream(new MemoryStream(data), CompressionMode.Decompress).CopyTo(ms);
                    ms.Position = 0;

                    Enemy[] enemies = Enumerable.Range(0, 3)
                        .Select(_ => new Enemy {
                            ID = ms.ReadI16(),
                        })
                        .ToArray();

                    ms.Position = 0x58;
                    BattleCamera[] cameras = Enumerable.Range(0, 4)
                        .Select(_ => new BattleCamera(ms))
                        .Take(3)
                        .ToArray();

                    ms.Position = 0x8;
                    BattleScene[] scenes = Enumerable.Range(0, 4)
                        .Select(i => new BattleScene(ms, cameras) {
                            FormationID = (sceneCount << 2) | i
                        })
                        .ToArray();
                    sceneCount++;

                    ms.Position = 0x118;
                    foreach(int f in Enumerable.Range(0, 4)) {
                        scenes[f].Enemies.AddRange(
                            Enumerable.Range(0, 6)
                            .Select(_ => new EnemyInstance(ms, enemies))
                            .Where(ei => ei.Enemy != null)
                        );                        
                    }

                    ms.Position = 0x298;
                    foreach (var enemy in enemies)
                        enemy.Load(ms);

                    ms.Position = 0x4c0;
                    Attack[] attacks = Enumerable.Range(0, 32)
                        .Select(_ => new Attack(ms))
                        .ToArray();
                    foreach (int a in Enumerable.Range(0, 32))
                        attacks[a].ActionID = ms.ReadI16();
                    byte[] aname = new byte[32];
                    foreach (int a in Enumerable.Range(0, 32)) {
                        ms.Read(aname, 0, 32);
                        attacks[a].Name = Text.Convert(aname.TakeWhile(b => b != 0xff).ToArray(), 0);
                    }

                    foreach (var enemy in enemies)
                        foreach (var action in enemy.Actions)
                            action.Attack = attacks.FirstOrDefault(a => a.ActionID == action.ActionID);

                    ms.Position = 0xC80;
                    short[] aiOffsets = Enumerable.Range(0, 4)
                        .Select(_ => ms.ReadI16())
                        .ToArray();

                    foreach (int b in Enumerable.Range(0, 4)) {
                        if (aiOffsets[b] < 0)
                            continue;
                        ms.Position = aiOffsets[b]; //VERIFY offset
                        byte[] ai = new byte[aiOffsets.ElementAtOrDefault(b + 1) <= 0 ? 0xE80 - ms.Position : aiOffsets[b + 1] - ms.Position];
                        ms.Read(ai, 0, ai.Length);
                        scenes[b].FormationAI = ai;
                    }

                    ms.Position = 0xE80;
                    aiOffsets = Enumerable.Range(0, 3)
                        .Select(_ => ms.ReadI16())
                        .ToArray();

                    foreach(int e in Enumerable.Range(0, 3)) {
                        if (aiOffsets[e] <= 0)
                            continue;
                        ms.Position = 0xE80 + aiOffsets[e];
                        next = aiOffsets
                            .Skip(e + 1)
                            .Where(os => os > 0)
                            .Select(os => os + 0xE80)
                            .FirstOrDefault((short)ms.Length);
                        byte[] ai = new byte[next - ms.Position];
                        ms.Read(ai, 0, ai.Length);
                        enemies[e].AI = ai;
                    }

                    foreach (var scene in scenes)
                        yield return scene;
                }

            }

        }

        public static string ModelIDToFileName(int modelID) {
            //0 = AA
            char first = (char)('A' + (modelID / 26)),
                second = (char)('A' + (modelID % 26));
            return $"{first}{second}";
        }

        public static string LocationIDToFileName(int locationID) {
            //0 = OG
            char first = (char)('A' + ((locationID + 6) / 26) + 14),
                second = (char)('A' + ((locationID + 6) % 26));
            return $"{first}{second}";
        }
    }
}
