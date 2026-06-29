using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TaskbarGroups.Background.Helpers;

/// <summary>Converts System.Drawing bitmaps into WPF ImageSource objects.</summary>
public static class ImageInterop
{
    public static ImageSource? ToImageSource(this Image? image)
    {
        if (image is null)
            return null;

        using var ms = new MemoryStream();
        image.Save(ms, ImageFormat.Png);
        ms.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
