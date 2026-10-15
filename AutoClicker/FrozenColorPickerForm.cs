using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace AutoClicker;

/// <summary>
/// Colour picker that works on a frozen snapshot of the screen, so moving things (a sliding marker, an animated bar)
/// can be picked at leisure. Click to pick, Escape to cancel; a magnifier shows the pixel under the cursor.
/// </summary>
public sealed class FrozenColorPickerForm : Form
{
    private const int Zoom = 11, Cell = 10;   // magnifier shows Zoom x Zoom source pixels, each Cell px wide

    private readonly Bitmap _snapshot;
    private Point _mouse;

    public Color? SelectedColor { get; private set; }

    /// <summary>Captures the screen immediately, so create it only once everything that shouldn't be in the picture is out of the way.</summary>
    public FrozenColorPickerForm()
    {
        var screen = SystemInformation.VirtualScreen;
        _snapshot = new Bitmap(screen.Width, screen.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(_snapshot))
            g.CopyFromScreen(screen.Location, Point.Empty, screen.Size);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition   = FormStartPosition.Manual;
        Bounds          = screen;
        TopMost         = true;
        ShowInTaskbar   = false;
        Cursor          = Cursors.Cross;
        DoubleBuffered  = true;
        KeyPreview      = true;
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _mouse = e.Location;
        Invalidate();
        base.OnMouseMove(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && InSnapshot(e.Location))
        {
            SelectedColor = _snapshot.GetPixel(e.X, e.Y);
            DialogResult = DialogResult.OK;
            Close();
            return;
        }
        base.OnMouseClick(e);
    }

    private bool InSnapshot(Point p) => p.X >= 0 && p.Y >= 0 && p.X < _snapshot.Width && p.Y < _snapshot.Height;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.DrawImageUnscaled(_snapshot, 0, 0);
        if (!InSnapshot(_mouse)) return;

        int size = Zoom * Cell;
        int x = _mouse.X + 24, y = _mouse.Y + 24;
        if (x + size + 4 > ClientSize.Width) x = _mouse.X - 24 - size;
        if (y + size + 30 > ClientSize.Height) y = _mouse.Y - 24 - size - 26;

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        var dest = new Rectangle(x, y, size, size);
        var src = new Rectangle(_mouse.X - Zoom / 2, _mouse.Y - Zoom / 2, Zoom, Zoom);
        g.DrawImage(_snapshot, dest, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel);

        var centre = new Rectangle(x + (Zoom / 2) * Cell, y + (Zoom / 2) * Cell, Cell, Cell);
        using (var outer = new Pen(Color.Black, 3)) g.DrawRectangle(outer, centre);
        using (var inner = new Pen(Color.White, 1)) g.DrawRectangle(inner, centre);
        using (var frame = new Pen(Color.White, 2)) g.DrawRectangle(frame, dest);

        var c = _snapshot.GetPixel(_mouse.X, _mouse.Y);
        var label = new Rectangle(x, y + size + 4, size, 20);
        using (var back = new SolidBrush(Color.FromArgb(230, 20, 20, 20))) g.FillRectangle(back, label);
        using (var swatch = new SolidBrush(c)) g.FillRectangle(swatch, label.X + 3, label.Y + 3, 14, 14);
        using var font = new Font("Segoe UI", 8.5f);
        g.DrawString($"#{c.R:X2}{c.G:X2}{c.B:X2}  click to pick", font, Brushes.White, label.X + 22, label.Y + 2);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _snapshot.Dispose();
        base.Dispose(disposing);
    }
}
