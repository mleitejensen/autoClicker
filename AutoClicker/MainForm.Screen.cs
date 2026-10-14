using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace AutoClicker;

public partial class MainForm
{
    private sealed class ConditionRow
    {
        public ScreenCondition Condition = new();
        public CheckBox Enabled = null!;
        public NumericUpDown MinPercent = null!;
        public Button RegionButton = null!;
        public Button ColorButton = null!;
        public Label Readout = null!;
    }

    private readonly List<ConditionRow> _conditionRows = new();
    private NumericUpDown _screenToleranceNumeric = null!;
    private ComboBox _screenModeCombo = null!;
    private readonly System.Windows.Forms.Timer _screenReadoutTimer = new() { Interval = 300 };

    private void AddScreenConditionsCard(int pad, int innerPad, int cardWidth, ref int my)
    {
        var card = Theme.CreateCard(pad, my, cardWidth, 186, Theme.AccentYellow);
        card.Controls.Add(Theme.CreateTagLabel("SCREEN CONDITIONS", innerPad, 10, Theme.AccentYellow));

        string[] names = { "Energy", "Health" };
        for (int k = 0; k < names.Length; k++)
        {
            var row = new ConditionRow { Condition = new ScreenCondition { Name = names[k], MinPercent = k == 0 ? 50 : 30 } };
            int y = 32 + k * 58;

            row.Enabled = Theme.CreateCheckBox($"{names[k]} bar", innerPad, y);
            var minLabel = new Label { Text = "Min", Location = new Point(196, y + 3), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
            row.MinPercent = Theme.CreateNumeric(222, y, 58, row.Condition.MinPercent, 1, 100, 5);
            var pctLabel = new Label { Text = "%", Location = new Point(284, y + 3), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };

            row.RegionButton = Theme.CreateButton("▭ REGION", innerPad, y + 28, 100, Theme.AccentYellow, height: 24, isOutline: true);
            row.ColorButton  = Theme.CreateButton("◉ COLOR", innerPad + 106, y + 28, 100, Theme.AccentYellow, height: 24, isOutline: true);
            row.RegionButton.Font = new Font("Segoe UI Semibold", 7.75f);
            row.ColorButton.Font  = new Font("Segoe UI Semibold", 7.75f);
            row.Readout = new Label
            {
                Text = "NOT SET", Location = new Point(innerPad + 214, y + 31), Size = new Size(96, 18),
                ForeColor = Theme.TextMuted, Font = new Font("Segoe UI Semibold", 8f), UseMnemonic = false,
            };

            var captured = row;
            row.MinPercent.ValueChanged += (_, _) => captured.Condition.MinPercent = (int)captured.MinPercent.Value;
            row.Enabled.CheckedChanged  += (_, _) => captured.Condition.Enabled = captured.Enabled.Checked;
            row.RegionButton.Click += (_, _) => PickConditionRegion(captured);
            row.ColorButton.Click  += (_, _) => PickConditionColor(captured);

            foreach (Control c in new Control[] { row.Enabled, minLabel, row.MinPercent, pctLabel, row.RegionButton, row.ColorButton, row.Readout })
                card.Controls.Add(c);
            _conditionRows.Add(row);
        }

        var tolLabel = new Label { Text = "Tolerance", Location = new Point(innerPad, 152), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _screenToleranceNumeric = Theme.CreateNumeric(innerPad + 62, 148, 56, 40, 0, 255, 5);
        _screenModeCombo = Theme.CreateCombo(innerPad + 132, 148, 172);
        _screenModeCombo.Items.AddRange(new object[] { "Wait until met", "Stop if not met" });
        _screenModeCombo.SelectedIndex = 0;

        card.Controls.Add(tolLabel);
        card.Controls.Add(_screenToleranceNumeric);
        card.Controls.Add(_screenModeCombo);

        _macroContainerPanel.Controls.Add(card);
        my += card.Height + 10;

        _screenReadoutTimer.Tick += (_, _) => UpdateConditionReadouts();
        _screenReadoutTimer.Start();
    }

    private void PickConditionRegion(ConditionRow row)
    {
        using var picker = new RegionPickerForm();
        if (picker.ShowDialog(this) == DialogResult.OK)
        {
            row.Condition.Region = picker.SelectedRegion;
            UpdateConditionReadouts();
        }
    }

    private void PickConditionColor(ConditionRow row)
    {
        using var picker = new CoordinatePickerForm();
        if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedPoint.HasValue)
        {
            // Give the overlay time to disappear before sampling the real screen
            Application.DoEvents();
            Thread.Sleep(120);
            row.Condition.FillColor = ScreenCondition.SamplePixel(picker.SelectedPoint.Value);
            UpdateConditionReadouts();
        }
    }

    private void UpdateConditionReadouts()
    {
        if (!_macroContainerPanel.Visible || WindowState == FormWindowState.Minimized) return;

        int tol = (int)_screenToleranceNumeric.Value;
        foreach (var row in _conditionRows)
        {
            var c = row.Condition;
            row.RegionButton.Text = c.RegionWidth > 0 ? $"▭ {c.RegionWidth}×{c.RegionHeight}" : "▭ REGION";
            row.ColorButton.Text  = c.HasColor ? $"◉ #{c.FillColor.R:X2}{c.FillColor.G:X2}{c.FillColor.B:X2}" : "◉ COLOR";

            double level = c.Measure(tol);
            if (level < 0)
            {
                row.Readout.Text = "NOT SET";
                row.Readout.ForeColor = Theme.TextMuted;
            }
            else
            {
                bool met = level >= c.MinPercent;
                row.Readout.Text = $"{level:F0}% {(met ? "✓ MET" : "✗ LOW")}";
                row.Readout.ForeColor = met ? Theme.AccentGreen : Theme.Danger;
            }
        }
    }

    private ScreenGate? BuildScreenGate()
    {
        var active = _conditionRows
            .Select(r => r.Condition)
            .Where(c => c.Enabled && c.IsConfigured)
            .Select(c => c.Clone())
            .ToList();
        if (active.Count == 0) return null;

        return new ScreenGate
        {
            Conditions   = active,
            Tolerance    = (int)_screenToleranceNumeric.Value,
            WaitUntilMet = _screenModeCombo.SelectedIndex == 0,
        };
    }

    private void OnMacroWaitingChanged(string? conditionName)
    {
        if (IsDisposed || Disposing) return;
        try
        {
            BeginInvoke(new Action(() =>
            {
                if (!_macroPlayer.IsPlaying) return;
                if (conditionName is null)
                {
                    _statusLabel.Text = "PLAYING MACRO...";
                    _statusLabel.ForeColor = Theme.AccentGreen;
                }
                else
                {
                    _statusLabel.Text = $"WAITING · {conditionName.ToUpperInvariant()} TOO LOW";
                    _statusLabel.ForeColor = Theme.AccentOrange;
                }
            }));
        }
        catch
        {
            // Form may be closing
        }
    }

    private void LoadScreenSettings(AppSettings s)
    {
        SetClamped(_screenToleranceNumeric, s.ScreenTolerance);
        _screenModeCombo.SelectedIndex = s.ScreenWaitUntilMet ? 0 : 1;

        for (int i = 0; i < _conditionRows.Count && i < s.ScreenConditions.Count; i++)
        {
            var row = _conditionRows[i];
            var saved = s.ScreenConditions[i];
            row.Condition = saved;
            row.Enabled.Checked = saved.Enabled;
            SetClamped(row.MinPercent, saved.MinPercent);
        }
    }

    private void SaveScreenSettings(AppSettings s)
    {
        s.ScreenTolerance = (int)_screenToleranceNumeric.Value;
        s.ScreenWaitUntilMet = _screenModeCombo.SelectedIndex == 0;
        s.ScreenConditions = _conditionRows.Select(r => r.Condition).ToList();
    }
}
