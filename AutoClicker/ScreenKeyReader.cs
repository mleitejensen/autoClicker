using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace AutoClicker;

/// <summary>A key glyph the user taught the reader (one look of one key). Stored as a 16x16 bitmap.</summary>
public sealed class GlyphTemplate
{
    public string Key { get; set; } = "";
    public string Bits { get; set; } = "";   // GlyphSize*GlyphSize chars of '0'/'1'
    public double Aspect { get; set; }       // original width / height
}

/// <summary>
/// Reads key prompts (e.g. "A" or "S D") from a screen region. Each glyph is split out of the region,
/// then identified by learned templates first (what the user taught) and Windows OCR otherwise.
/// </summary>
public static class ScreenKeyReader
{
    public const int GlyphSize = 16;
    private const double MaxGlyphDistance = 0.12;   // fraction of differing cells for a learned match
    private const double MinMatchMargin = 0.04;     // the best key must beat every other key by this much
    private const double MaxAspectDifference = 0.45;
    private const int LetterTolerance = 40;   // colour tolerance when the letter colour is known

    private sealed class Blob
    {
        public bool[,] Mask = null!;   // [x, y], true = glyph pixel, cropped to the glyph's bounds
        public int X, Y;               // where the crop sits in the captured region
        public int Width => Mask.GetLength(0);
        public int Height => Mask.GetLength(1);
    }

    public sealed class ReadResult
    {
        public List<string> Keys { get; } = new();
        public int Unrecognised { get; set; }
        public int GlyphCount => Keys.Count + Unrecognised;
        public bool IsComplete => GlyphCount > 0 && Unrecognised == 0;
        public override string ToString() =>
            GlyphCount == 0 ? "nothing found" : string.Join(" ", Keys) + (Unrecognised > 0 ? $" (+{Unrecognised} unreadable)" : "");
    }

