using System;
using System.Threading;
using System.Windows.Forms;

namespace AutoClicker;

/// <summary>
/// Handles mouse and keyboard input simulation, including modifier keys,
/// scan-code conversion, coordinate positioning with optional jitter,
/// and single/double click support.
/// </summary>
public static class InputSimulator
{
    private const uint VK_CONTROL = 0x11;
    private const uint VK_SHIFT   = 0x10;
    private const uint VK_MENU    = 0x12; // Alt

    public static void ClickMouse(
        string button,
        string clickType,
        string locationMode,
        int targetX,
        int targetY,
        bool jitter,
        int jitterRadius)
    {
        MoveCursorIfNeeded(locationMode, targetX, targetY, jitter, jitterRadius);

        (uint down, uint up) = GetMouseFlags(button);
        NativeMethods.SendMouseInput(down);
        NativeMethods.SendMouseInput(up);

        if (string.Equals(clickType, "Double", StringComparison.OrdinalIgnoreCase))
        {
            Thread.Sleep(30);
            NativeMethods.SendMouseInput(down);
            NativeMethods.SendMouseInput(up);
        }
    }

    public static Action HoldMouse(
        string button,
        string locationMode,
        int targetX,
        int targetY,
        bool jitter,
        int jitterRadius)
    {
        MoveCursorIfNeeded(locationMode, targetX, targetY, jitter, jitterRadius);

        (uint down, uint up) = GetMouseFlags(button);
        NativeMethods.SendMouseInput(down);
        return () => NativeMethods.SendMouseInput(up);
    }

    public static void PressKey(string keyName, bool ctrl, bool shift, bool alt)
    {
        ushort ctrlScan = 0, shiftScan = 0, altScan = 0;

        if (ctrl)  { ctrlScan  = (ushort)NativeMethods.MapVirtualKey(VK_CONTROL, NativeMethods.MAPVK_VK_TO_VSC); NativeMethods.SendKeyInput(ctrlScan,  NativeMethods.KEYEVENTF_SCANCODE); }
        if (shift) { shiftScan = (ushort)NativeMethods.MapVirtualKey(VK_SHIFT,   NativeMethods.MAPVK_VK_TO_VSC); NativeMethods.SendKeyInput(shiftScan, NativeMethods.KEYEVENTF_SCANCODE); }
        if (alt)   { altScan   = (ushort)NativeMethods.MapVirtualKey(VK_MENU,    NativeMethods.MAPVK_VK_TO_VSC); NativeMethods.SendKeyInput(altScan,   NativeMethods.KEYEVENTF_SCANCODE); }

        SendKey(keyName, keyUp: false);
        Thread.Sleep(20);
        SendKey(keyName, keyUp: true);

        if (alt)   NativeMethods.SendKeyInput(altScan,   NativeMethods.KEYEVENTF_SCANCODE | NativeMethods.KEYEVENTF_KEYUP);
        if (shift) NativeMethods.SendKeyInput(shiftScan, NativeMethods.KEYEVENTF_SCANCODE | NativeMethods.KEYEVENTF_KEYUP);
        if (ctrl)  NativeMethods.SendKeyInput(ctrlScan,  NativeMethods.KEYEVENTF_SCANCODE | NativeMethods.KEYEVENTF_KEYUP);
    }

    public static Action HoldKey(string keyName, bool ctrl, bool shift, bool alt)
    {
        ushort ctrlScan = 0, shiftScan = 0, altScan = 0;

        if (ctrl)  { ctrlScan  = (ushort)NativeMethods.MapVirtualKey(VK_CONTROL, NativeMethods.MAPVK_VK_TO_VSC); NativeMethods.SendKeyInput(ctrlScan,  NativeMethods.KEYEVENTF_SCANCODE); }
        if (shift) { shiftScan = (ushort)NativeMethods.MapVirtualKey(VK_SHIFT,   NativeMethods.MAPVK_VK_TO_VSC); NativeMethods.SendKeyInput(shiftScan, NativeMethods.KEYEVENTF_SCANCODE); }
        if (alt)   { altScan   = (ushort)NativeMethods.MapVirtualKey(VK_MENU,    NativeMethods.MAPVK_VK_TO_VSC); NativeMethods.SendKeyInput(altScan,   NativeMethods.KEYEVENTF_SCANCODE); }

        SendKey(keyName, keyUp: false);

        return () =>
        {
            SendKey(keyName, keyUp: true);
            if (alt)   NativeMethods.SendKeyInput(altScan,   NativeMethods.KEYEVENTF_SCANCODE | NativeMethods.KEYEVENTF_KEYUP);
            if (shift) NativeMethods.SendKeyInput(shiftScan, NativeMethods.KEYEVENTF_SCANCODE | NativeMethods.KEYEVENTF_KEYUP);
            if (ctrl)  NativeMethods.SendKeyInput(ctrlScan,  NativeMethods.KEYEVENTF_SCANCODE | NativeMethods.KEYEVENTF_KEYUP);
        };
    }

    private static void MoveCursorIfNeeded(string locationMode, int targetX, int targetY, bool jitter, int jitterRadius)
    {
        if (string.Equals(locationMode, "Fixed", StringComparison.OrdinalIgnoreCase))
        {
            int x = targetX;
            int y = targetY;
            if (jitter && jitterRadius > 0)
            {
                x += Random.Shared.Next(-jitterRadius, jitterRadius + 1);
                y += Random.Shared.Next(-jitterRadius, jitterRadius + 1);
            }
            NativeMethods.SetCursorPos(x, y);
        }
        else if (jitter && jitterRadius > 0)
        {
            if (NativeMethods.GetCursorPos(out var p))
            {
                int x = p.x + Random.Shared.Next(-jitterRadius, jitterRadius + 1);
                int y = p.y + Random.Shared.Next(-jitterRadius, jitterRadius + 1);
                NativeMethods.SetCursorPos(x, y);
            }
        }
    }

    private static (uint down, uint up) GetMouseFlags(string button) => button switch
    {
        "Right"  => (NativeMethods.MOUSEEVENTF_RIGHTDOWN,  NativeMethods.MOUSEEVENTF_RIGHTUP),
        "Middle" => (NativeMethods.MOUSEEVENTF_MIDDLEDOWN, NativeMethods.MOUSEEVENTF_MIDDLEUP),
        _        => (NativeMethods.MOUSEEVENTF_LEFTDOWN,   NativeMethods.MOUSEEVENTF_LEFTUP),
    };

    private static void SendKey(string keyName, bool keyUp)
    {
        if (!KeyRegistry.SimulationKeys.TryGetValue(keyName, out var key)) return;

        ushort scan = (ushort)NativeMethods.MapVirtualKey((uint)key, NativeMethods.MAPVK_VK_TO_VSC);
        if (scan == 0) return;

        uint flags = NativeMethods.KEYEVENTF_SCANCODE;
        if (key is Keys.Left or Keys.Right or Keys.Up or Keys.Down
            or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown
            or Keys.Insert or Keys.Delete)
        {
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        }

        if (keyUp)
            flags |= NativeMethods.KEYEVENTF_KEYUP;

        NativeMethods.SendKeyInput(scan, flags);
    }
}
