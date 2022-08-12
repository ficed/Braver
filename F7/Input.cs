﻿using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        PanLeft,
        PanRight,

        Debug1,
        Debug2,
        Debug3,
        Debug4,
        Debug5,

        DebugSpeed,
    }

    public class InputState {
        public Dictionary<InputKey, int> DownFor { get; } = new();

        public Vector2 Stick1 { get; set; }

        public bool IsDown(InputKey k) => DownFor[k] > 0;
        public bool IsJustDown(InputKey k) => DownFor[k] == 1;
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
