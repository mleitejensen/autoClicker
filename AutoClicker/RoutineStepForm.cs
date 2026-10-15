using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
        "Read keys from screen and press them",
        "Timing game: press a key when the marker is on the target",
        "Ring game: click balls as their ring closes",
    };

    private const int Pad = 14;
    private const int FormWidth = 372;
    private const int ConditionRowHeight = 36;

    /// <summary>One "Bar is/is not X%" row. The first row is mandatory and has no remove button.</summary>
    private sealed class ConditionRowUi
    {
        public ComboBox Bar = null!;
        public ComboBox Op = null!;
        public NumericUpDown Percent = null!;
        public Label PctLabel = null!;
        public Button? Remove;
    }

    private readonly ComboBox _typeCombo;
    private readonly ComboBox _keyCombo, _buttonCombo;
    private readonly NumericUpDown _timeout, _seconds, _x, _y, _jump, _bailPct, _bailJump;
    private readonly TextBox _macroPath;
    private readonly Button _browse, _pick, _okButton, _cancelButton;
    private readonly Label _timeoutHint;
    private readonly Control[] _macroControls, _clickControls, _delayControls;
    private readonly (Control Ctrl, int Offset)[] _keyControls, _timeoutControls, _jumpControls, _bailControls, _readControls;
    private readonly Button _regionButton, _teachButton, _testButton, _clearGlyphsButton, _letterColorButton;
    private readonly ComboBox _readKind;
    private readonly NumericUpDown _gap;
    private readonly Label _readInfo;
    private string _lastRead = "";

    private readonly (Control Ctrl, int Offset)[] _timingControls, _ringControls;
    private readonly Button _ringZoneButton, _ringColorButton, _ringTestButton;
    private readonly NumericUpDown _lead, _ringHits, _ballRadius;
    private readonly Label _ringInfo;
    private string _lastRing = "";
    private readonly Button _zoneButton, _targetColorButton, _markerColorButton, _timingTestButton;
    private readonly NumericUpDown _hits;
    private readonly Label _timingInfo;
    private string _lastTiming = "";
    private readonly int _stepCount;

    private readonly List<ConditionRowUi> _conditionRows = new();
    private readonly Label _matchLabel;
    private readonly ComboBox _matchCombo;
    private readonly Button _addConditionButton;

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
        ClientSize      = new Size(FormWidth, 286);

        Controls.Add(Theme.CreateTagLabel("STEP TYPE", Pad, 12, Theme.AccentOrange));
        _typeCombo = Theme.CreateCombo(Pad, 30, FormWidth - Pad * 2);
        _typeCombo.Items.AddRange(TypeLabels);
        Controls.Add(_typeCombo);

        // Match-mode row: whether every condition must hold (AND) or just one of them (OR)
        _matchLabel = Caption("Require", Pad, 74);
        _matchCombo = Theme.CreateCombo(70, 70, FormWidth - Pad - 70 - Pad);
        _matchCombo.Items.AddRange(new object[] { "ANY condition is true (OR)", "ALL conditions are true (AND)" });
        Controls.Add(_matchLabel);
        Controls.Add(_matchCombo);

        // The first condition row is mandatory (a step always needs at least one condition)
        _conditionRows.Add(BuildConditionRow(removable: false));

        _addConditionButton = Theme.CreateButton("+ ADD CONDITION", Pad, 0, 160, Theme.AccentBlue, height: 24, isOutline: true);
        _addConditionButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _addConditionButton.Click += (_, _) => { _conditionRows.Add(BuildConditionRow(removable: true)); Relayout(); };
        Controls.Add(_addConditionButton);

        // Key row
        var keyCap = Caption("Key", 14, 112);
        _keyCombo = Theme.CreateCombo(44, 108, 120);
        foreach (var k in KeyRegistry.SimulationKeys.Keys) _keyCombo.Items.Add(k);
        _keyControls = new (Control, int)[] { (keyCap, 4), (_keyCombo, 0) };

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
        var toCap = Caption("Give up after", 14, 0);
        _timeout = Theme.CreateNumeric(92, 0, 64, 0, 0, 86400, 5);
        var toSec = Caption("seconds", 160, 0);
        _timeoutHint = Caption("0 = wait forever", 218, 0);
        _timeoutControls = new (Control, int)[] { (toCap, 4), (_timeout, 0), (toSec, 4), (_timeoutHint, 4) };

        // Read-keys block: pick the region that shows the key prompt, teach glyph looks, test what gets read
        _regionButton = Theme.CreateButton("▭ REGION", Pad, 0, 110, Theme.AccentYellow, height: 26, isOutline: true);
        _regionButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _regionButton.Click += (_, _) => PickReadRegion();
        _regionButton.MouseEnter += (_, _) => ShowOutline(Result.ReadRegion);
        _regionButton.MouseLeave += (_, _) => HideOutline();
        _letterColorButton = Theme.CreateButton("◉ LETTER COLOR", Pad + 118, 0, 140, Theme.AccentYellow, height: 26, isOutline: true);
        _letterColorButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _letterColorButton.Click += (_, _) => PickLetterColor();
        _letterColorButton.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) ClearLetterColor(); };
        new ToolTip().SetToolTip(_letterColorButton,
            "Click, then click the inside colour of the letters (not the outline). Reads only that colour, which is much more reliable for outlined game text. Right-click to go back to automatic.");
        _teachButton = Theme.CreateButton("✎ TEACH", Pad, 0, 100, Theme.AccentPurple, height: 26, isOutline: true);
        _teachButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _teachButton.Click += (_, _) => TeachGlyphs();
        _testButton = Theme.CreateButton("▶ TEST", Pad + 108, 0, 80, Theme.AccentGreen, height: 26, isOutline: true);
        _testButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _testButton.Click += (_, _) => TestRead();
        _clearGlyphsButton = Theme.CreateButton("CLEAR", Pad + 196, 0, 70, Theme.BgCard, height: 26, isOutline: true);
        _clearGlyphsButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _clearGlyphsButton.Click += (_, _) => { Result.Glyphs.Clear(); RefreshReadInfo(); };
        _readInfo = new Label
        {
            Location = new Point(Pad, 0), Size = new Size(FormWidth - Pad * 2, 34), ForeColor = Theme.TextMuted,
            Font = new Font("Segoe UI", 8.25f), UseMnemonic = false,
        };
        _readKind = Theme.CreateCombo(Pad + 266, 0, 78);
        _readKind.Items.AddRange(new object[] { "Letters", "Arrows" });
        _readKind.SelectedIndex = 0;
        _readKind.SelectedIndexChanged += (_, _) => { Result.ReadArrows = _readKind.SelectedIndex == 1; _lastRead = ""; RefreshReadInfo(); };
        var gapCap = Caption("Pause between presses", Pad, 0);
        _gap = Theme.CreateNumeric(150, 0, 64, 150, 0, 2000, 20);
        var gapUnit = Caption("ms", 218, 0);
        _readControls = new (Control, int)[] { (_regionButton, 0), (_letterColorButton, 0), (_readKind, 0), (_teachButton, 36), (_testButton, 36), (_clearGlyphsButton, 36), (_readInfo, 72), (gapCap, 148 + 4), (_gap, 148), (gapUnit, 148 + 4) };

        // Timing-game block: the strip to watch, the target's colour(s), the marker's colour, how many presses
        _zoneButton = Theme.CreateButton("▭ REGION", Pad, 0, 108, Theme.AccentYellow, height: 26, isOutline: true);
        _zoneButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _zoneButton.Click += (_, _) => PickZoneRegion();
        _zoneButton.MouseEnter += (_, _) => ShowOutline(Result.ZoneRegion);
        _zoneButton.MouseLeave += (_, _) => HideOutline();
        _targetColorButton = Theme.CreateButton("◉ TARGET", Pad + 114, 0, 108, Theme.AccentYellow, height: 26, isOutline: true);
        _targetColorButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _targetColorButton.Click += (_, _) => PickTargetColor();
        _targetColorButton.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) { Result.TargetColorsArgb.Clear(); RefreshTimingInfo(); } };
        _markerColorButton = Theme.CreateButton("◉ MARKER", Pad + 228, 0, 108, Theme.AccentYellow, height: 26, isOutline: true);
        _markerColorButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _markerColorButton.Click += (_, _) => PickMarkerColor();
        var hitsCap = Caption("Stop after", Pad, 0);
        _hits = Theme.CreateNumeric(80, 0, 56, 0, 0, 100000, 1);
        var hitsHint = Caption("presses (0 = until the target disappears)", 142, 0);
        _timingTestButton = Theme.CreateButton("▶ TEST", Pad, 0, 80, Theme.AccentGreen, height: 26, isOutline: true);
        _timingTestButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _timingTestButton.Click += (_, _) => TestTiming();
        _timingInfo = new Label
        {
            Location = new Point(Pad + 88, 0), Size = new Size(FormWidth - Pad * 2 - 88, 34), ForeColor = Theme.TextMuted,
            Font = new Font("Segoe UI", 8.25f), UseMnemonic = false,
        };
        _timingControls = new (Control, int)[]
        {
            (_zoneButton, 0), (_targetColorButton, 0), (_markerColorButton, 0),
            (hitsCap, 36 + 40 + 4), (_hits, 36 + 40), (hitsHint, 36 + 40 + 4),
            (_timingTestButton, 150), (_timingInfo, 148),
        };
        var timingToolTips = new ToolTip();
        timingToolTips.SetToolTip(_targetColorButton, "Click to add a colour of the target zone (click again for each shade). The screen freezes while you pick. Right-click to clear.");
        timingToolTips.SetToolTip(_markerColorButton, "Colour of the moving marker (white by default; only change it if yours isn't white). The screen freezes while you pick.");

        // Ring-game block: the area to search, the ring's colour(s), click timing, how many balls and (optionally) the ball size
        _ringZoneButton = Theme.CreateButton("▭ REGION", Pad, 0, 108, Theme.AccentYellow, height: 26, isOutline: true);
        _ringZoneButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _ringZoneButton.Click += (_, _) => PickZoneRegion();
        _ringZoneButton.MouseEnter += (_, _) => ShowOutline(Result.ZoneRegion);
        _ringZoneButton.MouseLeave += (_, _) => HideOutline();
        _ringColorButton = Theme.CreateButton("◉ RING", Pad + 114, 0, 108, Theme.AccentYellow, height: 26, isOutline: true);
        _ringColorButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _ringColorButton.Click += (_, _) => PickRingColor();
        _ringColorButton.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) { Result.RingColorsArgb.Clear(); RefreshRingInfo(); } };
        var leadCap = Caption("Click", Pad, 0);
        _lead = Theme.CreateNumeric(50, 0, 60, 25, -200, 500, 5);
        var leadHint = Caption("ms before the ring closes", 116, 0);
        var ringHitsCap = Caption("Stop after", Pad, 0);
        _ringHits = Theme.CreateNumeric(80, 0, 56, 0, 0, 100000, 1);
        var ringHitsHint = Caption("balls (0 = until none are left)", 142, 0);
        var radiusCap = Caption("Ball radius", Pad, 0);
        _ballRadius = Theme.CreateNumeric(84, 0, 56, 0, 0, 400, 1);
        var radiusHint = Caption("px (0 = measure it from the screen)", 146, 0);
        _ringTestButton = Theme.CreateButton("▶ TEST", Pad, 0, 80, Theme.AccentGreen, height: 26, isOutline: true);
        _ringTestButton.Font = new Font("Segoe UI Semibold", 7.75f);
        _ringTestButton.Click += (_, _) => TestRing();
        _ringInfo = new Label
        {
            Location = new Point(Pad + 88, 0), Size = new Size(FormWidth - Pad * 2 - 88, 34), ForeColor = Theme.TextMuted,
            Font = new Font("Segoe UI", 8.25f), UseMnemonic = false,
        };
        _ringControls = new (Control, int)[]
        {
            (_ringZoneButton, 0), (_ringColorButton, 0),
            (leadCap, 36 + 4), (_lead, 36), (leadHint, 36 + 4),
            (ringHitsCap, 72 + 4), (_ringHits, 72), (ringHitsHint, 72 + 4),
            (radiusCap, 108 + 4), (_ballRadius, 108), (radiusHint, 108 + 4),
            (_ringTestButton, 182), (_ringInfo, 180),
        };
        timingToolTips.SetToolTip(_ringColorButton, "Click, then click the ring in the frozen screen. The ring is often a gradient, so click it two or three times (light, middle and dark parts). Right-click to clear.");
        timingToolTips.SetToolTip(_lead, "How long before the ring touches the ball the click is sent, to cover input and frame delay. Getting 'Good' because you are early? Lower it (it can go negative, meaning later). Late? Raise it.");
        timingToolTips.SetToolTip(_ballRadius, "Balls of any colour work. Leave at 0 to measure each ball's size from the screen, or enter its radius if the balls are always the same size (TEST shows the measured value).");

        // Jump row
        var jumpCap = Caption("Go to step", 14, 0);
        _jump = Theme.CreateNumeric(80, 0, 56, 1, 1, _stepCount + 1, 1);
        _jumpControls = new (Control, int)[] { (jumpCap, 4), (_jump, 0) };

        // Bail-out row (wait / hold): leave early when Health drops, so a death can't leave the routine stuck
        var bailCap = Caption("Or if Health <", 14, 0);
        _bailPct = Theme.CreateNumeric(92, 0, 56, 0, 0, 100, 1);
        var bailMid = Caption("% go to step", 152, 0);
        _bailJump = Theme.CreateNumeric(226, 0, 56, 0, 0, _stepCount + 1, 1);
        var bailHint = Caption("0 = off", 288, 0);
        _bailControls = new (Control, int)[] { (bailCap, 4), (_bailPct, 0), (bailMid, 4), (_bailJump, 0), (bailHint, 4) };

        foreach (var (c, _) in _bailControls) Controls.Add(c);
        foreach (var (c, _) in _keyControls) Controls.Add(c);
        foreach (var c in _macroControls) Controls.Add(c);
        foreach (var c in _clickControls) Controls.Add(c);
        foreach (var c in _delayControls) Controls.Add(c);
        foreach (var (c, _) in _timeoutControls) Controls.Add(c);
        foreach (var (c, _) in _jumpControls) Controls.Add(c);
        foreach (var (c, _) in _readControls) Controls.Add(c);
        foreach (var (c, _) in _timingControls) Controls.Add(c);
        foreach (var (c, _) in _ringControls) Controls.Add(c);

        _okButton = Theme.CreateButton("SAVE STEP", Pad, 238, 168, Theme.AccentGreen, height: 34);
        _cancelButton = Theme.CreateButton("CANCEL", 190, 238, 168, Theme.BgCard, height: 34, isOutline: true);
        _okButton.Click += (_, _) => { Commit(); DialogResult = DialogResult.OK; Close(); };
        _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(_okButton);
        Controls.Add(_cancelButton);
        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        Load_(Result);
        _typeCombo.SelectedIndexChanged += (_, _) => Relayout();
        Relayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int enabled = 1;
        NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
    }

    private static Label Caption(string text, int x, int y) => new()
    {
        Text = text, Location = new Point(x, y), AutoSize = true,
        ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f), UseMnemonic = false,
    };

    private ConditionRowUi BuildConditionRow(bool removable)
    {
        var row = new ConditionRowUi
        {
            Bar = Theme.CreateCombo(0, 0, 90),
            Op = Theme.CreateCombo(0, 0, 104),
            Percent = Theme.CreateNumeric(0, 0, 54, 50, 0, 100, 5),
            PctLabel = Caption("%", 0, 0),
        };
        row.Bar.Items.AddRange(RoutineStep.BarNames);
        row.Op.Items.AddRange(new object[] { "is at least", "is below" });
        Controls.Add(row.Bar);
        Controls.Add(row.Op);
        Controls.Add(row.Percent);
        Controls.Add(row.PctLabel);

        if (removable)
        {
            row.Remove = Theme.CreateButton("✕", 0, 0, 24, Theme.BgCard, height: 24, isOutline: true);
            row.Remove.Font = new Font("Segoe UI Semibold", 8f);
            row.Remove.Click += (_, _) => RemoveConditionRow(row);
            Controls.Add(row.Remove);
        }

        return row;
    }

    private void RemoveConditionRow(ConditionRowUi row)
    {
        if (_conditionRows.Count <= 1) return;
        Controls.Remove(row.Bar);
        Controls.Remove(row.Op);
        Controls.Remove(row.Percent);
        Controls.Remove(row.PctLabel);
        if (row.Remove != null) Controls.Remove(row.Remove);
        _conditionRows.Remove(row);
        Relayout();
    }

    private static void SetConditionRow(ConditionRowUi row, int bar, bool below, int percent)
    {
        row.Bar.SelectedIndex = Math.Clamp(bar, 0, RoutineStep.BarNames.Length - 1);
        row.Op.SelectedIndex = below ? 1 : 0;
        row.Percent.Value = Math.Clamp(percent, 0, 100);
    }

    private void Load_(RoutineStep s)
    {
        _typeCombo.SelectedIndex = (int)s.Type;

        while (_conditionRows.Count > 1) RemoveConditionRow(_conditionRows[^1]);
        SetConditionRow(_conditionRows[0], s.Bar, s.Below, s.Percent);
        foreach (var extra in s.ExtraConditions)
        {
            var row = BuildConditionRow(removable: true);
            _conditionRows.Add(row);
            SetConditionRow(row, extra.Bar, extra.Below, extra.Percent);
        }
        _matchCombo.SelectedIndex = s.MatchAll ? 1 : 0;

        _keyCombo.SelectedItem = KeyRegistry.SimulationKeys.ContainsKey(s.Key) ? s.Key : "Shift";
        _macroPath.Text = s.MacroPath;
        _x.Value = Math.Clamp(s.X, -20000, 20000);
        _y.Value = Math.Clamp(s.Y, -20000, 20000);
        _buttonCombo.SelectedItem = s.Button;
        if (_buttonCombo.SelectedIndex < 0) _buttonCombo.SelectedIndex = 0;
        _seconds.Value = Math.Clamp(s.Milliseconds / 1000m, 0.05m, 3600m);
        _timeout.Value = Math.Clamp(s.TimeoutSeconds, 0, 86400);
        _jump.Value = Math.Clamp(s.JumpTo, 1, _stepCount + 1);
        _bailPct.Value = Math.Clamp(s.BailPercent, 0, 100);
        _bailJump.Value = Math.Clamp(s.BailJumpTo, 0, _stepCount + 1);
        _hits.Value = Math.Clamp(s.HitCount, 0, 100000);
        _ringHits.Value = Math.Clamp(s.HitCount, 0, 100000);
        _lead.Value = Math.Clamp(s.LeadMs, -200, 500);
        _ballRadius.Value = Math.Clamp(s.BallRadiusPx, 0, 400);
        _readKind.SelectedIndex = s.ReadArrows ? 1 : 0;
        _gap.Value = Math.Clamp(s.PressGapMs, 0, 2000);

        Relayout();
    }

    private void Commit()
    {
        var s = Result;
        s.Type = (RoutineStepType)Math.Max(_typeCombo.SelectedIndex, 0);

        var primary = _conditionRows[0];
        s.Bar = primary.Bar.SelectedIndex;
        s.Below = primary.Op.SelectedIndex == 1;
        s.Percent = (int)primary.Percent.Value;
        s.ExtraConditions = _conditionRows.Skip(1)
            .Select(r => new BarCondition { Bar = r.Bar.SelectedIndex, Below = r.Op.SelectedIndex == 1, Percent = (int)r.Percent.Value })
            .ToList();
        s.MatchAll = _matchCombo.SelectedIndex == 1;

        s.Key = _keyCombo.SelectedItem?.ToString() ?? "Shift";
        s.MacroPath = _macroPath.Text.Trim();
        s.X = (int)_x.Value;
        s.Y = (int)_y.Value;
        s.Button = _buttonCombo.SelectedItem?.ToString() ?? "Left";
        s.Milliseconds = (int)(_seconds.Value * 1000);
        s.TimeoutSeconds = (int)_timeout.Value;
        s.JumpTo = (int)_jump.Value;
        s.BailPercent = (int)_bailPct.Value;
        s.BailJumpTo = (int)_bailJump.Value;
        s.HitCount = s.Type == RoutineStepType.RingGame ? (int)_ringHits.Value : (int)_hits.Value;
        s.LeadMs = (int)_lead.Value;
        s.BallRadiusPx = (int)_ballRadius.Value;
        s.ReadArrows = _readKind.SelectedIndex == 1;
        s.PressGapMs = (int)_gap.Value;
    }

    /// <summary>Repositions every control for the current step type and condition-row count, and resizes the dialog to fit.</summary>
    private void Relayout()
    {
        var type = (RoutineStepType)Math.Max(_typeCombo.SelectedIndex, 0);
        bool cond = type is RoutineStepType.WaitUntil or RoutineStepType.HoldKeyUntil or RoutineStepType.JumpIf;

        _matchLabel.Visible = cond;
        _matchCombo.Visible = cond;
        _addConditionButton.Visible = cond;
        foreach (var row in _conditionRows)
        {
            row.Bar.Visible = cond;
            row.Op.Visible = cond;
            row.Percent.Visible = cond;
            row.PctLabel.Visible = cond;
            if (row.Remove != null) row.Remove.Visible = cond;
        }

        int condBottom = 70;
        if (cond)
        {
            int rowY = 100;
            foreach (var row in _conditionRows)
            {
                row.Bar.Location = new Point(Pad, rowY);
                row.Op.Location = new Point(Pad + 98, rowY);
                row.Percent.Location = new Point(Pad + 98 + 112, rowY);
                row.PctLabel.Location = new Point(Pad + 98 + 112 + 62, rowY + 4);
                row.Remove?.SetBounds(Pad + 98 + 112 + 62 + 20, rowY, 24, 24);
                rowY += ConditionRowHeight;
            }
            _addConditionButton.Location = new Point(Pad, rowY + 2);
            condBottom = rowY + 2 + 24 + 14;
        }

        bool showKey = type is RoutineStepType.HoldKeyUntil or RoutineStepType.PressKey or RoutineStepType.TimingGame;
        bool keyWithCond = cond && type == RoutineStepType.HoldKeyUntil;
        bool timing = type == RoutineStepType.TimingGame;
        int keyY = keyWithCond ? condBottom : timing ? 70 + 36 : 74;
        ShowGroup(_keyControls, showKey);
        PositionGroup(_keyControls, keyY);
        int afterKey = keyWithCond ? keyY + 36 : condBottom;

        Show(_macroControls, type == RoutineStepType.RunMacro);
        Show(_clickControls, type == RoutineStepType.ClickAt);
        Show(_delayControls, type == RoutineStepType.Delay);

        bool readKeys = type == RoutineStepType.ReadKeys;
        bool ring = type == RoutineStepType.RingGame;
        bool showTimeout = type is RoutineStepType.WaitUntil or RoutineStepType.HoldKeyUntil or RoutineStepType.ReadKeys or RoutineStepType.TimingGame or RoutineStepType.RingGame;
        int timeoutY = readKeys ? 70 + 112 : timing ? 70 + 112 : ring ? 70 + 144 : afterKey;
        _timeoutHint.Text = readKeys ? "0 = try once" : timing || ring ? "0 = no limit" : "0 = wait forever";
        ShowGroup(_timeoutControls, showTimeout);
        PositionGroup(_timeoutControls, timeoutY);

        ShowGroup(_readControls, readKeys);
        PositionGroup(_readControls, 70);
        if (readKeys) RefreshReadInfo();

        ShowGroup(_timingControls, timing);
        PositionGroup(_timingControls, 70);
        if (timing) RefreshTimingInfo();

        ShowGroup(_ringControls, ring);
        PositionGroup(_ringControls, 70);
        if (ring) RefreshRingInfo();

        int jumpY = condBottom;
        ShowGroup(_jumpControls, type == RoutineStepType.JumpIf);
        PositionGroup(_jumpControls, jumpY);

        bool showBail = type is RoutineStepType.WaitUntil or RoutineStepType.HoldKeyUntil;
        int bailY = timeoutY + 40;
        ShowGroup(_bailControls, showBail);
        PositionGroup(_bailControls, bailY);

        int contentBottom = type switch
        {
            RoutineStepType.WaitUntil or RoutineStepType.HoldKeyUntil => bailY + 30,
            RoutineStepType.JumpIf => jumpY + 30,
            RoutineStepType.ReadKeys => 70 + 148 + 30,
            RoutineStepType.TimingGame => 70 + 150 + 34,
            RoutineStepType.RingGame => 70 + 182 + 34,
            _ => 170,
        };
        contentBottom = Math.Max(contentBottom, 170);

        int formHeight = contentBottom + 20 + 34 + Pad;
        ClientSize = new Size(FormWidth, formHeight);
        _okButton.Location = new Point(Pad, formHeight - 34 - Pad);
        _cancelButton.Location = new Point(FormWidth - Pad - 168, formHeight - 34 - Pad);
    }

    private static void Show(Control[] controls, bool visible)
    {
        foreach (var c in controls) c.Visible = visible;
    }

    private static void ShowGroup((Control Ctrl, int Offset)[] group, bool visible)
    {
        foreach (var (ctrl, _) in group) ctrl.Visible = visible;
    }

    private static void PositionGroup((Control Ctrl, int Offset)[] group, int baseY)
    {
        foreach (var (ctrl, offset) in group) ctrl.Top = baseY + offset;
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

    private RegionOutlineForm? _outline;

    /// <summary>Outlines a screen region while the pointer rests on its REGION button, like the bar buttons on the Routine tab.</summary>
    private void ShowOutline(Rectangle region)
    {
        HideOutline();
        if (region.Width <= 0 || region.Height <= 0) return;
        _outline = new RegionOutlineForm(region, Theme.AccentYellow);
        _outline.Show();
    }

    private void HideOutline()
    {
        _outline?.Close();
        _outline?.Dispose();
        _outline = null;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        HideOutline();
        base.OnFormClosed(e);
    }

    private void PickReadRegion()
    {
        HideOutline();
        var region = PickRegionHidden();
        if (region == null) return;

        Result.ReadRegion = region.Value;
        _lastRead = "";
        RefreshReadInfo();
    }

    /// <summary>Hides this dialog while a full-screen picker is open, so the dialog can't cover what is being picked.</summary>
    private T WhileHidden<T>(Func<T> action)
    {
        Opacity = 0;
        try
        {
            Application.DoEvents();
            return action();
        }
        finally
        {
            Opacity = 1;
        }
    }

    private Rectangle? PickRegionHidden() => WhileHidden<Rectangle?>(() =>
    {
        using var picker = new RegionPickerForm();
        return picker.ShowDialog(this) == DialogResult.OK ? picker.SelectedRegion : null;
    });

    private Color? PickColorHidden() => WhileHidden<Color?>(() =>
    {
        // Frozen snapshot: the picker works on a still image, so moving parts of the screen can be picked
        System.Threading.Thread.Sleep(150);
        using var picker = new FrozenColorPickerForm();
        return picker.ShowDialog(this) == DialogResult.OK ? picker.SelectedColor : null;
    });

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private void PickZoneRegion()
    {
        HideOutline();
        var region = PickRegionHidden();
        if (region == null) return;

        Result.ZoneRegion = region.Value;
        _lastTiming = "";
        _lastRing = "";
        RefreshTimingInfo();
        RefreshRingInfo();
    }

    private void PickTargetColor()
    {
        var color = PickColorHidden();
        if (color == null) return;

        Result.TargetColorsArgb.Add(color.Value.ToArgb());
        RefreshTimingInfo();
    }

    private void PickMarkerColor()
    {
        var color = PickColorHidden();
        if (color == null) return;

        Result.MarkerColorArgb = color.Value.ToArgb();
        RefreshTimingInfo();
    }

    private void RefreshTimingInfo()
    {
        var r = Result;
        _zoneButton.Text = r.ZoneWidth > 0 ? $"▭ {r.ZoneWidth}×{r.ZoneHeight}" : "▭ REGION";
        _targetColorButton.Text = r.TargetColorsArgb.Count == 0
            ? "◉ TARGET"
            : $"◉ {Hex(Color.FromArgb(r.TargetColorsArgb[0]))}{(r.TargetColorsArgb.Count > 1 ? $"+{r.TargetColorsArgb.Count - 1}" : "")}";
        _markerColorButton.Text = $"◉ {Hex(Color.FromArgb(r.MarkerColorArgb))}";

        _timingInfo.Text = string.IsNullOrEmpty(_lastTiming)
            ? "Pick the strip, click TARGET on the zone, then TEST."
            : _lastTiming;
    }

    private void TestTiming()
    {
        var r = Result;
        if (r.ZoneWidth <= 0 || r.ZoneHeight <= 0) { _lastTiming = "Pick a region first."; RefreshTimingInfo(); return; }
        if (r.TargetColorsArgb.Count == 0) { _lastTiming = "Click TARGET on the target zone first."; RefreshTimingInfo(); return; }

        using var bmp = CaptureHidden(r.ZoneRegion);
        var reading = TimingGameReader.Analyze(bmp, r.TargetColorsArgb.Select(Color.FromArgb).ToList(), Color.FromArgb(r.MarkerColorArgb), TimingGameReader.Tolerance);
        _lastTiming = reading.ToString();
        RefreshTimingInfo();
    }

    private Bitmap CaptureHidden(Rectangle region) => WhileHidden(() =>
    {
        System.Threading.Thread.Sleep(150);
        return ScreenKeyReader.Capture(region);
    });

    private void PickRingColor()
    {
        var color = PickColorHidden();
        if (color == null) return;

        Result.RingColorsArgb.Add(color.Value.ToArgb());
        RefreshRingInfo();
    }

    private void RefreshRingInfo()
    {
        var r = Result;
        _ringZoneButton.Text = r.ZoneWidth > 0 ? $"▭ {r.ZoneWidth}×{r.ZoneHeight}" : "▭ REGION";
        _ringColorButton.Text = r.RingColorsArgb.Count == 0
            ? "◉ RING"
            : $"◉ {Hex(Color.FromArgb(r.RingColorsArgb[0]))}{(r.RingColorsArgb.Count > 1 ? $"+{r.RingColorsArgb.Count - 1}" : "")}";

        _ringInfo.Text = string.IsNullOrEmpty(_lastRing)
            ? "Pick the play area, click RING a few times (light, middle, dark), then TEST with a ring on screen."
            : _lastRing;
    }

    /// <summary>Reads the play area once and reports the rings found and the ball size inside the first one, so the colours can be checked.</summary>
    private void TestRing()
    {
        var r = Result;
        if (r.ZoneWidth <= 0 || r.ZoneHeight <= 0) { _lastRing = "Pick a region first."; RefreshRingInfo(); return; }
        if (r.RingColorsArgb.Count == 0) { _lastRing = "Click RING on a ring first."; RefreshRingInfo(); return; }

        using var bmp = CaptureHidden(r.ZoneRegion);
        var rings = RingGameReader.FindRings(bmp, r.RingColorsArgb.Select(Color.FromArgb).ToList());
        if (rings.Count == 0) { _lastRing = "No ring found: click RING on more shades of it, or check that a ring is on screen."; RefreshRingInfo(); return; }

        var ring = rings[0];
        double? ball = r.BallRadiusPx > 0 ? r.BallRadiusPx : RingGameReader.EstimateBallRadius(bmp, ring.X, ring.Y, ring.OuterRadius - ring.Thickness);
        string ballText = ball is { } b ? $"ball radius {b:F0}px{(r.BallRadiusPx > 0 ? " (fixed)" : " (measured)")}" : "ball size not measurable (enter it in Ball radius)";
        _lastRing = $"{rings.Count} ring(s). First: radius {ring.OuterRadius:F0}px, {ring.Thickness:F0}px thick, {ballText}.";
        RefreshRingInfo();
    }

    private void PickLetterColor()
    {
        var color = PickColorHidden();
        if (color == null) return;

        Result.HasReadColor = true;
        Result.ReadColorArgb = color.Value.ToArgb();
        Result.Glyphs.Clear();   // looks taught under the old reading mode no longer match
        _lastRead = "Letter colour set. Taught looks were cleared, so teach the keys again.";
        RefreshReadInfo();
    }

    private void ClearLetterColor()
    {
        if (!Result.HasReadColor) return;

        Result.HasReadColor = false;
        Result.Glyphs.Clear();
        _lastRead = "Back to automatic. Taught looks were cleared.";
        RefreshReadInfo();
    }

    private void RefreshReadInfo()
    {
        var r = Result;
        bool arrows = r.ReadArrows;
        _regionButton.Text = r.ReadWidth > 0 ? $"▭ {r.ReadWidth}×{r.ReadHeight}" : "▭ REGION";
        _letterColorButton.Text = r.HasReadColor ? $"◉ {Hex(Color.FromArgb(r.ReadColorArgb))}" : "◉ LETTER COLOR";

        // Arrows are recognised by shape alone, so letter colour and teaching don't apply
        _letterColorButton.Enabled = !arrows;
        _teachButton.Enabled = !arrows;
        _clearGlyphsButton.Enabled = !arrows;

        string learned = arrows
            ? "Arrows: shapes are read automatically, a green badge counts as pressed."
            : r.Glyphs.Count == 0
                ? "No learned looks (Windows OCR only)."
                : $"Learned: {string.Join(" ", r.Glyphs.Select(g => g.Key).Distinct().OrderBy(k => k))} ({r.Glyphs.Count} looks).";
        _readInfo.Text = string.IsNullOrEmpty(_lastRead) ? learned : $"{learned}\n{_lastRead}";
    }

    /// <summary>Captures the read region while this dialog is out of the way, so the dialog can't cover what is being read.</summary>
    private Bitmap? CaptureReadRegion()
    {
        if (Result.ReadWidth <= 0 || Result.ReadHeight <= 0)
        {
            _lastRead = "Pick a region first.";
            RefreshReadInfo();
            return null;
        }

        return CaptureHidden(Result.ReadRegion);
    }

    private void TestRead()
    {
        using var bmp = CaptureReadRegion();
        if (bmp == null) return;

        if (Result.ReadArrows)
        {
            _lastRead = $"Reads: {ScreenKeyReader.DescribeArrows(ScreenKeyReader.ReadArrows(bmp))}  (✓ = green)";
            ScreenKeyReader.SaveDebug(bmp, Path.Combine(Path.GetTempPath(), "reflex-keys"), null, arrows: true);
        }
        else
        {
            var read = ScreenKeyReader.Read(bmp, Result.Glyphs, Result.ReadLetterColor);
            ScreenKeyReader.SaveDebug(bmp, Path.Combine(Path.GetTempPath(), "reflex-keys"), Result.ReadLetterColor);
            _lastRead = $"Reads: {read}";
        }
        RefreshReadInfo();
    }

    private void TeachGlyphs()
    {
        using var bmp = CaptureReadRegion();
        if (bmp == null) return;

        string? typed = PromptText("Teach keys", "Which key(s) are shown in the region right now?\nFor example: A  or  S D");
        if (typed == null) return;

        ScreenKeyReader.TryLearn(bmp, typed, Result.Glyphs, out string message, Result.ReadLetterColor);
        _lastRead = message;
        RefreshReadInfo();
    }

    private string? PromptText(string title, string prompt)
    {
        using var dlg = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
            ShowInTaskbar = false, TopMost = true, StartPosition = FormStartPosition.CenterParent,
            BackColor = Theme.BgBase, ForeColor = Theme.TextPrimary, Font = new Font("Segoe UI", 9.25f),
            ClientSize = new Size(340, 140),
        };
        dlg.Controls.Add(new Label
        {
            Text = prompt, Location = new Point(Pad, 12), Size = new Size(312, 40),
            ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f), UseMnemonic = false,
        });
        var box = new TextBox
        {
            Location = new Point(Pad, 58), Size = new Size(312, 26), BackColor = Theme.BgCard, ForeColor = Theme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10f), MaxLength = 12,
        };
        var ok = Theme.CreateButton("OK", Pad, 96, 150, Theme.AccentGreen, height: 30);
        var cancel = Theme.CreateButton("CANCEL", 176, 96, 150, Theme.BgCard, height: 30, isOutline: true);
        ok.Click += (_, _) => dlg.DialogResult = DialogResult.OK;
        cancel.Click += (_, _) => dlg.DialogResult = DialogResult.Cancel;
        dlg.Controls.Add(box);
        dlg.Controls.Add(ok);
        dlg.Controls.Add(cancel);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;
        dlg.HandleCreated += (_, _) =>
        {
            int enabled = 1;
            NativeMethods.DwmSetWindowAttribute(dlg.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
        };

        return dlg.ShowDialog(this) == DialogResult.OK ? box.Text : null;
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
