using System.Windows.Media;
using TaskbarGroups.Core;

namespace TaskbarGroups.Background.Models;

/// <summary>One launchable shortcut shown as an icon in the flyout.</summary>
public class PopupItem
{
    public ProgramShortcut Shortcut { get; init; } = null!;
    public ImageSource? Icon { get; init; }
    public string DisplayName { get; init; } = "";
}
