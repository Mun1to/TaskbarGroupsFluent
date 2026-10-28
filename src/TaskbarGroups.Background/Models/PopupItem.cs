using System.Windows.Media;
using TaskbarGroups.Core;

namespace TaskbarGroups.Background.Models;

/// <summary>
/// One launchable shortcut shown as an icon in the flyout, or a line that splits
/// the flyout into sections, with its title as the display name.
/// </summary>
public class PopupItem
{
    public ProgramShortcut Shortcut { get; init; } = null!;
    public ImageSource? Icon { get; init; }
    public string DisplayName { get; init; } = "";
    public bool IsSeparator => Shortcut.IsSeparator;
}
