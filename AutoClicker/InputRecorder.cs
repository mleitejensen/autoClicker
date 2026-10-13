using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoClicker;

/// <summary>
/// Records global mouse and keyboard inputs with high-precision relative timestamps
/// using low-level Windows hooks (WH_KEYBOARD_LL, WH_MOUSE_LL).
/// </summary>
public sealed class InputRecorder : IDisposable
{
    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook    = IntPtr.Zero;

    // Keep delegate references alive to prevent GC reclamation while hook is active
    private readonly NativeMethods.HookProc _keyboardProc;
    private readonly NativeMethods.HookProc _mouseProc;

    private readonly Stopwatch _stopwatch = new();
    private readonly List<MacroAction> _recordedActions = new();
    private readonly object _lock = new();

    private HashSet<Keys> _excludedKeys = new();
    private bool _recordMouseMove = true;

    private int _lastMoveX = -1;
    private int _lastMoveY = -1;
    private long _lastMoveTimestamp = -1;
    private int _buttonsDownCount;

    private readonly HashSet<ushort> _recordedDownKeys = new();
    private readonly HashSet<string> _recordedDownButtons = new();

    public bool IsRecording { get; private set; }

    public int CurrentActionCount
    {
        get
        {
            lock (_lock) return _recordedActions.Count;
        }
    }

    public long CurrentDurationMs => _stopwatch.ElapsedMilliseconds;

    public event Action<int, long>? ActionRecorded;

