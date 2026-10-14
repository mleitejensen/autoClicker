using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace AutoClicker;

/// <summary>
/// Native Win32 definitions for SendInput, cursor control, hotkeys, DWM dark mode,
/// and low-level mouse hooking to capture coordinates.
/// </summary>
internal static class NativeMethods
{
    public const int INPUT_MOUSE    = 0;
    public const int INPUT_KEYBOARD = 1;

    public const uint MOUSEEVENTF_MOVE        = 0x0001;
    public const uint MOUSEEVENTF_LEFTDOWN    = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP      = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN   = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP     = 0x0010;
    public const uint MOUSEEVENTF_MIDDLEDOWN  = 0x0020;
    public const uint MOUSEEVENTF_MIDDLEUP    = 0x0040;
    public const uint MOUSEEVENTF_WHEEL       = 0x0800;
    public const uint MOUSEEVENTF_ABSOLUTE    = 0x8000;

    public const uint KEYEVENTF_EXTENDEDKEY   = 0x0001;
    public const uint KEYEVENTF_KEYUP         = 0x0002;
    public const uint KEYEVENTF_SCANCODE      = 0x0008;

    public const uint MAPVK_VK_TO_VSC         = 0;

    public const int  WM_HOTKEY               = 0x0312;
    public const uint MOD_NONE                = 0x0000;

    public const int  WH_KEYBOARD_LL          = 13;
    public const int  WH_MOUSE_LL             = 14;

    public const int  WM_KEYDOWN              = 0x0100;
    public const int  WM_KEYUP                = 0x0101;
    public const int  WM_SYSKEYDOWN           = 0x0104;
    public const int  WM_SYSKEYUP             = 0x0105;

    public const int  WM_MOUSEMOVE            = 0x0200;
    public const int  WM_LBUTTONDOWN          = 0x0201;
    public const int  WM_LBUTTONUP            = 0x0202;
    public const int  WM_RBUTTONDOWN          = 0x0204;
    public const int  WM_RBUTTONUP            = 0x0205;
    public const int  WM_MBUTTONDOWN          = 0x0207;
    public const int  WM_MBUTTONUP            = 0x0208;
    public const int  WM_MOUSEWHEEL           = 0x020A;

    public const int  DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT   pt;
        public uint    mouseData;
        public uint    flags;
        public uint    time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint    vkCode;
        public uint    scanCode;
        public uint    flags;
        public uint    time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int    dx;
        public int    dy;
        public uint   mouseData;
        public uint   dwFlags;
        public uint   time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint   dwFlags;
        public uint   time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint   uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT    mi;
        [FieldOffset(0)] public KEYBDINPUT    ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint       type;
        public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    public static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string? subIdList);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
    public static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
    public static extern uint TimeEndPeriod(uint uMilliseconds);

    public static readonly UIntPtr InjectedSignature = new(0xAC11C);

    public static void SendMouseWheel(int delta)
    {
        var inputs = new INPUT[]
        {
            new()
            {
                type = INPUT_MOUSE,
                u = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        mouseData   = unchecked((uint)delta),
                        dwFlags     = MOUSEEVENTF_WHEEL,
                        dwExtraInfo = InjectedSignature
                    }
                }
            }
        };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    public static void SendMouseInput(uint dwFlags, int dx = 0, int dy = 0)
    {
        var inputs = new INPUT[]
        {
            new()
            {
                type = INPUT_MOUSE,
                u = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = dx,
                        dy = dy,
                        dwFlags = dwFlags,
                        dwExtraInfo = InjectedSignature
                    }
                }
            }
        };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    public static void SendKeyInput(ushort scanCode, uint flags, ushort vkCode = 0)
    {
        uint dwFlags = flags;
        ushort scan = scanCode;
        ushort vk = vkCode;

        if (scan != 0)
        {
            dwFlags |= KEYEVENTF_SCANCODE;
        }
        else if (vk != 0)
        {
            dwFlags &= ~KEYEVENTF_SCANCODE;
        }

        var inputs = new INPUT[]
        {
            new()
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk         = vk,
                        wScan       = scan,
                        dwFlags     = dwFlags,
                        dwExtraInfo = InjectedSignature
                    }
                }
            }
        };
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }
}
