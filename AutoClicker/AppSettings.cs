using System;
using System.Text.Json.Serialization;

namespace AutoClicker;

public sealed class AppSettings
{
    public bool KeyboardMode { get; set; }
    public string MouseButton { get; set; } = "Left";
    public string MouseClickType { get; set; } = "Single"; // "Single" or "Double"
    public string Key { get; set; } = "Space";
    public bool KeyCtrl { get; set; }
    public bool KeyShift { get; set; }
    public bool KeyAlt { get; set; }

    public int Interval { get; set; } = 100;
    public bool Random { get; set; }
    public int MinInterval { get; set; } = 100;
    public int MaxInterval { get; set; } = 300;

    public int StartDelay { get; set; }
    public bool Hold { get; set; }
    public bool LimitEnabled { get; set; }
    public int LimitValue { get; set; } = 100;
    public string LimitUnit { get; set; } = "clicks";

    public string HotkeyStartKey { get; set; } = "F6";
    public string HotkeyStopKey { get; set; } = "F7";

    public string ClickLocationMode { get; set; } = "Cursor"; // "Cursor" or "Fixed"
    public int FixedX { get; set; }
    public int FixedY { get; set; }
    public bool PositionJitter { get; set; }
    public int JitterPixels { get; set; } = 5;

    public bool AlwaysOnTop { get; set; }
    public bool PauseOnMouseMove { get; set; }
    public bool CloseToTray { get; set; } = true;

    // Macro / TinyTask settings
    public string ActiveTab { get; set; } = "AutoClicker"; // "AutoClicker" or "Macro"
    public string MacroSpeed { get; set; } = "1.0x";
    public string MacroRepeat { get; set; } = "1x";
    public bool MacroRecordMouseMove { get; set; } = true;
    public bool MacroPauseOnMouseMove { get; set; } = true;
    public string HotkeyRecordKey { get; set; } = "F8";
    public string HotkeyPlayKey { get; set; } = "F9";
    public string? LastMacroPath { get; set; }

    // Screen conditions (energy / health bars)
    public System.Collections.Generic.List<ScreenCondition> ScreenConditions { get; set; } = new();
    public int ScreenTolerance { get; set; } = 40;
    public bool ScreenWaitUntilMet { get; set; } = true;

    // Routine (multi-step flow)
    public System.Collections.Generic.List<RoutineStep> RoutineSteps { get; set; } = new();
    public int RoutineRepeat { get; set; }
}
