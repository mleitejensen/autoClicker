using System;
using System.Collections.Generic;
using System.Drawing;
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
}

/// <summary>One step of a routine. Only the fields relevant to <see cref="Type"/> are used.</summary>
public sealed class RoutineStep
{
    public RoutineStepType Type { get; set; } = RoutineStepType.WaitUntil;

    // Condition (WaitUntil, HoldKeyUntil, JumpIf)
    public int Bar { get; set; }              // index into the screen-condition slots (0 = Energy, 1 = Health)
    public bool Below { get; set; }           // false: bar >= Percent, true: bar < Percent
    public int Percent { get; set; } = 50;

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

    public static readonly string[] BarNames = { "Energy", "Health" };

    public string ConditionText() =>
        $"{BarNames[Math.Clamp(Bar, 0, BarNames.Length - 1)]} {(Below ? "<" : "≥")} {Percent}%";

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
        _ => Type.ToString(),
    };

    public RoutineStep Clone() => (RoutineStep)MemberwiseClone();
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
                    StepChanged?.Invoke(i, steps.Count, loop, step.Describe());

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
                            InputSimulator.PressKey(step.Key, false, false, false);
                            Sleep(60, token);
                            break;

                        case RoutineStepType.ClickAt:
                            InputSimulator.ClickMouse(step.Button, "Single", "Fixed", step.X, step.Y, false, 0);
                            Sleep(60, token);
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

    private static bool CheckOnce(RoutineStep step, IReadOnlyList<ScreenCondition> bars, int tolerance, out string? error)
    {
        error = null;
        var bar = step.Bar >= 0 && step.Bar < bars.Count ? bars[step.Bar] : null;
        if (bar is null || !bar.IsConfigured)
        {
            error = $"{RoutineStep.BarNames[Math.Clamp(step.Bar, 0, RoutineStep.BarNames.Length - 1)]} bar is not set up";
            return false;
        }

        double level = bar.Measure(tolerance);
        if (level < 0)
        {
            error = "Screen capture failed";
            return false;
        }
        return step.Below ? level < step.Percent : level >= step.Percent;
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
