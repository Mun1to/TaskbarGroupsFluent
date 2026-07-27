using System.Runtime.InteropServices;
using SkiaSharp;

// Final Taskbar Groups Fluent mark: the group's flyout panel popping out of its
// pinned taskbar button. Small sizes get a simplified drawing (fewer, bigger
// shapes) because the tail and the dimmed taskbar buttons turn to mush below 48 px.
//
// The app icon is generated, not hand-drawn, so it stays crisp at every size:
//   dotnet run --project tools/IconGen -- <outputFolder>
// then copy Icon.ico over src/assets/Icon.ico and logo-256.png over brand/logo.png.

string outDir = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(outDir);

var blueLight = new SKColor(0x4C, 0xC2, 0xFF);
var blueDark = new SKColor(0x00, 0x67, 0xC0);
var accent = new SKColor(0xFF, 0xC8, 0x3D);
var ink = new SKColor(0x1A, 0x1A, 0x1A);
var muted = new SKColor(0x60, 0x64, 0x6B);

SKPaint Fill(SKColor c) => new() { Color = c, IsAntialias = true, Style = SKPaintStyle.Fill };

SKPaint Grad(float s, SKColor a, SKColor b) => new()
{
    IsAntialias = true,
    Style = SKPaintStyle.Fill,
    Shader = SKShader.CreateLinearGradient(
        new SKPoint(0, 0), new SKPoint(s, s), new[] { a, b }, null, SKShaderTileMode.Clamp)
};

void RR(SKCanvas c, float s, float x, float y, float w, float h, float r, SKPaint p) =>
    c.DrawRoundRect(SKRect.Create(x * s, y * s, w * s, h * s), r * s, r * s, p);

SKRoundRect Shell(SKCanvas c, float s)
{
    var rr = new SKRoundRect(SKRect.Create(0.02f * s, 0.02f * s, 0.96f * s, 0.96f * s), 0.22f * s, 0.22f * s);
    using var bg = Grad(s, blueLight, blueDark);
    c.DrawRoundRect(rr, bg);
    return rr;
}

void Strip(SKCanvas c, float s, SKRoundRect shell, float top, byte alpha)
{
    c.Save();
    c.ClipRoundRect(shell, SKClipOperation.Intersect, true);
    using var p = Fill(SKColors.White.WithAlpha(alpha));
    c.DrawRect(SKRect.Create(0, top * s, s, s - top * s), p);
    c.Restore();
}

void Tail(SKCanvas c, float s, float cx, float top, float halfW, float depth, SKPaint p)
{
    using var path = new SKPath();
    path.MoveTo((cx - halfW) * s, (top - 0.01f) * s);
    path.LineTo((cx + halfW) * s, (top - 0.01f) * s);
    path.LineTo(cx * s, (top + depth) * s);
    path.Close();
    c.DrawPath(path, p);
}

// Full drawing, 48 px and up.
void DrawFull(SKCanvas c, float s)
{
    using var shell = Shell(c, s);
    Strip(c, s, shell, 0.72f, 0x59);

    using var w = Fill(SKColors.White);
    RR(c, s, 0.13f, 0.16f, 0.74f, 0.35f, 0.09f, w);
    Tail(c, s, 0.29f, 0.51f, 0.045f, 0.075f, w);

    using var b = Fill(blueDark);
    using var ac = Fill(accent);
    const float ic = 0.155f, iy = 0.25f;
    RR(c, s, 0.200f, iy, ic, ic, 0.05f, b);
    RR(c, s, 0.4225f, iy, ic, ic, 0.05f, b);
    RR(c, s, 0.645f, iy, ic, ic, 0.05f, ac);

    using var dim = Fill(SKColors.White.WithAlpha(0x66));
    RR(c, s, 0.235f, 0.765f, 0.11f, 0.11f, 0.032f, w);
    RR(c, s, 0.445f, 0.765f, 0.11f, 0.11f, 0.032f, dim);
    RR(c, s, 0.655f, 0.765f, 0.11f, 0.11f, 0.032f, dim);
}

