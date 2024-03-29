﻿// This program and the accompanying materials are made available under the terms of the
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
    public enum Element {
        None = 0,
        Fire,
        Ice,
        Lightning,
        Earth,
        Poison,
        Gravity,
        Water,
        Wind,
        Holy,
        Restore,
        Cut,
        Hit,
        Punch,
        Shoot,
        Shout,
    }

    [Flags]
    public enum Elements {
        None = 0,
        Fire = 0x1,
        Ice = 0x2,
        Lightning = 0x4,
        Earth = 0x8,
        Poison = 0x10,
        Gravity = 0x20,
        Water = 0x40,
        Wind = 0x80,
        Holy = 0x100,
        Restore = 0x200,
        Cut = 0x400,
        Hit = 0x800,
        Punch = 0x1000,
        Shoot = 0x2000,
        Shout = 0x4000,
        HiddenUltima = 0x8000
    }

    public static class ElementUtil {
        public static IEnumerable<Element> Split(this Elements elements) {
            foreach (Element element in Enum.GetValues<Element>()) {
                if (element != 0) {
                    Elements test = (Elements)(1 << ((int)element - 1));
                    if ((elements & test) != 0)
                        yield return element;
                }
            }
        }
    }

    [Flags]
    public enum Statuses : uint {
        None = 0,
        Death = 0x1,
        NearDeath = 0x2,
        Sleep = 0x4,
        Poison = 0x8,
        Sadness = 0x10,
        Fury = 0x20,
        Confusion = 0x40,
        Silence = 0x80,
        Haste = 0x100,
        Slow = 0x200,
        Stop = 0x400,
        Frog = 0x800,
        Small = 0x1000,
        SlowNumb = 0x2000,
        Petrify = 0x4000,
        Regen = 0x8000,
        Barrier = 0x10000,
        MBarrier = 0x20000,
        Reflect = 0x40000,
        Dual = 0x80000,
        Shield = 0x100000,
        DeathSentence = 0x200000,
        Manipulate = 0x400000,
        Berserk = 0x800000,
        Peerless = 0x1000000,
        Paralysed = 0x2000000,
        Darkness = 0x4000000,
        Seizure = 0x8000000,
        DeathForce = 0x10000000,
        Resist = 0x20000000,
        LuckyGirl = 0x40000000,
        Imprisoned = 0x80000000,
    }
}
