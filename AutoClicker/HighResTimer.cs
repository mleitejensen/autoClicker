using System;
using System.Diagnostics;
using System.Threading;

namespace AutoClicker;

/// <summary>
/// High-resolution repeating timer that runs on a dedicated background thread.
/// Achieves sub-millisecond precision without the ~15ms jitter of WinForms.Timer.
/// Uses SpinWait / short Sleep hybrid to maintain high accuracy without pegged CPU on long delays.
/// </summary>
public sealed class HighResTimer : IDisposable
{
    private readonly object _sync = new();
    private Thread? _thread;
    private CancellationTokenSource? _cts;
    private int _intervalMs = 100;
    private Func<int>? _intervalProvider;
    private Action? _tickCallback;
    private bool _disposed;

    public int IntervalMs
    {
        get => Volatile.Read(ref _intervalMs);
        set => Volatile.Write(ref _intervalMs, Math.Max(1, value));
    }

    public void Start(Action tickCallback, Func<int>? intervalProvider = null)
    {
        lock (_sync)
        {
            Stop();
            _tickCallback = tickCallback ?? throw new ArgumentNullException(nameof(tickCallback));
            _intervalProvider = intervalProvider;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _thread = new Thread(() => RunLoop(token))
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "AutoClicker.HighResTimer"
            };
            _thread.Start();
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_cts is not null)
            {
                try
                {
                    _cts.Cancel();
                    _thread?.Join(200);
                }
                catch
                {
                    // ignore
                }
                finally
                {
                    _cts.Dispose();
                    _cts = null;
                    _thread = null;
                }
            }
        }
    }

    private void RunLoop(CancellationToken token)
    {
        long freq = Stopwatch.Frequency;
        double ticksPerMs = freq / 1000.0;

        while (!token.IsCancellationRequested)
        {
            int currentInterval = _intervalProvider?.Invoke() ?? IntervalMs;
            if (currentInterval < 1) currentInterval = 1;

            long start = Stopwatch.GetTimestamp();
            long targetTicks = start + (long)(currentInterval * ticksPerMs);

            // Wait until target
            while (true)
            {
                if (token.IsCancellationRequested) return;
                long now = Stopwatch.GetTimestamp();
                long remainingTicks = targetTicks - now;
                if (remainingTicks <= 0) break;

                double remainingMs = remainingTicks / ticksPerMs;
                if (remainingMs > 3.0)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    Thread.SpinWait(50);
                }
            }

            if (token.IsCancellationRequested) return;

            try
            {
                _tickCallback?.Invoke();
            }
            catch
            {
                // Never let a callback crash the timer loop
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
