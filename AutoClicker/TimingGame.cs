using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace AutoClicker;

/// <summary>What a timing-minigame strip looks like in one frame: where the moving marker is and where the target zone spans.</summary>
public readonly record struct TimingReading(double? MarkerX, double? TargetLeft, double? TargetRight, int MarkerWidth = 0)
{
    // Share of the target's width, at each end, where the marker's centre must not be yet
    private const double EdgeMargin = 0.15;

    public bool HasTarget => TargetLeft.HasValue && TargetRight.HasValue;
    public bool HasMarker => MarkerX.HasValue;
    public double TargetCenter => HasTarget ? (TargetLeft!.Value + TargetRight!.Value) / 2 : 0;

    /// <summary>
    /// True when the marker is well inside the target: its whole width plus a margin, so a key press (which lands a
    /// moment after the frame was read, while the marker keeps moving) still lands on the target instead of its edge.
    /// </summary>
    public bool OnTarget
    {
        get
        {
            if (!HasMarker || !HasTarget) return false;
            double width = TargetRight!.Value - TargetLeft!.Value;
            double inset = Math.Min(Math.Max(MarkerWidth / 2.0 + 2, width * EdgeMargin), width / 2);
            return MarkerX >= TargetLeft + inset && MarkerX <= TargetRight - inset;
        }
    }

    public override string ToString()
    {
        string marker = HasMarker ? $"marker at x={MarkerX:F0}" : "no marker";
        string target = HasTarget ? $"target x={TargetLeft:F0}..{TargetRight:F0}" : "no target";
        return $"{marker}, {target}{(OnTarget ? " - ON TARGET" : "")}";
    }
}

/// <summary>
/// Finds the marker (a tall, narrow bar in the marker colour) and the target zone (a block of the target
/// colours) inside a horizontal strip, so a step can press a key the moment the marker sits on the target.
/// </summary>
public static class TimingGameReader
{
    /// <summary>Colour tolerance used for the timing step and its TEST button, independent of the bars' tolerance setting.</summary>
    public const int Tolerance = 40;

    public static TimingReading Analyze(Bitmap bmp, IReadOnlyList<Color> targets, Color marker, int tolerance)
    {
        int w = bmp.Width, h = bmp.Height;
        var markerCount = new int[w];
        var targetCount = new int[w];

        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = new byte[Math.Abs(data.Stride)];
            for (int y = 0; y < h; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, w * 4);
                for (int x = 0; x < w; x++)
                {
                    int i = x * 4; // BGRA
                    byte r = row[i + 2], g = row[i + 1], b = row[i];

                    // The marker uses strict RGB matching: brightness normalization would make any grey look white
                    if (ScreenCondition.RgbClose(r, g, b, marker, tolerance))
                    {
                        markerCount[x]++;
                        continue;
                    }
                    for (int t = 0; t < targets.Count; t++)
                    {
                        if (ScreenCondition.ColorMatches(r, g, b, targets[t], tolerance))
                        {
                            targetCount[x]++;
                            break;
                        }
                    }
                }
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        // The marker is a tall, narrow run of columns; wide runs of marker-coloured pixels are scenery, not the marker
        var markerRuns = Runs(x => markerCount[x] * 100 >= h * 40, w, maxGap: 0).Where(r => r.Width <= Math.Max(w / 6, 6)).ToList();
        double? markerX = null;
        int markerWidth = 0;
        if (markerRuns.Count > 0)
        {
            var best = markerRuns.OrderByDescending(r => r.Width).First();
            markerX = (best.Start + best.End) / 2.0;
            markerWidth = best.Width;
        }

        // Target columns are judged against the best column, so the region doesn't need to hug the target tightly.
        // The marker may cut a gap into the zone, so gaps up to its width are bridged.
        int maxTarget = targetCount.Max();
        if (maxTarget < 4 || maxTarget * 100 < h * 15) return new TimingReading(markerX, null, null);

        int needed = Math.Max(3, maxTarget / 2);
        var zones = Runs(x => targetCount[x] >= needed, w, maxGap: Math.Max(markerWidth + 4, 8)).Where(r => r.Width >= 4).ToList();
        if (zones.Count == 0) return new TimingReading(markerX, null, null);

        var zone = zones.OrderByDescending(r => r.Width).First();
        return new TimingReading(markerX, zone.Start, zone.End, markerWidth);
    }

    private readonly record struct Run(int Start, int End)
    {
        public int Width => End - Start + 1;
    }

    private static List<Run> Runs(Func<int, bool> isOn, int w, int maxGap)
    {
        var runs = new List<Run>();
        int start = -1, last = -1;
        for (int x = 0; x < w; x++)
        {
            if (!isOn(x)) continue;
            if (start >= 0 && x - last - 1 > maxGap)
            {
                runs.Add(new Run(start, last));
                start = -1;
            }
            if (start < 0) start = x;
            last = x;
        }
        if (start >= 0) runs.Add(new Run(start, last));
        return runs;
    }
}
