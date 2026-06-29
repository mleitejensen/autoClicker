using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoClicker;

/// <summary>
/// Main application window. Lets the user choose a mouse button or keyboard key,
/// set a repeat interval, then start/stop auto-clicking via UI buttons or the
/// global hotkeys F6 (Start) and F7 (Stop).
/// </summary>
public class MainForm : Form
{
    #region Win32 API – SendInput

    private const int INPUT_MOUSE    = 0;
    private const int INPUT_KEYBOARD = 1;

    private const uint MOUSEEVENTF_LEFTDOWN   = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP     = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN  = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP    = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP   = 0x0040;
    private const uint KEYEVENTF_KEYUP        = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int    dx, dy;
        public uint   mouseData;
        public uint   dwFlags;
        public uint   time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint   dwFlags;
        public uint   time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint   uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT    mi;
        [FieldOffset(0)] public KEYBDINPUT    ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint       type;
        public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    #endregion

    #region Win32 API – Global Hotkeys

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int  WM_HOTKEY       = 0x0312;
    private const int  HOTKEY_START_ID = 1;
    private const int  HOTKEY_STOP_ID  = 2;
    private const uint VK_F6           = 0x75;
    private const uint VK_F7           = 0x76;
    private const uint MOD_NONE        = 0x0000;

    #endregion

    #region UI Controls

    private readonly RadioButton   _mouseRadio;
    private readonly RadioButton   _keyboardRadio;
    private readonly Label         _inputLabel;
    private readonly ComboBox      _mouseButtonCombo;
    private readonly ComboBox      _keyCombo;
    private readonly NumericUpDown _intervalNumeric;
    private readonly Button        _startButton;
    private readonly Button        _stopButton;
    private readonly Label         _statusLabel;

    #endregion

    private readonly System.Windows.Forms.Timer _clickTimer;
    private bool _isRunning;

    // Mapping of display names to Keys enum values for keyboard mode.
    // F6 and F7 are deliberately excluded because they are used as hotkeys.
    private static readonly Dictionary<string, Keys> KeyMap = new()
    {
        ["Space"]  = Keys.Space,
        ["Enter"]  = Keys.Enter,
        ["Tab"]    = Keys.Tab,
        ["Escape"] = Keys.Escape,
        ["Left"]   = Keys.Left,
        ["Right"]  = Keys.Right,
        ["Up"]     = Keys.Up,
        ["Down"]   = Keys.Down,
        ["A"] = Keys.A, ["B"] = Keys.B, ["C"] = Keys.C, ["D"] = Keys.D,
        ["E"] = Keys.E, ["F"] = Keys.F, ["G"] = Keys.G, ["H"] = Keys.H,
        ["I"] = Keys.I, ["J"] = Keys.J, ["K"] = Keys.K, ["L"] = Keys.L,
        ["M"] = Keys.M, ["N"] = Keys.N, ["O"] = Keys.O, ["P"] = Keys.P,
        ["Q"] = Keys.Q, ["R"] = Keys.R, ["S"] = Keys.S, ["T"] = Keys.T,
        ["U"] = Keys.U, ["V"] = Keys.V, ["W"] = Keys.W, ["X"] = Keys.X,
        ["Y"] = Keys.Y, ["Z"] = Keys.Z,
        ["0"] = Keys.D0, ["1"] = Keys.D1, ["2"] = Keys.D2, ["3"] = Keys.D3,
        ["4"] = Keys.D4, ["5"] = Keys.D5, ["6"] = Keys.D6, ["7"] = Keys.D7,
        ["8"] = Keys.D8, ["9"] = Keys.D9,
        ["F1"]  = Keys.F1,  ["F2"]  = Keys.F2,  ["F3"]  = Keys.F3,
        ["F4"]  = Keys.F4,  ["F5"]  = Keys.F5,  ["F8"]  = Keys.F8,
        ["F9"]  = Keys.F9,  ["F10"] = Keys.F10, ["F11"] = Keys.F11,
        ["F12"] = Keys.F12,
    };

