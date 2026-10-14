using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace AutoClicker;

/// <summary>
/// Plays back recorded macros with high precision, speed multiplier support,
/// repeat looping, and safety pause on user mouse movement.
/// </summary>
public sealed class MacroPlayer : IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _playbackTask;
    private readonly object _lock = new();

    private readonly HashSet<(ushort scanCode, ushort vkCode, bool extended)> _heldKeys = new();
    private bool _leftDown;
    private bool _rightDown;
    private bool _middleDown;

    private Point _lastEmittedPoint;

    public bool IsPlaying { get; private set; }

    public event Action<int, int, int>? ProgressChanged;
    public event Action<bool, string?>? PlaybackFinished;
    public event Action<string?>? WaitingChanged;

    public void Play(
        MacroSequence macro,
        double speedMultiplier = 1.0,
        int repeatCount = 1,
        bool pauseOnMouseMove = true,
        ScreenGate? gate = null)
    {
        if (macro == null || macro.Actions.Count == 0) return;

        Stop();

        lock (_lock)
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsPlaying = true;

            _playbackTask = Task.Run(() => PlaybackLoop(macro, speedMultiplier, repeatCount, pauseOnMouseMove, gate, token), token);
        }
    }

    public void Stop(string? reason = null)
    {
        CancellationTokenSource? ctsToCancel = null;
        Task? taskToAwait = null;

        lock (_lock)
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            ctsToCancel = _cts;
            taskToAwait = _playbackTask;
            _cts = null;
            _playbackTask = null;
        }

        ctsToCancel?.Cancel();

        try
        {
            taskToAwait?.Wait(200);
        }
        catch
        {
            // Task canceled exception expected
        }
        finally
        {
            ReleaseHeld();
            ctsToCancel?.Dispose();
        }

        PlaybackFinished?.Invoke(false, reason ?? "Stopped");
    }

    private void PlaybackLoop(
        MacroSequence macro,
        double speedMultiplier,
        int repeatCount,
        bool pauseOnMouseMove,
        ScreenGate? gate,
        CancellationToken token)
    {
        if (speedMultiplier <= 0.001) speedMultiplier = 1.0;

        int currentLoop = 0;
        bool infinite = repeatCount <= 0;
        string? stopReason = null;
        bool completedCleanly = false;

        // Initialize mouse tracking point to current cursor position
        if (NativeMethods.GetCursorPos(out var initialPt))
            _lastEmittedPoint = new Point(initialPt.x, initialPt.y);

        NativeMethods.TimeBeginPeriod(1);

        try
        {
            double freq = Stopwatch.Frequency;
            long firstActionMs = macro.Actions[0].TimestampMs;
            long effectiveTotalDuration = Math.Max(macro.TotalDurationMs, macro.Actions[^1].TimestampMs);

            while (!token.IsCancellationRequested && (infinite || currentLoop < repeatCount))
            {
                currentLoop++;

                if (gate is { IsActive: true } && !PassGate(gate, token, out stopReason))
                {
                    return;
                }

                long loopStartTicks = Stopwatch.GetTimestamp();
                long lastProgressTicks = 0;

                for (int i = 0; i < macro.Actions.Count; i++)
                {
                    if (token.IsCancellationRequested) break;

                    var action = macro.Actions[i];

                    // Compute target tick on master timeline for zero-drift playback
                    double targetOffsetMs = (action.TimestampMs - firstActionMs) / speedMultiplier;
                    long targetTicks = loopStartTicks + (long)(targetOffsetMs * (freq / 1000.0));

                    // High-precision wait until targetTicks
                    if (!WaitUntilTicks(targetTicks, freq, pauseOnMouseMove, token, out stopReason))
                    {
                        return;
                    }

                    // Execute action
                    ExecuteAction(action);

                    // Throttle UI progress updates to ~20Hz or first/last
                    long now = Stopwatch.GetTimestamp();
                    if (i == 0 || i == macro.Actions.Count - 1 || (now - lastProgressTicks) >= (freq / 20.0))
                    {
                        lastProgressTicks = now;
                        ProgressChanged?.Invoke(i + 1, macro.Actions.Count, currentLoop);
                    }
                }

                // If repeating, wait for the tail of the recorded macro before starting the next loop
                if (!token.IsCancellationRequested && (infinite || currentLoop < repeatCount))
                {
                    double totalDurationScaledMs = (effectiveTotalDuration - firstActionMs) / speedMultiplier;
                    if (totalDurationScaledMs > 0)
                    {
                        long loopEndTicks = loopStartTicks + (long)(totalDurationScaledMs * (freq / 1000.0));
                        if (!WaitUntilTicks(loopEndTicks, freq, pauseOnMouseMove, token, out stopReason))
                        {
                            return;
                        }
                    }
                    else
                    {
                        long breathTicks = Stopwatch.GetTimestamp() + (long)(20.0 * (freq / 1000.0));
                        if (!WaitUntilTicks(breathTicks, freq, pauseOnMouseMove, token, out stopReason))
                        {
                            return;
                        }
                    }
                }
            }

            completedCleanly = !token.IsCancellationRequested && stopReason == null;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            stopReason = ex.Message;
        }
        finally
        {
            ReleaseHeld();
            NativeMethods.TimeEndPeriod(1);

            lock (_lock)
            {
                IsPlaying = false;
                _cts = null;
                _playbackTask = null;
            }

            PlaybackFinished?.Invoke(completedCleanly, stopReason);
        }
    }

    private void ExecuteAction(MacroAction action)
    {
        switch (action.Type)
        {
            case MacroActionType.MouseMove:
                NativeMethods.SetCursorPos(action.X, action.Y);
                _lastEmittedPoint = new Point(action.X, action.Y);
                break;

            case MacroActionType.MouseDown:
                NativeMethods.SetCursorPos(action.X, action.Y);
                _lastEmittedPoint = new Point(action.X, action.Y);

                uint downFlag = action.Button switch
                {
                    "Right"  => NativeMethods.MOUSEEVENTF_RIGHTDOWN,
                    "Middle" => NativeMethods.MOUSEEVENTF_MIDDLEDOWN,
                    _        => NativeMethods.MOUSEEVENTF_LEFTDOWN
                };

                if (action.Button == "Right") _rightDown = true;
                else if (action.Button == "Middle") _middleDown = true;
                else _leftDown = true;

                NativeMethods.SendMouseInput(downFlag);
                break;

            case MacroActionType.MouseUp:
                NativeMethods.SetCursorPos(action.X, action.Y);
                _lastEmittedPoint = new Point(action.X, action.Y);

                uint upFlag = action.Button switch
                {
                    "Right"  => NativeMethods.MOUSEEVENTF_RIGHTUP,
                    "Middle" => NativeMethods.MOUSEEVENTF_MIDDLEUP,
                    _        => NativeMethods.MOUSEEVENTF_LEFTUP
                };

                if (action.Button == "Right") _rightDown = false;
                else if (action.Button == "Middle") _middleDown = false;
                else _leftDown = false;

                NativeMethods.SendMouseInput(upFlag);
                break;

            case MacroActionType.MouseWheel:
                NativeMethods.SetCursorPos(action.X, action.Y);
                _lastEmittedPoint = new Point(action.X, action.Y);
                NativeMethods.SendMouseWheel(action.WheelDelta);
                break;

            case MacroActionType.KeyDown:
                uint keyFlags = 0;
                if (action.ScanCode != 0) keyFlags |= NativeMethods.KEYEVENTF_SCANCODE;
                if (action.ExtendedKey) keyFlags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;

                _heldKeys.Add((action.ScanCode, action.VkCode, action.ExtendedKey));
                NativeMethods.SendKeyInput(action.ScanCode, keyFlags, action.VkCode);
                break;

            case MacroActionType.KeyUp:
                uint upFlags = NativeMethods.KEYEVENTF_KEYUP;
                if (action.ScanCode != 0) upFlags |= NativeMethods.KEYEVENTF_SCANCODE;
                if (action.ExtendedKey) upFlags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;

                _heldKeys.Remove((action.ScanCode, action.VkCode, action.ExtendedKey));
                NativeMethods.SendKeyInput(action.ScanCode, upFlags, action.VkCode);
                break;
        }
    }

    private bool PassGate(ScreenGate gate, CancellationToken token, out string? stopReason)
    {
        stopReason = null;
        if (gate.Check(out var failing)) return true;

        if (!gate.WaitUntilMet)
        {
            stopReason = $"{failing} condition not met";
            return false;
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                WaitingChanged?.Invoke(failing);
                Thread.Sleep(100);
                if (gate.Check(out failing)) break;
            }
        }
        finally
        {
            WaitingChanged?.Invoke(null);
        }

        // The user may have moved the mouse while waiting, which must not count as interference
        if (NativeMethods.GetCursorPos(out var pt))
            _lastEmittedPoint = new Point(pt.x, pt.y);

        return !token.IsCancellationRequested;
    }

    private bool WaitUntilTicks(
        long targetTicks,
        double freq,
        bool pauseOnMouseMove,
        CancellationToken token,
        out string? stopReason)
    {
        stopReason = null;
        while (true)
        {
            if (token.IsCancellationRequested) return false;

            if (pauseOnMouseMove && NativeMethods.GetCursorPos(out var pt))
            {
                int dx = Math.Abs(pt.x - _lastEmittedPoint.X);
                int dy = Math.Abs(pt.y - _lastEmittedPoint.Y);
                if (dx > 25 || dy > 25)
                {
                    stopReason = "mouse moved";
                    return false;
                }
            }

            long now = Stopwatch.GetTimestamp();
            long remainingTicks = targetTicks - now;
            if (remainingTicks <= 0) return true;

            double remainingMs = remainingTicks * 1000.0 / freq;
            if (remainingMs > 15.0)
            {
                // Sleep in short slices (up to 15ms) to remain responsive to cancellation & mouse moves
                int sliceMs = Math.Min((int)(remainingMs - 10), 15);
                if (sliceMs > 0)
                {
                    Thread.Sleep(sliceMs);
                    continue;
                }
            }

            // Spin-wait the last <15ms with SpinWait for sub-millisecond precision
            var spinner = new SpinWait();
            while (Stopwatch.GetTimestamp() < targetTicks)
            {
                if (token.IsCancellationRequested) return false;
                spinner.SpinOnce();
            }
            return true;
        }
    }

    private void ReleaseHeld()
    {
        if (_leftDown)
        {
            NativeMethods.SendMouseInput(NativeMethods.MOUSEEVENTF_LEFTUP);
            _leftDown = false;
        }
        if (_rightDown)
        {
            NativeMethods.SendMouseInput(NativeMethods.MOUSEEVENTF_RIGHTUP);
            _rightDown = false;
        }
        if (_middleDown)
        {
            NativeMethods.SendMouseInput(NativeMethods.MOUSEEVENTF_MIDDLEUP);
            _middleDown = false;
        }

        foreach (var key in _heldKeys)
        {
            uint upFlags = NativeMethods.KEYEVENTF_KEYUP;
            if (key.scanCode != 0) upFlags |= NativeMethods.KEYEVENTF_SCANCODE;
            if (key.extended) upFlags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
            NativeMethods.SendKeyInput(key.scanCode, upFlags, key.vkCode);
        }
        _heldKeys.Clear();
    }

    public void Dispose()
    {
        Stop();
    }
}
