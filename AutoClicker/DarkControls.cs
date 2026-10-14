using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoClicker;

/// <summary>Button that never shows the dotted/accent focus rectangle.</summary>
public sealed class DarkButton : Button
{
    protected override bool ShowFocusCues => false;
}

/// <summary>Segmented tab radio without the focus rectangle.</summary>
public sealed class SegmentRadioButton : RadioButton
{
    public Color AccentColor { get; set; } = Theme.AccentBlue;

    public SegmentRadioButton()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        MouseEnter += (_, _) => Invalidate();
        MouseLeave += (_, _) => Invalidate();
    }

    protected override bool ShowFocusCues => false;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        bool hot = ClientRectangle.Contains(PointToClient(Cursor.Position));
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        using (var back = new SolidBrush(Checked ? Theme.BgCard : (hot ? Theme.BgHover : Theme.BgPanel)))
            g.FillRectangle(back, ClientRectangle);

        if (Checked)
        {
            // Soft glow: accent layers fading from the border inwards, plus a bright underline
            for (int i = 1; i <= 7; i++)
            {
                using var layer = new Pen(Color.FromArgb(70 - i * 9, AccentColor), 1);
                g.DrawRectangle(layer, i, i, Width - 1 - i * 2, Height - 1 - i * 2);
            }
            using var line = new SolidBrush(AccentColor);
            g.FillRectangle(line, 1, Height - 3, Width - 2, 2);
        }

        using (var border = new Pen(Checked ? AccentColor : (hot ? Theme.BorderHover : Theme.BorderColor)))
            g.DrawRectangle(border, rect);

        var textRect = new Rectangle(0, 0, Width, Checked ? Height - 2 : Height);
        TextRenderer.DrawText(g, Text, Font, textRect, Checked ? Theme.TextPrimary : (hot ? Theme.TextTertiary : Theme.TextMuted),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

public sealed class DarkCheckBox : CheckBox
{
    public Color AccentColor { get; set; } = Theme.AccentBlue;

    public DarkCheckBox()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        MouseEnter += (_, _) => Invalidate();
        MouseLeave += (_, _) => Invalidate();
    }

    protected override bool ShowFocusCues => false;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.BgPanel);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool hot = ClientRectangle.Contains(PointToClient(Cursor.Position));
        var box = new Rectangle(0, (Height - 14) / 2, 14, 14);
        using var path = DarkDraw.RoundRect(box, 3);

        if (!Enabled)
        {
            using var dis = new Pen(Theme.BorderColor);
            g.DrawPath(dis, path);
        }
        else if (Checked)
        {
            using var fill = new SolidBrush(AccentColor);
            g.FillPath(fill, path);
            using var tick = new Pen(Color.Black, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(tick, new[] { new PointF(3.4f, box.Y + 7.4f), new PointF(6f, box.Y + 10f), new PointF(10.8f, box.Y + 4.2f) });
        }
        else
        {
            using var back = new SolidBrush(Theme.BgCard);
            g.FillPath(back, path);
            using var border = new Pen(hot ? Theme.BorderHover : Theme.BorderOutline);
            g.DrawPath(border, path);
        }

        var textRect = new Rectangle(box.Right + 6, 0, Width - box.Right - 6, Height);
        TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? ForeColor : Theme.TextDim,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

public sealed class DarkRadioButton : RadioButton
{
    public Color AccentColor { get; set; } = Theme.AccentBlue;

    public DarkRadioButton()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        MouseEnter += (_, _) => Invalidate();
        MouseLeave += (_, _) => Invalidate();
    }

    protected override bool ShowFocusCues => false;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.BgPanel);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool hot = ClientRectangle.Contains(PointToClient(Cursor.Position));
        var ring = new Rectangle(0, (Height - 14) / 2, 13, 13);
        using var back = new SolidBrush(Theme.BgCard);
        g.FillEllipse(back, ring);
        using var border = new Pen(Checked ? AccentColor : (hot ? Theme.BorderHover : Theme.BorderOutline));
        g.DrawEllipse(border, ring);
        if (Checked)
        {
            using var dot = new SolidBrush(AccentColor);
            g.FillEllipse(dot, new Rectangle(ring.X + 3, ring.Y + 3, 7, 7));
        }

        var textRect = new Rectangle(ring.Right + 6, 0, Width - ring.Right - 6, Height);
        TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? ForeColor : Theme.TextDim,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

/// <summary>Drop-down list whose arrow button and border are painted in the dark theme.</summary>
public sealed class DarkComboBox : ComboBox
{
    private const int WM_PAINT = 0x000F;

