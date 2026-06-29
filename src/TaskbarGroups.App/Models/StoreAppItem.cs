using System.ComponentModel;
using System.Windows.Media;
using TaskbarGroups.Core;

namespace TaskbarGroups.App.Models;

/// <summary>
/// Selectable row in the Store app picker. Icon loads asynchronously so the
/// list appears instantly while icons stream in.
/// </summary>
public class StoreAppItem : INotifyPropertyChanged
{
    public StoreAppInfo Info { get; init; } = null!;
    public string DisplayName => Info.DisplayName;

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
