using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TaskbarGroups.App.Helpers;
using TaskbarGroups.App.Models;
using TaskbarGroups.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;
using DrawingImage = System.Drawing.Image;

namespace TaskbarGroups.App;

/// <summary>
/// Lets the user pick a built-in emoji to use as the group icon, instead of
/// uploading an image. Each cell's colour preview is rendered through Skia (WPF
/// can't paint colour glyphs itself), and the chosen emoji is rendered at 256px
/// into <see cref="Result"/>.
/// </summary>
public partial class EmojiPickerWindow : FluentWindow
{
    private const int CellSize = 48;

    /// <summary>The rendered emoji icon, or null if the user cancelled.</summary>
    public DrawingImage? Result { get; private set; }

    public EmojiPickerWindow()
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);

        var items = new List<EmojiItem>();
        foreach (string emoji in Emojis)
            items.Add(new EmojiItem { Emoji = emoji });
        EmojiItems.ItemsSource = items;

        Loaded += async (_, _) => await LoadPreviewsAsync(items);
    }

    // Render each cell's colour bitmap off the UI thread, then hand it back frozen.
    private async Task LoadPreviewsAsync(List<EmojiItem> items)
    {
        LoadingRing.Visibility = Visibility.Visible;
        await Task.Run(() =>
        {
            foreach (var item in items)
            {
                try
                {
                    using var bmp = EmojiIcon.Render(item.Emoji, CellSize);
                    var source = bmp.ToImageSource();
                    Dispatcher.Invoke(() => item.Image = source);
                }
                catch { /* skip glyphs this machine's emoji font can't render */ }
            }
        });
        LoadingRing.Visibility = Visibility.Collapsed;
    }

    private void Emoji_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string emoji } || string.IsNullOrEmpty(emoji))
            return;
        try
        {
            Result = EmojiIcon.Render(emoji, 256);
            DialogResult = true;
        }
        catch
        {
            DialogResult = false;
        }
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // A curated set of colour emojis that render well on Windows' Segoe UI Emoji,
    // ordered roughly folders/work → tech/media → nature → food → symbols so the
    // most group-like icons come first.
    private static readonly string[] Emojis =
    {
        "📁","📂","🗂️","📦","📌","📎","🔖","📝","📄","📚","📖","📅","✅","⭐","🔥","💡",
        "🔔","🔑","🔒","⚙️","🛠️","🧰","🔧","🔨","🧪","🧲","🔭","🔬","💼","🏆","🥇","🎯",
        "💻","🖥️","⌨️","🖱️","💾","📷","📸","🎥","🎬","📺","🎵","🎶","🎧","🎤","🎨","🖌️",
        "🌐","📡","🔗","📱","🎮","🕹️","👾","🎲","🧩","🚀","✈️","🚗","🏠","🏢","🎁","🎉",
        "🐱","🐶","🦊","🐼","🐨","🦁","🐯","🐸","🐧","🦄","🐢","🦋","🌵","🌲","🌳","🌸",
        "🌻","🍀","🌈","🌙","☀️","⚡","❄️","💧","🍎","🍊","🍋","🍉","🍇","🍓","🍒","🍕",
        "🍔","🍟","🌮","🍩","🍪","🎂","☕","🍺","😀","😎","🤖","👻","💀","🎃","⚽","🏀",
        "❤️","🧡","💛","💚","💙","💜","🖤","🤍",
    };
}
