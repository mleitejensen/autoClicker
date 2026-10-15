using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutoClicker;

public enum RoutineStepType
{
    WaitUntil,
    RunMacro,
    HoldKeyUntil,
    PressKey,
    ClickAt,
    Delay,
    JumpIf,
    ReadKeys,
    TimingGame,
    RingGame,
}

/// <summary>One extra bar condition beyond a step's primary one (see <see cref="RoutineStep.ExtraConditions"/>).</summary>
public struct BarCondition
{
    public int Bar { get; set; }      // index into the screen-condition slots (0 = Energy, 1 = Health)
    public bool Below { get; set; }   // false: bar >= Percent, true: bar < Percent
    public int Percent { get; set; }
}

/// <summary>One step of a routine. Only the fields relevant to <see cref="Type"/> are used.</summary>
public sealed class RoutineStep
{
    public RoutineStepType Type { get; set; } = RoutineStepType.WaitUntil;

    // Primary condition (WaitUntil, HoldKeyUntil, JumpIf)
    public int Bar { get; set; }              // index into the screen-condition slots (0 = Energy, 1 = Health)
    public bool Below { get; set; }           // false: bar >= Percent, true: bar < Percent
    public int Percent { get; set; } = 50;

    /// <summary>Extra conditions beyond the primary one, e.g. so a step can react to both bars.</summary>
    public List<BarCondition> ExtraConditions { get; set; } = new();

    /// <summary>true: every condition (primary + extra) must hold; false: any single one is enough.</summary>
    public bool MatchAll { get; set; }

    public string Key { get; set; } = "Shift";        // HoldKeyUntil, PressKey
    public string MacroPath { get; set; } = "";       // RunMacro; blank = the macro currently loaded in the Macro tab
    public int X { get; set; }                        // ClickAt
    public int Y { get; set; }
    public string Button { get; set; } = "Left";
    public int Milliseconds { get; set; } = 1000;     // Delay length
    public int TimeoutSeconds { get; set; }           // WaitUntil / HoldKeyUntil; 0 = no timeout
    public int JumpTo { get; set; } = 1;              // JumpIf, 1-based step number
    public int BailPercent { get; set; }              // WaitUntil / HoldKeyUntil: leave early if Health is below this %; 0 = off
    public int BailJumpTo { get; set; }               // 1-based step to go to on bail-out or timeout; 0 = none

    // ReadKeys: a screen region showing key prompts (e.g. "S D"), and the glyph looks the user taught for it
    public int ReadX { get; set; }
    public int ReadY { get; set; }
    public int ReadWidth { get; set; }
    public int ReadHeight { get; set; }
    public List<GlyphTemplate> Glyphs { get; set; } = new();

    // ReadKeys: the letters' fill colour. Games usually outline their letters, so reading only this colour gives clean glyphs.
    public bool HasReadColor { get; set; }
    public int ReadColorArgb { get; set; }

    // ReadKeys: the prompt shows arrow badges (pressed ones turn green) instead of letters
    public bool ReadArrows { get; set; }
    public int PressGapMs { get; set; } = 150;   // pause between key presses, so the game registers each one

    [System.Text.Json.Serialization.JsonIgnore]
    public Color? ReadLetterColor => HasReadColor ? Color.FromArgb(ReadColorArgb) : null;
    // TimingGame: a strip with a moving marker and a target zone; press Key whenever the marker is on the target
    public int ZoneX { get; set; }
    public int ZoneY { get; set; }
    public int ZoneWidth { get; set; }
    public int ZoneHeight { get; set; }
    public List<int> TargetColorsArgb { get; set; } = new();     // every shade the target zone can have
    public int MarkerColorArgb { get; set; } = Color.White.ToArgb();
    public int HitCount { get; set; }                            // stop after this many presses; 0 = until the target disappears

    // RingGame: balls with a ring that closes onto them; click each ball as its ring closes (uses the Zone region)
    public int BallRadiusPx { get; set; }                         // ball radius in pixels; 0 = measure it from the screen
    public List<int> RingColorsArgb { get; set; } = new();
    public int LeadMs { get; set; } = 25;                        // click this long before the ring closes, to cover input and frame delay (negative = later)

