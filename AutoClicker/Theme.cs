using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AutoClicker;

public static class Theme
{
    // --- 1. Base / Surface Colors (near-black scale from design.md) ---
    public static readonly Color BgDeep       = Color.FromArgb(0, 0, 0);       // #000000 Header, chrome
    public static readonly Color BgBase       = Color.FromArgb(5, 5, 5);       // #050505 App window background
    public static readonly Color BgPanel      = Color.FromArgb(13, 13, 13);    // #0D0D0D Cards / panels
    public static readonly Color BgCard       = Color.FromArgb(20, 20, 20);    // #141414 Item / input / control fill
    public static readonly Color BgTrack      = Color.FromArgb(28, 28, 28);    // #1C1C1C Tracks, inactive fills
    public static readonly Color BgHover      = Color.FromArgb(30, 30, 30);    // #1E1E1E Hover background

    // --- 2. Borders ---
    public static readonly Color BorderColor  = Color.FromArgb(30, 30, 30);    // #1E1E1E Default 1px borders
    public static readonly Color BorderHover  = Color.FromArgb(68, 68, 68);    // #444444 Hover border
    public static readonly Color BorderOutline= Color.FromArgb(42, 42, 42);    // #2A2A2A Default outline buttons

    // --- 3. Text Colors ---
    public static readonly Color TextPrimary  = Color.FromArgb(255, 255, 255); // #FFFFFF Primary text / headlines
    public static readonly Color TextMuted    = Color.FromArgb(102, 102, 102); // #666666 Secondary labels
    public static readonly Color TextTertiary = Color.FromArgb(136, 136, 136); // #888888 Tertiary text / outlines
    public static readonly Color TextDim      = Color.FromArgb(46, 46, 46);   // #2E2E2E Disabled / placeholders

    // --- 4. Accent / Semantic Colors ---
    public static readonly Color AccentGreen  = Color.FromArgb(74, 222, 128); // #4ADE80 Active / On / Start
    public static readonly Color AccentBlue   = Color.FromArgb(96, 165, 250); // #60A5FA Auto Clicker identity
    public static readonly Color AccentOrange = Color.FromArgb(251, 146, 60); // #FB923C Macro / TinyTask identity
    public static readonly Color AccentPurple = Color.FromArgb(192, 132, 252);// #C084FC Coordinates / Hotkeys
    public static readonly Color AccentYellow = Color.FromArgb(250, 204, 21); // #FACC15 Safety & Limits
    public static readonly Color Danger       = Color.FromArgb(192, 57, 43);   // #C0392B Red base (Destructive / Mute)
    public static readonly Color DangerHover  = Color.FromArgb(231, 76, 60);   // #E74C3C Red hover
    public static readonly Color DangerPress  = Color.FromArgb(169, 50, 38);   // #A93226 Red pressed

    // --- Backward-Compatible Aliases ---
    public static Color Background => BgBase;
    public static Color Card       => BgPanel;
    public static Color Field      => BgCard;
    public static Color Border     => BorderColor;
    public static Color Text       => TextPrimary;
    public static Color Muted      => TextMuted;
    public static Color Accent     => AccentBlue;
    public static Color Success    => AccentGreen;
    public static Color Warning    => AccentOrange;

    // -------------------------------------------------------------------------
    // Component Factories
    // -------------------------------------------------------------------------

    public static CardPanel CreateCard(int x, int y, int width, int height, Color? accent = null) => new(accent)
    {
        Location = new Point(x, y),
        Size     = new Size(width, height),
    };

    public static Label CreateTagLabel(string text, int x, int y, Color accent) => new()
    {
        Text        = text.ToUpperInvariant(),
        Location    = new Point(x, y),
        AutoSize    = true,
        ForeColor   = accent,
        Font        = new Font("Segoe UI", 7.75f, FontStyle.Bold),
        UseMnemonic = false,
    };

