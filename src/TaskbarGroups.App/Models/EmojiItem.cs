using System.ComponentModel;
using System.Windows.Media;

namespace TaskbarGroups.App.Models;

/// <summary>
/// A cell in the emoji picker. The colour preview is rendered (via Skia) off the UI
/// thread and assigned to <see cref="Image"/> when ready — WPF can't paint the
/// colour glyphs itself, so the grid shows the same bitmap the icon will use.
/// </summary>
public class EmojiItem : INotifyPropertyChanged
{
    public string Emoji { get; init; } = "";

    private ImageSource? _image;
    public ImageSource? Image
    {
        get => _image;
        set { _image = value; OnPropertyChanged(nameof(Image)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
