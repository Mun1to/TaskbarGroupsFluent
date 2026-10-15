using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Builds a group's taskbar icon out of the apps inside it, two by two, so the
    /// button says what it opens without the user having to design anything. Up to
    /// four apps are shown; the rest of the group still opens in the flyout.
    /// </summary>
    public static class GroupIconGrid
    {
        public static Bitmap Compose(IEnumerable<Image> icons, int size = 256)
        {
            var list = icons.Where(i => i is not null).Take(4).ToList();
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);

            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            if (list.Count == 0) return bmp;

            // A lone app fills most of the square: a 2x2 grid with three empty
            // cells would read as a broken icon.
            if (list.Count == 1)
            {
                float pad = size * 0.08f;
                Draw(g, list[0], pad, pad, size - 2 * pad);
                return bmp;
            }

            // The gap is what keeps four icons readable at taskbar size, where the
            // whole square ends up around 24 pixels and each cell under 12.
            float gap = size * 0.08f;
            float cell = (size - gap) / 2f;
            float lower = cell + gap;

            // Cells fill left to right, top to bottom. With two or three apps the
            // incomplete row is centred, so the icon stays balanced.
            var cells = list.Count switch
            {
                2 => new[] { (0f, size / 2f - cell / 2f), (lower, size / 2f - cell / 2f) },
                3 => new[] { (0f, 0f), (lower, 0f), (size / 2f - cell / 2f, lower) },
                _ => new[] { (0f, 0f), (lower, 0f), (0f, lower), (lower, lower) },
            };

            for (int i = 0; i < cells.Length; i++)
                Draw(g, list[i], cells[i].Item1, cells[i].Item2, cell);

            return bmp;
        }

        private static void Draw(Graphics g, Image icon, float x, float y, float side)
        {
            // Keep the icon's own proportions inside its square cell.
            float scale = Math.Min(side / icon.Width, side / icon.Height);
            float w = icon.Width * scale, h = icon.Height * scale;
            g.DrawImage(icon, x + (side - w) / 2f, y + (side - h) / 2f, w, h);
        }
    }
}
