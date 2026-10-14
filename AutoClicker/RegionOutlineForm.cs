using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoClicker;

/// <summary>
/// Click-through, non-activating overlay that outlines a screen region. The outline is drawn just
/// outside the region so it never ends up inside the pixels the bar is measured from.
/// </summary>
public sealed class RegionOutlineForm : Form
{
    private const int Margin = 4;
    private static readonly Color Key = Color.FromArgb(1, 2, 3);

    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private readonly Color _accent;

    public RegionOutlineForm(Rectangle region, Color accent)
    {
        _accent = accent;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Key;
        TransparencyKey = Key;
        DoubleBuffered = true;
        Bounds = Rectangle.Inflate(region, Margin, Margin);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        using var dark = new Pen(Color.FromArgb(220, 0, 0, 0), 1);
        using var glow = new Pen(_accent, 2);
        g.DrawRectangle(dark, 0, 0, Width - 1, Height - 1);
        g.DrawRectangle(glow, 1.5f, 1.5f, Width - 4, Height - 4);
    }
}
