using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoClicker;

/// <summary>
/// Full-screen overlay for drag-selecting a screen rectangle. Press Escape to cancel.
/// </summary>
public sealed class RegionPickerForm : Form
{
    private Point? _start;
    private Point _current;

    public Rectangle SelectedRegion { get; private set; }

    public RegionPickerForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.Manual;
        Bounds          = SystemInformation.VirtualScreen;
        TopMost         = true;
        ShowInTaskbar   = false;
        BackColor       = Color.Black;
        Opacity         = 0.3;
        Cursor          = Cursors.Cross;
        DoubleBuffered  = true;
        KeyPreview      = true;
    }

    private Rectangle CurrentRect()
    {
        if (_start is null) return Rectangle.Empty;
        int x = Math.Min(_start.Value.X, _current.X);
        int y = Math.Min(_start.Value.Y, _current.Y);
        return new Rectangle(x, y, Math.Abs(_current.X - _start.Value.X), Math.Abs(_current.Y - _start.Value.Y));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _start = e.Location;
        _current = e.Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_start is null) return;
        _current = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _start is null) return;
        var rect = CurrentRect();
        _start = null;

        // A plain click selects a single pixel
        if (rect.Width == 0) rect.Width = 1;
        if (rect.Height == 0) rect.Height = 1;

        SelectedRegion = new Rectangle(PointToScreen(rect.Location), rect.Size);
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var rect = CurrentRect();
        if (rect.Width < 1 || rect.Height < 1) return;
        using var fill = new SolidBrush(Color.FromArgb(120, 74, 222, 128));
        using var pen = new Pen(Color.White, 1);
        e.Graphics.FillRectangle(fill, rect);
        e.Graphics.DrawRectangle(pen, rect);
    }
}
