using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using TaskbarGroups.Background.Helpers;
using TaskbarGroups.Background.Models;
using TaskbarGroups.Core;

namespace TaskbarGroups.Background;

/// <summary>
/// Borderless flyout shown above the taskbar with the group's shortcuts.
/// Launches an app on click and closes when it loses focus.
/// </summary>
public partial class PopupWindow : Window
{
    private readonly Category _category;

    public PopupWindow(Category category)
    {
        InitializeComponent();
        _category = category;

        ApplyAppearance();
        LoadItems();

        Loaded += OnLoadedPosition;
        Deactivated += (_, _) => Close();
        Closed += (_, _) => Application.Current.Shutdown();
    }

    private void ApplyAppearance()
    {
        try
        {
            var c = System.Drawing.ColorTranslator.FromHtml(_category.ColorString);
            RootBorder.Background = new SolidColorBrush(Color.FromArgb(240, c.R, c.G, c.B));
        }
        catch { /* keep default background */ }

        int count = _category.ShortcutList?.Count ?? 0;
        int columns = _category.Width > 0 ? _category.Width : Math.Min(Math.Max(count, 1), 6);
        ItemsHost.MaxWidth = columns * 80 + 20;
    }

    private void LoadItems()
    {
        var items = new System.Collections.Generic.List<PopupItem>();
        if (_category.ShortcutList is not null)
        {
            foreach (var ps in _category.ShortcutList)
            {
                ImageSource? icon = null;
                try { icon = _category.loadImageCache(ps).ToImageSource(); }
                catch { }

                items.Add(new PopupItem
                {
                    Shortcut = ps,
                    Icon = icon,
                    DisplayName = ResolveName(ps)
                });
            }
        }
        ItemsHost.ItemsSource = items;
    }

    private static string ResolveName(ProgramShortcut ps)
    {
        if (!string.IsNullOrWhiteSpace(ps.name)) return ps.name;
        if (ps.isWindowsApp) return ps.FilePath;
        try { return Path.GetFileNameWithoutExtension(ps.FilePath); }
        catch { return ps.FilePath; }
    }

    private void OnLoadedPosition(object sender, RoutedEventArgs e)
    {
        var tb = TaskbarHelper.GetTaskbar();
        var (cursorX, cursorY) = TaskbarHelper.GetCursor();
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (scale <= 0) scale = 1;

        const double gap = 8;
        double w = ActualWidth, h = ActualHeight;
        double left, top;

        switch (tb.Edge)
        {
            case TaskbarHelper.Edge.Top:
                top = tb.Bottom / scale + gap;
                left = cursorX / scale - w / 2;
                break;
            case TaskbarHelper.Edge.Left:
                left = tb.Right / scale + gap;
                top = cursorY / scale - h / 2;
                break;
            case TaskbarHelper.Edge.Right:
                left = tb.Left / scale - w - gap;
                top = cursorY / scale - h / 2;
                break;
            default: // Bottom
                top = tb.Top / scale - h - gap;
                left = cursorX / scale - w / 2;
                break;
        }

        var area = SystemParameters.WorkArea;
        left = Math.Max(area.Left + 4, Math.Min(left, area.Right - w - 4));
        top = Math.Max(area.Top + 4, Math.Min(top, area.Bottom - h - 4));

        Left = left;
        Top = top;
        Activate();
    }

    private void Item_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PopupItem item)
            Launch(item.Shortcut);
        Close();
    }

    private static void Launch(ProgramShortcut ps)
    {
        try
        {
            if (ps.isWindowsApp)
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{ps.FilePath}")
                {
                    UseShellExecute = true
                });
            }
            else
            {
                var psi = new ProcessStartInfo(ps.FilePath) { UseShellExecute = true };
                if (!string.IsNullOrWhiteSpace(ps.Arguments))
                    psi.Arguments = ps.Arguments;
                Process.Start(psi);
            }
        }
        catch
        {
            // Launch failures shouldn't crash the flyout.
        }
    }
}