    public static Bitmap Capture(Rectangle region)
    {
        var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(region.X, region.Y, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }

    public static ReadResult Read(Rectangle region, IReadOnlyList<GlyphTemplate> templates, Color? letterColor = null)
    {
        using var bmp = Capture(region);
        return Read(bmp, templates, letterColor);
    }

    public static ReadResult Read(Bitmap bmp, IReadOnlyList<GlyphTemplate> templates, Color? letterColor = null)
    {
        var result = new ReadResult();
        foreach (var blob in Segment(bmp, letterColor))
        {
            string? key = MatchTemplate(blob, templates) ?? Ocr(blob);
            if (key != null) result.Keys.Add(key);
            else result.Unrecognised++;
        }
        return result;
    }

    /// <summary>One arrow badge of a prompt: which way it points (null if unreadable) and whether it has turned green (already pressed).</summary>
    public sealed record ArrowReading(string? Key, bool IsGreen, int X);

    /// <summary>
    /// Reads a row of arrow badges left to right. Arrows are told apart by shape alone (no taught looks or OCR), and a
    /// badge counts as pressed once its colour has turned green.
    /// </summary>
    public static List<ArrowReading> ReadArrows(Bitmap bmp)
    {
        var list = new List<ArrowReading>();
        foreach (var badge in FindBadges(bmp))
            list.Add(new ArrowReading(badge.Arrow == null ? null : ClassifyArrow(badge.Arrow), badge.IsGreen, badge.X));
        return list;
    }

    public static string DescribeArrows(IReadOnlyList<ArrowReading> arrows) =>
        arrows.Count == 0 ? "no arrows found" : string.Join("  ", arrows.Select(a => $"{a.Key ?? "?"}{(a.IsGreen ? "✓" : "")}"));

    private sealed record Badge(Blob? Arrow, bool IsGreen, int X);

    /// <summary>
    /// Finds the round badges in a row and the arrow drawn on each. Badges are whatever differs clearly from the
    /// background colour (so a yellow or a red backdrop both work); the arrow is the part of a badge that is clearly
    /// lighter than the badge itself, and a badge counts as pressed once its own colour has turned green.
    /// </summary>
    private static List<Badge> FindBadges(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        var rr = new byte[w, h]; var gg = new byte[w, h]; var bb = new byte[w, h]; var lum = new int[w, h];
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
                    rr[x, y] = row[i + 2]; gg[x, y] = row[i + 1]; bb[x, y] = row[i];
                    lum[x, y] = (row[i + 2] * 299 + row[i + 1] * 587 + row[i] * 114) / 1000;
                }
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        // Background colour: the median colour around the region's edge
        var er = new List<byte>(); var eg = new List<byte>(); var eb = new List<byte>();
        void Edge(int x, int y) { er.Add(rr[x, y]); eg.Add(gg[x, y]); eb.Add(bb[x, y]); }
        for (int x = 0; x < w; x++) { Edge(x, 0); Edge(x, h - 1); }
        for (int y = 1; y < h - 1; y++) { Edge(0, y); Edge(w - 1, y); }
        byte Median(List<byte> v) { v.Sort(); return v[v.Count / 2]; }
        byte bgR = Median(er), bgG = Median(eg), bgB = Median(eb);

        var fg = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                fg[x, y] = Math.Max(Math.Abs(rr[x, y] - bgR), Math.Max(Math.Abs(gg[x, y] - bgG), Math.Abs(bb[x, y] - bgB))) > 60;

        var shapes = Components(fg, w, h);   // wide bands (borders, bars) are already left out
        int tallest = shapes.Count == 0 ? 0 : shapes.Max(s => s.Height);
        shapes.RemoveAll(s => s.Height < tallest * 0.4 || s.Width < 6);

        var badges = new List<Badge>();
        foreach (var shape in shapes)
        {
            FillHoles(shape);   // an arrow close to the backdrop colour would otherwise be a hole in the badge
            var lums = new List<int>();
            for (int x = 0; x < shape.Width; x++)
                for (int y = 0; y < shape.Height; y++)
                    if (shape.Mask[x, y]) lums.Add(lum[shape.X + x, shape.Y + y]);
            lums.Sort();
            int threshold = lums[lums.Count / 2] + 45;   // the badge itself dominates, so its median is the badge colour

            long r = 0, g = 0, b = 0, n = 0;
            var bright = new bool[shape.Width, shape.Height];
            for (int x = 0; x < shape.Width; x++)
                for (int y = 0; y < shape.Height; y++)
                {
                    if (!shape.Mask[x, y]) continue;
                    int px = shape.X + x, py = shape.Y + y;
                    if (lum[px, py] > threshold) { bright[x, y] = true; continue; }
                    r += rr[px, py]; g += gg[px, py]; b += bb[px, py]; n++;
                }

            bool green = n > 0 && g / n - r / n > 25 && g / n - b / n > 25;
            badges.Add(new Badge(LargestGroup(bright, shape.X, shape.Y), green, shape.X));
        }
        return badges;
    }

