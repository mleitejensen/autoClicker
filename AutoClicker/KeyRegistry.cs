using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AutoClicker;

/// <summary>
/// Registry of all supported keys for keyboard simulation and global hotkey bindings.
/// </summary>
public static class KeyRegistry
{
    // Full map for simulation
    public static readonly Dictionary<string, Keys> SimulationKeys = new()
    {
        // Common
        ["Space"]     = Keys.Space,
        ["Enter"]     = Keys.Enter,
        ["Tab"]       = Keys.Tab,
        ["Escape"]    = Keys.Escape,
        ["Backspace"] = Keys.Back,
        ["Delete"]    = Keys.Delete,
        // Modifiers (useful for hold-to-charge actions)
        ["Shift"]     = Keys.ShiftKey,
        ["Ctrl"]      = Keys.ControlKey,
        ["Alt"]       = Keys.Menu,
        // Arrows
        ["Left"]      = Keys.Left,
        ["Right"]     = Keys.Right,
        ["Up"]        = Keys.Up,
        ["Down"]      = Keys.Down,
        // Navigation
        ["Home"]      = Keys.Home,
        ["End"]       = Keys.End,
        ["PageUp"]    = Keys.PageUp,
        ["PageDown"]  = Keys.PageDown,
        ["Insert"]    = Keys.Insert,
        // Letters A-Z
        ["A"] = Keys.A, ["B"] = Keys.B, ["C"] = Keys.C, ["D"] = Keys.D,
        ["E"] = Keys.E, ["F"] = Keys.F, ["G"] = Keys.G, ["H"] = Keys.H,
        ["I"] = Keys.I, ["J"] = Keys.J, ["K"] = Keys.K, ["L"] = Keys.L,
        ["M"] = Keys.M, ["N"] = Keys.N, ["O"] = Keys.O, ["P"] = Keys.P,
        ["Q"] = Keys.Q, ["R"] = Keys.R, ["S"] = Keys.S, ["T"] = Keys.T,
        ["U"] = Keys.U, ["V"] = Keys.V, ["W"] = Keys.W, ["X"] = Keys.X,
        ["Y"] = Keys.Y, ["Z"] = Keys.Z,
        // Digits
        ["0"] = Keys.D0, ["1"] = Keys.D1, ["2"] = Keys.D2, ["3"] = Keys.D3,
        ["4"] = Keys.D4, ["5"] = Keys.D5, ["6"] = Keys.D6, ["7"] = Keys.D7,
        ["8"] = Keys.D8, ["9"] = Keys.D9,
        // Function keys
        ["F1"]  = Keys.F1,  ["F2"]  = Keys.F2,  ["F3"]  = Keys.F3,  ["F4"]  = Keys.F4,
        ["F5"]  = Keys.F5,  ["F6"]  = Keys.F6,  ["F7"]  = Keys.F7,  ["F8"]  = Keys.F8,
        ["F9"]  = Keys.F9,  ["F10"] = Keys.F10, ["F11"] = Keys.F11, ["F12"] = Keys.F12,
    };

    // Keys that make sense as global hotkeys
    public static readonly string[] HotkeyChoices = new[]
    {
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "NumPad0", "NumPad1", "NumPad2", "NumPad3", "NumPad4",
        "NumPad5", "NumPad6", "NumPad7", "NumPad8", "NumPad9",
        "Home", "End", "Insert", "Delete", "PageUp", "PageDown",
        "Pause", "ScrollLock"
    };

    public static readonly Dictionary<string, Keys> HotkeyKeys = new()
    {
        ["F1"] = Keys.F1, ["F2"] = Keys.F2, ["F3"] = Keys.F3, ["F4"] = Keys.F4,
        ["F5"] = Keys.F5, ["F6"] = Keys.F6, ["F7"] = Keys.F7, ["F8"] = Keys.F8,
        ["F9"] = Keys.F9, ["F10"] = Keys.F10, ["F11"] = Keys.F11, ["F12"] = Keys.F12,
        ["NumPad0"] = Keys.NumPad0, ["NumPad1"] = Keys.NumPad1, ["NumPad2"] = Keys.NumPad2,
        ["NumPad3"] = Keys.NumPad3, ["NumPad4"] = Keys.NumPad4, ["NumPad5"] = Keys.NumPad5,
        ["NumPad6"] = Keys.NumPad6, ["NumPad7"] = Keys.NumPad7, ["NumPad8"] = Keys.NumPad8,
        ["NumPad9"] = Keys.NumPad9,
        ["Home"] = Keys.Home, ["End"] = Keys.End, ["Insert"] = Keys.Insert, ["Delete"] = Keys.Delete,
        ["PageUp"] = Keys.PageUp, ["PageDown"] = Keys.PageDown,
        ["Pause"] = Keys.Pause, ["ScrollLock"] = Keys.Scroll,
    };
}
