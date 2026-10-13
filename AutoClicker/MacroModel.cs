using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AutoClicker;

public enum MacroActionType
{
    MouseMove,
    MouseDown,
    MouseUp,
    MouseWheel,
    KeyDown,
    KeyUp
}

public sealed class MacroAction
{
    public long TimestampMs { get; set; }
    public MacroActionType Type { get; set; }

    public int X { get; set; }
    public int Y { get; set; }

    public string? Button { get; set; }
    public int WheelDelta { get; set; }

    public ushort VkCode { get; set; }
    public ushort ScanCode { get; set; }
    public bool ExtendedKey { get; set; }
    public string? KeyName { get; set; }
}

public sealed class MacroSequence
{
    public string Name { get; set; } = "Untitled Macro";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public long TotalDurationMs { get; set; }
    public List<MacroAction> Actions { get; set; } = new();

    [JsonIgnore]
    public int ActionCount => Actions.Count;

    [JsonIgnore]
    public string Summary
    {
        get
        {
            if (Actions.Count == 0) return "Empty macro (0 actions)";
            int clicks = 0;
            int keys = 0;
            int moves = 0;
            foreach (var a in Actions)
            {
                if (a.Type == MacroActionType.MouseDown) clicks++;
                else if (a.Type == MacroActionType.KeyDown) keys++;
                else if (a.Type == MacroActionType.MouseMove) moves++;
            }
            double seconds = TotalDurationMs / 1000.0;
            return $"{Actions.Count} actions ({seconds:F1}s) · {clicks} clicks, {keys} keys, {moves} moves";
        }
    }
}
