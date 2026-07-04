using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TaskbarGroups.Background.Helpers;
using TaskbarGroups.Background.Models;
using TaskbarGroups.Core;
using Wpf.Ui.Controls;

namespace TaskbarGroups.Background;

/// <summary>
/// Borderless flyout shown above the taskbar with the group's shortcuts.
/// Launches an app on click and closes when it loses focus.
/// </summary>
public partial class PopupWindow : Window
{
    private readonly Category _category;
    private Color _tint = Color.FromRgb(0x20, 0x20, 0x20);
    private bool _isDark = true;

    public PopupWindow(Category category)
    {
        InitializeComponent();
        _category = category;

        ApplyTheme();
        ApplyAppearance();
        LoadItems();

        Loaded += OnLoadedPosition;
        Deactivated += (_, _) => Close();
        Closed += (_, _) => Application.Current.Shutdown();
    }

    // Follow the Windows light/dark setting: a light panel with dark text under a
    // light theme, a dark panel with white text under a dark one. The template
    // binds text/border/hover to these window resources via DynamicResource.
    private void ApplyTheme()
    {
        _isDark = !IsLightTheme();
        _tint = _isDark ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xF3, 0xF3, 0xF3);

        Resources["FlyoutTextBrush"] = Frozen(_isDark ? Colors.White : Color.FromRgb(0x1A, 0x1A, 0x1A));
        Resources["FlyoutBorderBrush"] = Frozen(_isDark
            ? Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x22, 0x00, 0x00, 0x00));
        Resources["FlyoutHoverBrush"] = Frozen(_isDark
            ? Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x14, 0x00, 0x00, 0x00));
        Resources["FlyoutPressBrush"] = Frozen(_isDark
            ? Color.FromArgb(0x11, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x22, 0x00, 0x00, 0x00));
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private void ApplyAppearance()
    {
        int count = _category.ShortcutList?.Count ?? 0;
        int columns = _category.Width > 0 ? _category.Width : Math.Min(Math.Max(count, 1), 6);
        ItemsHost.MaxWidth = columns * 80 + 20;
    }

    // A per-pixel transparent (AllowsTransparency) window is software-rendered and
    // can show a frozen frame until something forces a repaint. Instead this is a
    // normal GPU-composited window: on Windows 11 it gets a translucent acrylic
    // backdrop and rounded corners via DWM; elsewhere it falls back to an opaque
    // rounded panel. Either way it never freezes, and it follows the system theme.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        bool acrylic = false;

        if (Environment.OSVersion.Version.Build >= 22000) // Windows 11
        {
            try { acrylic = WindowBackdrop.ApplyBackdrop(this, WindowBackdropType.Acrylic); }
            catch { acrylic = false; }
            // Set the acrylic tint (dark/light) after the backdrop so it sticks.
            int dark = _isDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            int round = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        }

        if (acrylic)
        {
            // Let the acrylic show through a translucent tint.
            Background = Brushes.Transparent;
            RootBorder.Background = new SolidColorBrush(Color.FromArgb(150, _tint.R, _tint.G, _tint.B));
        }
        else
        {
            // No acrylic: opaque so nothing shows through (avoids a black backdrop).
            var solid = new SolidColorBrush(Color.FromArgb(255, _tint.R, _tint.G, _tint.B));
            Background = solid;
            RootBorder.Background = solid;
        }
    }

    // The Windows "apps" light/dark preference (true = light). TBG_THEME=light|dark
    // forces it, mirroring the TBG_LANG language override.
    private static bool IsLightTheme()
    {
        string forced = Environment.GetEnvironmentVariable("TBG_THEME");
        if (!string.IsNullOrWhiteSpace(forced))
            return forced.Trim().StartsWith("l", StringComparison.OrdinalIgnoreCase);
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v != 0;
        }
        catch { return false; }
    }

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

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
