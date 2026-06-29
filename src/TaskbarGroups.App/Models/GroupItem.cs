using System.Windows.Media;
using TaskbarGroups.Core;

namespace TaskbarGroups.App.Models;

/// <summary>
/// Presentation model for a taskbar group shown in the main window,
/// wrapping the underlying <see cref="Category"/> with WPF-ready data.
/// </summary>
public class GroupItem
{
    public string Name { get; init; } = "";
    public int ShortcutCount { get; init; }
    public ImageSource? Icon { get; init; }
    public Category Source { get; init; } = null!;

    /// <summary>Path to the pinnable .lnk created for this group.</summary>
    public string ShortcutPath { get; init; } = "";

    public string ShortcutCountText =>
        ShortcutCount == 1 ? "1 acceso directo" : $"{ShortcutCount} accesos directos";
}