    public MainForm()
    {
        Text            = "AutoClicker";
        Size            = new Size(360, 330);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;

        // Build all controls and assign them to readonly fields.
        (
            _mouseRadio, _keyboardRadio, _inputLabel,
            _mouseButtonCombo, _keyCombo,
            _intervalNumeric,
            _startButton, _stopButton,
            _statusLabel
        ) = BuildUI();

        _clickTimer      = new System.Windows.Forms.Timer();
        _clickTimer.Tick += OnTimerTick;

        Load        += (_, _) => RegisterHotkeys();
        FormClosing += (_, _) => { UnregisterHotkeys(); _clickTimer.Stop(); };
    }

    // -------------------------------------------------------------------------
    // UI Construction
    // -------------------------------------------------------------------------

    private (
        RadioButton mouseRadio, RadioButton keyboardRadio, Label inputLabel,
        ComboBox mouseButtonCombo, ComboBox keyCombo,
        NumericUpDown intervalNumeric,
        Button startButton, Button stopButton,
        Label statusLabel
    ) BuildUI()
    {
        const int pad = 12;
        int y = pad;

        // --- Input type group ---
        var inputGroup = new GroupBox
        {
            Text     = "Input Type",
            Location = new Point(pad, y),
            Size     = new Size(320, 55),
        };
        var mouseRadio    = new RadioButton { Text = "Mouse Button", Location = new Point(10, 22), AutoSize = true, Checked = true };
        var keyboardRadio = new RadioButton { Text = "Keyboard Key", Location = new Point(165, 22), AutoSize = true };
        inputGroup.Controls.Add(mouseRadio);
        inputGroup.Controls.Add(keyboardRadio);
        Controls.Add(inputGroup);
        y += inputGroup.Height + pad;

        // --- Input selector row ---
        var inputLabel = new Label { Text = "Mouse Button:", Location = new Point(pad, y + 4), AutoSize = true };

        var mouseButtonCombo = new ComboBox
        {
            Location      = new Point(135, y),
            Size          = new Size(185, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        mouseButtonCombo.Items.AddRange(new object[] { "Left", "Right", "Middle" });
        mouseButtonCombo.SelectedIndex = 0;

        var keyCombo = new ComboBox
        {
            Location      = new Point(135, y),
            Size          = new Size(185, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Visible       = false,
        };
        foreach (var k in KeyMap.Keys) keyCombo.Items.Add(k);
        keyCombo.SelectedIndex = 0;

        Controls.Add(inputLabel);
        Controls.Add(mouseButtonCombo);
        Controls.Add(keyCombo);
        y += 35;

        // Wire radio buttons now that the combos exist.
        mouseRadio.CheckedChanged    += (_, _) => UpdateControls(mouseRadio, inputLabel, mouseButtonCombo, keyCombo);
        keyboardRadio.CheckedChanged += (_, _) => UpdateControls(mouseRadio, inputLabel, mouseButtonCombo, keyCombo);

        // --- Interval row ---
        var intervalLabel = new Label { Text = "Interval (ms):", Location = new Point(pad, y + 4), AutoSize = true };
        var intervalNumeric = new NumericUpDown
        {
            Location  = new Point(135, y),
            Size      = new Size(100, 25),
            Minimum   = 10,
            Maximum   = 60000,
            Value     = 100,
            Increment = 10,
        };
        Controls.Add(intervalLabel);
        Controls.Add(intervalNumeric);
        y += 35 + pad;

        // --- Start / Stop buttons ---
        var startButton = new Button
        {
            Text     = "▶  Start  (F6)",
            Location = new Point(pad, y),
            Size     = new Size(140, 36),
        };

        var stopButton = new Button
        {
            Text     = "■  Stop  (F7)",
            Location = new Point(pad + 150, y),
            Size     = new Size(140, 36),
            Enabled  = false,
        };

        Controls.Add(startButton);
        Controls.Add(stopButton);
        y += 46 + pad;

        // --- Status label ---
        var statusLabel = new Label
        {
            Text      = "● Stopped",
            Location  = new Point(pad, y),
            AutoSize  = true,
            ForeColor = Color.DarkRed,
            Font      = new Font(Font.FontFamily, 10, FontStyle.Bold),
        };
        Controls.Add(statusLabel);

        return (mouseRadio, keyboardRadio, inputLabel,
                mouseButtonCombo, keyCombo,
                intervalNumeric,
                startButton, stopButton,
                statusLabel);
    }

    private static void UpdateControls(
        RadioButton mouseRadio,
        Label       inputLabel,
        ComboBox    mouseButtonCombo,
        ComboBox    keyCombo)
    {
        bool isMouse             = mouseRadio.Checked;
        inputLabel.Text          = isMouse ? "Mouse Button:" : "Keyboard Key:";
        mouseButtonCombo.Visible = isMouse;
        keyCombo.Visible         = !isMouse;
    }

    // -------------------------------------------------------------------------
    // Global Hotkeys
    // -------------------------------------------------------------------------

    private void RegisterHotkeys()
    {
        if (!RegisterHotKey(Handle, HOTKEY_START_ID, MOD_NONE, VK_F6))
            MessageBox.Show(
                "Could not register F6 as the Start hotkey.\nAnother application may be using it.",
                "Hotkey Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        if (!RegisterHotKey(Handle, HOTKEY_STOP_ID, MOD_NONE, VK_F7))
            MessageBox.Show(
                "Could not register F7 as the Stop hotkey.\nAnother application may be using it.",
                "Hotkey Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void UnregisterHotkeys()
    {
        UnregisterHotKey(Handle, HOTKEY_START_ID);
        UnregisterHotKey(Handle, HOTKEY_STOP_ID);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            switch (m.WParam.ToInt32())
            {
                case HOTKEY_START_ID: StartClicking(); break;
                case HOTKEY_STOP_ID:  StopClicking();  break;
            }
        }
        base.WndProc(ref m);
    }

    // -------------------------------------------------------------------------
    // Start / Stop logic
    // -------------------------------------------------------------------------

    private void StartClicking()
    {
        if (_isRunning) return;
        _isRunning             = true;
        _clickTimer.Interval   = (int)_intervalNumeric.Value;
        _clickTimer.Start();
        _startButton.Enabled   = false;
        _stopButton.Enabled    = true;
        _statusLabel.Text      = "● Running";
        _statusLabel.ForeColor = Color.DarkGreen;
    }

    private void StopClicking()
    {
        if (!_isRunning) return;
        _isRunning             = false;
        _clickTimer.Stop();
        _startButton.Enabled   = true;
        _stopButton.Enabled    = false;
        _statusLabel.Text      = "● Stopped";
        _statusLabel.ForeColor = Color.DarkRed;
    }

    // -------------------------------------------------------------------------
    // Input simulation via SendInput
    // -------------------------------------------------------------------------

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_mouseRadio.Checked)
            SimulateMouseClick(_mouseButtonCombo.SelectedItem?.ToString() ?? "Left");
        else
            SimulateKeyPress(_keyCombo.SelectedItem?.ToString() ?? "Space");
    }

    private static void SimulateMouseClick(string button)
    {
        (uint downFlag, uint upFlag) = button switch
        {
            "Right"  => (MOUSEEVENTF_RIGHTDOWN,  MOUSEEVENTF_RIGHTUP),
            "Middle" => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
            _        => (MOUSEEVENTF_LEFTDOWN,   MOUSEEVENTF_LEFTUP),
        };

        var inputs = new INPUT[]
        {
            new INPUT { type = INPUT_MOUSE, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = downFlag } } },
            new INPUT { type = INPUT_MOUSE, u = new InputUnion { mi = new MOUSEINPUT { dwFlags = upFlag   } } },
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void SimulateKeyPress(string keyName)
    {
        if (!KeyMap.TryGetValue(keyName, out var key)) return;

        ushort vk = (ushort)key;
        var inputs = new INPUT[]
        {
            new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0              } } },
            new INPUT { type = INPUT_KEYBOARD, u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = KEYEVENTF_KEYUP } } },
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }
}