// Simplified drawing for 16-32 px: bigger panel and apps, a single taskbar
// button, no dimmed neighbours.
void DrawSmall(SKCanvas c, float s)
{
    using var shell = Shell(c, s);
    Strip(c, s, shell, 0.70f, 0x66);

    using var w = Fill(SKColors.White);
    RR(c, s, 0.09f, 0.14f, 0.82f, 0.38f, 0.10f, w);
    Tail(c, s, 0.28f, 0.52f, 0.06f, 0.08f, w);

    using var b = Fill(blueDark);
    using var ac = Fill(accent);
    const float ic = 0.18f, iy = 0.24f;
    RR(c, s, 0.155f, iy, ic, ic, 0.055f, b);
    RR(c, s, 0.410f, iy, ic, ic, 0.055f, b);
    RR(c, s, 0.665f, iy, ic, ic, 0.055f, ac);

    RR(c, s, 0.215f, 0.755f, 0.145f, 0.13f, 0.04f, w);
}

void Draw(SKCanvas c, float s)
{
    if (s >= 48) DrawFull(c, s); else DrawSmall(c, s);
}

SKBitmap RenderWith(int size, Action<SKCanvas, float> draw)
{
    var bmp = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
    using var c = new SKCanvas(bmp);
    c.Clear(SKColors.Transparent);
    draw(c, size);
    return bmp;
}

SKBitmap Render(int size) => RenderWith(size, Draw);

byte[] PngBytes(int size)
{
    using var bmp = Render(size);
    using var img = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}

// 32-bit bottom-up DIB with an empty AND mask, as classic .ico entries expect.
byte[] DibBytes(int size)
{
    using var bmp = Render(size);
    var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Unpremul);
    byte[] px = new byte[size * size * 4];
    var handle = GCHandle.Alloc(px, GCHandleType.Pinned);
    try { bmp.PeekPixels().ReadPixels(info, handle.AddrOfPinnedObject(), size * 4); }
    finally { handle.Free(); }

    int maskStride = (size + 31) / 32 * 4;
    int maskSize = maskStride * size;

    using var ms = new MemoryStream();
    using var bw = new BinaryWriter(ms);
    bw.Write(40);                       // biSize
    bw.Write(size);                     // biWidth
    bw.Write(size * 2);                 // biHeight (XOR + AND)
    bw.Write((ushort)1);                // biPlanes
    bw.Write((ushort)32);               // biBitCount
    bw.Write(0);                        // biCompression = BI_RGB
    bw.Write(size * size * 4 + maskSize); // biSizeImage
    bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
    for (int y = size - 1; y >= 0; y--) bw.Write(px, y * size * 4, size * 4);
    bw.Write(new byte[maskSize]);
    bw.Flush();
    return ms.ToArray();
}

void WriteIco(string path, int[] sizes)
{
    var entries = sizes.Select(sz => (size: sz, data: sz >= 128 ? PngBytes(sz) : DibBytes(sz))).ToList();
    using var fs = File.Create(path);
    using var bw = new BinaryWriter(fs);
    bw.Write((ushort)0);
    bw.Write((ushort)1);                // type: icon
    bw.Write((ushort)entries.Count);
    int offset = 6 + 16 * entries.Count;
    foreach (var e in entries)
    {
        bw.Write((byte)(e.size >= 256 ? 0 : e.size));
        bw.Write((byte)(e.size >= 256 ? 0 : e.size));
        bw.Write((byte)0);              // palette colours
        bw.Write((byte)0);              // reserved
        bw.Write((ushort)1);            // planes
        bw.Write((ushort)32);           // bits per pixel
        bw.Write(e.data.Length);
        bw.Write(offset);
        offset += e.data.Length;
    }
    foreach (var e in entries) bw.Write(e.data);
}

void SavePng(string path, int size)
{
    var bytes = PngBytes(size);
    File.WriteAllBytes(path, bytes);
}

