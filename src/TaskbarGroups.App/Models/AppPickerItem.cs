using System.ComponentModel;
using System.Windows.Media;
using TaskbarGroups.Core;

namespace TaskbarGroups.App.Models;

/// <summary>
/// Selectable row in the unified app picker. Wraps the <see cref="ProgramShortcut"/>
/// that gets added if selected, so the icon resolves through the same shared path
/// (AppsFolder id for catalog apps, file path for browsed executables).
/// </summary>
public class AppPickerItem : INotifyPropertyChanged
{
    public ProgramShortcut Shortcut { get; init; } = null!;

    public string DisplayName => Shortcut.name;

    /// <summary>Key used to dedupe and to load the icon on demand.</summary>
    public string Key => (Shortcut.isWindowsApp ? "app:" : "file:") + Shortcut.FilePath;

    private ImageSource? _icon;
    public ImageSource? Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(nameof(Icon)); }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
