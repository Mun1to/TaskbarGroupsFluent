using System.Drawing;
using TaskbarGroups.Core;
using Xunit;

public class AppCatalogCropToContentTests
{
    // A transparent canvas with an opaque square at (x, y).
    private static Bitmap Canvas(int canvas, int x, int y, int solid)
    {
        var bmp = new Bitmap(canvas, canvas, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        using var brush = new SolidBrush(Color.FromArgb(255, 30, 120, 220));
        g.FillRectangle(brush, x, y, solid, solid);
        return bmp;
    }

    [Fact] // 48px logo centred in 256, +2px margin on four sides.
    public void SolidContent_IsCroppedToItsBoxWithMargin()
    {
        using var bmp = Canvas(256, 104, 104, 48);
        using var crop = AppCatalog.CropToSolidContent(bmp);
        Assert.Equal(52, crop.Width);
        Assert.Equal(52, crop.Height);
        Assert.Equal(0, crop.GetPixel(0, 0).A);    // margin is transparent
        Assert.Equal(255, crop.GetPixel(2, 2).A);  // logo starts at the margin
    }

    [Fact] // A margin of 0 gives exactly the solid box.
    public void ZeroMargin_ReturnsExactBox()
    {
        using var bmp = Canvas(256, 104, 104, 48);
        using var crop = AppCatalog.CropToSolidContent(bmp, 0);
        Assert.Equal(48, crop.Width);
        Assert.Equal(48, crop.Height);
    }

    [Fact] // Review Focus 3: the box touches the top-left corner.
    public void BoxAtCanvasEdge_ClampsTheMargin()
    {
        using var bmp = Canvas(256, 0, 0, 48);
        using var crop = AppCatalog.CropToSolidContent(bmp);
        Assert.Equal(50, crop.Width);
        Assert.Equal(50, crop.Height);
        Assert.Equal(255, crop.GetPixel(0, 0).A);
    }

    [Fact] // A wide logo is letterboxed: Category.CreateConfig resizes ignoring aspect.
    public void NonSquareContent_IsLetterboxedToASquare()
    {
        using var bmp = new Bitmap(256, 256, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        using (var brush = new SolidBrush(Color.FromArgb(255, 30, 120, 220)))
            g.FillRectangle(brush, 88, 118, 80, 20);   // 80x20, +2px margin -> 84x24

        using var crop = AppCatalog.CropToSolidContent(bmp);
        Assert.Equal(84, crop.Width);
        Assert.Equal(84, crop.Height);
        Assert.Equal(0, crop.GetPixel(42, 2).A);       // above the band
        Assert.Equal(255, crop.GetPixel(42, 35).A);    // the band, centred
    }

    [Fact] // Review Focus 2: nothing solid -> no crop.
    public void FullyTransparent_ReturnsNull()
    {
        using var bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Assert.Null(AppCatalog.CropToSolidContent(bmp));
    }
}