    public static RadioButton CreateSegment(string text, int x, int y, int width, bool isChecked, Color? checkedAccent = null)
    {
        var accent = checkedAccent ?? AccentBlue;
        var radio = new SegmentRadioButton
        {
            Text                  = text,
            Appearance            = Appearance.Button,
            FlatStyle             = FlatStyle.Flat,
            Location              = new Point(x, y),
            Size                  = new Size(width, 32),
            TextAlign             = ContentAlignment.MiddleCenter,
            BackColor             = isChecked ? BgCard : BgPanel,
            ForeColor             = isChecked ? TextPrimary : TextMuted,
            Checked               = isChecked,
            Cursor                = Cursors.Hand,
            Font                  = new Font("Segoe UI Semibold", 8.75f),
            UseMnemonic           = false,
            AccentColor           = accent,
        };
        radio.FlatAppearance.BorderSize         = 1;
        radio.FlatAppearance.BorderColor        = isChecked ? accent : BorderColor;
        radio.FlatAppearance.CheckedBackColor   = BgCard;
        radio.FlatAppearance.MouseOverBackColor = BgHover;

        radio.CheckedChanged += (_, _) =>
        {
            bool c = radio.Checked;
            radio.BackColor = c ? BgCard : BgPanel;
            radio.ForeColor = c ? TextPrimary : TextMuted;
            radio.FlatAppearance.BorderColor = c ? accent : BorderColor;
        };
        return radio;
    }

    public static ComboBox CreateCombo(int x, int y, int width)
    {
        var combo = new DarkComboBox
        {
            Location      = new Point(x, y),
            Size          = new Size(width, 28),
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle     = FlatStyle.Flat,
            BackColor     = BgCard,
            ForeColor     = TextPrimary,
            DrawMode      = DrawMode.OwnerDrawFixed,
            ItemHeight    = 22,
            Cursor        = Cursors.Hand,
            Font          = new Font("Segoe UI", 8.75f),
        };
        combo.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using var back = new SolidBrush(selected ? BgHover : BgCard);
            using var fore = new SolidBrush(selected ? TextPrimary : TextTertiary);
            e.Graphics.FillRectangle(back, e.Bounds);

            if (selected)
            {
                using var borderPen = new Pen(BorderHover, 1);
                e.Graphics.DrawRectangle(borderPen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }

            string itemText = combo.Items[e.Index]?.ToString() ?? string.Empty;
            using var sf = new StringFormat
            {
                Alignment     = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                FormatFlags   = StringFormatFlags.NoWrap
            };
            var textRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            e.Graphics.DrawString(itemText, combo.Font, fore, textRect, sf);
        };
        return combo;
    }

    public static NumericUpDown CreateNumeric(
        int x, int y, int width, decimal value,
        decimal min = 10, decimal max = 60000, decimal increment = 10) => new DarkNumericUpDown()
    {
        Location    = new Point(x, y),
        Size        = new Size(width, 26),
        Minimum     = min,
        Maximum     = max,
        Value       = value,
        Increment   = increment,
        BackColor   = BgCard,
        ForeColor   = TextPrimary,
        BorderStyle = BorderStyle.None,
        Font        = new Font("Segoe UI", 8.75f),
    };

    public static CheckBox CreateCheckBox(string text, int x, int y, bool isChecked = false)
    {
        var cb = new DarkCheckBox
        {
            Text      = text,
            Location  = new Point(x, y),
            AutoSize  = true,
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextPrimary,
            Checked   = isChecked,
            Cursor    = Cursors.Hand,
            Font      = new Font("Segoe UI", 8.75f),
        };
        cb.FlatAppearance.BorderSize         = 1;
        cb.FlatAppearance.BorderColor        = BorderOutline;
        cb.FlatAppearance.CheckedBackColor   = AccentBlue;
        cb.FlatAppearance.MouseOverBackColor = BgHover;
        return cb;
    }

