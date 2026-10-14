using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace AutoClicker;

public partial class MainForm
{
    private static void SetClamped(NumericUpDown numeric, int value) =>
        numeric.Value = Math.Clamp(value, (int)numeric.Minimum, (int)numeric.Maximum);

    private static void Select(ComboBox combo, string value)
    {
        int index = combo.Items.IndexOf(value);
        if (index >= 0) combo.SelectedIndex = index;
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
            if (s is null) return;

            Select(_mouseButtonCombo, s.MouseButton);
            Select(_mouseClickTypeCombo, s.MouseClickType);
            Select(_keyCombo, s.Key);
            _keyCtrlCheck.Checked  = s.KeyCtrl;
            _keyShiftCheck.Checked = s.KeyShift;
            _keyAltCheck.Checked   = s.KeyAlt;

            _keyboardRadio.Checked = s.KeyboardMode;
            _mouseRadio.Checked    = !s.KeyboardMode;

            if (s.ClickLocationMode == "Fixed") _locationFixedRadio.Checked = true;
            else _locationCursorRadio.Checked = true;
            SetClamped(_fixedXNumeric, s.FixedX);
            SetClamped(_fixedYNumeric, s.FixedY);
            _jitterCheck.Checked = s.PositionJitter;
            SetClamped(_jitterNumeric, s.JitterPixels);

            SetClamped(_intervalNumeric, s.Interval);
            _randomIntervalCheckBox.Checked = s.Random;
            SetClamped(_minimumIntervalNumeric, s.MinInterval);
            SetClamped(_maximumIntervalNumeric, s.MaxInterval);

            SetClamped(_startDelayNumeric, s.StartDelay);
            _holdCheckBox.Checked  = s.Hold;
            _limitCheckBox.Checked = s.LimitEnabled;
            SetClamped(_limitNumeric, s.LimitValue);
            Select(_limitUnitCombo, s.LimitUnit);

            Select(_hotkeyStartCombo, s.HotkeyStartKey);
            Select(_hotkeyStopCombo,  s.HotkeyStopKey);

            _alwaysOnTopCheck.Checked = s.AlwaysOnTop;
            _pauseOnMoveCheck.Checked = s.PauseOnMouseMove;
            _closeToTrayCheck.Checked = s.CloseToTray;

            // Macro / TinyTask settings
            if (s.ActiveTab == "Macro") _tabMacroRadio.Checked = true;
            else if (s.ActiveTab == "Routine") _tabRoutineRadio.Checked = true;
            else _tabClickerRadio.Checked = true;

            Select(_macroSpeedCombo, s.MacroSpeed);
            Select(_macroRepeatCombo, s.MacroRepeat);
            _macroMouseMoveCheck.Checked   = s.MacroRecordMouseMove;
            _macroPauseOnMoveCheck.Checked = s.MacroPauseOnMouseMove;

            Select(_hotkeyRecCombo, s.HotkeyRecordKey);
            Select(_hotkeyPlayCombo, s.HotkeyPlayKey);
            LoadScreenSettings(s);
            LoadRoutineSettings(s);

            if (!string.IsNullOrEmpty(s.LastMacroPath) && File.Exists(s.LastMacroPath))
            {
                try
                {
                    string json = File.ReadAllText(s.LastMacroPath);
                    var macro = JsonSerializer.Deserialize<MacroSequence>(json);
                    if (macro != null && macro.Actions.Count > 0)
                    {
                        _currentMacro = macro;
                        _lastLoadedMacroPath = s.LastMacroPath;
                        _macroFileLabel.Text = Path.GetFileName(s.LastMacroPath);
                        UpdateMacroInfo();
                    }
                }
                catch
                {
                    // Ignore corrupt last macro file
                }
            }
        }
        catch
        {
            // Defaults
        }
    }

    private void SaveSettings()
    {
        try
        {
            var s = new AppSettings
            {
                KeyboardMode      = _keyboardRadio.Checked,
                MouseButton       = _mouseButtonCombo.SelectedItem?.ToString() ?? "Left",
                MouseClickType    = _mouseClickTypeCombo.SelectedItem?.ToString() ?? "Single",
                Key               = _keyCombo.SelectedItem?.ToString() ?? "Space",
                KeyCtrl           = _keyCtrlCheck.Checked,
                KeyShift          = _keyShiftCheck.Checked,
                KeyAlt            = _keyAltCheck.Checked,

                ClickLocationMode = _locationFixedRadio.Checked ? "Fixed" : "Cursor",
                FixedX            = (int)_fixedXNumeric.Value,
                FixedY            = (int)_fixedYNumeric.Value,
                PositionJitter    = _jitterCheck.Checked,
                JitterPixels      = (int)_jitterNumeric.Value,

                Interval          = (int)_intervalNumeric.Value,
                Random            = _randomIntervalCheckBox.Checked,
                MinInterval       = (int)_minimumIntervalNumeric.Value,
                MaxInterval       = (int)_maximumIntervalNumeric.Value,

                StartDelay        = (int)_startDelayNumeric.Value,
                Hold              = _holdCheckBox.Checked,
                LimitEnabled      = _limitCheckBox.Checked,
                LimitValue        = (int)_limitNumeric.Value,
                LimitUnit         = _limitUnitCombo.SelectedItem?.ToString() ?? "clicks",

                HotkeyStartKey    = _hotkeyStartCombo.SelectedItem?.ToString() ?? "F6",
                HotkeyStopKey     = _hotkeyStopCombo.SelectedItem?.ToString() ?? "F7",

                AlwaysOnTop       = _alwaysOnTopCheck.Checked,
                PauseOnMouseMove  = _pauseOnMoveCheck.Checked,
                CloseToTray       = _closeToTrayCheck.Checked,

                // Macro / TinyTask settings
                ActiveTab              = _tabRoutineRadio.Checked ? "Routine" : _tabMacroRadio.Checked ? "Macro" : "AutoClicker",
                MacroSpeed             = _macroSpeedCombo.SelectedItem?.ToString() ?? "1.0x",
                MacroRepeat            = _macroRepeatCombo.SelectedItem?.ToString() ?? "1x",
                MacroRecordMouseMove   = _macroMouseMoveCheck.Checked,
                MacroPauseOnMouseMove  = _macroPauseOnMoveCheck.Checked,
                HotkeyRecordKey        = _hotkeyRecCombo.SelectedItem?.ToString() ?? "F8",
                HotkeyPlayKey          = _hotkeyPlayCombo.SelectedItem?.ToString() ?? "F9",
                LastMacroPath          = _lastLoadedMacroPath,
            };
            SaveScreenSettings(s);
            SaveRoutineSettings(s);
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s));
        }
        catch
        {
            // Ignore
        }
    }
}
