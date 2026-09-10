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
    // Not readonly: a warm flyout is reused, and each showing brings a new group
    // and a new icon to sit above.
    private Category _category;
    private HoverAnchor? _anchor;
    private Color _tint = Color.FromRgb(0x20, 0x20, 0x20);
    private bool _isDark = true;
    private DispatcherTimer? _guard;
    private bool _hadFocus;
    private bool _mouseWentUp;
    private bool _closing;
    private DateTime? _cursorLeftAt;

    /// <summary>
    /// How long the cursor may wander off a hover-opened flyout before it closes.
    /// Long enough to cross the gap between the icon and the panel, or to overshoot
    /// it slightly, and short enough that it never feels left behind.
    /// </summary>
    private static readonly TimeSpan HoverGrace = TimeSpan.FromMilliseconds(600);

    /// <summary>Slack around the panel and the icon, so their edges are forgiving.</summary>
    private const int HoverMargin = 16;

    /// <summary>
    /// Somewhere off every screen. A warm flyout is rendered here first so that all
    /// the slow parts, laying out and painting for the first time, are already paid
    /// for by the time the user rests on an icon.
    /// </summary>
    private const double OffScreen = -32000;

    /// <summary>Marks the warm flyout's window so other processes can spot it.</summary>
    public const string WarmWindowTitle = "TaskbarGroupsFluent.WarmFlyout";

    /// <summary>
    /// True while this window is being kept warm for reuse. Dismissing it then
    /// hides it and leaves the process listening, rather than shutting down.
    /// </summary>
    public bool Reusable { get; set; }

    public PopupWindow(Category category, HoverAnchor? anchor = null)
    {
        InitializeComponent();
        _category = category;
        _anchor = anchor;

        // Opened by hover, so it must appear without taking the keyboard. Not
        // calling Activate() is not enough: Show() activates by default, and it was
        // measured doing exactly that, which both stole the focus from whatever the
        // user was typing in and left the panel unable to close, since its only way
        // out was losing a focus that moving the mouse never takes away. A click
        // inside still activates it, which is when the user does mean it.
        if (_anchor is not null) ShowActivated = false;

        ApplyTheme();
        ApplyAppearance();
        LoadItems();

        Loaded += OnLoadedPosition;
        Deactivated += (_, _) => { if (!Reusable || Left > OffScreen / 2) CloseOnce(); };
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

    /// <summary>
    /// Renders this window off screen so it is ready to appear instantly. Called on
    /// the warm instance before anything is asked of it.
    /// </summary>
    public void Prewarm()
    {
        Reusable = true;
        // A title nobody sees, so the process can be recognised from outside without
        // reading command lines: a flyout started by a click must not kill the warm
        // one on its way in.
        Title = WarmWindowTitle;
        ShowActivated = false;
        Left = OffScreen;
        Top = OffScreen;
        Show();
        UpdateLayout();
    }

    /// <summary>Parks a warm flyout off screen without unloading it.</summary>
    public void HideForReuse()
    {
        if (!Reusable) return;
        _guard?.Stop();
        _closing = false;
        _hadFocus = false;
        _mouseWentUp = false;
        _cursorLeftAt = null;
        _anchor = null;
        Left = OffScreen;
        Top = OffScreen;
    }

    /// <summary>
    /// Puts an already warm flyout on screen showing <paramref name="category"/>,
    /// positioned over <paramref name="anchor"/>. Measured at about fifty
    /// milliseconds, against roughly nine hundred to start a fresh process.
    /// </summary>
    public void ShowFor(Category category, HoverAnchor anchor)
    {
        _category = category;
        _anchor = anchor;
        _closing = false;
        _hadFocus = false;
        _mouseWentUp = false;
        _cursorLeftAt = null;

        ApplyAppearance();
        LoadItems();
        InvalidateMeasure();
        UpdateLayout();

        PositionOverTaskbar();
        StartDismissGuard();
    }

    private void OnLoadedPosition(object sender, RoutedEventArgs e)
    {
        // A warm flyout positions itself when it is asked to show, not when it is
        // first loaded; at that point it is parked off screen with nothing to say.
        if (Reusable) return;

        PositionOverTaskbar();

        // A hover-opened flyout must not steal the keyboard. The cursor only brushed
        // past the taskbar, and the user may well be typing somewhere else; yanking
        // the focus away mid-sentence would make the feature unusable. It still
        // takes the focus the moment it is clicked, which is when the user meant it.
        if (_anchor is null) TakeFocus();

        StartDismissGuard();
    }

    /// <summary>Places the panel against the taskbar, above the icon it belongs to.</summary>
    private void PositionOverTaskbar()
    {
        var tb = TaskbarHelper.GetTaskbar();
        var (cursorX, cursorY) = TaskbarHelper.GetCursor();
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (scale <= 0) scale = 1;

        // Opened by hover: line the panel up with the icon rather than the cursor,
        // so it lands in the same place whether the cursor stopped at the edge of
        // the icon or dead centre.
        if (_anchor is not null)
        {
            cursorX = _anchor.CenterX;
            cursorY = _anchor.CenterY;
        }

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
    //
    // A flyout opened by hover has a third way out. It deliberately holds no focus,
    // so neither of the above would ever fire, and waiting for a click to dismiss
    // something the user opened without clicking is the wrong bargain: it closes
    // once the cursor has been away from both the panel and its icon for a moment.
    private void StartDismissGuard()
    {
        // A reused flyout would otherwise stack a fresh timer on every showing, and
        // the old ones keep firing against state that has moved on.
        _guard?.Stop();

        _guard = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _guard.Tick += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            // Parked off screen: nothing to dismiss, and the checks below would read
            // a position that means nothing.
            if (Reusable && Left <= OffScreen / 2) { _guard?.Stop(); return; }

            if (GetForegroundWindow() == hwnd) { _hadFocus = true; _cursorLeftAt = null; return; }
            if (_hadFocus) { CloseOnce(); return; }

            bool pressed = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0
                        || (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;

            if (pressed)
            {
                // The click that opened the flyout can still be down when we get
                // here, and it landed on the taskbar, which is outside us. Wait for
                // the mouse to come up once so we never dismiss on the press that
                // opened us.
                if (_mouseWentUp && !CursorIsOver(hwnd)) CloseOnce();
                return;
            }

            _mouseWentUp = true;

            if (_anchor is null) return;

            if (CursorIsInHoverZone(hwnd)) { _cursorLeftAt = null; return; }
            _cursorLeftAt ??= DateTime.UtcNow;
            if (DateTime.UtcNow - _cursorLeftAt >= HoverGrace) CloseOnce();
        };
        _guard.Start();
    }

    private static bool CursorIsOver(IntPtr hwnd)
    {
        // Both are physical screen pixels, so they compare without DPI scaling.
        if (!GetCursorPos(out POINT p) || !GetWindowRect(hwnd, out RECT r)) return true;
        return p.X >= r.Left && p.X < r.Right && p.Y >= r.Top && p.Y < r.Bottom;
    }

    // The panel and the icon that opened it count as one region, with slack around
    // both: the panel sits a few pixels clear of the taskbar, and without the slack
    // that gap would read as "the cursor left" every time it travelled between them.
    private bool CursorIsInHoverZone(IntPtr hwnd)
    {
        // Cannot tell where the cursor is: assume it is still here. Erring the other
        // way would close the flyout under the user's hand.
        if (!GetCursorPos(out POINT p)) return true;
        if (_anchor is not null && _anchor.Contains(p.X, p.Y, HoverMargin)) return true;
        if (!GetWindowRect(hwnd, out RECT r)) return true;

        return p.X >= r.Left - HoverMargin && p.X < r.Right + HoverMargin
            && p.Y >= r.Top - HoverMargin && p.Y < r.Bottom + HoverMargin;
    }

    private void CloseOnce()
    {
        if (_closing) return;
        _closing = true;
        _guard?.Stop();

        // Kept warm: park it off screen instead of closing, so the next showing is
        // the fifty milliseconds it takes to swap the contents rather than the near
        // second it takes to start a process and paint a window for the first time.
        if (Reusable)
        {
            _closing = false;
            _hadFocus = false;
            _mouseWentUp = false;
            _cursorLeftAt = null;
            _anchor = null;
            Left = OffScreen;
            Top = OffScreen;
            return;
        }

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
