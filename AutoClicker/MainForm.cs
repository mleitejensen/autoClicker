using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

namespace AutoClicker;

/// <summary>
/// Main application window. Lets the user choose a mouse button or keyboard key,
/// set a repeat interval, then start/stop auto-clicking via UI buttons or the
/// global hotkeys F6 (Start) and F7 (Stop).
/// </summary>
public partial class MainForm : Form
{
    private const int HOTKEY_START_ID = 1;
    private const int HOTKEY_STOP_ID  = 2;
    private const int HOTKEY_REC_ID   = 3;
    private const int HOTKEY_PLAY_ID  = 4;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoClicker", "settings.json");

    public enum AppMode { AutoClicker, Macro, Routine }
    private AppMode _currentMode = AppMode.AutoClicker;

    #region UI Controls

    // Tabs & views
    private RadioButton   _tabClickerRadio = null!;
    private RadioButton   _tabMacroRadio = null!;
    private Panel         _clickerContainerPanel = null!;
    private Panel         _macroContainerPanel = null!;

    // Input card
    private RadioButton   _mouseRadio = null!;
    private RadioButton   _keyboardRadio = null!;
    private ComboBox      _mouseButtonCombo = null!;
    private ComboBox      _mouseClickTypeCombo = null!;
    private ComboBox      _keyCombo = null!;
    private CheckBox      _keyCtrlCheck = null!;
    private CheckBox      _keyShiftCheck = null!;
    private CheckBox      _keyAltCheck = null!;
    private Panel         _mouseOptionsPanel = null!;
    private Panel         _keyboardOptionsPanel = null!;

    // Location card
    private RadioButton   _locationCursorRadio = null!;
    private RadioButton   _locationFixedRadio = null!;
    private Panel         _fixedLocationPanel = null!;
    private NumericUpDown _fixedXNumeric = null!;
    private NumericUpDown _fixedYNumeric = null!;
    private Button        _pickCoordsButton = null!;
    private CheckBox      _jitterCheck = null!;
    private NumericUpDown _jitterNumeric = null!;

    // Interval card
    private NumericUpDown _intervalNumeric = null!;
    private CheckBox      _randomIntervalCheckBox = null!;
    private Panel         _intervalRangePanel = null!;
    private NumericUpDown _minimumIntervalNumeric = null!;
    private NumericUpDown _maximumIntervalNumeric = null!;
    private Panel         _intervalCard = null!;

    // Limits & timing card
    private NumericUpDown _startDelayNumeric = null!;
    private CheckBox      _holdCheckBox = null!;
    private CheckBox      _limitCheckBox = null!;
    private NumericUpDown _limitNumeric = null!;
    private ComboBox      _limitUnitCombo = null!;

    // Hotkeys & window settings card
    private ComboBox      _hotkeyStartCombo = null!;
    private ComboBox      _hotkeyStopCombo = null!;
    private CheckBox      _alwaysOnTopCheck = null!;
    private CheckBox      _pauseOnMoveCheck = null!;
    private CheckBox      _closeToTrayCheck = null!;

    // Action buttons & status
    private Button        _startButton = null!;
    private Button        _stopButton = null!;
    private Label         _statusLabel = null!;

    // Macro controls
    private Button        _macroRecButton = null!;
    private Button        _macroPlayButton = null!;
    private Label         _macroInfoLabel = null!;
    private Button        _macroClearButton = null!;
    private ComboBox      _macroSpeedCombo = null!;
    private ComboBox      _macroRepeatCombo = null!;
    private CheckBox      _macroMouseMoveCheck = null!;
    private CheckBox      _macroPauseOnMoveCheck = null!;
    private Button        _macroLoadButton = null!;
    private Button        _macroSaveButton = null!;
    private Label         _macroFileLabel = null!;
    private ComboBox      _hotkeyRecCombo = null!;
    private ComboBox      _hotkeyPlayCombo = null!;

    // Tray icon
    private NotifyIcon    _trayIcon = null!;

    #endregion

    private enum Phase { Idle, Countdown, Running }

    private Phase  _phase = Phase.Idle;
    private long   _countdownEndTicks;
    private long   _runStartTicks;
    private int    _clickCount;
    private Action? _releaseHeld;

    // Movement monitoring
    private Point  _lastTrackedMousePoint;