    [System.Text.Json.Serialization.JsonIgnore]
    public Rectangle ZoneRegion
    {
        get => new(ZoneX, ZoneY, ZoneWidth, ZoneHeight);
        set { ZoneX = value.X; ZoneY = value.Y; ZoneWidth = value.Width; ZoneHeight = value.Height; }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public Rectangle ReadRegion
    {
        get => new(ReadX, ReadY, ReadWidth, ReadHeight);
        set { ReadX = value.X; ReadY = value.Y; ReadWidth = value.Width; ReadHeight = value.Height; }
    }
    public static readonly string[] BarNames = { "Energy", "Health" };

    /// <summary>The primary condition followed by every extra one.</summary>
    public IEnumerable<BarCondition> AllConditions()
    {
        yield return new BarCondition { Bar = Bar, Below = Below, Percent = Percent };
        foreach (var c in ExtraConditions) yield return c;
    }

    private static string FormatCondition(BarCondition c) =>
        $"{BarNames[Math.Clamp(c.Bar, 0, BarNames.Length - 1)]} {(c.Below ? "<" : "≥")} {c.Percent}%";

    public string ConditionText() =>
        string.Join(MatchAll ? " AND " : " OR ", AllConditions().Select(FormatCondition));

    private string BailText() => BailJumpTo > 0 ? $" (else go to step {BailJumpTo})" : "";

    public string Describe() => Type switch
    {
        RoutineStepType.WaitUntil    => $"Wait until {ConditionText()}{BailText()}",
        RoutineStepType.RunMacro     => $"Run macro: {(string.IsNullOrWhiteSpace(MacroPath) ? "(current macro)" : System.IO.Path.GetFileNameWithoutExtension(MacroPath))}",
        RoutineStepType.HoldKeyUntil => $"Hold {Key} until {ConditionText()}{BailText()}",
        RoutineStepType.PressKey     => $"Press {Key}",
        RoutineStepType.ClickAt      => $"{Button} click at {X}, {Y}",
        RoutineStepType.Delay        => $"Wait {Milliseconds / 1000.0:0.##}s",
        RoutineStepType.JumpIf       => $"Go to step {JumpTo} if {ConditionText()}",
        RoutineStepType.ReadKeys     => ReadWidth > 0 ? $"Read {(ReadArrows ? "arrows" : "keys")} at {ReadX},{ReadY} ({ReadWidth}×{ReadHeight}) and press them" : $"Read {(ReadArrows ? "arrows" : "keys")} from screen (no region set)",
        RoutineStepType.TimingGame   => $"Timing game: press {Key} when the marker is on the target{(HitCount > 0 ? $" ({HitCount}x)" : "")}",
        RoutineStepType.RingGame     => $"Ring game: click balls as their ring closes{(HitCount > 0 ? $" ({HitCount}x)" : "")}",
        _ => Type.ToString(),
    };

    public RoutineStep Clone()
    {
        var clone = (RoutineStep)MemberwiseClone();
        clone.ExtraConditions = new List<BarCondition>(ExtraConditions);
        clone.Glyphs = Glyphs.Select(g => new GlyphTemplate { Key = g.Key, Bits = g.Bits, Aspect = g.Aspect }).ToList();
        clone.TargetColorsArgb = new List<int>(TargetColorsArgb);
        clone.RingColorsArgb = new List<int>(RingColorsArgb);
        return clone;
    }
}


/// <summary>Executes a list of <see cref="RoutineStep"/>s on a background thread.</summary>
public sealed class RoutineRunner : IDisposable
{
    private readonly MacroPlayer _player = new();
    private CancellationTokenSource? _cts;
    private Task? _task;

    public bool IsRunning { get; private set; }

    /// <summary>(zero-based step index, total steps, loop number, description)</summary>
    public event Action<int, int, int, string>? StepChanged;
    public event Action<bool, string?>? Finished;

