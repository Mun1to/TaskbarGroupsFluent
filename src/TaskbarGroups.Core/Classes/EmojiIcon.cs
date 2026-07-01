using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Renders a colour emoji glyph to a square bitmap, for use as a group icon.
    /// WPF's <c>RenderTargetBitmap</c> rasterises colour fonts as solid black, so we
    /// render through Skia (which honours the Segoe UI Emoji colour layers) and then
    /// crop to the glyph's ink bounds and centre it, so every emoji fills the icon
    /// consistently regardless of the font's per-glyph metrics.
    /// </summary>
    public static class EmojiIcon
    {
        /// <summary>Renders <paramref name="emoji"/> centred on a transparent square.</summary>
        public static Bitmap Render(string emoji, int size)
        {
            int codepoint = char.ConvertToUtf32(emoji, 0);
            using SKTypeface typeface =
                SKFontManager.Default.MatchCharacter("Segoe UI Emoji", SKFontStyle.Normal, null, codepoint)
                ?? SKTypeface.FromFamilyName("Segoe UI Emoji");

            // Render on a roomy (2x) canvas so no glyph gets clipped, then crop+centre.
            int big = size * 2;
            var info = new SKImageInfo(big, big, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);
            SKCanvas canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            using var font = new SKFont(typeface, size) { Edging = SKFontEdging.SubpixelAntialias };
            using var paint = new SKPaint { IsAntialias = true };
            using (var blob = SKTextBlob.Create(emoji, font))
                if (blob != null) canvas.DrawText(blob, size * 0.5f, size * 1.4f, paint);
            canvas.Flush();

            using SKImage image = surface.Snapshot();
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = new MemoryStream(data.ToArray());
            using var rendered = new Bitmap(Image.FromStream(ms));

            return CropAndCenter(rendered, size, 0.88);
        }

        // Crop to the non-transparent content and re-centre it, scaled to fill a
        // fraction of the output square while preserving aspect ratio.
        private static Bitmap CropAndCenter(Bitmap src, int outSize, double fill)
        {
            Rectangle box = InkBounds(src);
            var outBmp = new Bitmap(outSize, outSize, PixelFormat.Format32bppArgb);
            if (box.Width <= 0 || box.Height <= 0) return outBmp;

            int target = (int)(outSize * fill);
            double scale = Math.Min((double)target / box.Width, (double)target / box.Height);
            int w = Math.Max(1, (int)Math.Round(box.Width * scale));
            int h = Math.Max(1, (int)Math.Round(box.Height * scale));

            using var g = Graphics.FromImage(outBmp);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(src, new Rectangle((outSize - w) / 2, (outSize - h) / 2, w, h),
                box.X, box.Y, box.Width, box.Height, GraphicsUnit.Pixel);
            return outBmp;
        }

        private static Rectangle InkBounds(Bitmap src)
        {
            int minX = src.Width, minY = src.Height, maxX = -1, maxY = -1;
            BitmapData d = src.LockBits(new Rectangle(0, 0, src.Width, src.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] bytes = new byte[d.Stride * src.Height];
                Marshal.Copy(d.Scan0, bytes, 0, bytes.Length);
                for (int y = 0; y < src.Height; y++)
                    for (int x = 0; x < src.Width; x++)
                        if (bytes[y * d.Stride + x * 4 + 3] > 16)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
            }
            finally { src.UnlockBits(d); }
            return maxX < minX ? Rectangle.Empty : new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