    public InputRecorder()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc    = MouseHookCallback;
    }

    public void Start(bool recordMouseMove = true, IEnumerable<Keys>? excludedKeys = null)
    {
        if (IsRecording) return;

        lock (_lock)
        {
            _recordedActions.Clear();
            _excludedKeys = excludedKeys != null ? new HashSet<Keys>(excludedKeys) : new HashSet<Keys>();
            _recordMouseMove = recordMouseMove;
            _lastMoveX = -1;
            _lastMoveY = -1;
            _lastMoveTimestamp = -1;
            _buttonsDownCount = 0;
            _recordedDownKeys.Clear();
            _recordedDownButtons.Clear();
        }

        using var process = Process.GetCurrentProcess();
        using var module  = process.MainModule;
        IntPtr hMod = NativeMethods.GetModuleHandle(module?.ModuleName);

        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
        _mouseHook    = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, hMod, 0);

        _stopwatch.Restart();
        IsRecording = true;
    }

    public MacroSequence Stop()
    {
        if (!IsRecording) return new MacroSequence();

        IsRecording = false;
        _stopwatch.Stop();
        long totalDuration = _stopwatch.ElapsedMilliseconds;

        Unhook();

        lock (_lock)
        {
            // Release any keys or buttons that were still held when recording stopped
            foreach (var btn in _recordedDownButtons)
            {
                _recordedActions.Add(new MacroAction
                {
                    TimestampMs = totalDuration,
                    Type        = MacroActionType.MouseUp,
                    X           = _lastMoveX >= 0 ? _lastMoveX : 0,
                    Y           = _lastMoveY >= 0 ? _lastMoveY : 0,
                    Button      = btn
                });
            }
            _recordedDownButtons.Clear();

            foreach (var vk in _recordedDownKeys)
            {
                ushort scan = (ushort)NativeMethods.MapVirtualKey(vk, NativeMethods.MAPVK_VK_TO_VSC);
                _recordedActions.Add(new MacroAction
                {
                    TimestampMs = totalDuration,
                    Type        = MacroActionType.KeyUp,
                    VkCode      = vk,
                    ScanCode    = scan,
                    KeyName     = ((Keys)vk).ToString()
                });
            }
            _recordedDownKeys.Clear();

            var seq = new MacroSequence
            {
                Name            = $"Macro {DateTime.Now:HH:mm:ss}",
                CreatedAt       = DateTime.UtcNow,
                TotalDurationMs = totalDuration,
                Actions         = new List<MacroAction>(_recordedActions)
            };
            return seq;
        }
    }

    private void Unhook()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsRecording)
        {
            var kb = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

            // Ignore injected inputs generated by our own simulator/player
            if (kb.dwExtraInfo == NativeMethods.InjectedSignature)
                return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

            var key = (Keys)kb.vkCode;

            // Discard hotkeys used to stop/toggle recording so they don't pollute the macro
            if (_excludedKeys.Contains(key))
                return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

            int msg = (int)wParam;
            bool isDown = msg is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
            bool isUp   = msg is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;

            if (isDown)
            {
                ushort scan = (ushort)kb.scanCode;
                if (scan == 0)
                    scan = (ushort)NativeMethods.MapVirtualKey(kb.vkCode, NativeMethods.MAPVK_VK_TO_VSC);

                bool extended = (kb.flags & 1) != 0 || key is Keys.Left or Keys.Right or Keys.Up or Keys.Down
                                                          or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown;

                _recordedDownKeys.Add((ushort)kb.vkCode);

                AddAction(new MacroAction
                {
                    TimestampMs = _stopwatch.ElapsedMilliseconds,
                    Type        = MacroActionType.KeyDown,
                    VkCode      = (ushort)kb.vkCode,
                    ScanCode    = scan,
                    ExtendedKey = extended,
                    KeyName     = key.ToString()
                });
            }
            else if (isUp)
            {
                // Discard orphaned KeyUp if KeyDown was not recorded in this session
                if (!_recordedDownKeys.Remove((ushort)kb.vkCode))
                    return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                ushort scan = (ushort)kb.scanCode;
                if (scan == 0)
                    scan = (ushort)NativeMethods.MapVirtualKey(kb.vkCode, NativeMethods.MAPVK_VK_TO_VSC);

                bool extended = (kb.flags & 1) != 0 || key is Keys.Left or Keys.Right or Keys.Up or Keys.Down
                                                          or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown;

                AddAction(new MacroAction
                {
                    TimestampMs = _stopwatch.ElapsedMilliseconds,
                    Type        = MacroActionType.KeyUp,
                    VkCode      = (ushort)kb.vkCode,
                    ScanCode    = scan,
                    ExtendedKey = extended,
                    KeyName     = key.ToString()
                });
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsRecording)
        {
            var ms = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

            // Ignore injected inputs
            if (ms.dwExtraInfo == NativeMethods.InjectedSignature)
                return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);

            int msg = (int)wParam;
            int x = ms.pt.x;
            int y = ms.pt.y;
            long now = _stopwatch.ElapsedMilliseconds;

            switch (msg)
            {
                case NativeMethods.WM_MOUSEMOVE:
                    if (_recordMouseMove)
                    {
                        int dx = Math.Abs(x - _lastMoveX);
                        int dy = Math.Abs(y - _lastMoveY);
                        long dt = now - _lastMoveTimestamp;

                        // Sample at ~125Hz (dt >= 8) or ~166Hz (dt >= 6) when dragging
                        int minDt = _buttonsDownCount > 0 ? 6 : 8;
                        if ((dx >= 2 || dy >= 2) && (dt >= minDt || _lastMoveTimestamp < 0))
                        {
                            _lastMoveX = x;
                            _lastMoveY = y;
                            _lastMoveTimestamp = now;

                            AddAction(new MacroAction
                            {
                                TimestampMs = now,
                                Type        = MacroActionType.MouseMove,
                                X           = x,
                                Y           = y
                            });
                        }
                    }
                    break;

                case NativeMethods.WM_LBUTTONDOWN:
                case NativeMethods.WM_RBUTTONDOWN:
                case NativeMethods.WM_MBUTTONDOWN:
                    string btnDown = msg == NativeMethods.WM_LBUTTONDOWN ? "Left"
                        : msg == NativeMethods.WM_RBUTTONDOWN ? "Right" : "Middle";

                    _recordedDownButtons.Add(btnDown);
                    _buttonsDownCount++;

                    // Ensure position is recorded right before the click
                    EnsurePositionRecorded(x, y, now);

                    AddAction(new MacroAction
                    {
                        TimestampMs = now,
                        Type        = MacroActionType.MouseDown,
                        X           = x,
                        Y           = y,
                        Button      = btnDown
                    });
                    break;

                case NativeMethods.WM_LBUTTONUP:
                case NativeMethods.WM_RBUTTONUP:
                case NativeMethods.WM_MBUTTONUP:
                    string btnUp = msg == NativeMethods.WM_LBUTTONUP ? "Left"
                        : msg == NativeMethods.WM_RBUTTONUP ? "Right" : "Middle";

                    // Discard orphaned MouseUp (e.g. releasing the mouse after clicking UI Record button)
                    if (!_recordedDownButtons.Remove(btnUp))
                        break;

                    if (_buttonsDownCount > 0) _buttonsDownCount--;
                    EnsurePositionRecorded(x, y, now);

                    AddAction(new MacroAction
                    {
                        TimestampMs = now,
                        Type        = MacroActionType.MouseUp,
                        X           = x,
                        Y           = y,
                        Button      = btnUp
                    });
                    break;

                case NativeMethods.WM_MOUSEWHEEL:
                    EnsurePositionRecorded(x, y, now);
                    short wheelDelta = unchecked((short)((ms.mouseData >> 16) & 0xFFFF));

                    AddAction(new MacroAction
                    {
                        TimestampMs = now,
                        Type        = MacroActionType.MouseWheel,
                        X           = x,
                        Y           = y,
                        WheelDelta  = wheelDelta
                    });
                    break;
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void EnsurePositionRecorded(int x, int y, long now)
    {
        if (!_recordMouseMove) return;

        if (x != _lastMoveX || y != _lastMoveY)
        {
            _lastMoveX = x;
            _lastMoveY = y;
            _lastMoveTimestamp = now;

            AddAction(new MacroAction
            {
                TimestampMs = now,
                Type        = MacroActionType.MouseMove,
                X           = x,
                Y           = y
            });
        }
    }

    private void AddAction(MacroAction action)
    {
        int count;
        long duration;
        lock (_lock)
        {
            _recordedActions.Add(action);
            count = _recordedActions.Count;
            duration = _stopwatch.ElapsedMilliseconds;
        }
        ActionRecorded?.Invoke(count, duration);
    }

    public void Dispose()
    {
        Stop();
    }
}
