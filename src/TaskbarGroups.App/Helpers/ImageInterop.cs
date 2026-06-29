using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TaskbarGroups.App.Helpers;

/// <summary>
/// Bridges System.Drawing bitmaps (used by the ported icon logic) into
/// WPF ImageSource objects for display.
/// </summary>
public static class ImageInterop
{
    public static ImageSource? ToImageSource(this Bitmap? bitmap)
    {
        if (bitmap is null)
            return null;

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        ms.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static ImageSource? ToImageSource(this Image? image)
        => image is Bitmap bmp ? bmp.ToImageSource() : (image is null ? null : new Bitmap(image).ToImageSource());
}