    public void Start(
        IReadOnlyList<RoutineStep> steps,
        IReadOnlyList<ScreenCondition> bars,
        int tolerance,
        int repeatCount,
        Func<string, MacroSequence?> macroLoader)
    {
        if (IsRunning || steps.Count == 0) return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        IsRunning = true;
        var snapshot = new List<RoutineStep>(steps.Count);
        foreach (var s in steps) snapshot.Add(s.Clone());

        _task = Task.Run(() => Run(snapshot, bars, tolerance, repeatCount, macroLoader, token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _task?.Wait(500); } catch { /* cancellation */ }
    }

    private void Run(
        List<RoutineStep> steps,
        IReadOnlyList<ScreenCondition> bars,
        int tolerance,
        int repeatCount,
        Func<string, MacroSequence?> macroLoader,
        CancellationToken token)
    {
        string? error = null;
        bool clean = false;
        string? lastNote = null;     // what the last read-keys step pressed, kept visible in the status for a few seconds
        long lastNoteAt = 0;
        string RecentNote() => lastNote != null && Environment.TickCount64 - lastNoteAt < 8000 ? $" · LAST: {lastNote}" : "";
        NativeMethods.TimeBeginPeriod(1);

        try
        {
            int loop = 0;
            while (!token.IsCancellationRequested && (repeatCount <= 0 || loop < repeatCount))
            {
                loop++;
                int i = 0;
                while (i < steps.Count && !token.IsCancellationRequested)
                {
                    var step = steps[i];
                    StepChanged?.Invoke(i, steps.Count, loop, step.Describe() + RecentNote());

                    int next = i + 1;
                    switch (step.Type)
                    {
                        case RoutineStepType.WaitUntil:
                            {
                                var r = WaitFor(step, bars, tolerance, token, out error);
                                if (r == Outcome.Failed) return;
                                if (r == Outcome.Bail) next = Math.Clamp(step.BailJumpTo, 1, steps.Count) - 1;
                            }
                            break;

                        case RoutineStepType.HoldKeyUntil:
                            {
                                var r = HoldUntil(step, bars, tolerance, token, out error);
                                if (r == Outcome.Failed) return;
                                if (r == Outcome.Bail) next = Math.Clamp(step.BailJumpTo, 1, steps.Count) - 1;
                            }
                            break;

                        case RoutineStepType.RunMacro:
                            if (!RunMacro(step, macroLoader, token, out error)) return;
                            break;

                        case RoutineStepType.PressKey:
                            {
                                // Roblox samples input once per frame, so a very short tap can be missed
                                var release = InputSimulator.HoldKey(step.Key, false, false, false);
                                try { Sleep(120, token); } finally { release(); }
                                Sleep(60, token);
                            }
                            break;

                        case RoutineStepType.ClickAt:
                            NativeMethods.SetCursorPos(step.X, step.Y);
                            Sleep(80, token);
                            InputSimulator.ClickMouse(step.Button, "Single", "Fixed", step.X, step.Y, false, 0);
                            Sleep(100, token);
                            break;

                        case RoutineStepType.Delay:
                            Sleep(step.Milliseconds, token);
                            break;

                        case RoutineStepType.JumpIf:
                            if (Check(step, bars, tolerance, out error))
                            {
                                next = Math.Clamp(step.JumpTo, 1, steps.Count) - 1;
                                // Prevents a tight loop from hammering the screen capture
                                Sleep(100, token);
                            }
                            else if (error != null) return;
                            break;

                        case RoutineStepType.ReadKeys:
                            {
                                // Live status while reading, and the pressed keys stay in the status afterwards
                                void Say(string text) => StepChanged?.Invoke(i, steps.Count, loop, text);
                                void Pressed(string keys) { lastNote = $"pressed {keys}"; lastNoteAt = Environment.TickCount64; Say($"Read keys · pressed {keys}"); }
                                bool ok = step.ReadArrows
                                    ? ReadAndPressArrows(step, token, Say, Pressed, out error)
                                    : ReadAndPressKeys(step, token, Say, Pressed, out error);
                                if (!ok) return;
                            }
                            break;

                        case RoutineStepType.TimingGame:
                            if (!RunTimingGame(step, tolerance, token, text => StepChanged?.Invoke(i, steps.Count, loop, text), out error)) return;
                            break;

                        case RoutineStepType.RingGame:
                            if (!RunRingGame(step, token, text => StepChanged?.Invoke(i, steps.Count, loop, text), out error)) return;
                            break;
                    }

                    i = next;
                }

                if (!token.IsCancellationRequested) Sleep(100, token);
            }

            clean = !token.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            // Stopped by user
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            NativeMethods.TimeEndPeriod(1);
            IsRunning = false;
            Finished?.Invoke(clean, error);
        }
    }

    private enum Outcome { Met, Bail, Failed }

    /// <summary>
    /// Evaluates a step's condition. A "true" result must be confirmed by further reads so a
    /// single flickering frame can't trigger it. Sets <paramref name="error"/> when the bar isn't configured.
    /// </summary>
    private static bool Check(RoutineStep step, IReadOnlyList<ScreenCondition> bars, int tolerance, out string? error)
    {
        if (!CheckOnce(step, bars, tolerance, out error)) return false;
        for (int n = 0; n < 2; n++)
        {
            Thread.Sleep(60);
            if (!CheckOnce(step, bars, tolerance, out error)) return false;
        }
        return true;
    }

    /// <summary>
    /// Evaluates every condition on the step (primary + extra). With <see cref="RoutineStep.MatchAll"/>
    /// every condition's bar must be configured and currently met; otherwise any single met condition
    /// is enough, and unconfigured bars are simply skipped (unless none of them are configured at all).
    /// </summary>
    private static bool CheckOnce(RoutineStep step, IReadOnlyList<ScreenCondition> bars, int tolerance, out string? error)
    {
        error = null;
        bool anyConfigured = false;
        foreach (var c in step.AllConditions())
        {
            var bar = c.Bar >= 0 && c.Bar < bars.Count ? bars[c.Bar] : null;
            if (bar is null || !bar.IsConfigured)
            {
                if (step.MatchAll)
                {
                    error = $"{RoutineStep.BarNames[Math.Clamp(c.Bar, 0, RoutineStep.BarNames.Length - 1)]} bar is not set up";
                    return false;
                }
                continue;
            }
            anyConfigured = true;

            double level = bar.Measure(tolerance);
            if (level < 0)
            {
                error = "Screen capture failed";
                return false;
            }

            bool met = c.Below ? level < c.Percent : level >= c.Percent;
            if (step.MatchAll)
            {
                if (!met) return false;
            }
            else if (met)
            {
                return true;
            }
        }

        if (step.MatchAll) return true;
        if (!anyConfigured)
        {
            error = $"{RoutineStep.BarNames[Math.Clamp(step.Bar, 0, RoutineStep.BarNames.Length - 1)]} bar is not set up";
        }
        return false;
    }

    /// <summary>True when the step's optional "Health is low" escape has triggered.</summary>
    private static bool ShouldBail(RoutineStep step, IReadOnlyList<ScreenCondition> bars, int tolerance)
    {
        if (step.BailPercent <= 0 || step.BailJumpTo <= 0) return false;
        var probe = new RoutineStep { Bar = 1, Below = true, Percent = step.BailPercent };
        return Check(probe, bars, tolerance, out _);
    }

    private static Outcome WaitFor(RoutineStep step, IReadOnlyList<ScreenCondition> bars, int tolerance, CancellationToken token, out string? error)
    {
        error = null;
        long deadline = step.TimeoutSeconds > 0 ? Environment.TickCount64 + step.TimeoutSeconds * 1000L : long.MaxValue;
        while (!token.IsCancellationRequested)
        {
            if (Check(step, bars, tolerance, out error)) return Outcome.Met;
            if (error != null) return Outcome.Failed;
            if (ShouldBail(step, bars, tolerance)) return Outcome.Bail;
            if (Environment.TickCount64 >= deadline)
            {
                if (step.BailJumpTo > 0) return Outcome.Bail;
                error = $"Timed out waiting for {step.ConditionText()}";
                return Outcome.Failed;
            }
            Sleep(100, token);
        }
        return Outcome.Failed;
    }

    private static Outcome HoldUntil(RoutineStep step, IReadOnlyList<ScreenCondition> bars, int tolerance, CancellationToken token, out string? error)
    {
        error = null;
        if (Check(step, bars, tolerance, out error)) return Outcome.Met;
        if (error != null) return Outcome.Failed;

        var release = InputSimulator.HoldKey(step.Key, false, false, false);
        try
        {
            long deadline = step.TimeoutSeconds > 0 ? Environment.TickCount64 + step.TimeoutSeconds * 1000L : long.MaxValue;
            while (!token.IsCancellationRequested)
            {
                Sleep(50, token);
                if (Check(step, bars, tolerance, out error)) return Outcome.Met;
                if (error != null) return Outcome.Failed;
                if (ShouldBail(step, bars, tolerance)) return Outcome.Bail;
                if (Environment.TickCount64 >= deadline)
                {
                    // Running out of time isn't fatal: bail out if configured, otherwise carry on.
                    return step.BailJumpTo > 0 ? Outcome.Bail : Outcome.Met;
                }
            }
            return Outcome.Failed;
        }
        finally
        {
            release();
        }
    }
    /// <summary>
    /// Reads the step's region until a complete, stable set of key prompts is seen, then presses those keys
    /// left to right. Waits up to TimeoutSeconds (0 = a single attempt); if nothing readable shows up the
    /// routine simply carries on.
    /// </summary>
    private static bool ReadAndPressKeys(RoutineStep step, CancellationToken token, Action<string> say, Action<string> pressed, out string? error)
    {
        error = null;
        if (step.ReadWidth <= 0 || step.ReadHeight <= 0)
        {
            error = "Read-keys step has no screen region";
            return false;
        }

        // Troubleshooting trail in %TEMP%\reflex-keys\run: a log, plus pictures and per-glyph details of reads that failed
        string dir = KeysDir;
        void Log(string line) => KeysLog(line);
        KeysLogStart($"letters region={step.ReadRegion} taught={step.Glyphs.Count} letterColor={(step.HasReadColor ? "set" : "auto")}                 timeout={step.TimeoutSeconds}s gap={step.PressGapMs}ms");
        int failDumps = 0;
        long lastFailDump = 0;
        string? lastLogged = null;

        long deadline = Environment.TickCount64 + step.TimeoutSeconds * 1000L;
        string? previous = null;
        while (!token.IsCancellationRequested)
        {
            using var frame = ScreenKeyReader.Capture(step.ReadRegion);
            var read = ScreenKeyReader.Read(frame, step.Glyphs, step.ReadLetterColor);
            string current = read.IsComplete ? string.Join(" ", read.Keys) : "";

            string state = read.ToString();
            if (state != lastLogged) { Log($"read: {state}"); say($"Read keys · sees: {state}"); lastLogged = state; }

            if (!read.IsComplete && read.GlyphCount > 0 && failDumps < 8 && Environment.TickCount64 - lastFailDump > 700)
            {
                failDumps++;
                lastFailDump = Environment.TickCount64;
                string failDir = Path.Combine(dir, $"fail-{failDumps}");
                ScreenKeyReader.SaveDebug(frame, failDir, step.ReadLetterColor);
                try { File.WriteAllText(Path.Combine(failDir, "details.txt"), ScreenKeyReader.Diagnose(frame, step.Glyphs, step.ReadLetterColor)); } catch { }
                Log($"saved fail-{failDumps}");
            }

            // The same prompt must be read twice in a row so a mid-animation frame can't press the wrong keys
            if (current.Length > 0 && current == previous)
            {
                Log($"PRESS {current}");
                pressed(current);
                foreach (var key in read.Keys)
                {
                    var release = InputSimulator.HoldKey(key, false, false, false);
                    try { Sleep(120, token); } finally { release(); }
                    Sleep(Math.Max(step.PressGapMs, 0), token);
                }
                return true;
            }

            previous = current.Length > 0 ? current : null;
            long now = Environment.TickCount64;
            if (now >= deadline && (previous == null || now >= deadline + 2000)) return true;
            Sleep(80, token);
        }
        return false;
    }

    /// <summary>
    /// Arrow prompts: a row of badges where each pressed arrow turns green. The first badge that isn't green yet is
    /// the one to press; after each press the step waits for that badge to turn green before moving to the next, so a
    /// wrong or missed key is noticed instead of blindly typing the rest. Waits up to TimeoutSeconds for a prompt to
    /// appear (0 = try once) and carries on when the prompt is finished or stops progressing.
    /// </summary>
    private static bool ReadAndPressArrows(RoutineStep step, CancellationToken token, Action<string> say, Action<string> pressed, out string? error)
    {
        error = null;
        if (step.ReadWidth <= 0 || step.ReadHeight <= 0)
        {
            error = "Read-keys step has no screen region";
            return false;
        }

        string dir = KeysDir;
        void Log(string line) => KeysLog(line);
        KeysLogStart($"arrows region={step.ReadRegion} timeout={step.TimeoutSeconds}s gap={step.PressGapMs}ms");

        List<ScreenKeyReader.ArrowReading> Look()
        {
            using var frame = ScreenKeyReader.Capture(step.ReadRegion);
            return ScreenKeyReader.ReadArrows(frame);
        }

        long deadline = Environment.TickCount64 + step.TimeoutSeconds * 1000L;
        long lastProgress = Environment.TickCount64;
        long lastPressEnd = 0;
        int gap = Math.Max(step.PressGapMs, 0);   // grows when the game turns out to need more time between keys
        string? candidate = null, lastLogged = null;
        var sequence = new List<string>();   // what was pressed this prompt, for the status line
        int attemptsOnBadge = 0, lastBadge = -1, presses = 0, missDumps = 0;

        while (!token.IsCancellationRequested)
        {
            var arrows = Look();
            long now = Environment.TickCount64;

            string state = ScreenKeyReader.DescribeArrows(arrows);
            if (state != lastLogged) { Log($"read: {state}"); say($"Read arrows · {state}"); lastLogged = state; }

            int badge = arrows.FindIndex(a => !a.IsGreen);
            if (arrows.Count == 0 || badge < 0)
            {
                // Everything pressed (or nothing there): finished if we pressed something, otherwise keep waiting for a prompt
                if (presses > 0 && (badge < 0 && arrows.Count > 0 || now - lastProgress > 1500)) return true;
                if (presses == 0 && now >= deadline) return true;
                candidate = null;
                Sleep(40, token);
                continue;
            }

            if (presses > 0 && now - lastProgress > 4000) return true;   // stalled, e.g. the prompt failed and was cleared

            var current = arrows[badge];
            string? key = current.Key;
            string cand = key == null ? "" : $"{badge}:{key}";
            if (key == null || cand != candidate)
            {
                // Only press once the same arrow has been read twice in a row, so a mid-animation frame can't press the wrong key
                candidate = key == null ? null : cand;
                if (presses == 0 && now >= deadline && candidate == null) return true;
                Sleep(40, token);
                continue;
            }

            if (badge != lastBadge) { lastBadge = badge; attemptsOnBadge = 0; }
            if (++attemptsOnBadge > 3) { Log($"gave up on arrow {badge + 1} ({key})"); return true; }

            // Give the game a moment after the previous key: pressing right as a badge flips can be ignored
            long sinceLast = Environment.TickCount64 - lastPressEnd;
            if (lastPressEnd != 0 && sinceLast < gap) Sleep((int)(gap - sinceLast), token);

            Log($"PRESS {key} (arrow {badge + 1}/{arrows.Count}, attempt {attemptsOnBadge})");
            var release = InputSimulator.HoldKey(key, false, false, false);
            try { Sleep(120, token); } finally { release(); }
            lastPressEnd = Environment.TickCount64;
            presses++;
            sequence.Add(key);
            pressed(string.Join(" ", sequence));
            lastProgress = Environment.TickCount64;
            candidate = null;

            // Wait for the badge to turn green: that confirms the game accepted the key
            long confirmBy = Environment.TickCount64 + 700;
            bool confirmed = false;
            while (Environment.TickCount64 < confirmBy && !token.IsCancellationRequested)
            {
                Sleep(40, token);
                using var after = ScreenKeyReader.Capture(step.ReadRegion);
                var afterArrows = ScreenKeyReader.ReadArrows(after);
                if (afterArrows.Count > badge && afterArrows[badge].IsGreen) { confirmed = true; break; }
            }
            Log(confirmed ? $"arrow {badge + 1} turned green" : $"arrow {badge + 1} did NOT turn green");
            if (!confirmed)
            {
                // The key was probably pressed too soon after the last one: leave more time before every further press
                gap = Math.Min(gap + 150, 900);
                Log($"slowing down: pause between presses is now {gap}ms");
            }
            if (!confirmed && missDumps < 6)
            {
                missDumps++;
                using var missed = ScreenKeyReader.Capture(step.ReadRegion);
                ScreenKeyReader.SaveDebug(missed, Path.Combine(dir, $"miss-{DateTime.Now:HHmmss}-{missDumps}"), null, arrows: true);
            }
        }
        return false;
    }

    // Troubleshooting trail for the read-keys step, kept across runs (trimmed when it grows large)
    private static readonly string KeysDir = Path.Combine(Path.GetTempPath(), "reflex-keys", "run");

    private static void KeysLogStart(string header)
    {
        try
        {
            Directory.CreateDirectory(KeysDir);
            string path = Path.Combine(KeysDir, "log.txt");
            if (File.Exists(path) && new FileInfo(path).Length > 200_000) File.Delete(path);
            File.AppendAllText(path, $"{Environment.NewLine}=== {DateTime.Now:HH:mm:ss} {header}{Environment.NewLine}");
        }
        catch { /* diagnostics must never break the step */ }
    }

    private static void KeysLog(string line)
    {
        try { File.AppendAllText(Path.Combine(KeysDir, "log.txt"), $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}"); }
        catch { }
    }

    // The target vanishes briefly after every hit before the next one spawns, so only a longer absence means the minigame is over
    private const long TargetGoneMs = 3000;

    private sealed class RingTrack
    {
        public double X, Y;                               // ring (= ball) centre in screen pixels
        public double Radius;                             // the ball's radius; 0 until it is known
        public double MaxRing;                            // the largest ring radius seen: the ring only shrinks
        public double Thickness;                          // ring thickness in pixels
        public readonly List<double> RadiusEstimates = new();
        public readonly List<(long T, double R)> Samples = new();
        public long Born, LastSeen;
        public bool CursorThere;
    }

    // Where the ring should be, in ball radii, when the click is due: the ring's outer edge has reached the ball's edge
    private const double RingTargetRatio = 1.0;

    /// <summary>
    /// Ring minigame: balls appear with a ring that closes onto them, and each ball has to be clicked as the ring
    /// reaches it. Rings are found by colour (so any ball colour works) and the ball's size is read from the edge inside
    /// the ring (or taken from the step). The ring around each ball is measured many times a second and its closing
    /// speed estimated, so the click can be sent a little ahead of time to cover input and frame delay. Several balls
    /// can be tracked at once; whichever ring will close first is clicked first.
    /// </summary>
    private static bool RunRingGame(RoutineStep step, CancellationToken token, Action<string> report, out string? error)
    {
        error = null;
        if (step.ZoneWidth <= 0 || step.ZoneHeight <= 0) { error = "Ring step has no screen region"; return false; }
        if (step.RingColorsArgb.Count == 0) { error = "Ring step has no ring color"; return false; }

        var ringColors = step.RingColorsArgb.Select(Color.FromArgb).ToList();
        var region = step.ZoneRegion;
        var screen = System.Windows.Forms.SystemInformation.VirtualScreen;

        string logPath = Path.Combine(Path.GetTempPath(), "reflex-rings", "log.txt");
        void Log(string line) { try { File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}"); } catch { } }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            if (File.Exists(logPath) && new FileInfo(logPath).Length > 200_000) File.Delete(logPath);
            File.AppendAllText(logPath, $"{Environment.NewLine}=== {DateTime.Now:HH:mm:ss} ring game region={region} lead={step.LeadMs}ms ballRadius={(step.BallRadiusPx > 0 ? step.BallRadiusPx + "px" : "auto")} hits={step.HitCount} timeout={step.TimeoutSeconds}s{Environment.NewLine}");
        }
        catch { }

        long deadline = step.TimeoutSeconds > 0 ? Environment.TickCount64 + step.TimeoutSeconds * 1000L : long.MaxValue;
        var tracks = new List<RingTrack>();
        var recent = new List<(double X, double Y, long Until)>();   // balls just clicked: ignored while they fade out
        var clickedRadii = new List<double>();                        // ball radius at each click, to learn the (constant) ball size
        var afterClick = new List<(double X, double Y, double Radius, double MaxRing, long At)>();   // clicks to check a moment later
        int checkBudget = 20;                                                                          // how many clicks get that check (it is only a diagnostic)
        long lastScan = 0, lastSeenAny = 0, lastReport = 0;
        bool everSeen = false;
        int clicks = 0;

        // The ball's radius: fixed by the step, else the one learned from earlier balls (they are all the same size), else
        // the median of the edges measured inside this ball's ring so far
        double? LearnedRadius() => clickedRadii.Count >= 3 ? clickedRadii.OrderBy(v => v).ElementAt(clickedRadii.Count / 2) : null;

        void UpdateBallRadius(RingTrack t, Bitmap image, double cx, double cy, double ringInner)
        {
            if (step.BallRadiusPx > 0) { t.Radius = step.BallRadiusPx; return; }
            if (LearnedRadius() is { } learned) { t.Radius = learned; return; }
            if (t.RadiusEstimates.Count >= 8) return;
            if (RingGameReader.EstimateBallRadius(image, cx, cy, ringInner) is not { } est) return;

            // The ring starts well outside the ball, so an "edge" nearly as big as the ring is something else in the scene
            if (est < 12 || est > t.MaxRing * 0.85) return;

            t.RadiusEstimates.Add(est);
            t.Radius = t.RadiusEstimates.OrderBy(v => v).ElementAt(t.RadiusEstimates.Count / 2);
        }

        // Ring closing speed (px/ms) learned from earlier balls: the game's speed is constant, so it makes timing both
        // possible from a single reading (a ring found late) and far less noisy than a fit through a few readings
        var speeds = new List<double>();
        double? LearnedSpeed() => speeds.Count >= 3 ? speeds.OrderBy(v => v).ElementAt(speeds.Count / 2) : null;

        // Speed of one ball's ring from all its readings, or null when there are too few to trust
        double? MeasuredSpeed(RingTrack t)
        {
            if (t.Samples.Count < 5 || t.Samples[^1].T - t.Samples[0].T < 80) return null;
            double meanT = t.Samples.Average(s => (double)s.T), meanR = t.Samples.Average(s => s.R);
            double den = t.Samples.Sum(s => (s.T - meanT) * (s.T - meanT));
            if (den <= 0) return null;
            double speed = -t.Samples.Sum(s => (s.T - meanT) * (s.R - meanR)) / den;
            return speed is > 0.03 and < 1.0 ? speed : null;
        }

        // Milliseconds until the ring reaches the target size
        double? MsToHit(RingTrack t, long now)
        {
            if (t.Radius <= 0) return null;
            var recentSamples = t.Samples.Where(s => now - s.T <= 250).ToList();
            if (recentSamples.Count == 0) return null;
            double target = t.Radius * RingTargetRatio;

            if (LearnedSpeed() is { } speed)
            {
                // Known speed: every reading says where the ring is now; average them
                double ringNow = recentSamples.Average(s => s.R - speed * (now - s.T));
                return (ringNow - target) / speed;
            }

            // Speed not known yet: fit a straight line through the recent readings
            if (recentSamples.Count < 3 || recentSamples[^1].T - recentSamples[0].T < 25) return null;
            double meanT = recentSamples.Average(s => (double)s.T), meanR = recentSamples.Average(s => s.R);
            double num = recentSamples.Sum(s => (s.T - meanT) * (s.R - meanR));
            double den = recentSamples.Sum(s => (s.T - meanT) * (s.T - meanT));
            if (den <= 0) return null;

            double slope = num / den;                       // pixels per millisecond (negative while closing)
            if (slope > -0.02) return null;                  // not closing (or too slow to trust)
            double fitted = meanR + slope * (now - meanT);
            return (fitted - target) / -slope;
        }

        while (!token.IsCancellationRequested)
        {
            long now = Environment.TickCount64;
            if (now >= deadline) return true;

            // Look for new rings, unless one is about to close: a full scan is slow and could make us miss the click
            bool urgent = tracks.Any(t => MsToHit(t, now) is < 350);
            if (!urgent && now - lastScan >= 120)
            {
                lastScan = now;
                using var frame = ScreenKeyReader.Capture(region);
                recent.RemoveAll(r => r.Until < now);
                foreach (var ring in RingGameReader.FindRings(frame, ringColors))
                {
                    // Once the ball size is known, a ring that is already smaller than a ball can't be a real one
                    var knownRadius = step.BallRadiusPx > 0 ? step.BallRadiusPx : LearnedRadius();
                    if (knownRadius is { } kr && ring.OuterRadius < kr * 0.95) continue;

                    double sx = region.X + ring.X, sy = region.Y + ring.Y;
                    if (recent.Any(r => Math.Abs(r.X - sx) < ring.OuterRadius && Math.Abs(r.Y - sy) < ring.OuterRadius)) continue;

                    var known = tracks.FirstOrDefault(t => Math.Abs(t.X - sx) < Math.Max(15, t.MaxRing * 0.3) && Math.Abs(t.Y - sy) < Math.Max(15, t.MaxRing * 0.3));
                    if (known != null) { known.LastSeen = now; continue; }

                    var track = new RingTrack { X = sx, Y = sy, MaxRing = ring.OuterRadius, Thickness = ring.Thickness, Born = now, LastSeen = now };
                    UpdateBallRadius(track, frame, ring.X, ring.Y, ring.OuterRadius - ring.Thickness);
                    tracks.Add(track);
                    Log($"new ring at ({sx:F0},{sy:F0}) outer radius {ring.OuterRadius:F1}, thickness {ring.Thickness:F1}, ball radius {(track.Radius > 0 ? track.Radius.ToString("F1") : "unknown")}");
                }
            }

            // Measure the ring around every tracked ball
            foreach (var t in tracks)
            {
                int half = (int)Math.Min(t.MaxRing + 8, 450);
                var rect = Rectangle.Intersect(new Rectangle((int)(t.X - half), (int)(t.Y - half), half * 2, half * 2), screen);
                if (rect.Width < 10 || rect.Height < 10) continue;

                using var window = ScreenKeyReader.Capture(rect);
                double cx = t.X - rect.X, cy = t.Y - rect.Y;
                double start = t.Radius > 0 ? t.Radius + 2 : t.MaxRing * 0.35;
                var ring = RingGameReader.MeasureRing(window, cx, cy, start, Math.Min(half, Math.Min(rect.Width, rect.Height) / 2.0 - 1), ringColors);
                if (ring is not { } r) continue;

                t.Samples.Add((Environment.TickCount64, r.OuterRadius));
                if (t.Samples.Count > 16) t.Samples.RemoveAt(0);
                t.Thickness = r.Thickness;
                t.LastSeen = Environment.TickCount64;

                // While the ring is still well outside the ball its inside is clear, so measure the ball again
                if (step.BallRadiusPx <= 0 && (t.Radius <= 0 || r.OuterRadius - r.Thickness > t.Radius * 1.5))
                    UpdateBallRadius(t, window, cx, cy, r.OuterRadius - r.Thickness);
            }

            // Diagnostic: did the click register? The ball and its ring normally vanish right away, so a ring that is
            // still there a moment after the click suggests the click didn't count (visible in the log)
            foreach (var c in afterClick.Where(c => Environment.TickCount64 - c.At >= 150).ToList())
            {
                afterClick.Remove(c);
                int half = (int)Math.Min(c.MaxRing + 8, 450);
                var rect = Rectangle.Intersect(new Rectangle((int)(c.X - half), (int)(c.Y - half), half * 2, half * 2), screen);
                if (rect.Width < 10 || rect.Height < 10) continue;

                using var win = ScreenKeyReader.Capture(rect);
                var still = RingGameReader.MeasureRing(win, c.X - rect.X, c.Y - rect.Y, c.Radius + 2, Math.Min(half, Math.Min(rect.Width, rect.Height) / 2.0 - 1), ringColors);
                Log(still is { } s ? $"after click at ({c.X:F0},{c.Y:F0}): ring STILL THERE at {s.OuterRadius:F1}" : $"after click at ({c.X:F0},{c.Y:F0}): ring gone");
            }

            // Click the ball whose ring closes first once it is due; put the cursor on it beforehand (with real mouse
            // movement, which games like Roblox need before they notice the cursor has moved)
            now = Environment.TickCount64;
            RingTrack? due = null;
            double dueMs = double.MaxValue;
            RingTrack? soonest = null;
            double soonestMs = double.MaxValue;
            foreach (var t in tracks)
            {
                double? ms = MsToHit(t, now);
                if (ms is null) continue;
                if (ms < soonestMs) { soonest = t; soonestMs = ms.Value; }
                if (ms <= step.LeadMs && ms < dueMs && t.Samples[^1].R <= t.Radius * 1.4) { due = t; dueMs = ms.Value; }
            }
            if (soonest != null && soonestMs <= Math.Max(step.LeadMs, 0) + 150 && !soonest.CursorThere)
            {
                NativeMethods.MoveCursorForGame((int)soonest.X, (int)soonest.Y);
                soonest.CursorThere = true;
            }

            if (due != null)
            {
                if (!due.CursorThere) NativeMethods.MoveCursorForGame((int)due.X, (int)due.Y);
                var release = InputSimulator.HoldMouse(step.Button, "Current", (int)due.X, (int)due.Y, false, 0);
                try { Sleep(25, token); } finally { release(); }
                clicks++;
                if (step.BallRadiusPx <= 0 && due.Radius > 0) clickedRadii.Add(due.Radius);
                if (MeasuredSpeed(due) is { } measured) speeds.Add(measured);
                if (checkBudget > 0) { checkBudget--; afterClick.Add((due.X, due.Y, due.Radius, due.MaxRing, Environment.TickCount64)); }
                Log($"CLICK #{clicks} at ({due.X:F0},{due.Y:F0}) ball r={due.Radius:F1} ring={due.Samples[^1].R:F1} thickness={due.Thickness:F1} due in {dueMs:F0}ms (lead {step.LeadMs}ms)");
                tracks.Remove(due);
                recent.Add((due.X, due.Y, Environment.TickCount64 + 700));
                report($"Ring game · clicked {clicks}");
                if (step.HitCount > 0 && clicks >= step.HitCount) return true;
                continue;
            }

            now = Environment.TickCount64;
            foreach (var gone in tracks.Where(t => now - t.LastSeen > 600).ToList())
                Log($"dropped ring at ({gone.X:F0},{gone.Y:F0}) ball r={gone.Radius:F1} without a click: {gone.Samples.Count} ring readings, last ring {(gone.Samples.Count > 0 ? gone.Samples[^1].R.ToString("F1") : "none")}, age {now - gone.Born}ms");
            tracks.RemoveAll(t => now - t.LastSeen > 600);   // ball (and ring) gone: expired or already handled by the game
            if (tracks.Count > 0) { everSeen = true; lastSeenAny = now; }
            else if (everSeen && now - lastSeenAny > TargetGoneMs) return true;

            if (now - lastReport >= 250)
            {
                lastReport = now;
                report($"Ring game · {clicks} clicks · {tracks.Count} ball(s) in view{(urgent ? " · closing" : "")}");
            }

            Sleep(2, token);
        }
        return false;
    }

    /// <summary>
    /// Watches a strip with a moving marker and a target zone, pressing the key every time the marker is on the
    /// target. Ends after HitCount presses, once the target has vanished for a few seconds (the minigame is over), or
    /// at the timeout (0 = no limit); in all those cases the routine carries on.
    /// </summary>
    private static bool RunTimingGame(RoutineStep step, int tolerance, CancellationToken token, Action<string> report, out string? error)
    {
        error = null;
        if (step.ZoneWidth <= 0 || step.ZoneHeight <= 0) { error = "Timing step has no screen region"; return false; }
        if (step.TargetColorsArgb.Count == 0) { error = "Timing step has no target color"; return false; }

        var targets = step.TargetColorsArgb.Select(Color.FromArgb).ToList();
        var marker = Color.FromArgb(step.MarkerColorArgb);
        var region = step.ZoneRegion;

        long deadline = step.TimeoutSeconds > 0 ? Environment.TickCount64 + step.TimeoutSeconds * 1000L : long.MaxValue;
        long lastSeenTarget = 0, lastPress = 0, lastReport = 0;
        bool everSeen = false, armed = true;
        double pressedCenter = 0;
        int hits = 0;

        while (!token.IsCancellationRequested)
        {
            long now = Environment.TickCount64;
            if (now >= deadline) return true;

            TimingReading reading;
            using (var bmp = ScreenKeyReader.Capture(region))
                reading = TimingGameReader.Analyze(bmp, targets, marker, TimingGameReader.Tolerance);

            // Live feedback in the status bar, so it's visible whether the target/marker are being found
            if (now - lastReport >= 250)
            {
                lastReport = now;
                report($"Timing game · {hits} presses · {(reading.HasTarget ? "target found" : "NO TARGET")}, {(reading.HasMarker ? "marker found" : "NO MARKER")}{(reading.OnTarget ? " · ON TARGET" : "")}");
            }

            if (reading.HasTarget) { everSeen = true; lastSeenTarget = now; }
            else if (everSeen && now - lastSeenTarget > TargetGoneMs) return true;

            // After a press the target normally jumps elsewhere; stay disarmed until it has, so one pass can't press twice
            if (!armed)
            {
                bool moved = reading.HasTarget && Math.Abs(reading.TargetCenter - pressedCenter) > 8;
                if (now - lastPress > 1500 || (now - lastPress > 250 && (!reading.OnTarget || moved))) armed = true;
            }

            if (armed && reading.OnTarget)
            {
                // Games sample input per frame, so the key is held long enough to span a few frames (same as the Press key step)
                var release = InputSimulator.HoldKey(step.Key, false, false, false);
                try { Sleep(120, token); } finally { release(); }

                hits++;
                armed = false;
                lastPress = Environment.TickCount64;
                pressedCenter = reading.TargetCenter;
                report($"Timing game · pressed {step.Key} ({hits})");
                if (step.HitCount > 0 && hits >= step.HitCount) return true;
            }

            Sleep(4, token);
        }
        return false;
    }

    private bool RunMacro(RoutineStep step, Func<string, MacroSequence?> loader, CancellationToken token, out string? error)
    {
        error = null;
        var macro = loader(step.MacroPath);
        if (macro == null || macro.Actions.Count == 0)
        {
            error = "Macro step has no recorded macro";
            return false;
        }

        using var done = new ManualResetEventSlim(false);
        void OnFinished(bool ok, string? reason) => done.Set();

        _player.PlaybackFinished += OnFinished;
        try
        {
            _player.Play(macro, 1.0, 1, pauseOnMouseMove: false);
            while (!done.Wait(20))
            {
                if (token.IsCancellationRequested)
                {
                    _player.Stop();
                    return false;
                }
            }
            return !token.IsCancellationRequested;
        }
        finally
        {
            _player.PlaybackFinished -= OnFinished;
        }
    }

    private static void Sleep(int ms, CancellationToken token)
    {
        if (token.WaitHandle.WaitOne(ms)) throw new OperationCanceledException(token);
    }

    public void Dispose()
    {
        Stop();
        _player.Dispose();
    }
}
