using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AutoClicker;

public partial class MainForm
{
    private void BuildUI()
    {
        const int pad = 14;
        const int innerPad = 12;
        const int cardWidth = 328;
        int y = pad;

        BackColor = Theme.BgBase;
        ForeColor = Theme.TextPrimary;
        Font      = new Font("Segoe UI", 9.25f);

        // --- Header Wordmark (design.md Section 3) ---
        var wordmarkPanel = new FlowLayoutPanel
        {
            Location     = new Point(pad, y),
            Size         = new Size(cardWidth, 28),
            BackColor    = Color.Transparent,
            WrapContents = false,
            Margin       = Padding.Empty,
            Padding      = Padding.Empty,
        };

        var wordAuto = new Label
        {
            Text        = "AUTO",
            AutoSize    = true,
            ForeColor   = Theme.TextPrimary,
            Font        = new Font("Segoe UI", 14f, FontStyle.Bold),
            Margin      = new Padding(0, 0, 4, 0),
            UseMnemonic = false,
        };
        var dividerLabel = new Label
        {
            Text        = "│",
            AutoSize    = true,
            ForeColor   = Theme.BorderHover,
            Font        = new Font("Segoe UI", 12f),
            Margin      = new Padding(0, 2, 4, 0),
            UseMnemonic = false,
        };
        var wordClicker = new Label
        {
            Text        = "CLICKER",
            AutoSize    = true,
            ForeColor   = Theme.TextPrimary,
            Font        = new Font("Segoe UI", 14f, FontStyle.Regular),
            Margin      = new Padding(0, 0, 0, 0),
            UseMnemonic = false,
        };

        wordmarkPanel.Controls.Add(wordAuto);
        wordmarkPanel.Controls.Add(dividerLabel);
        wordmarkPanel.Controls.Add(wordClicker);
        Controls.Add(wordmarkPanel);
        y += 28;

        var subtitleLabel = new Label
        {
            Text        = "HIGH-PRECISION AUTOMATION & MACRO RECORDER",
            Location    = new Point(pad + 1, y),
            AutoSize    = true,
            ForeColor   = Theme.TextMuted,
            Font        = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            UseMnemonic = false,
        };
        Controls.Add(subtitleLabel);
        y += 22;

        // --- Mode Selector (Tabs) ---
        const int tabWidth = (cardWidth - 16) / 3;
        _tabClickerRadio = Theme.CreateSegment("⚡ CLICKER", pad, y, tabWidth, true, Theme.AccentBlue);
        _tabMacroRadio   = Theme.CreateSegment("⏺ MACRO", pad + tabWidth + 8, y, tabWidth, false, Theme.AccentOrange);
        _tabRoutineRadio = Theme.CreateSegment("⛓ ROUTINE", pad + (tabWidth + 8) * 2, y, tabWidth, false, Theme.AccentGreen);
        _tabRoutineRadio.Font = new Font("Segoe UI Semibold", 8.5f);
        _tabRoutineRadio.UseMnemonic = false;
        _tabRoutineRadio.CheckedChanged += (_, _) => { if (_tabRoutineRadio.Checked) SwitchTab(AppMode.Routine); };
        Controls.Add(_tabRoutineRadio);
        _tabClickerRadio.Font = new Font("Segoe UI Semibold", 8.5f);
        _tabMacroRadio.Font   = new Font("Segoe UI Semibold", 8.5f);
        _tabClickerRadio.UseMnemonic = false;
        _tabMacroRadio.UseMnemonic   = false;

        _tabClickerRadio.CheckedChanged += (_, _) => { if (_tabClickerRadio.Checked) SwitchTab(AppMode.AutoClicker); };
        _tabMacroRadio.CheckedChanged   += (_, _) => { if (_tabMacroRadio.Checked) SwitchTab(AppMode.Macro); };

        Controls.Add(_tabClickerRadio);
        Controls.Add(_tabMacroRadio);
        y += 38;

        int contentStartY = y;

        // =====================================================================
        // Container 1: Auto Clicker View
        // =====================================================================
        int cy = 0;
        _clickerContainerPanel = new Panel
        {
            Location  = new Point(0, contentStartY),
            Size      = new Size(cardWidth + pad * 2, 600),
            BackColor = Color.Transparent,
        };

        // --- 1. Input Card ---
        var inputCard = Theme.CreateCard(pad, cy, cardWidth, 136, Theme.AccentBlue);
        var inputHeader = Theme.CreateTagLabel("INPUT", innerPad, 10, Theme.AccentBlue);
        inputCard.Controls.Add(inputHeader);

        _mouseRadio    = Theme.CreateSegment("MOUSE",    innerPad,           28, 148, true,  Theme.AccentBlue);
        _keyboardRadio = Theme.CreateSegment("KEYBOARD", innerPad + 148 + 8, 28, 148, false, Theme.AccentBlue);
        inputCard.Controls.Add(_mouseRadio);
        inputCard.Controls.Add(_keyboardRadio);

        // Mouse options sub-panel
        _mouseOptionsPanel = new Panel
        {
            Location  = new Point(innerPad, 66),
            Size      = new Size(304, 60),
            BackColor = Color.Transparent,
        };
        var btnLabel = new Label { Text = "Button", Location = new Point(0, 6), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _mouseButtonCombo = Theme.CreateCombo(58, 2, 100);
        _mouseButtonCombo.Items.AddRange(new object[] { "Left", "Right", "Middle" });
        _mouseButtonCombo.SelectedIndex = 0;

        var typeLabel = new Label { Text = "Type", Location = new Point(170, 6), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _mouseClickTypeCombo = Theme.CreateCombo(208, 2, 96);
        _mouseClickTypeCombo.Items.AddRange(new object[] { "Single", "Double" });
        _mouseClickTypeCombo.SelectedIndex = 0;

        _mouseOptionsPanel.Controls.Add(btnLabel);
        _mouseOptionsPanel.Controls.Add(_mouseButtonCombo);
        _mouseOptionsPanel.Controls.Add(typeLabel);
        _mouseOptionsPanel.Controls.Add(_mouseClickTypeCombo);
        inputCard.Controls.Add(_mouseOptionsPanel);

        // Keyboard options sub-panel
        _keyboardOptionsPanel = new Panel
        {
            Location  = new Point(innerPad, 66),
            Size      = new Size(304, 60),
            BackColor = Color.Transparent,
            Visible   = false,
        };
        var keyLabel = new Label { Text = "Key", Location = new Point(0, 6), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _keyCombo = Theme.CreateCombo(38, 2, 118);
        foreach (var k in KeyRegistry.SimulationKeys.Keys) _keyCombo.Items.Add(k);
        _keyCombo.SelectedIndex = 0;

        _keyCtrlCheck  = Theme.CreateCheckBox("Ctrl",  168, 6);
        _keyShiftCheck = Theme.CreateCheckBox("Shift", 220, 6);
        _keyAltCheck   = Theme.CreateCheckBox("Alt",   38,  34);

        _keyboardOptionsPanel.Controls.Add(keyLabel);
        _keyboardOptionsPanel.Controls.Add(_keyCombo);
        _keyboardOptionsPanel.Controls.Add(_keyCtrlCheck);
        _keyboardOptionsPanel.Controls.Add(_keyShiftCheck);
        _keyboardOptionsPanel.Controls.Add(_keyAltCheck);
        inputCard.Controls.Add(_keyboardOptionsPanel);

        _mouseRadio.CheckedChanged += (_, _) =>
        {
            _mouseOptionsPanel.Visible    = _mouseRadio.Checked;
            _keyboardOptionsPanel.Visible = !_mouseRadio.Checked;
        };
        _clickerContainerPanel.Controls.Add(inputCard);
        cy += inputCard.Height + 10;

        // --- 2. Location Card ---
        var locationCard = Theme.CreateCard(pad, cy, cardWidth, 126, Theme.AccentPurple);
        var locHeader = Theme.CreateTagLabel("CLICK LOCATION", innerPad, 10, Theme.AccentPurple);
        locationCard.Controls.Add(locHeader);

        _locationCursorRadio = new DarkRadioButton
        {
            Text      = "Cursor",
            Location  = new Point(innerPad, 30),
            AutoSize  = true,
            Checked   = true,
            ForeColor = Theme.TextPrimary,
            Cursor    = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 8.5f),
        };
        _locationFixedRadio = new DarkRadioButton
        {
            Text      = "Fixed (X, Y)",
            Location  = new Point(innerPad + 80, 30),
            AutoSize  = true,
            ForeColor = Theme.TextPrimary,
            Cursor    = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 8.5f),
        };
        locationCard.Controls.Add(_locationCursorRadio);
        locationCard.Controls.Add(_locationFixedRadio);

        _fixedLocationPanel = new Panel
        {
            Location  = new Point(innerPad, 56),
            Size      = new Size(304, 32),
            BackColor = Color.Transparent,
            Visible   = false,
        };
        var xLabel = new Label { Text = "X", Location = new Point(0, 5), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _fixedXNumeric = Theme.CreateNumeric(18, 2, 70, 0, min: 0, max: 10000, increment: 10);
        var yLabel = new Label { Text = "Y", Location = new Point(96, 5), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _fixedYNumeric = Theme.CreateNumeric(114, 2, 70, 0, min: 0, max: 10000, increment: 10);
        _pickCoordsButton = Theme.CreateButton("PICK", 196, 1, 100, Theme.BgCard, height: 26, isOutline: true);
        _pickCoordsButton.Font = new Font("Segoe UI Semibold", 8f);
        _pickCoordsButton.Click += (_, _) => PickFixedCoordinates();

        _fixedLocationPanel.Controls.Add(xLabel);
        _fixedLocationPanel.Controls.Add(_fixedXNumeric);
        _fixedLocationPanel.Controls.Add(yLabel);
        _fixedLocationPanel.Controls.Add(_fixedYNumeric);
        _fixedLocationPanel.Controls.Add(_pickCoordsButton);
        locationCard.Controls.Add(_fixedLocationPanel);

        // Jitter row
        _jitterCheck = Theme.CreateCheckBox("Position jitter (± px):", innerPad, 94);
        _jitterNumeric = Theme.CreateNumeric(220, 92, 76, 5, min: 1, max: 100, increment: 1);
        _jitterNumeric.Enabled = false;
        _jitterCheck.CheckedChanged += (_, _) => _jitterNumeric.Enabled = _jitterCheck.Checked;

        locationCard.Controls.Add(_jitterCheck);
        locationCard.Controls.Add(_jitterNumeric);

        _locationFixedRadio.CheckedChanged += (_, _) =>
        {
            _fixedLocationPanel.Visible = _locationFixedRadio.Checked;
        };
        _clickerContainerPanel.Controls.Add(locationCard);
        cy += locationCard.Height + 10;

        // --- 3. Interval Card ---
        _intervalCard = Theme.CreateCard(pad, cy, cardWidth, 114, Theme.AccentGreen);
        var intervalHeader = Theme.CreateTagLabel("INTERVAL", innerPad, 10, Theme.AccentGreen);
        _intervalCard.Controls.Add(intervalHeader);

        var intervalLabel = new Label
        {
            Text      = "Interval (ms)",
            Location  = new Point(innerPad, 30),
            AutoSize  = true,
            ForeColor = Theme.TextMuted,
            Font      = new Font("Segoe UI", 8.5f),
        };
        _intervalNumeric = Theme.CreateNumeric(110, 27, 86, 100, min: 1, max: 3_600_000, increment: 10);
        _randomIntervalCheckBox = Theme.CreateCheckBox("Random", 212, 29);

        _intervalRangePanel = new Panel
        {
            Location  = new Point(innerPad, 64),
            Size      = new Size(304, 36),
            BackColor = Color.Transparent,
            Visible   = false,
        };
        var minLabel = new Label { Text = "Min", Location = new Point(0, 5), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _minimumIntervalNumeric = Theme.CreateNumeric(32, 2, 88, 100, min: 1, max: 3_600_000, increment: 10);
        var maxLabel = new Label { Text = "Max", Location = new Point(130, 5), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _maximumIntervalNumeric = Theme.CreateNumeric(164, 2, 88, 300, min: 1, max: 3_600_000, increment: 10);

        _minimumIntervalNumeric.ValueChanged += (_, _) =>
        {
            if (_minimumIntervalNumeric.Value > _maximumIntervalNumeric.Value)
                _maximumIntervalNumeric.Value = _minimumIntervalNumeric.Value;
        };
        _maximumIntervalNumeric.ValueChanged += (_, _) =>
        {
            if (_maximumIntervalNumeric.Value < _minimumIntervalNumeric.Value)
                _minimumIntervalNumeric.Value = _maximumIntervalNumeric.Value;
        };

        _intervalRangePanel.Controls.Add(minLabel);
        _intervalRangePanel.Controls.Add(_minimumIntervalNumeric);
        _intervalRangePanel.Controls.Add(maxLabel);
        _intervalRangePanel.Controls.Add(_maximumIntervalNumeric);

        var randomHint = new Label
        {
            Text        = "High-res precision (~0.5 ms). Tick Random to vary delay.",
            Location    = new Point(innerPad, 72),
            AutoSize    = true,
            ForeColor   = Theme.TextMuted,
            Font        = new Font("Segoe UI", 7.75f),
            UseMnemonic = false,
        };

        _randomIntervalCheckBox.CheckedChanged += (_, _) =>
        {
            bool random = _randomIntervalCheckBox.Checked;
            _intervalNumeric.Enabled     = !random;
            _intervalRangePanel.Visible  = random;
            randomHint.Visible           = !random;
        };

        _intervalCard.Controls.Add(intervalLabel);
        _intervalCard.Controls.Add(_intervalNumeric);
        _intervalCard.Controls.Add(_randomIntervalCheckBox);
        _intervalCard.Controls.Add(_intervalRangePanel);
        _intervalCard.Controls.Add(randomHint);
        _clickerContainerPanel.Controls.Add(_intervalCard);
        cy += _intervalCard.Height + 10;

        // --- 4. Limits & Timing Card ---
        var optionsCard = Theme.CreateCard(pad, cy, cardWidth, 108, Theme.AccentOrange);
        var limitsHeader = Theme.CreateTagLabel("LIMITS & TIMING", innerPad, 10, Theme.AccentOrange);
        optionsCard.Controls.Add(limitsHeader);

        var delayLabel = new Label
        {
            Text      = "Start delay (s)",
            Location  = new Point(innerPad, 32),
            AutoSize  = true,
            ForeColor = Theme.TextMuted,
            Font      = new Font("Segoe UI", 8.5f),
        };
        _startDelayNumeric = Theme.CreateNumeric(110, 29, 86, 0, min: 0, max: 60, increment: 1);
        _holdCheckBox      = Theme.CreateCheckBox("Hold", 212, 31);

        _limitCheckBox  = Theme.CreateCheckBox("Stop after", innerPad, 72);
        _limitNumeric   = Theme.CreateNumeric(110, 70, 86, 100, min: 1, max: 1_000_000, increment: 1);
        _limitUnitCombo = Theme.CreateCombo(206, 69, 96);
        _limitUnitCombo.Items.AddRange(new object[] { "clicks", "seconds" });
        _limitUnitCombo.SelectedIndex = 0;

        _holdCheckBox.CheckedChanged += (_, _) =>
        {
            bool hold = _holdCheckBox.Checked;
            _intervalCard.Enabled   = !hold;
            if (hold) _limitUnitCombo.SelectedIndex = 1; // seconds
            _limitUnitCombo.Enabled = !hold;
        };

        optionsCard.Controls.Add(delayLabel);
        optionsCard.Controls.Add(_startDelayNumeric);
        optionsCard.Controls.Add(_holdCheckBox);
        optionsCard.Controls.Add(_limitCheckBox);
        optionsCard.Controls.Add(_limitNumeric);
        optionsCard.Controls.Add(_limitUnitCombo);
        _clickerContainerPanel.Controls.Add(optionsCard);
        cy += optionsCard.Height + 10;

        // --- 5. Hotkeys & Safety Card ---
        var hotkeysCard = Theme.CreateCard(pad, cy, cardWidth, 134, Theme.AccentYellow);
        var hkMainHeader = Theme.CreateTagLabel("HOTKEYS & SAFETY", innerPad, 10, Theme.AccentYellow);
        hotkeysCard.Controls.Add(hkMainHeader);

        var hkToggleLabel = new Label { Text = "Toggle", Location = new Point(innerPad, 32), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _hotkeyStartCombo = Theme.CreateCombo(64, 28, 80);
        foreach (var k in KeyRegistry.HotkeyChoices) _hotkeyStartCombo.Items.Add(k);
        _hotkeyStartCombo.SelectedItem = "F6";

        var hkStopLabel = new Label { Text = "Stop", Location = new Point(156, 32), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _hotkeyStopCombo = Theme.CreateCombo(196, 28, 80);
        foreach (var k in KeyRegistry.HotkeyChoices) _hotkeyStopCombo.Items.Add(k);
        _hotkeyStopCombo.SelectedItem = "F7";

        _hotkeyStartCombo.SelectedIndexChanged += (_, _) => RegisterConfiguredHotkeys();
        _hotkeyStopCombo.SelectedIndexChanged  += (_, _) => RegisterConfiguredHotkeys();

        _alwaysOnTopCheck = Theme.CreateCheckBox("Always on top", innerPad, 68);
        _alwaysOnTopCheck.CheckedChanged += (_, _) => TopMost = _alwaysOnTopCheck.Checked;

        _pauseOnMoveCheck = Theme.CreateCheckBox("Pause on move", 156, 68);

        _closeToTrayCheck = Theme.CreateCheckBox("Close / minimize to tray", innerPad, 98, isChecked: true);

        hotkeysCard.Controls.Add(hkToggleLabel);
        hotkeysCard.Controls.Add(_hotkeyStartCombo);
        hotkeysCard.Controls.Add(hkStopLabel);
        hotkeysCard.Controls.Add(_hotkeyStopCombo);
        hotkeysCard.Controls.Add(_alwaysOnTopCheck);
        hotkeysCard.Controls.Add(_pauseOnMoveCheck);
        hotkeysCard.Controls.Add(_closeToTrayCheck);
        _clickerContainerPanel.Controls.Add(hotkeysCard);
        cy += hotkeysCard.Height + 14;

        // --- Action Buttons ---
        _startButton = Theme.CreateButton("▶  START", pad, cy, 160, Theme.AccentGreen, height: 42, isOutline: true);
        _stopButton  = Theme.CreateButton("■  STOP",  pad + 168, cy, 160, Theme.Danger, height: 42);
        _stopButton.Enabled = false;

        _startButton.Click += (_, _) => StartClicking();
        _stopButton.Click  += (_, _) => StopClicking();

        _clickerContainerPanel.Controls.Add(_startButton);
        _clickerContainerPanel.Controls.Add(_stopButton);
        cy += _startButton.Height + 6;

        _clickerContainerPanel.Height = cy;
        Controls.Add(_clickerContainerPanel);

        // =====================================================================
        // Container 2: TinyTask Macro View
        // =====================================================================
        int my = 0;
        _macroContainerPanel = new Panel
        {
            Location  = new Point(0, contentStartY),
            Size      = new Size(cardWidth + pad * 2, 500),
            BackColor = Color.Transparent,
            Visible   = false,
        };

        // --- Card 1: Record & Playback ---
        var macroActionCard = Theme.CreateCard(pad, my, cardWidth, 126, Theme.Danger);
        var recPlayHeader = Theme.CreateTagLabel("RECORD & PLAYBACK", innerPad, 10, Color.FromArgb(248, 113, 113));
        macroActionCard.Controls.Add(recPlayHeader);

        _macroRecButton  = Theme.CreateButton("⏺  RECORD (F8)", innerPad, 28, 148, Theme.Danger, height: 42, isOutline: true);
        _macroPlayButton = Theme.CreateButton("▶  PLAY (F9)", innerPad + 148 + 8, 28, 148, Theme.AccentGreen, height: 42, isOutline: true);
        _macroPlayButton.Enabled = false;

        _macroRecButton.Click  += (_, _) => ToggleMacroRecording();
        _macroPlayButton.Click += (_, _) => ToggleMacroPlayback();

        macroActionCard.Controls.Add(_macroRecButton);
        macroActionCard.Controls.Add(_macroPlayButton);

        _macroInfoLabel = new Label
        {
            Text        = "NO MACRO RECORDED YET",
            Location    = new Point(innerPad, 86),
            Size        = new Size(220, 24),
            ForeColor   = Theme.TextTertiary,
            TextAlign   = ContentAlignment.MiddleLeft,
            Font        = new Font("Segoe UI", 8.25f),
            UseMnemonic = false,
        };
        _macroClearButton = Theme.CreateButton("✕ CLEAR", cardWidth - innerPad - 68, 84, 68, Theme.BgCard, height: 26, isOutline: true);
        _macroClearButton.Font = new Font("Segoe UI Semibold", 8f);
        _macroClearButton.Enabled = false;
        _macroClearButton.Click += (_, _) => ClearMacro();

        macroActionCard.Controls.Add(_macroInfoLabel);
        macroActionCard.Controls.Add(_macroClearButton);
        _macroContainerPanel.Controls.Add(macroActionCard);
        my += macroActionCard.Height + 10;

        // --- Card 2: Playback Settings ---
        var macroSettingsCard = Theme.CreateCard(pad, my, cardWidth, 112, Theme.AccentOrange);
        var playSettingsHeader = Theme.CreateTagLabel("PLAYBACK SETTINGS", innerPad, 10, Theme.AccentOrange);
        macroSettingsCard.Controls.Add(playSettingsHeader);

        var speedLabel = new Label { Text = "Speed", Location = new Point(innerPad, 32), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _macroSpeedCombo = Theme.CreateCombo(innerPad + 44, 28, 80);
        _macroSpeedCombo.Items.AddRange(new object[] { "0.5x", "1.0x", "2.0x", "5.0x", "10.0x", "Max" });
        _macroSpeedCombo.SelectedIndex = 1;

        var repeatLabel = new Label { Text = "Repeat", Location = new Point(innerPad + 140, 32), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _macroRepeatCombo = Theme.CreateCombo(innerPad + 186, 28, 118);
        _macroRepeatCombo.Items.AddRange(new object[] { "1x", "2x", "3x", "5x", "10x", "Continuous" });
        _macroRepeatCombo.SelectedIndex = 0;

        macroSettingsCard.Controls.Add(speedLabel);
        macroSettingsCard.Controls.Add(_macroSpeedCombo);
        macroSettingsCard.Controls.Add(repeatLabel);
        macroSettingsCard.Controls.Add(_macroRepeatCombo);

        _macroMouseMoveCheck   = Theme.CreateCheckBox("Record mouse moves", innerPad, 72, isChecked: true);
        _macroPauseOnMoveCheck = Theme.CreateCheckBox("Pause on move", innerPad + 160, 72, isChecked: true);

        macroSettingsCard.Controls.Add(_macroMouseMoveCheck);
        macroSettingsCard.Controls.Add(_macroPauseOnMoveCheck);
        _macroContainerPanel.Controls.Add(macroSettingsCard);
        my += macroSettingsCard.Height + 10;

        // --- Card 3: Macro File (Save / Load) ---
        var macroFileCard = Theme.CreateCard(pad, my, cardWidth, 96, Theme.AccentBlue);
        var fileHeader = Theme.CreateTagLabel("MACRO FILE", innerPad, 10, Theme.AccentBlue);
        macroFileCard.Controls.Add(fileHeader);

        _macroLoadButton = Theme.CreateButton("📂  OPEN MACRO...", innerPad, 28, 148, Theme.BgCard, height: 32, isOutline: true);
        _macroLoadButton.Font = new Font("Segoe UI Semibold", 8.25f);
        _macroLoadButton.Click += (_, _) => LoadMacroFromFile();

        _macroSaveButton = Theme.CreateButton("💾  SAVE MACRO...", innerPad + 148 + 8, 28, 148, Theme.BgCard, height: 32, isOutline: true);
        _macroSaveButton.Font = new Font("Segoe UI Semibold", 8.25f);
        _macroSaveButton.Enabled = false;
        _macroSaveButton.Click += (_, _) => SaveMacroToFile();

        _macroFileLabel = new Label
        {
            Text        = "No file loaded",
            Location    = new Point(innerPad, 68),
            Size        = new Size(cardWidth - innerPad * 2, 18),
            ForeColor   = Theme.TextMuted,
            Font        = new Font("Segoe UI", 8f),
            UseMnemonic = false,
        };

        macroFileCard.Controls.Add(_macroLoadButton);
        macroFileCard.Controls.Add(_macroSaveButton);
        macroFileCard.Controls.Add(_macroFileLabel);
        _macroContainerPanel.Controls.Add(macroFileCard);
        my += macroFileCard.Height + 10;


        // --- Card 4: Macro Hotkeys ---
        var macroHotkeysCard = Theme.CreateCard(pad, my, cardWidth, 96, Theme.AccentPurple);
        var hkHeader = Theme.CreateTagLabel("HOTKEYS", innerPad, 10, Theme.AccentPurple);
        macroHotkeysCard.Controls.Add(hkHeader);

        var hkRecLabel = new Label { Text = "Rec", Location = new Point(innerPad, 32), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _hotkeyRecCombo = Theme.CreateCombo(innerPad + 32, 28, 92);
        foreach (var k in KeyRegistry.HotkeyChoices) _hotkeyRecCombo.Items.Add(k);
        _hotkeyRecCombo.SelectedItem = "F8";
        _hotkeyRecCombo.SelectedIndexChanged += (_, _) => { RegisterConfiguredHotkeys(); UpdateMacroButtonLabels(); };

        var hkPlayLabel = new Label { Text = "Play", Location = new Point(innerPad + 136, 32), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 8.5f) };
        _hotkeyPlayCombo = Theme.CreateCombo(innerPad + 172, 28, 92);
        foreach (var k in KeyRegistry.HotkeyChoices) _hotkeyPlayCombo.Items.Add(k);
        _hotkeyPlayCombo.SelectedItem = "F9";
        _hotkeyPlayCombo.SelectedIndexChanged += (_, _) => { RegisterConfiguredHotkeys(); UpdateMacroButtonLabels(); };

        var hkHintLabel = new Label
        {
            Text        = "F7 stops clicking, recording, or playback globally",
            Location    = new Point(innerPad, 68),
            AutoSize    = true,
            ForeColor   = Theme.TextMuted,
            Font        = new Font("Segoe UI", 7.75f),
            UseMnemonic = false,
        };

        macroHotkeysCard.Controls.Add(hkRecLabel);
        macroHotkeysCard.Controls.Add(_hotkeyRecCombo);
        macroHotkeysCard.Controls.Add(hkPlayLabel);
        macroHotkeysCard.Controls.Add(_hotkeyPlayCombo);
        macroHotkeysCard.Controls.Add(hkHintLabel);
        _macroContainerPanel.Controls.Add(macroHotkeysCard);
        my += macroHotkeysCard.Height + 6;

        _macroContainerPanel.Height = my;
        Controls.Add(_macroContainerPanel);

        AddRoutinePanel(pad, innerPad, cardWidth, contentStartY);

        // --- Status Badge ---
        _statusLabel = new StatusBadgeLabel
        {
            Text      = "READY",
            Location  = new Point(pad, contentStartY + _clickerContainerPanel.Height + 8),
            Size      = new Size(cardWidth, 28),
            ForeColor = Theme.TextMuted,
        };
        Controls.Add(_statusLabel);

        SwitchTab(AppMode.AutoClicker);
    }

    private bool _isSwitchingTab;

    private void SwitchTab(AppMode mode)
    {
        if (_isSwitchingTab) return;
        _isSwitchingTab = true;
        try
        {
            _currentMode = mode;
            if (IsHandleCreated) RegisterConfiguredHotkeys();
            bool isClicker = mode == AppMode.AutoClicker;

            _tabClickerRadio.Checked = isClicker;
            _tabMacroRadio.Checked   = mode == AppMode.Macro;
            _tabRoutineRadio.Checked = mode == AppMode.Routine;

            _clickerContainerPanel.Visible = isClicker;
            _macroContainerPanel.Visible   = mode == AppMode.Macro;
            _routineContainerPanel.Visible = mode == AppMode.Routine;

            int activeHeight = mode switch
            {
                AppMode.Macro   => _macroContainerPanel.Height,
                AppMode.Routine => _routineContainerPanel.Height,
                _               => _clickerContainerPanel.Height,
            };
            _statusLabel.Location = new Point(14, _clickerContainerPanel.Top + activeHeight + 8);
            _statusLabel.BringToFront();

            ClientSize = new Size(356, _statusLabel.Bottom + 14);
        }
        finally
        {
            _isSwitchingTab = false;
        }
    }

    private void PickFixedCoordinates()
    {
        using var picker = new CoordinatePickerForm();
        if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedPoint.HasValue)
        {
            var pt = picker.SelectedPoint.Value;
            SetClamped(_fixedXNumeric, pt.X);
            SetClamped(_fixedYNumeric, pt.Y);
            _locationFixedRadio.Checked = true;
        }
    }
}