    public DarkComboBox()
    {
        MouseEnter += (_, _) => Invalidate();
        MouseLeave += (_, _) => Invalidate();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WM_PAINT && Width > 0 && Height > 0)
        {
            using var g = Graphics.FromHwnd(Handle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool hot = ClientRectangle.Contains(PointToClient(Cursor.Position)) || DroppedDown;
            var button = new Rectangle(Width - 22, 1, 21, Height - 2);
            using (var back = new SolidBrush(Enabled ? Theme.BgCard : Theme.BgPanel))
                g.FillRectangle(back, button);

            using (var chevron = new Pen(Enabled ? (hot ? Theme.TextPrimary : Theme.TextTertiary) : Theme.TextDim, 1.6f)
                   { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                float cx = button.X + button.Width / 2f, cy = button.Y + button.Height / 2f;
                g.DrawLines(chevron, new[] { new PointF(cx - 3.5f, cy - 1.5f), new PointF(cx, cy + 2f), new PointF(cx + 3.5f, cy - 1.5f) });
            }

            using var border = new Pen(hot && Enabled ? Theme.BorderHover : Theme.BorderOutline);
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }
    }
}

/// <summary>Numeric box whose spin buttons are painted in the dark theme.</summary>
public sealed class DarkNumericUpDown : NumericUpDown
{
    public DarkNumericUpDown()
    {
        foreach (Control child in Controls)
        {
            if (child is TextBox) continue;
            child.Paint += PaintSpin;
            child.MouseMove += (s, _) => ((Control)s!).Invalidate();
            child.MouseLeave += (s, _) => ((Control)s!).Invalidate();
            child.MouseDown += (s, _) => ((Control)s!).Invalidate();
            child.MouseUp += (s, _) => ((Control)s!).Invalidate();
        }
    }

    private Control? _borderHost;

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (_borderHost != null) _borderHost.Paint -= PaintBorder;
        _borderHost = Parent;
        if (_borderHost != null) _borderHost.Paint += PaintBorder;
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); RefreshBorder(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); RefreshBorder(); }
    protected override void OnEnter(EventArgs e) { base.OnEnter(e); RefreshBorder(); }
    protected override void OnLeave(EventArgs e) { base.OnLeave(e); RefreshBorder(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); RefreshBorder(); }

    private Rectangle BorderRect => new(Left - 1, Top - 1, Width + 1, Height + 1);

    private void RefreshBorder() => _borderHost?.Invalidate(Rectangle.Inflate(BorderRect, 2, 2));

    // The inner edit/spin controls cover the whole client area, so the 1px border is drawn on the parent just outside this control
    private void PaintBorder(object? sender, PaintEventArgs e)
    {
        if (!Visible) return;
        bool hot = Enabled && (ContainsFocus || ClientRectangle.Contains(PointToClient(Cursor.Position)));
        using var pen = new Pen(hot ? Theme.BorderHover : Theme.BorderOutline);
        e.Graphics.DrawRectangle(pen, BorderRect);
    }
    private void PaintSpin(object? sender, PaintEventArgs e)
    {
        var c = (Control)sender!;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var back = new SolidBrush(Enabled ? Theme.BgCard : Theme.BgPanel))
            g.FillRectangle(back, c.ClientRectangle);

        var pt = c.PointToClient(Cursor.Position);
        bool inside = Enabled && c.ClientRectangle.Contains(pt);
        int half = c.Height / 2;
        var up = new Rectangle(0, 0, c.Width, half);
        var down = new Rectangle(0, half, c.Width, c.Height - half);

        void Arrow(Rectangle r, bool isUp)
        {
            bool hot = inside && r.Contains(pt);
            if (hot)
            {
                using var hover = new SolidBrush(Theme.BgHover);
                g.FillRectangle(hover, r);
            }
            using var pen = new Pen(!Enabled ? Theme.TextDim : (hot ? Theme.TextPrimary : Theme.TextTertiary), 1.5f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            float d = isUp ? 1.5f : -1.5f;
            g.DrawLines(pen, new[] { new PointF(cx - 3f, cy + d), new PointF(cx, cy - d), new PointF(cx + 3f, cy + d) });
        }

        Arrow(up, true);
        Arrow(down, false);

        using var line = new Pen(Theme.BorderColor);
        g.DrawLine(line, 0, 0, 0, c.Height);
        g.DrawLine(line, 0, half, c.Width, half);
    }
}

internal static class DarkDraw
{
    public static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
