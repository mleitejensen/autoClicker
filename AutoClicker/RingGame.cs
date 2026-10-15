using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace AutoClicker;

/// <summary>A ring found on screen: its centre (relative to the bitmap it was found in), outer radius and thickness in pixels.</summary>
public sealed record RingInfo(double X, double Y, double OuterRadius, double Thickness);

/// <summary>The measured size of a ring around a known centre.</summary>
public readonly record struct RingReading(double OuterRadius, double Thickness);

/// <summary>
/// Finds the closing rings of a ring minigame and measures them. Rings are found by their colour alone, so balls of
/// any colour work; the ball's size is read from the edge inside the ring.
/// </summary>
public static class RingGameReader
{
    public const int Tolerance = 45;

    private static byte[] ReadBgra(Bitmap bmp, out int stride)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            stride = Math.Abs(data.Stride);
            var bytes = new byte[stride * bmp.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    // The picked colours plus every colour on a straight line between two of them: a ring is usually a gradient
    // (say cream at the top fading to orange at the bottom), and this covers the whole fade from a couple of picks.
    private sealed class Palette
    {
        private readonly (double Ax, double Ay, double Az, double Dx, double Dy, double Dz, double Len2)[] _segments;

        public Palette(IReadOnlyList<Color> colors)
        {
            var list = new List<(double, double, double, double, double, double, double)>();
            for (int i = 0; i < colors.Count; i++)
                for (int j = i; j < colors.Count; j++)
                {
                    double dx = colors[j].R - colors[i].R, dy = colors[j].G - colors[i].G, dz = colors[j].B - colors[i].B;
                    list.Add((colors[i].R, colors[i].G, colors[i].B, dx, dy, dz, dx * dx + dy * dy + dz * dz));
                }
            _segments = list.ToArray();
        }

        public bool Matches(byte r, byte g, byte b)
        {
            const double tol2 = Tolerance * Tolerance;
            foreach (var s in _segments)
            {
                double t = s.Len2 > 0 ? Math.Clamp(((r - s.Ax) * s.Dx + (g - s.Ay) * s.Dy + (b - s.Az) * s.Dz) / s.Len2, 0, 1) : 0;
                double er = r - (s.Ax + t * s.Dx), eg = g - (s.Ay + t * s.Dy), eb = b - (s.Az + t * s.Dz);
                if (er * er + eg * eg + eb * eb <= tol2) return true;
            }
            return false;
        }
    }

    // Plain colour distance (no shade tolerance), so pale backgrounds don't count as a pale ring
    private static bool IsRingColor(byte[] px, int i, Palette palette) => palette.Matches(px[i + 2], px[i + 1], px[i]);

    /// <summary>
    /// Finds rings in a screenshot: connected groups of ring-coloured pixels that are roughly square and hollow, and
    /// which a ray test confirms really are a ring around their centre. Samples every <paramref name="stride"/>th pixel,
    /// which keeps a full-screen search fast.
    /// </summary>
    public static List<RingInfo> FindRings(Bitmap bmp, IReadOnlyList<Color> ringColors, int stride = 2)
    {
        int w = bmp.Width, h = bmp.Height;
        int gw = (w + stride - 1) / stride, gh = (h + stride - 1) / stride;
        var px = ReadBgra(bmp, out int rowBytes);
        var palette = new Palette(ringColors);

        var mask = new bool[gw, gh];
        for (int gy = 0; gy < gh; gy++)
            for (int gx = 0; gx < gw; gx++)
                mask[gx, gy] = IsRingColor(px, gy * stride * rowBytes + gx * stride * 4, palette);

        var rings = new List<RingInfo>();
        var seen = new bool[gw, gh];
        var stack = new Stack<(int X, int Y)>();
        for (int sx = 0; sx < gw; sx++)
            for (int sy = 0; sy < gh; sy++)
            {
                if (!mask[sx, sy] || seen[sx, sy]) continue;

                int x0 = sx, x1 = sx, y0 = sy, y1 = sy, area = 0;
                stack.Push((sx, sy)); seen[sx, sy] = true;
                while (stack.Count > 0)
                {
                    var (x, y) = stack.Pop();
                    area++;
                    x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= gw || ny >= gh || seen[nx, ny] || !mask[nx, ny]) continue;
                            seen[nx, ny] = true;
                            stack.Push((nx, ny));
                        }
                }

                double bw = (x1 - x0 + 1) * stride, bh = (y1 - y0 + 1) * stride;
                if (bw < 40 || bh < 40 || bw > 900 || bh > 900) continue;
                double aspect = bw / bh;
                if (aspect < 0.8 || aspect > 1.25) continue;
                // A ring is hollow: its pixels fill only a thin band of the bounding box
                double fill = area * stride * stride / (bw * bh);
                if (fill > 0.45) continue;

                double cx = (x0 + x1 + 1) * stride / 2.0, cy = (y0 + y1 + 1) * stride / 2.0, radius = (bw + bh) / 4.0;
                var reading = MeasureRing(px, w, h, rowBytes, cx, cy, radius * 0.35, radius + 8, palette, outermostFirst: true);
                if (reading is { } r && Math.Abs(r.OuterRadius - radius) <= 8)
                    rings.Add(new RingInfo(cx, cy, r.OuterRadius, r.Thickness));
            }
        return rings;
    }

    /// <summary>
    /// Measures the ring around a known centre by casting rays outward. For each ray the first ring-coloured run at or
    /// beyond <paramref name="startRadius"/> gives the ring's inner and outer edge; the ring nearest the centre is used,
    /// so rings of neighbouring balls further out don't interfere. Returns null when too few rays agree.
    /// </summary>
    public static RingReading? MeasureRing(Bitmap window, double cx, double cy, double startRadius, double maxRadius, IReadOnlyList<Color> ringColors)
    {
        var px = ReadBgra(window, out int rowBytes);
        return MeasureRing(px, window.Width, window.Height, rowBytes, cx, cy, startRadius, maxRadius, new Palette(ringColors));
    }

    private static RingReading? MeasureRing(byte[] px, int w, int h, int rowBytes, double cx, double cy, double startRadius, double maxRadius, Palette ringColors, bool outermostFirst = false)
    {
        const int rays = 28;
        var outer = new List<double>();
        var thick = new List<double>();

        for (int k = 0; k < rays; k++)
        {
            double ang = 2 * Math.PI * k / rays, dx = Math.Cos(ang), dy = Math.Sin(ang);

            bool IsRing(double r)
            {
                int x = (int)Math.Round(cx + dx * r), y = (int)Math.Round(cy + dy * r);
                return x >= 0 && y >= 0 && x < w && y < h && IsRingColor(px, y * rowBytes + x * 4, ringColors);
            }

            double rInner, rOuter;
            if (outermostFirst)
            {
                // Used when first finding a ring: the outermost band is the ring even if the ball itself is ring-coloured
                rOuter = -1;
                for (double r = maxRadius; r >= Math.Max(startRadius, 1); r -= 1)
                    if (IsRing(r)) { rOuter = r; break; }
                if (rOuter < 0) continue;

                rInner = rOuter;
                int gap = 0;
                for (double r = rOuter - 1; r >= Math.Max(startRadius, 1); r -= 1)
                {
                    if (IsRing(r)) { rInner = r; gap = 0; }
                    else if (++gap > 1) break;
                }
            }
            else
            {
                rInner = -1;
                for (double r = Math.Max(startRadius, 1); r <= maxRadius; r += 1)
                    if (IsRing(r)) { rInner = r; break; }
                if (rInner < 0) continue;

                // The ring belonging to this ball is the one nearest to it; rings of neighbouring balls lie further out
                rOuter = rInner;
                int miss = 0;
                for (double r = rInner + 1; r <= maxRadius; r += 1)
                {
                    if (IsRing(r)) { rOuter = r; miss = 0; }
                    else if (++miss > 1) break;
                }
            }
            outer.Add(rOuter);
            thick.Add(rOuter - rInner + 1);
        }

        if (outer.Count < rays * 0.4) return null;
        double median = outer.OrderBy(v => v).ElementAt(outer.Count / 2);

        // Rays that disagree with the others hit something else with a ring-like colour
        var agree = outer.Select((v, i) => (v, i)).Where(t => Math.Abs(t.v - median) <= 3).Select(t => t.i).ToList();
        if (agree.Count < rays * 0.4) return null;

        double thickness = agree.Select(i => thick[i]).OrderBy(v => v).ElementAt(agree.Count / 2);
        return new RingReading(agree.Select(i => outer[i]).Average(), thickness);
    }

    /// <summary>
    /// Estimates the radius of the ball inside a ring: along rays from the centre, the outermost strong colour change
    /// before the ring is the ball's edge. The middle of the ball (a digit, say) is skipped. Works for any ball colour.
    /// Returns null when the rays don't agree.
    /// </summary>
    public static double? EstimateBallRadius(Bitmap window, double cx, double cy, double ringInnerRadius)
    {
        var px = ReadBgra(window, out int rowBytes);
        int w = window.Width, h = window.Height;
        const int rays = 24;
        double from = ringInnerRadius * 0.28, to = ringInnerRadius - 4;
        if (to - from < 8) return null;

        double Dist(int x1, int y1, int x2, int y2)
        {
            int a = y1 * rowBytes + x1 * 4, b = y2 * rowBytes + x2 * 4;
            int db = px[a] - px[b], dg = px[a + 1] - px[b + 1], dr = px[a + 2] - px[b + 2];
            return Math.Sqrt(db * db + dg * dg + dr * dr);
        }

        var edges = new List<double>();
        for (int k = 0; k < rays; k++)
        {
            double ang = 2 * Math.PI * k / rays, dx = Math.Cos(ang), dy = Math.Sin(ang);
            var jumps = new List<(double R, double D)>();
            for (double r = from; r <= to; r += 1)
            {
                int x1 = (int)Math.Round(cx + dx * (r - 2)), y1 = (int)Math.Round(cy + dy * (r - 2));
                int x2 = (int)Math.Round(cx + dx * (r + 2)), y2 = (int)Math.Round(cy + dy * (r + 2));
                if (x1 < 0 || y1 < 0 || x2 < 0 || y2 < 0 || x1 >= w || y1 >= h || x2 >= w || y2 >= h) continue;
                jumps.Add((r, Dist(x1, y1, x2, y2)));
            }
            if (jumps.Count == 0) continue;

            double strongest = jumps.Max(j => j.D);
            if (strongest < 40) continue;
            // The outermost jump that is nearly as strong as the strongest one
            edges.Add(jumps.Where(j => j.D >= strongest * 0.6).Max(j => j.R));
        }

        if (edges.Count < rays * 0.5) return null;
        double median = edges.OrderBy(v => v).ElementAt(edges.Count / 2);
        var agree = edges.Where(e => Math.Abs(e - median) <= 4).ToList();
        return agree.Count >= rays * 0.4 ? agree.Average() : null;
    }
}
