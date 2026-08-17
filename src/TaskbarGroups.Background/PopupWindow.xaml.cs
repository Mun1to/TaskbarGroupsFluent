using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
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
    private DispatcherTimer? _guard;
    private bool _hadFocus;
    private bool _mouseWentUp;
    private bool _closing;

    public PopupWindow(Category category)
    {
        InitializeComponent();
        _category = category;

        ApplyTheme();
        ApplyAppearance();
        LoadItems();

        Loaded += OnLoadedPosition;
        Deactivated += (_, _) => CloseOnce();
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) CloseOnce(); };
        Closed += (_, _) =>
        {
            _guard?.Stop();
            Application.Current.Shutdown();
        };
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
        TakeFocus();
        StartDismissGuard();
    }

    // Activate() alone is not enough. Windows only lets the process that already
    // owns the foreground hand it to someone else, and a flyout launched from a
    // pinned shortcut does not always inherit that right, so Activate() silently
    // does nothing. Attaching our input queue to the thread that does own the
    // foreground makes Windows treat us as that same thread and accept the swap.
    private void TakeFocus()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        Activate();
        if (GetForegroundWindow() == hwnd) return;

        uint theirs = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
        uint ours = GetCurrentThreadId();
        bool attached = theirs != 0 && theirs != ours && AttachThreadInput(ours, theirs, true);
        try
        {
            SetForegroundWindow(hwnd);
            Activate();
        }
        finally
        {
            if (attached) AttachThreadInput(ours, theirs, false);
        }
    }

    // The flyout used to close on Deactivated alone, which never fires if the
    // window never got the focus in the first place. When that happened the panel
    // stayed pinned on top of everything (it is Topmost) and the only way out was
    // launching something from it. This poll is the safety net:
    //
    //  * once we have held the focus, losing the foreground closes us, even if the
    //    Deactivated event gets lost;
    //  * if we never got it, we cannot see clicks going elsewhere, so we watch the
    //    mouse buttons directly and close on the first press outside our bounds.
    //
    // It never closes the flyout on its own, so a focus we could not take just
    // degrades to "click anywhere to dismiss" instead of a stuck window.
    private void StartDismissGuard()
    {
        _guard = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _guard.Tick += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (GetForegroundWindow() == hwnd) { _hadFocus = true; return; }
            if (_hadFocus) { CloseOnce(); return; }

            bool pressed = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0
                        || (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;

            // The click that opened the flyout can still be down when we get here,
            // and it landed on the taskbar, which is outside us. Wait for the mouse
            // to come up once so we never dismiss on the press that opened us.
            if (!pressed) { _mouseWentUp = true; return; }
            if (_mouseWentUp && !CursorIsOver(hwnd)) CloseOnce();
        };
        _guard.Start();
    }

    private static bool CursorIsOver(IntPtr hwnd)
    {
        // Both are physical screen pixels, so they compare without DPI scaling.
        if (!GetCursorPos(out POINT p) || !GetWindowRect(hwnd, out RECT r)) return true;
        return p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom;
    }

    private void CloseOnce()
    {
        if (_closing) return;
        _closing = true;
        _guard?.Stop();
        Close();
    }

    private const int VK_LBUTTON = 0x01;
    private const int VK_RBUTTON = 0x02;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint attach, uint attachTo, bool join);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    private void Item_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PopupItem item)
            Launch(item.Shortcut);
        CloseOnce();
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
