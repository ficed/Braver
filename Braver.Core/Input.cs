﻿// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Braver {
    public enum InputKey {
        Up,
        Down,
        Left,
        Right,
        OK,
        Cancel,

        Start,
        Select,
        Menu,
        Pause,

        PanLeft,
        PanRight,

        Debug1,
        Debug2,
        Debug3,
        Debug4,
        Debug5,

        DebugOptions,
        DebugSpeed,
    }

    public class InputState {
        public const int REPEAT_DELAY = 60;
        public const int REPEAT_INTERVAL = 6;

        public Dictionary<InputKey, int> DownFor { get; } = new();

        //public Vector2 Stick1 { get; set; }

        public bool IsDown(InputKey k) => DownFor[k] > 0;
        public bool IsJustDown(InputKey k) => DownFor[k] == 1;
        public bool IsRepeating(InputKey k) {
            int down = DownFor[k];
            if (down == 1) return true;
            if (down > REPEAT_DELAY)
                return (down % REPEAT_INTERVAL) == 1;
            else 
                return false;  
        }
        public bool IsJustReleased(InputKey k) => DownFor[k] == -1;

        public bool IsAnyDirectionDown() {
            return IsDown(InputKey.Up) || IsDown(InputKey.Down) || IsDown(InputKey.Left) || IsDown(InputKey.Right);
        }

        public InputState() {
            foreach (InputKey k in Enum.GetValues<InputKey>())
                DownFor[k] = 0;
        }
    }
}
