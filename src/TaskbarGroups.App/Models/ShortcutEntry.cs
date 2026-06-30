using System.IO;
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

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Shortcut.name)
            ? Shortcut.name
            : (Shortcut.isWindowsApp
                ? Shortcut.FilePath
                : Path.GetFileNameWithoutExtension(Shortcut.FilePath));

    public string SubtitleText =>
        Shortcut.isWindowsApp ? Loc.Get("Loc_Sub_StoreApp")
        : Directory.Exists(Shortcut.FilePath) ? Loc.Get("Loc_Sub_Folder")
        : Loc.Get("Loc_Sub_Program");
}
