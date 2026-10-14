using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text.Json.Serialization;

namespace AutoClicker;

/// <summary>
/// A bar-style screen check (energy, health, ...). A screen region is scanned for pixels close
/// to the bar's fill color; the percentage of filled columns is the bar level.
/// A 1x1 region works as a plain pixel-color check (0% or 100%).
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

    [JsonIgnore]
    public bool IsConfigured => HasColor && RegionWidth > 0 && RegionHeight > 0;

    public ScreenCondition Clone() => (ScreenCondition)MemberwiseClone();

    /// <summary>Returns the bar level 0-100, or -1 when not configured or the capture failed.</summary>
    public double Measure(int tolerance)
    {
        if (!IsConfigured) return -1;
        try
        {
            using var bmp = new Bitmap(RegionWidth, RegionHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(RegionX, RegionY, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            return Analyze(bmp, FillColor, tolerance);
        }
        catch
        {
            return -1;
        }
    }

    public bool IsMet(int tolerance) => Measure(tolerance) >= MinPercent;

    private static double Analyze(Bitmap bmp, Color target, int tolerance)
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
                    if (Math.Abs(row[i + 2] - target.R) <= tolerance &&
                        Math.Abs(row[i + 1] - target.G) <= tolerance &&
                        Math.Abs(row[i]     - target.B) <= tolerance)
                        columnMatches[x]++;
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
