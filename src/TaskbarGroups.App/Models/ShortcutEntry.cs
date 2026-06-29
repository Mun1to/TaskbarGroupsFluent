using System.IO;
using System.Windows.Media;
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
        Shortcut.isWindowsApp ? "App de Microsoft Store"
        : Directory.Exists(Shortcut.FilePath) ? "Carpeta"
        : Path.GetExtension(Shortcut.FilePath).TrimStart('.').ToUpperInvariant();
}
