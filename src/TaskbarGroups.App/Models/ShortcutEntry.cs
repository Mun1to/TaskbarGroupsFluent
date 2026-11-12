using System.IO;
using System.Windows;
using System.Windows.Media;
using TaskbarGroups.App.Helpers;
using TaskbarGroups.Core;

namespace TaskbarGroups.App.Models;

/// <summary>
/// Presentation wrapper for a <see cref="ProgramShortcut"/> while it is being
/// edited inside the group editor (carries its resolved preview icon).
/// </summary>
public class ShortcutEntry
{
    public ProgramShortcut Shortcut { get; init; } = null!;
    public ImageSource? Icon { get; set; }

    public bool IsSeparator => Shortcut.IsSeparator;

    /// <summary>What a separator says in the panel: its title, or nothing for just the line.</summary>
    public string SeparatorTitle => (Shortcut.name ?? "").Trim();

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Shortcut.name)
            ? Shortcut.name
            : Shortcut.IsSeparator
                ? Loc.Get("Loc_Editor_Separator")
                : (Shortcut.isWindowsApp
                    ? Shortcut.FilePath
                    : Path.GetFileNameWithoutExtension(Shortcut.FilePath));

    public string SubtitleText =>
        Shortcut.IsSeparator ? Loc.Get("Loc_Sub_Separator")
        : Shortcut.IsGroup ? Loc.Get("Loc_Sub_Group")
        : Shortcut.isWindowsApp ? Loc.Get("Loc_Sub_StoreApp")
        : WebApps.IsWebApp(Shortcut.FilePath, Shortcut.Arguments) ? Loc.Get("Loc_Sub_WebApp")
        : Directory.Exists(Shortcut.FilePath) ? Loc.Get("Loc_Sub_Folder")
        : Loc.Get("Loc_Sub_Program");

    /// <summary>
    /// A short line where a separator's row would have an icon. Mid grey, so it
    /// reads on the light and the dark theme alike.
    /// </summary>
    public static readonly ImageSource SeparatorGlyph = MakeSeparatorGlyph();

    private static ImageSource MakeSeparatorGlyph()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)), 2.5)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        var drawing = new DrawingGroup();
        // An empty square first, so the line keeps its place in the middle of the
        // 32 px cell instead of being stretched to fill it.
        drawing.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 32, 32))));
        drawing.Children.Add(new GeometryDrawing(null, pen, new LineGeometry(new Point(5, 16), new Point(27, 16))));
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}
