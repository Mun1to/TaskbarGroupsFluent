using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using TaskbarGroups.Core;

namespace TaskbarGroups.Hover;

/// <summary>
/// Tells which group the cursor rests on by looking at the taskbar, for taskbars
/// that will not say it any other way (issue #12).
///
/// Start11 draws a taskbar of its own over the one of Windows 11 ("SDTaskbar"),
/// and it offers nothing to read: no buttons through UI Automation, no tooltip
/// while the cursor rests (both measured on ProNotion's PC). The Windows 11
/// taskbar is still there underneath, with its buttons squeezed to a few pixels
/// each, so reading it pointed at the wrong icon. What Start11 does show is the
/// icon itself, and the icon of a group is ours: we drew it. So the pixels around
/// the cursor are compared with each pinned group's icon, at the sizes a taskbar
/// draws icons, and the group whose icon is clearly there is the one under it.
///
/// It only ever runs when the cursor has come to rest on such a taskbar, and it
/// reads a small square of the screen around the cursor, in memory, nothing more.
/// </summary>
internal static class IconMatcher
{
    /// <summary>
    /// How different the screen may be from a group's icon and still be that icon,
    /// as <see cref="Score"/> counts it. Measured on a real Windows 11 taskbar with
    /// six groups and seven other apps: the groups scored 4.9 to 10.1, and no other
    /// app came under 27.4. On taskbars drawn in memory the way another program
    /// might, at 16 to 30 pixels: the groups came out at up to 13.7, one drawn in
    /// thin lines up to 27 at the smallest sizes, and another app's icon passed for
    /// that one at 22.7. Kept well under that: a group missed now and then costs a
    /// click, one opened that was not meant costs more.
    /// </summary>
    internal const double Threshold = 16;

    /// <summary>
    /// A pixel of the icon is compared when it is at least this opaque, and taken
    /// as background when it is less than <see cref="Clear"/>.
    /// </summary>
    private const int Covered = 64, Clear = 8;

    /// <summary>
    /// The group whose icon is under <paramref name="cursor"/> on the taskbar
    /// <paramref name="bar"/>, with the rectangle of its icon, or null when no
    /// group's icon is clearly there.
    /// </summary>
    public static (string Group, RECT Icon)? GroupAt(POINT cursor, RECT bar, IReadOnlyList<PinnedGroup> pins)
        => GroupAt(cursor, bar, pins, out _);

    /// <summary>As above, and with every group's best score, for tests and reports.</summary>
    public static (string Group, RECT Icon)? GroupAt(POINT cursor, RECT bar, IReadOnlyList<PinnedGroup> pins,
        out List<(string Group, double Score, RECT Icon)> scores)
    {
        scores = new();
        if (pins.Count == 0) return null;

        bool across = bar.Width >= bar.Height;          // a taskbar along the top or bottom
        int thick = across ? bar.Height : bar.Width;
        if (thick < 16) return null;

        // Where the icon's centre can be: within about a button of the cursor along
        // the bar, and close to the middle of the bar across it.
        int along = (int)Math.Round(thick * 0.75);
        int acrossSlack = Math.Max(2, (int)Math.Round(thick * 0.15));
        int maxSize = (int)Math.Round(thick * 0.70);
        int pad = maxSize / 2 + 2;

        Rectangle area = across
            ? Rectangle.FromLTRB(cursor.X - along - pad, (bar.Top + bar.Bottom) / 2 - acrossSlack - pad,
                                 cursor.X + along + pad, (bar.Top + bar.Bottom) / 2 + acrossSlack + pad)
            : Rectangle.FromLTRB((bar.Left + bar.Right) / 2 - acrossSlack - pad, cursor.Y - along - pad,
                                 (bar.Left + bar.Right) / 2 + acrossSlack + pad, cursor.Y + along + pad);
        area.Intersect(Rectangle.FromLTRB(bar.Left, bar.Top, bar.Right, bar.Bottom));
        if (area.Width <= 0 || area.Height <= 0) return null;

        Screen screen;
        try { screen = Screen.Grab(area); }
        catch { return null; }

        foreach (var pin in pins)
        {
            double best = double.MaxValue;
            RECT bestRect = default;
            foreach (int size in Sizes(thick))
            foreach (var template in Template.For(pin.GroupName, size))
            {
                var (score, x, y) = Search(screen, template, across, cursor, bar, along, acrossSlack);
                if (score < best)
                {
                    best = score;
                    bestRect = new RECT { Left = x, Top = y, Right = x + size, Bottom = y + size };
                }
            }
            if (best < double.MaxValue) scores.Add((pin.GroupName, best, bestRect));
        }

        scores.Sort((a, b) => a.Score.CompareTo(b.Score));

        // The search reaches past the button under the cursor, so a neighbouring
        // group's icon can be found too: of the icons that are clearly there, the
        // one meant is the one the cursor is on, which is the nearest. Two found in
        // the same place are the same icon used by two groups, and the closer
        // likeness wins.
        (string Group, double Score, RECT Icon)? pick = null;
        double pickDistance = double.MaxValue;
        foreach (var s in scores)
        {
            if (s.Score > Threshold) continue;
            double centre = across ? (s.Icon.Left + s.Icon.Right) / 2.0 : (s.Icon.Top + s.Icon.Bottom) / 2.0;
            double distance = Math.Abs(centre - (across ? cursor.X : cursor.Y));
            if (distance > thick * 0.5) continue;
            if (distance < pickDistance - 3) { pick = s; pickDistance = distance; }
        }
        return pick is { } p ? (p.Group, p.Icon) : null;
    }

