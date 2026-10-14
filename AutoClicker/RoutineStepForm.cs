using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AutoClicker;

/// <summary>Dark-themed dialog for creating or editing one routine step.</summary>
public sealed class RoutineStepForm : Form
{
    private static readonly string[] TypeLabels =
    {
        "Wait until a bar condition",
        "Run a macro",
        "Hold a key until a bar condition",
        "Press a key",
        "Click at a screen point",
        "Wait for a fixed time",
        "Go to step if a bar condition",
    };

    private readonly ComboBox _typeCombo;
    private readonly ComboBox _barCombo, _opCombo, _keyCombo, _buttonCombo;
    private readonly NumericUpDown _percent, _timeout, _seconds, _x, _y, _jump;
    private readonly TextBox _macroPath;
    private readonly Button _browse, _pick;
    private readonly Label _timeoutHint;
    private readonly Control[] _barControls, _keyControls, _macroControls, _clickControls, _delayControls, _timeoutControls, _jumpControls;
    private readonly int _stepCount;

    public RoutineStep Result { get; private set; }

    public RoutineStepForm(RoutineStep? existing, int stepCount)
    {
        _stepCount = Math.Max(stepCount, 1);
        Result = existing?.Clone() ?? new RoutineStep();

        Text            = existing == null ? "Add step" : "Edit step";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ShowInTaskbar   = false;
        TopMost         = true;
        StartPosition   = FormStartPosition.CenterParent;
        BackColor       = Theme.BgBase;
        ForeColor       = Theme.TextPrimary;
        Font            = new Font("Segoe UI", 9.25f);
        ClientSize      = new Size(340, 286);

        Label Caption(string text, int x, int y) => new()
        {
            Text = text, Location = new Point(x, y), AutoSize = true,
            ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f), UseMnemonic = false,
        };

        Controls.Add(Theme.CreateTagLabel("STEP TYPE", 14, 12, Theme.AccentOrange));
        _typeCombo = Theme.CreateCombo(14, 30, 312);
        _typeCombo.Items.AddRange(TypeLabels);
        Controls.Add(_typeCombo);

        // Condition row
        var barCap = Caption("Bar", 14, 74);
        _barCombo = Theme.CreateCombo(44, 70, 84);
        _barCombo.Items.AddRange(RoutineStep.BarNames);
        _opCombo = Theme.CreateCombo(136, 70, 96);
        _opCombo.Items.AddRange(new object[] { "is at least", "is below" });
        _percent = Theme.CreateNumeric(240, 70, 56, 50, 0, 100, 5);
        var pctCap = Caption("%", 300, 74);
        _barControls = new Control[] { barCap, _barCombo, _opCombo, _percent, pctCap };

        // Key row
        var keyCap = Caption("Key", 14, 112);
        _keyCombo = Theme.CreateCombo(44, 108, 120);
        foreach (var k in KeyRegistry.SimulationKeys.Keys) _keyCombo.Items.Add(k);
        _keyControls = new Control[] { keyCap, _keyCombo };