    // High-resolution click timer + lightweight UI update timer
    private readonly HighResTimer _highResTimer = new();
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 100 };

    // Macro recorder and player
    private readonly InputRecorder _inputRecorder = new();
    private readonly MacroPlayer   _macroPlayer   = new();
    private MacroSequence          _currentMacro  = new();
    private string?                _lastLoadedMacroPath;

    public MainForm()
    {
        Text            = "Reflex";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;

        Icon? appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (appIcon is not null) Icon = appIcon;

        BuildUI();
        BuildTrayIcon(appIcon);

        _statusTimer.Tick += OnStatusTick;

        _inputRecorder.ActionRecorded += OnMacroActionRecorded;
        _macroPlayer.ProgressChanged += OnMacroPlaybackProgress;
        _macroPlayer.PlaybackFinished += OnMacroPlaybackFinished;
        _macroPlayer.WaitingChanged += OnMacroWaitingChanged;

        LoadSettings();

        Load += (_, _) =>
        {
            RegisterConfiguredHotkeys();
            UpdateMacroButtonLabels();
            UpdateMacroInfo();
        };
        FormClosing += OnFormClosing;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int enabled = 1;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
    }

    // -------------------------------------------------------------------------
    // Global Hotkeys
    // -------------------------------------------------------------------------

    private void RegisterConfiguredHotkeys()
    {
        UnregisterHotkeys();

        string startKeyName = _hotkeyStartCombo?.SelectedItem?.ToString() ?? "F6";
        string stopKeyName  = _hotkeyStopCombo?.SelectedItem?.ToString()  ?? "F7";
        string recKeyName   = _hotkeyRecCombo?.SelectedItem?.ToString()   ?? "F8";
        string playKeyName  = _hotkeyPlayCombo?.SelectedItem?.ToString()  ?? "F9";

        var registered = new HashSet<Keys>();

        void Reg(int id, string name)
        {
            if (KeyRegistry.HotkeyKeys.TryGetValue(name, out var key) && registered.Add(key))
                NativeMethods.RegisterHotKey(Handle, id, NativeMethods.MOD_NONE, (uint)key);
        }

        switch (_currentMode)
        {
            case AppMode.AutoClicker:
                Reg(HOTKEY_START_ID, startKeyName);
                Reg(HOTKEY_STOP_ID, stopKeyName);
                break;
            case AppMode.Macro:
                Reg(HOTKEY_REC_ID, recKeyName);
                Reg(HOTKEY_PLAY_ID, playKeyName);
                break;
            case AppMode.Routine:
                Reg(HOTKEY_PLAY_ID, playKeyName);
                Reg(HOTKEY_STOP_ID, stopKeyName);
                break;
        }
    }

    private void UnregisterHotkeys()
    {
        NativeMethods.UnregisterHotKey(Handle, HOTKEY_START_ID);
        NativeMethods.UnregisterHotKey(Handle, HOTKEY_STOP_ID);
        NativeMethods.UnregisterHotKey(Handle, HOTKEY_REC_ID);
        NativeMethods.UnregisterHotKey(Handle, HOTKEY_PLAY_ID);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            switch (m.WParam.ToInt32())
            {
                case HOTKEY_START_ID:
                    if (_currentMode == AppMode.AutoClicker)
                    {
                        if (_phase == Phase.Idle) StartClicking(); else StopClicking();
                    }
                    break;
                case HOTKEY_STOP_ID:
                    StopAll();
                    break;
                case HOTKEY_REC_ID:
                    ToggleMacroRecording();
                    break;
                case HOTKEY_PLAY_ID:
                    if (_currentMode == AppMode.Routine) ToggleRoutine(); else ToggleMacroPlayback();
                    break;
            }
        }
        base.WndProc(ref m);
    }

    private void StopAll()
    {
        if (_phase != Phase.Idle)
            StopClicking();

        if (_inputRecorder.IsRecording)
            StopMacroRecording();

        if (_macroPlayer.IsPlaying)
            StopMacroPlayback();

        if (_routineRunner.IsRunning)
            StopRoutine();
    }

    // -------------------------------------------------------------------------
    // Start / Stop logic
    // -------------------------------------------------------------------------

    private void StartClicking()
    {
        if (_phase != Phase.Idle) return;

        _startButton.Enabled = false;
        _stopButton.Enabled  = true;
        _clickCount          = 0;

        int delaySeconds = (int)_startDelayNumeric.Value;
        if (delaySeconds > 0)
        {
            _phase             = Phase.Countdown;
            _countdownEndTicks = Environment.TickCount64 + delaySeconds * 1000L;
            _statusTimer.Start();
            UpdateStatus();
        }
        else
        {
            BeginRunning();
        }
    }

    private void BeginRunning()
    {
        _phase         = Phase.Running;
        _runStartTicks = Environment.TickCount64;

        if (NativeMethods.GetCursorPos(out var pt))
            _lastTrackedMousePoint = new Point(pt.x, pt.y);

        if (_holdCheckBox.Checked)
        {
            _releaseHeld = PressAndHold();
        }
        else
        {
            _highResTimer.Start(OnHighResTick, GetNextInterval);
        }

        _statusTimer.Start();
        UpdateStatus();
    }

    private void StopClicking(bool finished = false, string? reason = null)
    {
        if (_phase == Phase.Idle) return;

        _highResTimer.Stop();
        _statusTimer.Stop();
        _releaseHeld?.Invoke();
        _releaseHeld = null;

        bool hadRun = _phase == Phase.Running;
        _phase               = Phase.Idle;
        _startButton.Enabled = true;
        _stopButton.Enabled  = false;

        string summary = hadRun && _clickCount > 0 ? $" � {_clickCount} CLICKS" : string.Empty;
        if (reason is not null)
        {
            _statusLabel.Text      = $"PAUSED ({reason.ToUpperInvariant()}){summary}";
            _statusLabel.ForeColor = Theme.AccentOrange;
        }
        else
        {
            _statusLabel.Text      = (finished ? "FINISHED" : "STOPPED") + summary;
            _statusLabel.ForeColor = finished ? Theme.AccentBlue : Theme.TextMuted;
        }
    }

    private void UpdateStatus()
    {
        switch (_phase)
        {
            case Phase.Countdown:
                long remainingMs = _countdownEndTicks - Environment.TickCount64;
                _statusLabel.Text      = $"STARTING IN {Math.Max(1, (remainingMs + 999) / 1000)}S�";
                _statusLabel.ForeColor = Theme.AccentOrange;
                break;
            case Phase.Running:
                _statusLabel.Text = _releaseHeld is not null
                    ? "HOLDING"
                    : $"RUNNING � {_clickCount} CLICKS";
                _statusLabel.ForeColor = Theme.AccentGreen;
                break;
        }
    }

    private void OnStatusTick(object? sender, EventArgs e)
    {
        if (_inputRecorder.IsRecording)
        {
            long ms = _inputRecorder.CurrentDurationMs;
            int count = _inputRecorder.CurrentActionCount;
            _statusLabel.Text = $"RECORDING � {count} ACTIONS ({ms / 1000.0:F1}S)";
            _statusLabel.ForeColor = Theme.Danger;
            _macroInfoLabel.Text = $"{count} ACTIONS RECORDED ({ms / 1000.0:F1}S)...";
            return;
        }

        if (_phase == Phase.Countdown)
        {
            if (Environment.TickCount64 >= _countdownEndTicks) BeginRunning();
            else UpdateStatus();
            return;
        }

        if (_phase != Phase.Running) return;

        // Safety check: pause on mouse move
        if (_pauseOnMoveCheck.Checked && NativeMethods.GetCursorPos(out var currentPt))
        {
            int dx = Math.Abs(currentPt.x - _lastTrackedMousePoint.X);
            int dy = Math.Abs(currentPt.y - _lastTrackedMousePoint.Y);
            if (dx > 15 || dy > 15)
            {
                StopClicking(finished: false, reason: "mouse moved");
                return;
            }
        }

        // Duration limit check
        if (_limitCheckBox.Checked && _limitUnitCombo.SelectedItem?.ToString() == "seconds"
            && Environment.TickCount64 - _runStartTicks >= (long)_limitNumeric.Value * 1000)
        {
            StopClicking(finished: true);
            return;
        }

        UpdateStatus();
    }

    // -------------------------------------------------------------------------
    // Execution (called from high-res timer thread)
    // -------------------------------------------------------------------------

    private void OnHighResTick()
    {
        ExecuteSingleAction();

        _clickCount++;

        if (NativeMethods.GetCursorPos(out var pt))
            _lastTrackedMousePoint = new Point(pt.x, pt.y);

        if (_limitCheckBox.Checked && _limitUnitCombo.SelectedItem?.ToString() == "clicks"
            && _clickCount >= (int)_limitNumeric.Value)
        {
            BeginInvoke(new Action(() => StopClicking(finished: true)));
        }
    }

    private void ExecuteSingleAction()
    {
        if (_mouseRadio.Checked)
        {
            string button     = _mouseButtonCombo.SelectedItem?.ToString() ?? "Left";
            string clickType  = _mouseClickTypeCombo.SelectedItem?.ToString() ?? "Single";
            string locMode    = _locationFixedRadio.Checked ? "Fixed" : "Cursor";
            int fixedX        = (int)_fixedXNumeric.Value;
            int fixedY        = (int)_fixedYNumeric.Value;
            bool jitter       = _jitterCheck.Checked;
            int jitterRadius  = (int)_jitterNumeric.Value;

            InputSimulator.ClickMouse(button, clickType, locMode, fixedX, fixedY, jitter, jitterRadius);
        }
        else
        {
            string key   = _keyCombo.SelectedItem?.ToString() ?? "Space";
            bool ctrl    = _keyCtrlCheck.Checked;
            bool shift   = _keyShiftCheck.Checked;
            bool alt     = _keyAltCheck.Checked;

            InputSimulator.PressKey(key, ctrl, shift, alt);
        }
    }

    private Action PressAndHold()
    {
        if (_mouseRadio.Checked)
        {
            string button    = _mouseButtonCombo.SelectedItem?.ToString() ?? "Left";
            string locMode   = _locationFixedRadio.Checked ? "Fixed" : "Cursor";
            int fixedX       = (int)_fixedXNumeric.Value;
            int fixedY       = (int)_fixedYNumeric.Value;
            bool jitter      = _jitterCheck.Checked;
            int jitterRadius = (int)_jitterNumeric.Value;

            return InputSimulator.HoldMouse(button, locMode, fixedX, fixedY, jitter, jitterRadius);
        }
        else
        {
            string key = _keyCombo.SelectedItem?.ToString() ?? "Space";
            bool ctrl  = _keyCtrlCheck.Checked;
            bool shift = _keyShiftCheck.Checked;
            bool alt   = _keyAltCheck.Checked;

            return InputSimulator.HoldKey(key, ctrl, shift, alt);
        }
    }

    private int GetNextInterval()
    {
        if (!_randomIntervalCheckBox.Checked)
            return (int)_intervalNumeric.Value;

        int minimum = (int)_minimumIntervalNumeric.Value;
        int maximum = (int)_maximumIntervalNumeric.Value;
        return Random.Shared.Next(minimum, maximum + 1);
    }

    // -------------------------------------------------------------------------
    // Macro Recording & Playback (TinyTask style)
    // -------------------------------------------------------------------------

    private void ToggleMacroRecording()
    {
        if (_inputRecorder.IsRecording)
        {
            StopMacroRecording();
        }
        else
        {
            StartMacroRecording();
        }
    }

    private void StartMacroRecording()
    {
        if (_inputRecorder.IsRecording) return;
        if (_phase != Phase.Idle) StopClicking();
        if (_macroPlayer.IsPlaying) StopMacroPlayback();

        var excluded = new List<Keys>();
        string recHk  = _hotkeyRecCombo?.SelectedItem?.ToString()  ?? "F8";
        string playHk = _hotkeyPlayCombo?.SelectedItem?.ToString() ?? "F9";
        string stopHk = _hotkeyStopCombo?.SelectedItem?.ToString() ?? "F7";
        if (KeyRegistry.HotkeyKeys.TryGetValue(recHk, out var k1)) excluded.Add(k1);
        if (KeyRegistry.HotkeyKeys.TryGetValue(playHk, out var k2)) excluded.Add(k2);
        if (KeyRegistry.HotkeyKeys.TryGetValue(stopHk, out var k3)) excluded.Add(k3);

        bool recMouseMove = _macroMouseMoveCheck?.Checked ?? true;
        _inputRecorder.Start(recMouseMove, excluded);

        Theme.SetButtonActiveState(_macroRecButton, true, Theme.Danger,         $"■  STOP REC ({recHk})", $"⏺  RECORD ({recHk})");
        _macroPlayButton.Enabled = false;

        _statusLabel.Text = "RECORDING MACRO...";
        _statusLabel.ForeColor = Theme.Danger;

        _statusTimer.Start();

        if (WindowState == FormWindowState.Minimized || !Visible)
        {
            _trayIcon.ShowBalloonTip(1000, "Reflex", $"Recording started. Press {recHk} or {stopHk} to stop.", ToolTipIcon.Info);
        }
    }

    private void StopMacroRecording()
    {
        if (!_inputRecorder.IsRecording) return;

        _currentMacro = _inputRecorder.Stop();

        UpdateMacroButtonLabels();
        _macroPlayButton.Enabled = _currentMacro.Actions.Count > 0;

        UpdateMacroInfo();

        _statusLabel.Text = $"RECORDED {_currentMacro.Actions.Count} ACTIONS ({_currentMacro.TotalDurationMs / 1000.0:F1}S)";
        _statusLabel.ForeColor = Theme.AccentBlue;

        string recHk = _hotkeyRecCombo?.SelectedItem?.ToString() ?? "F8";
        if (WindowState == FormWindowState.Minimized || !Visible)
        {
            _trayIcon.ShowBalloonTip(1000, "Reflex", $"Recording finished: {_currentMacro.Actions.Count} actions.", ToolTipIcon.Info);
        }
    }

    private void ToggleMacroPlayback()
    {
        if (_macroPlayer.IsPlaying)
        {
            StopMacroPlayback();
        }
        else
        {
            StartMacroPlayback();
        }
    }

    private void StartMacroPlayback()
    {
        if (_macroPlayer.IsPlaying) return;
        if (_currentMacro == null || _currentMacro.Actions.Count == 0)
        {
            MessageBox.Show("No macro recorded yet. Click Record or open a macro file first.", "Macro Playback", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_phase != Phase.Idle) StopClicking();
        if (_inputRecorder.IsRecording) StopMacroRecording();

        double speed = ParseSpeedMultiplier(_macroSpeedCombo?.SelectedItem?.ToString());
        int repeat   = ParseRepeatCount(_macroRepeatCombo?.SelectedItem?.ToString());
        bool pauseOnMove = _macroPauseOnMoveCheck?.Checked ?? true;

        string playHk = _hotkeyPlayCombo?.SelectedItem?.ToString() ?? "F9";
        Theme.SetButtonActiveState(_macroPlayButton, true, Theme.AccentGreen, $"■  STOP PLAY ({playHk})", $"▶  PLAY ({playHk})");
        _macroRecButton.Enabled = false;

        _statusLabel.Text = "PLAYING MACRO...";
        _statusLabel.ForeColor = Theme.AccentGreen;

        string stopHk = _hotkeyStopCombo?.SelectedItem?.ToString() ?? "F7";
        if (WindowState == FormWindowState.Minimized || !Visible)
        {
            _trayIcon.ShowBalloonTip(1000, "Reflex", $"Playback started. Press {playHk} or {stopHk} to stop.", ToolTipIcon.Info);
        }

        _macroPlayer.Play(_currentMacro, speed, repeat, pauseOnMove);
    }

    private void StopMacroPlayback(string? reason = null)
    {
        if (!_macroPlayer.IsPlaying) return;
        _macroPlayer.Stop(reason);
    }

    private void OnMacroActionRecorded(int count, long durationMs)
    {
        // Continuous updates handled in OnStatusTick
    }

    private void OnMacroPlaybackProgress(int current, int total, int loop)
    {
        if (IsDisposed || Disposing) return;
        try
        {
            BeginInvoke(new Action(() =>
            {
                if (!_macroPlayer.IsPlaying) return;
                string loopStr = _macroRepeatCombo?.SelectedItem?.ToString() == "Continuous"
                    ? $"LOOP {loop}"
                    : $"LOOP {loop}";
                _statusLabel.Text = $"PLAYING � {loopStr} ({current}/{total})";
                _statusLabel.ForeColor = Theme.AccentGreen;
            }));
        }
        catch
        {
            // Form may be closing
        }
    }

    private void OnMacroPlaybackFinished(bool completed, string? reason)
    {
        if (IsDisposed || Disposing) return;
        try
        {
            BeginInvoke(new Action(() =>
            {
                UpdateMacroButtonLabels();
                _macroRecButton.Enabled = true;

                if (reason == "mouse moved")
                {
                    _statusLabel.Text = "PLAYBACK PAUSED (MOUSE MOVED)";
                    _statusLabel.ForeColor = Theme.AccentOrange;
                }
                else if (completed)
                {
                    _statusLabel.Text = "PLAYBACK FINISHED";
                    _statusLabel.ForeColor = Theme.AccentBlue;
                }
                else
                {
                    _statusLabel.Text = "PLAYBACK STOPPED";
                    _statusLabel.ForeColor = Theme.TextMuted;
                }
            }));
        }
        catch
        {
            // Form may be closing
        }
    }

    private void ClearMacro()
    {
        if (_macroPlayer.IsPlaying) StopMacroPlayback();
        if (_inputRecorder.IsRecording) StopMacroRecording();
        _currentMacro = new MacroSequence();
        _lastLoadedMacroPath = null;
        _macroFileLabel.Text = "No file loaded";
        UpdateMacroInfo();
        _statusLabel.Text = "MACRO CLEARED";
        _statusLabel.ForeColor = Theme.TextMuted;
    }

    private void LoadMacroFromFile()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Macro Files (*.macro.json;*.json)|*.macro.json;*.json|All Files (*.*)|*.*",
            Title  = "Open Recorded Macro"
        };
        if (ofd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                string json = File.ReadAllText(ofd.FileName);
                var macro = JsonSerializer.Deserialize<MacroSequence>(json);
                if (macro != null && macro.Actions.Count > 0)
                {
                    _currentMacro = macro;
                    _lastLoadedMacroPath = ofd.FileName;
                    _macroFileLabel.Text = Path.GetFileName(ofd.FileName);
                    UpdateMacroInfo();
                    _statusLabel.Text = $"LOADED {_currentMacro.Actions.Count} ACTIONS ({_currentMacro.TotalDurationMs / 1000.0:F1}S)";
                    _statusLabel.ForeColor = Theme.AccentBlue;
                }
                else
                {
                    MessageBox.Show("The selected file contains no actions.", "Open Macro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load macro: {ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void SaveMacroToFile()
    {
        if (_currentMacro.Actions.Count == 0)
        {
            MessageBox.Show("No actions recorded to save.", "Save Macro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "Macro Files (*.macro.json)|*.macro.json|JSON Files (*.json)|*.json",
            Title  = "Save Recorded Macro",
            FileName = $"{_currentMacro.Name.Replace(" ", "_").Replace(":", "-")}.macro.json"
        };
        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                string json = JsonSerializer.Serialize(_currentMacro, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(sfd.FileName, json);
                _lastLoadedMacroPath = sfd.FileName;
                _macroFileLabel.Text = Path.GetFileName(sfd.FileName);
                _statusLabel.Text = "MACRO SAVED";
                _statusLabel.ForeColor = Theme.AccentBlue;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save macro: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void UpdateMacroButtonLabels()
    {
        string recHk  = _hotkeyRecCombo?.SelectedItem?.ToString()  ?? "F8";
        string playHk = _hotkeyPlayCombo?.SelectedItem?.ToString() ?? "F9";
        if (_macroRecButton != null && !_inputRecorder.IsRecording)
        {
            Theme.SetButtonActiveState(_macroRecButton, false, Theme.Danger, $"■  STOP ({recHk})", $"⏺  RECORD ({recHk})");
        }
        if (_macroPlayButton != null && !_macroPlayer.IsPlaying)
        {
            Theme.SetButtonActiveState(_macroPlayButton, false, Theme.AccentGreen, $"■  STOP ({playHk})", $"▶  PLAY ({playHk})");
            _macroPlayButton.Enabled = _currentMacro != null && _currentMacro.Actions.Count > 0;
        }
    }

    private void UpdateMacroInfo()
    {
        if (_currentMacro == null || _currentMacro.Actions.Count == 0)
        {
            _macroInfoLabel.Text      = "NO MACRO RECORDED YET";
            _macroInfoLabel.ForeColor = Theme.TextMuted;
            _macroPlayButton.Enabled  = false;
            _macroSaveButton.Enabled  = false;
            _macroClearButton.Enabled = false;
        }
        else
        {
            _macroInfoLabel.Text      = _currentMacro.Summary.ToUpperInvariant();
            _macroInfoLabel.ForeColor = Theme.TextPrimary;
            _macroPlayButton.Enabled  = !_macroPlayer.IsPlaying && !_inputRecorder.IsRecording;
            _macroSaveButton.Enabled  = true;
            _macroClearButton.Enabled = true;
            UpdateMacroButtonLabels();
        }
    }

    private static double ParseSpeedMultiplier(string? speed) => speed switch
    {
        "0.5x" => 0.5,
        "1.0x" => 1.0,
        "2.0x" => 2.0,
        "5.0x" => 5.0,
        "10.0x" => 10.0,
        "Max"  => 50.0,
        _      => 1.0
    };

    private static int ParseRepeatCount(string? repeat) => repeat switch
    {
        "1x"         => 1,
        "2x"         => 2,
        "3x"         => 3,
        "5x"         => 5,
        "10x"        => 10,
        "Continuous" => 0,
        _            => 1
    };
}