    public static Button CreateButton(string text, int x, int y, int width, Color color, int height = 38, bool isOutline = false)
    {
        bool outlineMode = isOutline || color == Field || color == BgCard;
        Color initialBorder = outlineMode ? (color == Field || color == BgCard ? BorderOutline : color) : color;
        Color initialFore   = outlineMode ? (color == Field || color == BgCard ? TextTertiary : color) : (color == AccentGreen ? Color.Black : Color.White);
        Color initialBack   = outlineMode ? BgCard : color;

        var button = new DarkButton
        {
            Text                  = text,
            Location              = new Point(x, y),
            Size                  = new Size(width, height),
            FlatStyle             = FlatStyle.Flat,
            BackColor             = initialBack,
            ForeColor             = initialFore,
            Font                  = new Font("Segoe UI Semibold", 9.5f),
            Cursor                = Cursors.Hand,
            UseVisualStyleBackColor = false,
            UseMnemonic           = false,
        };

        button.FlatAppearance.BorderSize         = 1;
        button.FlatAppearance.BorderColor        = initialBorder;
        button.FlatAppearance.MouseOverBackColor = outlineMode ? BgHover : (color == Danger ? DangerHover : ControlPaint.Light(color, 0.15f));
        button.FlatAppearance.MouseDownBackColor = outlineMode ? BgTrack : (color == Danger ? DangerPress : ControlPaint.Dark(color, 0.1f));

        button.EnabledChanged += (_, _) =>
        {
            if (button.Enabled)
            {
                button.BackColor = initialBack;
                button.ForeColor = initialFore;
                button.FlatAppearance.BorderColor = initialBorder;
            }
            else
            {
                button.BackColor = BgPanel;
                button.ForeColor = TextDim;
                button.FlatAppearance.BorderColor = BorderColor;
            }
        };

        return button;
    }

    public static void SetButtonActiveState(Button btn, bool active, Color activeColor, string activeText, string idleText, bool idleOutline = true)
    {
        btn.Text = active ? activeText : idleText;
        if (active)
        {
            btn.BackColor = activeColor;
            btn.ForeColor = activeColor == AccentGreen ? Color.Black : Color.White;
            btn.FlatAppearance.BorderColor = activeColor;
            btn.FlatAppearance.BorderSize = 1;
        }
        else
        {
            if (idleOutline)
            {
                btn.BackColor = BgCard;
                btn.ForeColor = activeColor;
                btn.FlatAppearance.BorderColor = activeColor;
                btn.FlatAppearance.BorderSize = 1;
            }
            else
            {
                btn.BackColor = BgCard;
                btn.ForeColor = TextTertiary;
                btn.FlatAppearance.BorderColor = BorderOutline;
                btn.FlatAppearance.BorderSize = 1;
            }
        }
    }
}

/// <summary>
/// A sleek, flat card panel conforming to design.md with a 1px BorderColor border
/// and an optional 2px top accent stripe.
/// </summary>
public sealed class CardPanel : Panel
{
    public Color? AccentStripeColor { get; set; }

    public CardPanel(Color? accentStripe = null)
    {
        AccentStripeColor = accentStripe;
        BackColor         = Theme.BgPanel;
        DoubleBuffered    = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        // 1px BorderColor around card
        using (var borderPen = new Pen(Theme.BorderColor, 1))
        {
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }

        // 2px top accent stripe
        if (AccentStripeColor.HasValue)
        {
            using var stripeBrush = new SolidBrush(AccentStripeColor.Value);
            g.FillRectangle(stripeBrush, 1, 1, Width - 2, 2);
        }
    }
}

