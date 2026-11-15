using System.Drawing;
using TaskbarGroups.Core;
using Xunit;

public class AppCatalogPaddedTileTests
{
    // A transparent canvas with a centred opaque square of `solid` px, at 32bpp ARGB.
    private static Bitmap CanvasWithSolidSquare(int canvas, int solid)
    {
        var bmp = new Bitmap(canvas, canvas, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        int off = (canvas - solid) / 2;
        using var brush = new SolidBrush(Color.FromArgb(255, 30, 120, 220));
        g.FillRectangle(brush, off, off, solid, solid);
        return bmp;
    }

    [Fact] // A small logo in a large canvas is a padded tile (spec 0.04 case).
    public void SmallLogoInLargeCanvas_IsFlagged()
    {
        using var bmp = CanvasWithSolidSquare(256, 48);   // 0.035 area
        Assert.True(AppCatalog.IsPaddedTile(bmp));
    }

    [Fact] // A real icon fills most of the canvas (spec 0.53-1.00 cases).
    public void FullBleedIcon_IsNotFlagged()
    {
        using var bmp = CanvasWithSolidSquare(256, 240);  // 0.88 area
        Assert.False(AppCatalog.IsPaddedTile(bmp));
    }

    [Fact] // Review Focus 1: nothing solid -> not this predicate's call.
    public void FullyTransparentCanvas_IsNotFlagged()
    {
        using var bmp = new Bitmap(256, 256, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Assert.False(AppCatalog.IsPaddedTile(bmp));
    }

    [Fact] // Review Focus 2: a legit small glyph is flagged, but via the safe path.
    public void SmallButRealContent_IsFlagged()
    {
        using var bmp = CanvasWithSolidSquare(256, 96);   // 0.14 area
        Assert.True(AppCatalog.IsPaddedTile(bmp));
    }

    [Fact] // Review Focus 3: no alpha -> content reads as full canvas.
    public void Opaque24bppCanvas_IsNotFlagged()
    {
        using var bmp = new Bitmap(256, 256, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.White);
        Assert.False(AppCatalog.IsPaddedTile(bmp));
    }

    [Fact] // Review Focus 4: correct at small sizes.
    public void TinyCanvas_BehavesLikeLargeOne()
    {
        using var padded = CanvasWithSolidSquare(16, 6);  // 0.14 area
        using var full = CanvasWithSolidSquare(16, 15);   // 0.88 area
        Assert.True(AppCatalog.IsPaddedTile(padded));
        Assert.False(AppCatalog.IsPaddedTile(full));
    }

    [Fact] // Review Focus 5: boundary is inclusive below 0.30.
    public void AtThreshold_Boundary()
    {
        // 256*256*0.30 = 19660.8. 138*138 = 19044 (< 0.30) ; 141*141 = 19881 (> 0.30).
        using var below = CanvasWithSolidSquare(256, 138);
        using var above = CanvasWithSolidSquare(256, 141);
        Assert.True(AppCatalog.IsPaddedTile(below));
        Assert.False(AppCatalog.IsPaddedTile(above));
    }
}