    /// <summary>The icon sizes worth trying on a taskbar this thick.</summary>
    private static IEnumerable<int> Sizes(int thick)
    {
        int step = Math.Max(1, thick / 24);
        for (int s = (int)Math.Round(thick * 0.30); s <= (int)Math.Round(thick * 0.70); s += step)
            yield return s;
    }

    /// <summary>
    /// The best place for <paramref name="t"/> in the search range: a coarse pass
    /// every two pixels, then every pixel around the best of it.
    /// </summary>
    private static (double Score, int X, int Y) Search(Screen screen, Template t, bool across,
        POINT cursor, RECT bar, int along, int acrossSlack)
    {
        int midAcross = across ? (bar.Top + bar.Bottom) / 2 : (bar.Left + bar.Right) / 2;
        int centreAlong = across ? cursor.X : cursor.Y;

        double best = double.MaxValue;
        int bestA = 0, bestB = 0;
        for (int a = centreAlong - along; a <= centreAlong + along; a += 2)
            for (int b = midAcross - acrossSlack; b <= midAcross + acrossSlack; b += 2)
                Try(a, b);

        int coarseA = bestA, coarseB = bestB;
        for (int a = coarseA - 1; a <= coarseA + 1; a++)
            for (int b = coarseB - 1; b <= coarseB + 1; b++)
                Try(a, b);

        int half = t.Size / 2;
        return across
            ? (best, bestA - half, bestB - half)
            : (best, bestB - half, bestA - half);

        void Try(int a, int b)
        {
            int cx = across ? a : b, cy = across ? b : a;
            double s = Score(screen, t, cx - t.Size / 2, cy - t.Size / 2, best);
            if (s < best) { best = s; bestA = a; bestB = b; }
        }
    }

    /// <summary>
    /// How unlike the icon the screen is with the icon's top-left corner at
    /// (<paramref name="left"/>, <paramref name="top"/>), in screen pixels: the mean
    /// difference per colour channel, out of 255. A taskbar blends the edges of an
    /// icon, and all of an icon drawn in thin lines, with whatever is behind it, so
    /// the icon is laid over the background seen around it in the same spot (a
    /// highlight, a translucent bar) and compared as the taskbar would show it,
    /// each pixel counting as much as it is opaque.
    /// </summary>
    private static double Score(Screen screen, Template t, int left, int top, double giveUpAbove)
    {
        int x0 = left - screen.Left, y0 = top - screen.Top;
        if (x0 < 0 || y0 < 0 || x0 + t.Size > screen.Width || y0 + t.Size > screen.Height)
            return double.MaxValue;

        double br = 0, bg = 0, bb = 0;
        int nb = t.BackX.Length;
        for (int i = 0; i < nb; i++)
        {
            int px = screen.Pixels[(y0 + t.BackY[i]) * screen.Width + x0 + t.BackX[i]];
            br += (px >> 16) & 0xFF; bg += (px >> 8) & 0xFF; bb += px & 0xFF;
        }
        if (nb > 0) { br /= nb; bg /= nb; bb /= nb; }

        double limit = giveUpAbove * t.Weight * 3;
        double sum = 0;
        for (int i = 0; i < t.Count; i++)
        {
            int px = screen.Pixels[(y0 + t.Y[i]) * screen.Width + x0 + t.X[i]];
            double a = t.A[i], rest = 1 - a;
            sum += a * (Math.Abs(((px >> 16) & 0xFF) - (a * t.R[i] + rest * br))
                      + Math.Abs(((px >> 8) & 0xFF) - (a * t.G[i] + rest * bg))
                      + Math.Abs((px & 0xFF) - (a * t.B[i] + rest * bb)));
            if (sum > limit) return double.MaxValue;
        }
        return sum / (t.Weight * 3.0);
    }

