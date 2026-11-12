using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TaskbarGroups.App.Helpers;
using TaskbarGroups.Core;
using Wpf.Ui.Controls;

namespace TaskbarGroups.App;

/// <summary>
/// The icons inside a program or a library, to pick the one an app wears in a
/// group (issue #15). They are read away from the window and show as they come:
/// a library such as shell32.dll holds hundreds.
/// </summary>
public partial class IconPickerWindow : FluentWindow
{
    public sealed class Choice
    {
        public int Index { get; init; }
        public ImageSource? Icon { get; init; }
    }

    private readonly ObservableCollection<Choice> _choices = new();
    private readonly CancellationTokenSource _cts = new();

    /// <summary>Which icon of the file was picked.</summary>
    public int SelectedIconIndex { get; private set; }

    public IconPickerWindow(string path, int count)
    {
        InitializeComponent();
        AppTheme.Watch(this);
        FileText.Text = Path.GetFileName(path);
        IconsList.ItemsSource = _choices;
        Loaded += (_, _) => LoadIcons(path, count, _cts.Token);
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts.Cancel();
        _cts.Dispose();
        base.OnClosed(e);
    }

    private void LoadIcons(string path, int count, CancellationToken token)
    {
        var thread = new Thread(() =>
        {
            for (int i = 0; i < count && !token.IsCancellationRequested; i++)
            {
                ImageSource? icon;
                using (var bitmap = PickedIcon.Extract(path, i, 48))
                {
                    // Some libraries keep empty slots, which would only leave gaps.
                    if (bitmap is null || IsBlank(bitmap)) continue;
                    icon = bitmap.ToImageSource();
                }
                if (icon is null) continue;

                int index = i;
                Dispatcher.BeginInvoke(() =>
                {
                    if (token.IsCancellationRequested) return;
                    _choices.Add(new Choice { Index = index, Icon = icon });
                    if (IconsList.SelectedIndex < 0) IconsList.SelectedIndex = 0;
                }, DispatcherPriority.Background);
            }
        })
        { IsBackground = true, Name = "icon-choices" };
        thread.Start();
    }

    // Nothing in it can be seen: every pixel is (almost) fully transparent.
    private static bool IsBlank(System.Drawing.Bitmap bitmap)
    {
        var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[data.Stride * data.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            for (int y = 0; y < data.Height; y++)
                for (int x = 0; x < data.Width; x++)
                    if (bytes[y * data.Stride + x * 4 + 3] > 16) return false;
            return true;
        }
        finally { bitmap.UnlockBits(data); }
    }

    private void IconsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UseButton.IsEnabled = IconsList.SelectedItem is Choice;

    private void Icon_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (IconsList.SelectedItem is Choice) Use_Click(sender, e);
    }

    private void Use_Click(object sender, RoutedEventArgs e)
    {
        if (IconsList.SelectedItem is not Choice choice) return;
        SelectedIconIndex = choice.Index;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
