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

    public static readonly string[] BarNames = { "Energy", "Health" };

    public string ConditionText() =>
        $"{BarNames[Math.Clamp(Bar, 0, BarNames.Length - 1)]} {(Below ? "<" : "≥")} {Percent}%";

    public string Describe() => Type switch
    {
        RoutineStepType.WaitUntil    => $"Wait until {ConditionText()}",
        RoutineStepType.RunMacro     => $"Run macro: {(string.IsNullOrWhiteSpace(MacroPath) ? "(current macro)" : System.IO.Path.GetFileNameWithoutExtension(MacroPath))}",
        RoutineStepType.HoldKeyUntil => $"Hold {Key} until {ConditionText()}",
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
                            if (!WaitFor(step, bars, tolerance, token, out error)) return;
                            break;

                        case RoutineStepType.HoldKeyUntil:
                            if (!HoldUntil(step, bars, tolerance, token, out error)) return;
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

    /// <summary>Evaluates a step's condition. Sets <paramref name="error"/> when the bar isn't configured.</summary>
    private static bool Check(RoutineStep step, IReadOnlyList<ScreenCondition> bars, int tolerance, out string? error)
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

    private static bool WaitFor(RoutineStep step, IReadOnlyList<ScreenCondition> bars, int tolerance, CancellationToken token, out string? error)
    {
        error = null;
        long deadline = step.TimeoutSeconds > 0 ? Environment.TickCount64 + step.TimeoutSeconds * 1000L : long.MaxValue;
        while (!token.IsCancellationRequested)
        {
            if (Check(step, bars, tolerance, out error)) return true;
            if (error != null) return false;
            if (Environment.TickCount64 >= deadline)
            {
                error = $"Timed out waiting for {step.ConditionText()}";
                return false;
            }
            Sleep(100, token);
        }
        return false;
    }

    private static bool HoldUntil(RoutineStep step, IReadOnlyList<ScreenCondition> bars, int tolerance, CancellationToken token, out string? error)
    {
        error = null;
        if (Check(step, bars, tolerance, out error)) return true;
        if (error != null) return false;

        var release = InputSimulator.HoldKey(step.Key, false, false, false);
        try
        {
            long deadline = step.TimeoutSeconds > 0 ? Environment.TickCount64 + step.TimeoutSeconds * 1000L : long.MaxValue;
            while (!token.IsCancellationRequested)
            {
                Sleep(50, token);
                if (Check(step, bars, tolerance, out error)) return true;
                if (error != null) return false;
                if (Environment.TickCount64 >= deadline)
                {
                    error = $"Timed out holding {step.Key} for {step.ConditionText()}";
                    return false;
                }
            }
            return false;
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