void Text(SKCanvas c, string s, float x, float y, float size, SKColor color,
    SKTextAlign align = SKTextAlign.Left, SKFontStyleWeight weight = SKFontStyleWeight.Normal)
{
    using var p = new SKPaint
    {
        IsAntialias = true,
        Color = color,
        Typeface = SKTypeface.FromFamilyName("Segoe UI", weight, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
        TextSize = size,
        TextAlign = align
    };
    c.DrawText(s, x, y, p);
}

// Proof sheet: real sizes plus a nearest-neighbour blow-up of each one, so the
// small-size drawing can be judged pixel by pixel.
void Preview(string path)
{
    int[] sizes = { 64, 48, 32, 24, 16 };
    const int w = 1180, h = 830;
    using var bmp = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
    using var c = new SKCanvas(bmp);
    c.Clear(new SKColor(0xF6, 0xF7, 0xF9));

    Text(c, "Taskbar Groups Fluent · icono definitivo", 32, 50, 32, ink, SKTextAlign.Left, SKFontStyleWeight.SemiBold);
    Text(c, "Arriba a tamaño real; abajo cada tamaño ampliado x6 sin suavizado, para ver el píxel real.",
        32, 80, 18, muted);

    c.Save();
    c.Translate(32, 104);
    Draw(c, 256);
    c.Restore();

    // real sizes on light and dark
    float bx = 330, by = 120;
    foreach (var (back, label) in new[] { (SKColors.White, "fondo claro"), (new SKColor(0x1F, 0x1F, 0x1F), "fondo oscuro") })
    {
        using var bg = Fill(back);
        c.DrawRoundRect(SKRect.Create(bx, by, 300, 110), 12, 12, bg);
        using var edge = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            Color = new SKColor(0, 0, 0, 0x22)
        };
        c.DrawRoundRect(SKRect.Create(bx + 0.5f, by + 0.5f, 299, 109), 12, 12, edge);

        float x = bx + 24;
        foreach (int sz in sizes)
        {
            c.Save();
            c.Translate(x, by + 55 - sz / 2f);
            Draw(c, sz);
            c.Restore();
            x += sz + 18;
        }
        Text(c, label, bx + 12, by + 128, 15, muted);
        by += 160;
    }

    using var nearest = new SKPaint { FilterQuality = SKFilterQuality.None, IsAntialias = false };
    using var chk = Fill(new SKColor(0xE8, 0xEA, 0xEE));

    void Zoom(SKBitmap src, float x, float y, float scale, string label)
    {
        float side = src.Width * scale;
        c.DrawRect(SKRect.Create(x, y, side, side), chk);
        c.DrawBitmap(src, SKRect.Create(x, y, side, side), nearest);
        Text(c, label, x, y + side + 22, 15, muted);
    }

    // which drawing wins at 48 px, the detailed one or the simplified one
    Text(c, "48 px: dibujo completo vs simplificado", 700, 140, 18, ink, SKTextAlign.Left, SKFontStyleWeight.SemiBold);
    using (var full48 = RenderWith(48, DrawFull))
    using (var small48 = RenderWith(48, DrawSmall))
    {
        Zoom(full48, 700, 160, 4.5f, "completo");
        Zoom(small48, 940, 160, 4.5f, "simplificado");
    }

    float zx = 32, zy = 440;
    Text(c, "Ampliado x5 (sin suavizado)", zx, zy - 14, 18, ink, SKTextAlign.Left, SKFontStyleWeight.SemiBold);
    foreach (int sz in sizes)
    {
        using var small = Render(sz);
        Zoom(small, zx, zy, 5f, sz + " px");
        zx += sz * 5f + 24;
    }

    using var img = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(path, data.ToArray());
}

foreach (int sz in new[] { 1024, 512, 256, 128, 64, 48, 32, 24, 16 })
{
    SavePng(Path.Combine(outDir, $"logo-{sz}.png"), sz);
    Console.WriteLine($"  logo-{sz}.png");
}

WriteIco(Path.Combine(outDir, "Icon.ico"), new[] { 16, 24, 32, 48, 64, 128, 256 });
Console.WriteLine("  Icon.ico (16/24/32/48/64/128/256)");

Preview(Path.Combine(outDir, "preview.png"));
Console.WriteLine("  preview.png");
