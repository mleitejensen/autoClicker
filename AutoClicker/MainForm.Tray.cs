using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoClicker;

public partial class MainForm
{
    private void BuildTrayIcon(Icon? appIcon)
    {
        var contextMenu = new ContextMenuStrip
        {
            Renderer        = new DarkMenuRenderer(),
            BackColor       = Theme.BgPanel,
            ShowImageMargin = false,
        };
        var itemShow       = new ToolStripMenuItem("Open Reflex", null, (_, _) => RestoreFromTray());
        var itemStartClick = new ToolStripMenuItem("Start Clicker", null, (_, _) => StartClicking());
        var itemStopClick  = new ToolStripMenuItem("Stop Clicker", null, (_, _) => StopClicking());
        var itemRecMacro   = new ToolStripMenuItem("Record Macro", null, (_, _) => ToggleMacroRecording());
        var itemPlayMacro  = new ToolStripMenuItem("Play Macro", null, (_, _) => ToggleMacroPlayback());
        var itemStopAll    = new ToolStripMenuItem("Stop All", null, (_, _) => StopAll());
        var itemExit       = new ToolStripMenuItem("Exit", null, (_, _) => ForceExit());

        contextMenu.Items.Add(itemShow);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(itemStartClick);
        contextMenu.Items.Add(itemStopClick);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(itemRecMacro);
        contextMenu.Items.Add(itemPlayMacro);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(itemStopAll);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(itemExit);

        _trayIcon = new NotifyIcon
        {
            Icon             = appIcon ?? SystemIcons.Application,
            Text             = "Reflex",
            Visible          = true,
            ContextMenuStrip = contextMenu,
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
    }

    private void ForceExit()
    {
        _closeToTrayCheck.Checked = false;
        Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && _closeToTrayCheck.Checked)
        {
            e.Cancel = true;
            Hide();
            _trayIcon.ShowBalloonTip(1200, "Reflex", "Minimized to system tray. Double-click to restore, or use your hotkeys.", ToolTipIcon.Info);
            return;
        }

        StopAll();
        UnregisterHotkeys();
        SaveSettings();
        _highResTimer.Dispose();
        _inputRecorder.Dispose();
        _macroPlayer.Dispose();
        _routineRunner.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }
}
