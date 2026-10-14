using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace AutoClicker;

public partial class MainForm
{
    private Panel _routineContainerPanel = null!;
    private ListBox _routineList = null!;
    private NumericUpDown _routineRepeatNumeric = null!;
    private Button _routineStartButton = null!;
    private Button _routineStopButton = null!;
    private RadioButton _tabRoutineRadio = null!;
    private readonly List<RoutineStep> _routineSteps = new();
    private readonly RoutineRunner _routineRunner = new();

    private void AddRoutinePanel(int pad, int innerPad, int cardWidth, int contentStartY)
    {
        int ry = 0;
        _routineContainerPanel = new Panel
        {
            Location  = new Point(0, contentStartY),
            Size      = new Size(cardWidth + pad * 2, 400),
            BackColor = Color.Transparent,
            Visible   = false,
        };

        // --- Steps card ---
        var stepsCard = Theme.CreateCard(pad, ry, cardWidth, 226, Theme.AccentOrange);
        stepsCard.Controls.Add(Theme.CreateTagLabel("ROUTINE STEPS", innerPad, 10, Theme.AccentOrange));

        _routineList = new ListBox
        {
            Location      = new Point(innerPad, 30),
            Size          = new Size(cardWidth - innerPad * 2, 152),
            BackColor     = Theme.BgCard,
            ForeColor     = Theme.TextPrimary,
            BorderStyle   = BorderStyle.FixedSingle,
            Font          = new Font("Segoe UI", 8.75f),
            DrawMode      = DrawMode.OwnerDrawFixed,
            ItemHeight    = 22,
            IntegralHeight = false,
        };
        _routineList.DrawItem += DrawRoutineItem;
        _routineList.DoubleClick += (_, _) => EditRoutineStep();

        var addBtn  = Theme.CreateButton("＋ ADD",  innerPad,       190, 70, Theme.AccentGreen, height: 28, isOutline: true);
        var editBtn = Theme.CreateButton("✎ EDIT",  innerPad + 76,  190, 70, Theme.AccentBlue,  height: 28, isOutline: true);
        var upBtn   = Theme.CreateButton("▲",       innerPad + 152, 190, 40, Theme.BgCard, height: 28, isOutline: true);
        var downBtn = Theme.CreateButton("▼",       innerPad + 198, 190, 40, Theme.BgCard, height: 28, isOutline: true);
        var delBtn  = Theme.CreateButton("✕",       innerPad + 244, 190, 40, Theme.Danger, height: 28, isOutline: true);
        foreach (var b in new[] { addBtn, editBtn, upBtn, downBtn, delBtn }) b.Font = new Font("Segoe UI Semibold", 8f);

        addBtn.Click  += (_, _) => AddRoutineStep();
        editBtn.Click += (_, _) => EditRoutineStep();
        upBtn.Click   += (_, _) => MoveRoutineStep(-1);
        downBtn.Click += (_, _) => MoveRoutineStep(1);
        delBtn.Click  += (_, _) => DeleteRoutineStep();

        stepsCard.Controls.Add(_routineList);
        foreach (var b in new Control[] { addBtn, editBtn, upBtn, downBtn, delBtn }) stepsCard.Controls.Add(b);
        _routineContainerPanel.Controls.Add(stepsCard);
        ry += stepsCard.Height + 10;

        // --- Bars card ---
        AddScreenConditionsCard(_routineContainerPanel, pad, innerPad, cardWidth, ref ry);

        // --- Run card ---
        var runCard = Theme.CreateCard(pad, ry, cardWidth, 150, Theme.AccentGreen);
        runCard.Controls.Add(Theme.CreateTagLabel("RUN", innerPad, 10, Theme.AccentGreen));

        runCard.Controls.Add(new Label
        {
            Text = "Repeat routine", Location = new Point(innerPad, 36), AutoSize = true,
            ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f),
        });
        _routineRepeatNumeric = Theme.CreateNumeric(innerPad + 90, 32, 64, 0, 0, 100000, 1);
        runCard.Controls.Add(_routineRepeatNumeric);
        runCard.Controls.Add(new Label
        {
            Text = "times (0 = forever)", Location = new Point(innerPad + 160, 36), AutoSize = true,
            ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f),
        });

        _routineStartButton = Theme.CreateButton("▶  START ROUTINE", innerPad, 64, 148, Theme.AccentGreen, height: 34, isOutline: true);
        _routineStopButton  = Theme.CreateButton("■  STOP", innerPad + 156, 64, 148, Theme.Danger, height: 34);
        _routineStopButton.Enabled = false;
        _routineStartButton.Click += (_, _) => StartRoutine();
        _routineStopButton.Click  += (_, _) => StopRoutine();
        runCard.Controls.Add(_routineStartButton);
        runCard.Controls.Add(_routineStopButton);

        runCard.Controls.Add(new Label
        {
            Text = "Set each bar's region and color above, then use them in steps.\nThe Play hotkey starts / stops the routine.",
            Location = new Point(innerPad, 106), Size = new Size(cardWidth - innerPad * 2, 34),
            ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 7.75f), UseMnemonic = false,
        });
        _routineContainerPanel.Controls.Add(runCard);
        ry += runCard.Height + 6;

        _routineContainerPanel.Height = ry;
        Controls.Add(_routineContainerPanel);

        _routineRunner.StepChanged += OnRoutineStepChanged;
        _routineRunner.Finished    += OnRoutineFinished;
        RefreshRoutineList();
    }

    private int _routineActiveIndex = -1;

    private void DrawRoutineItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _routineSteps.Count) return;

        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool active = e.Index == _routineActiveIndex;
        using var back = new SolidBrush(selected ? Theme.BgHover : Theme.BgCard);
        e.Graphics.FillRectangle(back, e.Bounds);

        var numColor  = active ? Theme.AccentGreen : Theme.TextMuted;
        var textColor = active ? Theme.AccentGreen : (selected ? Theme.TextPrimary : Theme.TextTertiary);
        using var numBrush  = new SolidBrush(numColor);
        using var textBrush = new SolidBrush(textColor);
        using var numFont   = new Font("Segoe UI Semibold", 8f);
        var sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.EllipsisCharacter };

        e.Graphics.DrawString($"{e.Index + 1}", numFont, numBrush, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, 22, e.Bounds.Height), sf);
        e.Graphics.DrawString(_routineSteps[e.Index].Describe(), e.Font ?? Font, textBrush,
            new Rectangle(e.Bounds.X + 28, e.Bounds.Y, e.Bounds.Width - 32, e.Bounds.Height), sf);
    }

    private void RefreshRoutineList(int? select = null)
    {
        _routineList.BeginUpdate();
        _routineList.Items.Clear();
        foreach (var s in _routineSteps) _routineList.Items.Add(s.Describe());
        _routineList.EndUpdate();
        if (select.HasValue && select.Value >= 0 && select.Value < _routineSteps.Count)
            _routineList.SelectedIndex = select.Value;
    }

    private void AddRoutineStep()
    {
        using var dlg = new RoutineStepForm(null, _routineSteps.Count);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        int at = _routineList.SelectedIndex >= 0 ? _routineList.SelectedIndex + 1 : _routineSteps.Count;
        _routineSteps.Insert(at, dlg.Result);
        RefreshRoutineList(at);
    }

    private void EditRoutineStep()
    {
        int i = _routineList.SelectedIndex;
        if (i < 0) return;

        using var dlg = new RoutineStepForm(_routineSteps[i], _routineSteps.Count);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _routineSteps[i] = dlg.Result;
        RefreshRoutineList(i);
    }

    private void MoveRoutineStep(int delta)
    {
        int i = _routineList.SelectedIndex;
        int j = i + delta;
        if (i < 0 || j < 0 || j >= _routineSteps.Count) return;

        (_routineSteps[i], _routineSteps[j]) = (_routineSteps[j], _routineSteps[i]);
        RefreshRoutineList(j);
    }

    private void DeleteRoutineStep()
    {
        int i = _routineList.SelectedIndex;
        if (i < 0) return;

        _routineSteps.RemoveAt(i);
        RefreshRoutineList(Math.Min(i, _routineSteps.Count - 1));
    }

    private MacroSequence? LoadRoutineMacro(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return _currentMacro;
        try
        {
            return JsonSerializer.Deserialize<MacroSequence>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private void ToggleRoutine()
    {
        if (_routineRunner.IsRunning) StopRoutine(); else StartRoutine();
    }

    private void StartRoutine()
    {
        if (_routineRunner.IsRunning) return;
        if (_routineSteps.Count == 0)
        {
            _statusLabel.Text = "ADD AT LEAST ONE ROUTINE STEP";
            _statusLabel.ForeColor = Theme.AccentOrange;
            return;
        }

        StopAll();
        _routineStartButton.Enabled = false;
        _routineStopButton.Enabled  = true;
        SetRoutineRunningStatus();

        var bars = _conditionRows.Select(r => r.Condition.Clone()).ToList();
        _routineRunner.Start(_routineSteps, bars, (int)_screenToleranceNumeric.Value, (int)_routineRepeatNumeric.Value, LoadRoutineMacro);
    }

    private void SetRoutineRunningStatus()
    {
        _statusLabel.Text = "ROUTINE RUNNING...";
        _statusLabel.ForeColor = Theme.AccentGreen;
    }

    private void StopRoutine()
    {
        if (!_routineRunner.IsRunning) return;
        _routineRunner.Stop();
    }

    private void OnRoutineStepChanged(int index, int total, int loop, string description)
    {
        if (IsDisposed || Disposing) return;
        try
        {
            BeginInvoke(new Action(() =>
            {
                if (!_routineRunner.IsRunning) return;
                _routineActiveIndex = index;
                _routineList.Invalidate();
                _statusLabel.Text = $"STEP {index + 1}/{total} · {description.ToUpperInvariant()}";
                _statusLabel.ForeColor = Theme.AccentGreen;
            }));
        }
        catch
        {
            // Form may be closing
        }
    }

    private void OnRoutineFinished(bool completed, string? error)
    {
        if (IsDisposed || Disposing) return;
        try
        {
            BeginInvoke(new Action(() =>
            {
                _routineActiveIndex = -1;
                _routineList.Invalidate();
                _routineStartButton.Enabled = true;
                _routineStopButton.Enabled  = false;

                if (error != null)
                {
                    _statusLabel.Text = $"ROUTINE STOPPED · {error.ToUpperInvariant()}";
                    _statusLabel.ForeColor = Theme.AccentOrange;
                }
                else if (completed)
                {
                    _statusLabel.Text = "ROUTINE FINISHED";
                    _statusLabel.ForeColor = Theme.AccentBlue;
                }
                else
                {
                    _statusLabel.Text = "ROUTINE STOPPED";
                    _statusLabel.ForeColor = Theme.TextMuted;
                }
            }));
        }
        catch
        {
            // Form may be closing
        }
    }

    private void LoadRoutineSettings(AppSettings s)
    {
        _routineSteps.Clear();
        _routineSteps.AddRange(s.RoutineSteps);
        SetClamped(_routineRepeatNumeric, s.RoutineRepeat);
        RefreshRoutineList();
    }

    private void SaveRoutineSettings(AppSettings s)
    {
        s.RoutineSteps = _routineSteps.Select(x => x.Clone()).ToList();
        s.RoutineRepeat = (int)_routineRepeatNumeric.Value;
    }
}