    // Marks every empty pixel that is enclosed by the shape (not reachable from its bounding box edge) as part of it
    private static void FillHoles(Blob shape)
    {
        int w = shape.Width, h = shape.Height;
        var outside = new bool[w, h];
        var stack = new Stack<(int X, int Y)>();

        void Push(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || outside[x, y] || shape.Mask[x, y]) return;
            outside[x, y] = true;
            stack.Push((x, y));
        }
        for (int x = 0; x < w; x++) { Push(x, 0); Push(x, h - 1); }
        for (int y = 0; y < h; y++) { Push(0, y); Push(w - 1, y); }
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            Push(x + 1, y); Push(x - 1, y); Push(x, y + 1); Push(x, y - 1);
        }

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (!shape.Mask[x, y] && !outside[x, y]) shape.Mask[x, y] = true;
    }

    // The biggest connected group of lit pixels: the arrow, rather than a small highlight on the badge
    private static Blob? LargestGroup(bool[,] lit, int offsetX, int offsetY)
    {
        int w = lit.GetLength(0), h = lit.GetLength(1);
        var seen = new bool[w, h];
        var stack = new Stack<(int X, int Y)>();
        List<(int X, int Y)>? best = null;

        for (int sx = 0; sx < w; sx++)
            for (int sy = 0; sy < h; sy++)
            {
                if (!lit[sx, sy] || seen[sx, sy]) continue;
                var cells = new List<(int X, int Y)>();
                stack.Push((sx, sy)); seen[sx, sy] = true;
                while (stack.Count > 0)
                {
                    var (x, y) = stack.Pop();
                    cells.Add((x, y));
                    foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                    {
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[nx, ny] || !lit[nx, ny]) continue;
                        seen[nx, ny] = true;
                        stack.Push((nx, ny));
                    }
                }
                if (best == null || cells.Count > best.Count) best = cells;
            }

        if (best == null || best.Count < 12) return null;
        int x0 = best.Min(c => c.X), x1 = best.Max(c => c.X), y0 = best.Min(c => c.Y), y1 = best.Max(c => c.Y);
        var mask = new bool[x1 - x0 + 1, y1 - y0 + 1];
        foreach (var (cx, cy) in best) mask[cx - x0, cy - y0] = true;
        return new Blob { Mask = mask, X = offsetX + x0, Y = offsetY + y0 };
    }

    /// <summary>
    /// Works out which way an arrow points. The shape is compared with the four rotations of a reference arrow
    /// (in a few proportions); the direction is only reported when one rotation is clearly the best.
    /// </summary>
    private static string? ClassifyArrow(Blob shape)
    {
        if (shape.Width < 6 || shape.Height < 6) return null;

        var (bits, _) = Normalize(shape);
        var best = new Dictionary<string, double>();
        foreach (var (direction, template) in ArrowTemplates)
        {
            double d = Distance(bits, template);
            if (!best.TryGetValue(direction, out var current) || d < current) best[direction] = d;
        }

        var ranked = best.OrderBy(kv => kv.Value).ToList();
        if (ranked[0].Value > 0.28 || ranked[1].Value - ranked[0].Value < 0.06) return null;
        return ranked[0].Key;
    }

    // Reference arrows: a Down arrow (shaft on top, triangular head below) rasterised to the glyph grid in a few
    // proportions, then turned into Up, Left and Right.
    private static readonly List<(string Direction, string Bits)> ArrowTemplates = BuildArrowTemplates();

    private static List<(string, string)> BuildArrowTemplates()
    {
        var list = new List<(string, string)>();
        foreach (var (shaft, headStart) in new[] { (0.34, 0.45), (0.46, 0.45), (0.34, 0.60), (0.46, 0.60), (0.55, 0.55) })
        {
            int n = GlyphSize;
            var down = new bool[n, n];   // [row, column]
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double px = (x + 0.5) / n, py = (y + 0.5) / n;
                    down[y, x] = py < headStart
                        ? Math.Abs(px - 0.5) <= shaft / 2
                        : Math.Abs(px - 0.5) <= 0.5 * (1 - py) / (1 - headStart);
                }

            string Make(Func<int, int, bool> at)
            {
                var sb = new StringBuilder(n * n);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++) sb.Append(at(y, x) ? '1' : '0');
                return sb.ToString();
            }

            list.Add(("Down", Make((y, x) => down[y, x])));
            list.Add(("Up", Make((y, x) => down[n - 1 - y, x])));
            list.Add(("Right", Make((y, x) => down[x, y])));
            list.Add(("Left", Make((y, x) => down[n - 1 - x, y])));
        }
        return list;
    }

    /// <summary>Splits the region into glyphs and stores each as a template for the matching letter of <paramref name="text"/>.</summary>
    public static bool TryLearn(Bitmap bmp, string text, List<GlyphTemplate> templates, out string message, Color? letterColor = null)
    {
        var keys = text.Where(c => !char.IsWhiteSpace(c)).Select(c => char.ToUpperInvariant(c).ToString()).ToList();
        var blobs = Segment(bmp, letterColor);

        if (keys.Count == 0) { message = "Type the key(s) shown in the region."; return false; }
        if (keys.Any(k => !KeyRegistry.SimulationKeys.ContainsKey(k))) { message = "Only letters and digits can be learned."; return false; }
        if (blobs.Count != keys.Count)
        {
            message = $"Found {blobs.Count} glyph(s) in the region but you typed {keys.Count}. Adjust the region so it tightly covers the prompt.";
            return false;
        }

        for (int i = 0; i < keys.Count; i++)
        {
            var (bits, aspect) = Normalize(blobs[i]);
            // Re-teaching an identical look must not pile up duplicates
            if (templates.Any(t => t.Key == keys[i] && Distance(bits, t.Bits) < 0.02)) continue;

            templates.Add(new GlyphTemplate { Key = keys[i], Bits = bits, Aspect = aspect });
            // Keep the newest looks per key so a changing prompt can't grow the list forever
            var same = templates.Where(t => t.Key == keys[i]).ToList();
            if (same.Count > 6) templates.Remove(same[0]);
        }

        message = $"Learned {string.Join(" ", keys)}.";
        return true;
    }

    private static string? MatchTemplate(Blob blob, IReadOnlyList<GlyphTemplate> templates)
    {
        if (templates.Count == 0) return null;
        var (bits, aspect) = Normalize(blob);

        // Best distance per key, so the winner can be compared with the closest *different* key
        var bestPerKey = new Dictionary<string, double>();
        foreach (var t in templates)
        {
            if (t.Bits.Length != bits.Length) continue;
            if (Math.Abs(t.Aspect - aspect) > MaxAspectDifference * Math.Max(t.Aspect, aspect)) continue;
            double d = Distance(bits, t.Bits);
            if (!bestPerKey.TryGetValue(t.Key, out var current) || d < current) bestPerKey[t.Key] = d;
        }
        if (bestPerKey.Count == 0) return null;

        var ranked = bestPerKey.OrderBy(kv => kv.Value).ToList();
        if (ranked[0].Value > MaxGlyphDistance) return null;

        // Look-alike letters (E/F, O/Q, C/G) differ by only a few cells: if another key is nearly as close, refuse to guess
        if (ranked.Count > 1 && ranked[1].Value - ranked[0].Value < MinMatchMargin) return null;
        return ranked[0].Key;
    }

    private static double Distance(string a, string b)
    {
        int diff = 0;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) diff++;
        return diff / (double)a.Length;
    }

    private static (string Bits, double Aspect) Normalize(Blob blob)
    {
        var sb = new StringBuilder(GlyphSize * GlyphSize);
        for (int gy = 0; gy < GlyphSize; gy++)
        {
            for (int gx = 0; gx < GlyphSize; gx++)
            {
                // A cell is set when most of the source pixels it covers are glyph pixels
                int x0 = gx * blob.Width / GlyphSize, x1 = Math.Max((gx + 1) * blob.Width / GlyphSize, x0 + 1);
                int y0 = gy * blob.Height / GlyphSize, y1 = Math.Max((gy + 1) * blob.Height / GlyphSize, y0 + 1);
                int on = 0, total = 0;
                for (int y = y0; y < y1 && y < blob.Height; y++)
                    for (int x = x0; x < x1 && x < blob.Width; x++) { total++; if (blob.Mask[x, y]) on++; }
                sb.Append(total > 0 && on * 2 >= total ? '1' : '0');
            }
        }
        return (sb.ToString(), blob.Width / (double)blob.Height);
    }

    /// <summary>
    /// Separates glyph pixels from background (the background is whatever colour dominates the region's edge,
    /// so light-on-dark and dark-on-light both work) and splits the glyphs into left-to-right blobs.
    /// </summary>
    private static List<Blob> Segment(Bitmap bmp, Color? letterColor)
    {
        int w = bmp.Width, h = bmp.Height;
        var gray = new byte[w, h];
        var fg = new bool[w, h];
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
                    gray[x, y] = (byte)((r * 299 + g * 587 + b * 114) / 1000);

                    // With a known letter colour only that colour is the glyph, so outlines and scenery drop out
                    if (letterColor.HasValue) fg[x, y] = ScreenCondition.ColorMatches(r, g, b, letterColor.Value, LetterTolerance);
                }
            }
        }
        finally
        {
            bmp.UnlockBits(data);
        }

        if (letterColor.HasValue)
        {
            // A letter colour that matches most of the region (e.g. a plain white background) can't isolate any glyph
            int covered = 0;
            foreach (var on in fg) if (on) covered++;
            if (covered > w * h * 0.7) return new List<Blob>();
        }
        else
        {
            int threshold = Otsu(gray, w, h);

            // Classify the edge ring to decide which side of the threshold is the background
            int edgeLight = 0, edgeDark = 0;
            for (int x = 0; x < w; x++) { Tally(gray[x, 0]); Tally(gray[x, h - 1]); }
            for (int y = 1; y < h - 1; y++) { Tally(gray[0, y]); Tally(gray[w - 1, y]); }
            void Tally(byte v) { if (v > threshold) edgeLight++; else edgeDark++; }
            bool glyphIsLight = edgeDark >= edgeLight;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    fg[x, y] = glyphIsLight ? gray[x, y] > threshold : gray[x, y] <= threshold;
        }

        var blobs = ColumnRuns(fg, w, h);

        // Drop specks (noise, stray antialiasing) that are far smaller than the tallest glyph
        int tallest = blobs.Count == 0 ? 0 : blobs.Max(b => b.Height);
        blobs.RemoveAll(b => b.Height < tallest * 0.4 || b.Width < 2);
        return blobs;
    }

    // Column runs that contain glyph pixels become blobs (letters sit side by side)
    private static List<Blob> ColumnRuns(bool[,] fg, int w, int h)
    {
        var columnHasGlyph = new bool[w];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (fg[x, y]) { columnHasGlyph[x] = true; break; }

        var blobs = new List<Blob>();
        int start = -1;
        for (int x = 0; x <= w; x++)
        {
            bool on = x < w && columnHasGlyph[x];
            if (on && start < 0) start = x;
            else if (!on && start >= 0)
            {
                var blob = Crop(fg, start, x - 1, h);
                if (blob != null) blobs.Add(blob);
                start = -1;
            }
        }
        return blobs;
    }

    // Separate connected shapes, so a band or border touching several glyphs can't fuse them into one
    private static List<Blob> Components(bool[,] fg, int w, int h)
    {
        var seen = new bool[w, h];
        var blobs = new List<Blob>();
        var stack = new Stack<(int X, int Y)>();

        for (int sx = 0; sx < w; sx++)
            for (int sy = 0; sy < h; sy++)
            {
                if (!fg[sx, sy] || seen[sx, sy]) continue;

                var cells = new List<(int X, int Y)>();
                stack.Push((sx, sy)); seen[sx, sy] = true;
                while (stack.Count > 0)
                {
                    var (x, y) = stack.Pop();
                    cells.Add((x, y));
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h || seen[nx, ny] || !fg[nx, ny]) continue;
                            seen[nx, ny] = true;
                            stack.Push((nx, ny));
                        }
                }

                int x0 = cells.Min(c => c.X), x1 = cells.Max(c => c.X), y0 = cells.Min(c => c.Y), y1 = cells.Max(c => c.Y);
                int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
                if (bw > bh * 2) continue;   // a wide band (border, bar) is not a badge

                var mask = new bool[bw, bh];
                foreach (var (x, y) in cells) mask[x - x0, y - y0] = true;
                blobs.Add(new Blob { Mask = mask, X = x0, Y = y0 });
            }

        blobs.Sort((a, b) => a.X.CompareTo(b.X));
        return blobs;
    }

    private static Blob? Crop(bool[,] fg, int x0, int x1, int h)
    {
        int top = h, bottom = -1;
        for (int x = x0; x <= x1; x++)
            for (int y = 0; y < h; y++)
                if (fg[x, y]) { top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
        if (bottom < top) return null;

        var mask = new bool[x1 - x0 + 1, bottom - top + 1];
        for (int x = x0; x <= x1; x++)
            for (int y = top; y <= bottom; y++)
                mask[x - x0, y - top] = fg[x, y];
        return new Blob { Mask = mask, X = x0, Y = top };
    }

    private static int Otsu(byte[,] gray, int w, int h)
    {
        var hist = new int[256];
        foreach (var v in gray) hist[v]++;

        long total = (long)w * h, sumAll = 0;
        for (int i = 0; i < 256; i++) sumAll += (long)i * hist[i];

        long weightBg = 0, sumBg = 0;
        double bestVariance = -1;
        int best = 127;
        for (int t = 0; t < 256; t++)
        {
            weightBg += hist[t];
            if (weightBg == 0) continue;
            long weightFg = total - weightBg;
            if (weightFg == 0) break;

            sumBg += (long)t * hist[t];
            double meanBg = sumBg / (double)weightBg;
            double meanFg = (sumAll - sumBg) / (double)weightFg;
            double variance = (double)weightBg * weightFg * (meanBg - meanFg) * (meanBg - meanFg);
            if (variance > bestVariance) { bestVariance = variance; best = t; }
        }
        return best;
    }

    /// <summary>Describes how each glyph in the region was (or wasn't) identified, for troubleshooting misreads.</summary>
    public static string Diagnose(Bitmap bmp, IReadOnlyList<GlyphTemplate> templates, Color? letterColor = null)
    {
        var sb = new StringBuilder();
        var blobs = Segment(bmp, letterColor);
        sb.AppendLine($"region {bmp.Width}x{bmp.Height}, {blobs.Count} glyph(s), {templates.Count} taught look(s), letterColor={(letterColor.HasValue ? letterColor.Value.ToString() : "auto")}");
        for (int i = 0; i < blobs.Count; i++)
        {
            var blob = blobs[i];
            var (bits, aspect) = Normalize(blob);
            var ranked = templates
                .Where(t => t.Bits.Length == bits.Length)
                .GroupBy(t => t.Key)
                .Select(g => (Key: g.Key, D: g.Min(t => Distance(bits, t.Bits)), AspectOk: g.Any(t => Math.Abs(t.Aspect - aspect) <= MaxAspectDifference * Math.Max(t.Aspect, aspect))))
                .OrderBy(r => r.D).Take(3).ToList();
            string matches = ranked.Count == 0 ? "no templates" : string.Join(", ", ranked.Select(r => $"{r.Key}={r.D:F3}{(r.AspectOk ? "" : "(aspect)")}"));
            sb.AppendLine($"  glyph {i}: {blob.Width}x{blob.Height} aspect {aspect:F2}; nearest taught: {matches}; match={MatchTemplate(blob, templates) ?? "-"}; ocr={Ocr(blob) ?? "-"}");
        }
        return sb.ToString();
    }

    /// <summary>Writes what the reader sees (the captured region and the glyph mask it extracted) so a misread can be inspected.</summary>
    public static void SaveDebug(Bitmap bmp, string directory, Color? letterColor = null, bool arrows = false)
    {
        try
        {
            Directory.CreateDirectory(directory);
            bmp.Save(Path.Combine(directory, "region.png"), ImageFormat.Png);

            var blobs = arrows
                ? FindBadges(bmp).Where(b => b.Arrow != null).Select(b => b.Arrow!).ToList()
                : Segment(bmp, letterColor);
            const int scale = 6, gap = 12;
            int width = Math.Max(1, blobs.Sum(b => b.Width * scale + gap));
            int height = Math.Max(1, blobs.Count == 0 ? 1 : blobs.Max(b => b.Height) * scale);
            using var img = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(img))
            {
                g.Clear(Color.White);
                int x0 = 0;
                foreach (var b in blobs)
                {
                    for (int x = 0; x < b.Width; x++)
                        for (int y = 0; y < b.Height; y++)
                            if (b.Mask[x, y]) g.FillRectangle(Brushes.Black, x0 + x * scale, y * scale, scale, scale);
                    x0 += b.Width * scale + gap;
                }
            }
            img.Save(Path.Combine(directory, "glyphs.png"), ImageFormat.Png);
        }
        catch { /* diagnostics must never break the editor */ }
    }

    private static OcrEngine? _engine;
    private static bool _engineChecked;

    // Windows OCR ignores isolated single characters, but reads them fine as the last word of a line,
    // so each glyph is rendered next to a fixed anchor word and only the text after the anchor is used.
    private const string OcrAnchor = "Press";

    // A guess is worse than no answer (it presses a wrong key), so OCR must agree with itself at two sizes
    private static string? Ocr(Blob blob)
    {
        string? first = OcrAt(blob, 64);
        if (first == null) return null;
        return OcrAt(blob, 96) == first ? first : null;
    }

    private static string? OcrAt(Blob blob, int glyphHeight)
    {
        try
        {
            if (!_engineChecked)
            {
                _engine = OcrEngine.TryCreateFromUserProfileLanguages();
                _engineChecked = true;
            }
            if (_engine == null) return null;

            // Black-on-white and scaled to a fixed height, which OCR handles far better than raw game pixels
            const int pad = 24;
            float anchorPx = glyphHeight / 0.716f; // Arial cap height is ~0.716 em
            float glyphScale = glyphHeight / (float)blob.Height;
            int glyphWidth = Math.Max(1, (int)Math.Round(blob.Width * glyphScale));

            using var font = new Font("Arial", anchorPx, FontStyle.Regular, GraphicsUnit.Pixel);
            using var measure = Graphics.FromHwnd(IntPtr.Zero);
            var anchorSize = measure.MeasureString(OcrAnchor, font, int.MaxValue, StringFormat.GenericTypographic);
            int gap = (int)(anchorPx * 0.35f);

            int width = pad * 2 + (int)Math.Ceiling(anchorSize.Width) + gap + glyphWidth;
            int height = pad * 2 + (int)Math.Ceiling(anchorSize.Height);
            using var img = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(img))
            {
                g.Clear(Color.White);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.DrawString(OcrAnchor, font, Brushes.Black, pad, pad, StringFormat.GenericTypographic);

                // Smooth upscaling: blocky mask edges confuse OCR far more than a slightly soft glyph does
                using var small = new Bitmap(blob.Width, blob.Height, PixelFormat.Format32bppArgb);
                for (int x = 0; x < blob.Width; x++)
                    for (int y = 0; y < blob.Height; y++)
                        small.SetPixel(x, y, blob.Mask[x, y] ? Color.Black : Color.White);

                // Align the glyph's bottom with the anchor's baseline area
                int gx0 = pad + (int)Math.Ceiling(anchorSize.Width) + gap;
                int gy0 = pad + (int)(anchorPx * 0.93f) - glyphHeight;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(small, new Rectangle(gx0, gy0, glyphWidth, glyphHeight), 0, 0, blob.Width, blob.Height, GraphicsUnit.Pixel);
            }

            string text = Task.Run(() => RecognizeAsync(img)).GetAwaiter().GetResult().Trim();
            if (!text.StartsWith(OcrAnchor, StringComparison.OrdinalIgnoreCase)) return null;

            foreach (char c in text.Substring(OcrAnchor.Length))
            {
                string key = char.ToUpperInvariant(c).ToString();
                if (char.IsLetterOrDigit(c) && KeyRegistry.SimulationKeys.ContainsKey(key)) return key;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> RecognizeAsync(Bitmap img)
    {
        using var ms = new MemoryStream();
        img.Save(ms, ImageFormat.Png);

        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(ms.ToArray());
            await writer.StoreAsync();
            writer.DetachStream();
        }
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var software = await decoder.GetSoftwareBitmapAsync();
        var ocr = await _engine!.RecognizeAsync(software);
        return ocr.Text ?? "";
    }
}