    /// <summary>
    /// For tests only: hands over a picture of the given area instead of reading the
    /// screen, so a taskbar drawn in memory can stand in for one on screen.
    /// </summary>
    internal static Func<Rectangle, Bitmap>? PictureForTests;

    /// <summary>A copy of a piece of the screen, as 0xAARRGGBB values.</summary>
    private sealed class Screen
    {
        public int Left, Top, Width, Height;
        public int[] Pixels = Array.Empty<int>();

        public static Screen Grab(Rectangle area)
        {
            using var bmp = PictureForTests?.Invoke(area) ?? new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
            if (PictureForTests is null)
                using (var g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(area.Left, area.Top, 0, 0, area.Size, CopyPixelOperation.SourceCopy);
            var data = bmp.LockBits(new Rectangle(0, 0, area.Width, area.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var pixels = new int[area.Width * area.Height];
                for (int y = 0; y < area.Height; y++)
                    Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * area.Width, area.Width);
                return new Screen { Left = area.Left, Top = area.Top, Width = area.Width, Height = area.Height, Pixels = pixels };
            }
            finally { bmp.UnlockBits(data); }
        }
    }

    /// <summary>
    /// A group's icon at one size: the pixels it covers, with how opaque each is,
    /// and a sample of the ones it leaves clear, where the background shows. Built
    /// once per group and size, and again if the icon file changes.
    /// </summary>
    private sealed class Template
    {
        public int Size, Count;
        public double Weight;
        public int[] X = Array.Empty<int>(), Y = Array.Empty<int>();
        public int[] R = Array.Empty<int>(), G = Array.Empty<int>(), B = Array.Empty<int>();
        public double[] A = Array.Empty<double>();
        public int[] BackX = Array.Empty<int>(), BackY = Array.Empty<int>();

        private static readonly Dictionary<(string, int), (DateTime Written, List<Template> T)> Cache = new();

        /// <summary>
        /// The group's icon at <paramref name="size"/>, drawn the ways a taskbar may
        /// draw it: from the picture in the icon file nearest that size, or shrunk
        /// from a bigger one. A taskbar's choice shows most in icons made of thin
        /// lines, which come out quite different either way.
        /// </summary>
        public static List<Template> For(string group, int size)
        {
            string path = Path.Combine(Paths.ConfigPath, group, "GroupIcon.ico");
            DateTime written;
            try { written = File.GetLastWriteTimeUtc(path); }
            catch { return new(); }

            if (Cache.TryGetValue((group, size), out var hit) && hit.Written == written) return hit.T;
            var list = new List<Template>();
            foreach (int from in new[] { size, 32, 48 })
            {
                if (from < size && from != size) continue;
                try { if (Build(path, size, from) is { } t) list.Add(t); } catch { }
            }
            Cache[(group, size)] = (written, list);
            return list;
        }

        private static Template? Build(string path, int size, int from)
        {
            if (!File.Exists(path)) return null;
            using var icon = new Icon(path, from, from);
            using var raw = icon.ToBitmap();
            using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(raw, 0, 0, size, size);
            }

            var xs = new List<int>(); var ys = new List<int>();
            var rs = new List<int>(); var gs = new List<int>(); var bs = new List<int>();
            var alphas = new List<double>();
            var backX = new List<int>(); var backY = new List<int>();
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.A < Covered)
                    {
                        if (c.A < Clear) { backX.Add(x); backY.Add(y); }
                        continue;
                    }
                    xs.Add(x); ys.Add(y); rs.Add(c.R); gs.Add(c.G); bs.Add(c.B); alphas.Add(c.A / 255.0);
                }

            // An icon that covers too little of its square leaves too little to tell it by.
            double weight = alphas.Sum();
            if (weight < size * size / 10.0) return null;

            // A few dozen background pixels are plenty to tell its colour.
            int step = Math.Max(1, backX.Count / 48);
            return new Template
            {
                Size = size, Count = xs.Count, Weight = weight,
                X = xs.ToArray(), Y = ys.ToArray(), R = rs.ToArray(), G = gs.ToArray(), B = bs.ToArray(),
                A = alphas.ToArray(),
                BackX = backX.Where((_, i) => i % step == 0).ToArray(),
                BackY = backY.Where((_, i) => i % step == 0).ToArray()
            };
        }
    }
}
