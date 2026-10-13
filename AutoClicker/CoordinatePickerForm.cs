using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoClicker;

/// <summary>
/// A transparent, full-screen overlay that captures a screen coordinate with a single click.
/// Press Escape to cancel.
/// </summary>
public sealed class CoordinatePickerForm : Form
{
    private Point? _selectedPoint;
    public Point? SelectedPoint => _selectedPoint;

    public CoordinatePickerForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState     = FormWindowState.Maximized;
        TopMost         = true;
        ShowInTaskbar   = false;
        BackColor       = Color.Black;
        Opacity         = 0.25;
        Cursor          = Cursors.Cross;
        DoubleBuffered  = true;
        KeyPreview      = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            _selectedPoint = null;
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _selectedPoint = Cursor.Position;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }
        base.OnMouseClick(e);
    }
}