        // Macro row
        var macroCap = Caption("Macro file (blank = the macro currently in the Macro tab)", 14, 112);
        _macroPath = new TextBox
        {
            Location = new Point(14, 132), Size = new Size(252, 26), BackColor = Theme.BgCard, ForeColor = Theme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 8.75f),
        };
        _browse = Theme.CreateButton("BROWSE", 272, 131, 54, Theme.BgCard, height: 26, isOutline: true);
        _browse.Font = new Font("Segoe UI Semibold", 7f);
        _browse.Click += (_, _) => BrowseMacro();
        _macroControls = new Control[] { macroCap, _macroPath, _browse };

        // Click row
        var xCap = Caption("X", 14, 114);
        _x = Theme.CreateNumeric(30, 110, 64, 0, -20000, 20000, 1);
        var yCap = Caption("Y", 102, 114);
        _y = Theme.CreateNumeric(118, 110, 64, 0, -20000, 20000, 1);
        _pick = Theme.CreateButton("◎ PICK", 190, 109, 64, Theme.AccentPurple, height: 28, isOutline: true);
        _pick.Font = new Font("Segoe UI Semibold", 7.75f);
        _pick.Click += (_, _) => PickPoint();
        _buttonCombo = Theme.CreateCombo(262, 110, 64);
        _buttonCombo.Items.AddRange(new object[] { "Left", "Right", "Middle" });
        _clickControls = new Control[] { xCap, _x, yCap, _y, _pick, _buttonCombo };

        // Delay row
        var secCap = Caption("Seconds", 14, 114);
        _seconds = Theme.CreateNumeric(70, 110, 80, 1, 0.05m, 3600, 0.5m);
        _seconds.DecimalPlaces = 2;
        _delayControls = new Control[] { secCap, _seconds };

        // Timeout row (conditions)
        var toCap = Caption("Give up after", 14, 152);
        _timeout = Theme.CreateNumeric(92, 148, 64, 0, 0, 86400, 5);
        var toSec = Caption("seconds", 160, 152);
        _timeoutHint = Caption("0 = wait forever", 218, 152);
        _timeoutControls = new Control[] { toCap, _timeout, toSec, _timeoutHint };

        // Jump row
        var jumpCap = Caption("Go to step", 14, 152);
        _jump = Theme.CreateNumeric(80, 148, 56, 1, 1, _stepCount + 1, 1);
        _jumpControls = new Control[] { jumpCap, _jump };

        foreach (var c in _barControls) Controls.Add(c);
        foreach (var c in _keyControls) Controls.Add(c);
        foreach (var c in _macroControls) Controls.Add(c);
        foreach (var c in _clickControls) Controls.Add(c);
        foreach (var c in _delayControls) Controls.Add(c);
        foreach (var c in _timeoutControls) Controls.Add(c);
        foreach (var c in _jumpControls) Controls.Add(c);

        var ok = Theme.CreateButton("SAVE STEP", 14, 238, 152, Theme.AccentGreen, height: 34);
        var cancel = Theme.CreateButton("CANCEL", 174, 238, 152, Theme.BgCard, height: 34, isOutline: true);
        ok.Click += (_, _) => { Commit(); DialogResult = DialogResult.OK; Close(); };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(ok);
        Controls.Add(cancel);
        AcceptButton = ok;
        CancelButton = cancel;

        Load_(Result);
        _typeCombo.SelectedIndexChanged += (_, _) => UpdateVisibility();
        UpdateVisibility();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int enabled = 1;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
    }

    private void Load_(RoutineStep s)
    {
        _typeCombo.SelectedIndex = (int)s.Type;
        _barCombo.SelectedIndex = Math.Clamp(s.Bar, 0, RoutineStep.BarNames.Length - 1);
        _opCombo.SelectedIndex = s.Below ? 1 : 0;
        _percent.Value = Math.Clamp(s.Percent, 0, 100);
        _keyCombo.SelectedItem = KeyRegistry.SimulationKeys.ContainsKey(s.Key) ? s.Key : "Shift";
        _macroPath.Text = s.MacroPath;
        _x.Value = Math.Clamp(s.X, -20000, 20000);
        _y.Value = Math.Clamp(s.Y, -20000, 20000);
        _buttonCombo.SelectedItem = s.Button;
        if (_buttonCombo.SelectedIndex < 0) _buttonCombo.SelectedIndex = 0;
        _seconds.Value = Math.Clamp(s.Milliseconds / 1000m, 0.05m, 3600m);
        _timeout.Value = Math.Clamp(s.TimeoutSeconds, 0, 86400);
        _jump.Value = Math.Clamp(s.JumpTo, 1, _stepCount + 1);
    }

    private void Commit()
    {
        var s = Result;
        s.Type = (RoutineStepType)Math.Max(_typeCombo.SelectedIndex, 0);
        s.Bar = _barCombo.SelectedIndex;
        s.Below = _opCombo.SelectedIndex == 1;
        s.Percent = (int)_percent.Value;
        s.Key = _keyCombo.SelectedItem?.ToString() ?? "Shift";
        s.MacroPath = _macroPath.Text.Trim();
        s.X = (int)_x.Value;
        s.Y = (int)_y.Value;
        s.Button = _buttonCombo.SelectedItem?.ToString() ?? "Left";
        s.Milliseconds = (int)(_seconds.Value * 1000);
        s.TimeoutSeconds = (int)_timeout.Value;
        s.JumpTo = (int)_jump.Value;
    }

    private void UpdateVisibility()
    {
        var type = (RoutineStepType)Math.Max(_typeCombo.SelectedIndex, 0);
        bool cond = type is RoutineStepType.WaitUntil or RoutineStepType.HoldKeyUntil or RoutineStepType.JumpIf;

        Show(_barControls, cond);
        Show(_keyControls, type is RoutineStepType.HoldKeyUntil or RoutineStepType.PressKey);
        Show(_macroControls, type == RoutineStepType.RunMacro);
        Show(_clickControls, type == RoutineStepType.ClickAt);
        Show(_delayControls, type == RoutineStepType.Delay);
        Show(_timeoutControls, type is RoutineStepType.WaitUntil or RoutineStepType.HoldKeyUntil);
        Show(_jumpControls, type == RoutineStepType.JumpIf);

        // The key row sits below the condition row only when both are shown
        int keyY = cond ? 112 : 74;
        foreach (var c in _keyControls) c.Top = c is Label ? keyY + 4 : keyY;
    }

    private static void Show(Control[] controls, bool visible)
    {
        foreach (var c in controls) c.Visible = visible;
    }

    private void BrowseMacro()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Macro files (*.macro.json)|*.macro.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title  = "Choose a saved macro",
        };
        if (!string.IsNullOrWhiteSpace(_macroPath.Text) && File.Exists(_macroPath.Text))
            dlg.FileName = _macroPath.Text;
        if (dlg.ShowDialog(this) == DialogResult.OK) _macroPath.Text = dlg.FileName;
    }

    private void PickPoint()
    {
        using var picker = new CoordinatePickerForm();
        if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedPoint.HasValue)
        {
            _x.Value = Math.Clamp(picker.SelectedPoint.Value.X, -20000, 20000);
            _y.Value = Math.Clamp(picker.SelectedPoint.Value.Y, -20000, 20000);
        }
    }
}