/// <summary>
/// A status badge chip complying with design.md Section 5:
/// Small rounded/flat 4px bordered chip containing a 5x5px colored dot + uppercase 10px SemiBold label.
/// </summary>
public sealed class StatusBadgeLabel : Label
{
    public StatusBadgeLabel()
    {
        AutoSize       = false;
        TextAlign      = ContentAlignment.MiddleLeft;
        Font           = new Font("Segoe UI Semibold", 9.5f);
        UseMnemonic    = false;
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        string rawText = Text ?? string.Empty;
        if (rawText.StartsWith("● ") || rawText.StartsWith("• "))
            rawText = rawText.Substring(2).Trim();
        else if (rawText.StartsWith("●") || rawText.StartsWith("•"))
            rawText = rawText.Substring(1).Trim();

        string upperText = rawText.ToUpperInvariant();

        Color dotColor;
        Color bgColor;
        Color borderColor;
        Color textColor;

        if (ForeColor == Theme.AccentGreen || ForeColor == Theme.Success)
        {
            dotColor    = Theme.AccentGreen;
            bgColor     = Color.FromArgb(13, 31, 13);    // #0D1F0D
            borderColor = Color.FromArgb(26, 61, 26);    // #1A3D1A
            textColor   = Theme.AccentGreen;
        }
        else if (ForeColor == Theme.Danger || ForeColor == Color.FromArgb(220, 70, 85))
        {
            dotColor    = Theme.Danger;
            bgColor     = Color.FromArgb(31, 13, 13);    // #1F0D0D
            borderColor = Color.FromArgb(61, 26, 26);    // #3D1A1A
            textColor   = Color.FromArgb(248, 113, 113); // #F87171
        }
        else if (ForeColor == Theme.AccentOrange || ForeColor == Theme.Warning)
        {
            dotColor    = Theme.AccentOrange;
            bgColor     = Color.FromArgb(31, 21, 10);    // #1F150A
            borderColor = Color.FromArgb(61, 40, 16);    // #3D2810
            textColor   = Theme.AccentOrange;
        }
        else if (ForeColor == Theme.AccentBlue || ForeColor == Theme.Accent)
        {
            dotColor    = Theme.AccentBlue;
            bgColor     = Color.FromArgb(13, 21, 32);    // #0D1520
            borderColor = Color.FromArgb(22, 40, 64);    // #162840
            textColor   = Theme.AccentBlue;
        }
        else
        {
            dotColor    = Theme.TextMuted;
            bgColor     = Theme.BgPanel;
            borderColor = Theme.BorderColor;
            textColor   = Theme.TextMuted;
        }

        // Draw pill background
        using (var bgBrush = new SolidBrush(bgColor))
        {
            g.FillRectangle(bgBrush, 0, 0, Width - 1, Height - 1);
        }
        using (var borderPen = new Pen(borderColor, 1))
        {
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }

        // Draw status dot (6x6 px circle)
        const int dotSize = 6;
        const int dotX = 10;
        int dotY = (Height - dotSize) / 2;
        using (var dotBrush = new SolidBrush(dotColor))
        {
            g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
        }

        // Draw uppercase label
        int textX = dotX + dotSize + 8;
        using (var textBrush = new SolidBrush(textColor))
        {
            var textRect = new Rectangle(textX, 0, Width - textX - 6, Height);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming      = StringTrimming.EllipsisCharacter,
                FormatFlags   = StringFormatFlags.NoWrap
            };
            g.DrawString(upperText, Font, textBrush, textRect, sf);
        }
    }
}

/// <summary>
/// Dark context menu renderer conforming to design.md Section 5.
/// </summary>
public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkMenuRenderer() : base(new DarkMenuColorTable()) { }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.TextPrimary : Theme.TextDim;
        base.OnRenderItemText(e);
    }
}

public sealed class DarkMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.BgPanel;
    public override Color MenuBorder                 => Theme.BorderColor;
    public override Color MenuItemBorder             => Theme.BorderColor;
    public override Color MenuItemSelected           => Theme.BgCard;
    public override Color MenuItemSelectedGradientBegin => Theme.BgCard;
    public override Color MenuItemSelectedGradientEnd   => Theme.BgCard;
    public override Color MenuItemPressedGradientBegin  => Theme.BgTrack;
    public override Color MenuItemPressedGradientEnd    => Theme.BgTrack;
    public override Color ImageMarginGradientBegin   => Theme.BgPanel;
    public override Color ImageMarginGradientMiddle  => Theme.BgPanel;
    public override Color ImageMarginGradientEnd     => Theme.BgPanel;
    public override Color SeparatorDark              => Theme.BorderColor;
    public override Color SeparatorLight             => Theme.BorderColor;
}
