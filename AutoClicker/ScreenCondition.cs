using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text.Json.Serialization;

namespace AutoClicker;

/// <summary>
/// A bar-style screen check (energy, health, ...). A screen region is scanned for pixels close
/// to any of the bar's known fill colors; the percentage of filled columns is the bar level.
/// A 1x1 region works as a plain pixel-color check (0% or 100%).
/// Matching tolerates different shades of a picked color (e.g. a gradient bar with several
/// shades of green) by also comparing colors after normalizing out brightness - see
/// <see cref="ColorMatches"/>. Bars that recolor as they deplete (e.g. green -> yellow -> red
/// health warnings) are supported by picking more than one color - see <see cref="AddColor"/>
/// and <see cref="AllColors"/>.
/// </summary>
public sealed class ScreenCondition
{
    public string Name { get; set; } = "Bar";
    public bool Enabled { get; set; }
    public int RegionX { get; set; }
    public int RegionY { get; set; }
    public int RegionWidth { get; set; }
    public int RegionHeight { get; set; }
    public int ColorArgb { get; set; }
    public bool HasColor { get; set; }
    public int MinPercent { get; set; } = 50;

    /// <summary>Extra fill colors beyond the primary one (e.g. the yellow/red stages of a health bar).</summary>
    public List<int> ExtraColorsArgb { get; set; } = new();

    [JsonIgnore]
    public Rectangle Region
    {
        get => new(RegionX, RegionY, RegionWidth, RegionHeight);
        set { RegionX = value.X; RegionY = value.Y; RegionWidth = value.Width; RegionHeight = value.Height; }
    }

    [JsonIgnore]
    public Color FillColor
    {
        get => Color.FromArgb(ColorArgb);
        set { ColorArgb = value.ToArgb(); HasColor = true; }
    }

    /// <summary>All colors that count as "filled" - the primary color plus any extra ones.</summary>
    [JsonIgnore]
    public IReadOnlyList<Color> AllColors
    {
        get
        {
            if (!HasColor) return Array.Empty<Color>();
            var colors = new List<Color>(ExtraColorsArgb.Count + 1) { FillColor };
            colors.AddRange(ExtraColorsArgb.Select(Color.FromArgb));
            return colors;
        }
    }

    [JsonIgnore]
    public bool IsConfigured => HasColor && RegionWidth > 0 && RegionHeight > 0;

    /// <summary>Adds another fill color (e.g. the bar's low-level warning color), without replacing existing ones.</summary>
    public void AddColor(Color color)
    {
        if (!HasColor) FillColor = color;
        else ExtraColorsArgb.Add(color.ToArgb());
    }

    /// <summary>Forgets every picked color so the user can start over.</summary>
    public void ClearColors()
    {
        HasColor = false;
        ColorArgb = 0;
        ExtraColorsArgb.Clear();
    }

    public ScreenCondition Clone()
    {
        var clone = (ScreenCondition)MemberwiseClone();
        clone.ExtraColorsArgb = new List<int>(ExtraColorsArgb);
        return clone;
    }

    /// <summary>Returns the bar level 0-100, or -1 when not configured or the capture failed.</summary>
    public double Measure(int tolerance)
    {
        if (!IsConfigured) return -1;
        try
        {
            using var bmp = new Bitmap(RegionWidth, RegionHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(RegionX, RegionY, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            return Analyze(bmp, AllColors, tolerance);
        }
        catch
        {
            return -1;
        }
    }

    public bool IsMet(int tolerance) => Measure(tolerance) >= MinPercent;

    private static double Analyze(Bitmap bmp, IReadOnlyList<Color> targets, int tolerance)
    {
        int w = bmp.Width, h = bmp.Height;
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var columnMatches = new int[w];
            var row = new byte[Math.Abs(data.Stride)];
            for (int y = 0; y < h; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, w * 4);
                for (int x = 0; x < w; x++)
                {
                    int i = x * 4; // BGRA
                    byte pr = row[i + 2], pg = row[i + 1], pb = row[i];
                    for (int t = 0; t < targets.Count; t++)
                    {
                        if (ColorMatches(pr, pg, pb, targets[t], tolerance))
                        {
                            columnMatches[x]++;
                            break;
                        }
                    }
                }
            }

            // A column counts as filled when at least 40% of its pixels match, so borders and text overlays don't matter
            int filled = columnMatches.Count(c => c * 100 >= h * 40);
            return filled * 100.0 / w;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    /// <summary>Plain per-channel RGB closeness, with no brightness normalization (needed for white/grey targets).</summary>
    internal static bool RgbClose(byte r, byte g, byte b, Color target, int tolerance) =>
        Math.Abs(r - target.R) <= tolerance &&
        Math.Abs(g - target.G) <= tolerance &&
        Math.Abs(b - target.B) <= tolerance;

    /// <summary>
    /// Returns true when a sampled pixel (r,g,b) should count as the bar's fill color.
    /// Checks plain RGB closeness first, then a brightness-normalized comparison so that
    /// different shades of the same hue (gradients, highlights, anti-aliased edges - e.g. a
    /// bar with several shades of green) still match without needing a huge raw tolerance
    /// that would also start matching unrelated colors.
    /// </summary>
    internal static bool ColorMatches(byte r, byte g, byte b, Color target, int tolerance)
    {
        if (RgbClose(r, g, b, target, tolerance)) return true;

        int pixelMax = Math.Max(r, Math.Max(g, b));
        int targetMax = Math.Max(target.R, Math.Max(target.G, target.B));
        int targetMin = Math.Min(target.R, Math.Min(target.G, target.B));
        if (pixelMax < 12 || targetMax < 12) return false; // too dark to judge hue reliably
        if (targetMax - targetMin < 40) return false;      // grey/white targets have no hue: every dark grey would "match"

        double scaleP = 255.0 / pixelMax;
        double scaleT = 255.0 / targetMax;
        return Math.Abs(r * scaleP - target.R * scaleT) <= tolerance &&
               Math.Abs(g * scaleP - target.G * scaleT) <= tolerance &&
               Math.Abs(b * scaleP - target.B * scaleT) <= tolerance;
    }

    public static Color SamplePixel(Point p)
    {
        using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(p.X, p.Y, 0, 0, new Size(1, 1));
        return bmp.GetPixel(0, 0);
    }
}

/// <summary>Snapshot of the enabled conditions that gates macro playback.</summary>
public sealed class ScreenGate
{
    public List<ScreenCondition> Conditions { get; init; } = new();
    public int Tolerance { get; init; } = 40;

    // true: wait until all conditions are met; false: stop playback when one is not met
    public bool WaitUntilMet { get; init; } = true;

    public bool IsActive => Conditions.Count > 0;

    /// <summary>Returns true when every condition is met; otherwise reports the first failing one.</summary>
    public bool Check(out string? failing)
    {
        failing = null;
        foreach (var c in Conditions)
        {
            if (!c.IsMet(Tolerance))
            {
                failing = c.Name;
                return false;
            }
        }
        return true;
    }
}
