using System.ComponentModel;
using System.Windows.Media;
using TaskbarGroups.Core;

namespace TaskbarGroups.Background.Models;

/// <summary>
/// One launchable shortcut shown as an icon in the flyout, or a line that splits
/// the flyout into sections, with its title as the display name.
///
/// In a panel that shows a folder the icons arrive after the names, and whether a
/// folder in it opens as a submenu is found out after that, so both tell the panel
/// when they change.
/// </summary>
public class PopupItem : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private bool _hasChildren;

    public ProgramShortcut Shortcut { get; init; } = null!;
    public string DisplayName { get; init; } = "";
    public bool IsSeparator => Shortcut.IsSeparator;

    public ImageSource? Icon
    {
        get => _icon;
        set { if (!Equals(_icon, value)) { _icon = value; Changed(nameof(Icon)); } }
    }

    /// <summary>A folder known to hold something: it opens beside the panel, and shows a chevron.</summary>
    public bool HasChildren
    {
        get => _hasChildren;
        set { if (_hasChildren != value) { _hasChildren = value; Changed(nameof(HasChildren)); } }
    }

    /// <summary>Whether a folder entry was looked into yet; until then a click looks for itself.</summary>
    public bool ChildrenKnown { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
