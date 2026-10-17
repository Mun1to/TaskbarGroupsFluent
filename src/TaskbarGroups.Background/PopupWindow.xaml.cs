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
    private bool _menuOpen;
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

    /// <summary>True while a warm flyout is waiting off screen rather than showing a group.</summary>
    public bool IsParked => Left <= OffScreen / 2;

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
        PreviewKeyDown += OnKey;
        ItemsHost.PreviewMouseRightButtonUp += Item_RightClick;
        Closed += (_, _) =>
        {
            _guard?.Stop();
            StopSlide();
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

    // A warm flyout is reused across groups, so every showing sets all of this
    // again rather than trusting what the previous group left behind.
    private void ApplyAppearance()
    {
        var plan = PanelLayouts.Plan(_category.PanelLayout, _category.ShowLabels,
            _category.Width, _category.ShortcutList?.Count ?? 0);
        ItemsHost.ItemsPanel = (System.Windows.Controls.ItemsPanelTemplate)Resources[plan.Panel + "Panel"];
        ItemsHost.ItemTemplate = (DataTemplate)Resources[plan.Template];
        ItemsHost.MaxWidth = plan.MaxWidth;

        // Scaled as a whole, names included, so a bigger icon keeps its proportions
        // with the text under it. MaxWidth is in the unscaled units, which is what
        // keeps the column count the same at every size.
        double scale = PanelLayouts.Scale(_category.IconScale);
        ItemsHost.LayoutTransform = scale == 1.0 ? Transform.Identity : new ScaleTransform(scale, scale);

        bool bottom = PanelLayouts.IsBottom(_category.HeaderPosition);
        System.Windows.Controls.DockPanel.SetDock(HeaderBar,
            bottom ? System.Windows.Controls.Dock.Bottom : System.Windows.Controls.Dock.Top);
        HeaderBar.Margin = bottom ? new Thickness(8, 4, 0, 0) : new Thickness(8, 0, 0, 4);
        HeaderBar.Visibility = _category.ShowHeader ? Visibility.Visible : Visibility.Collapsed;
        HeaderText.Text = _category.Name;
        EditButton.ToolTip = SpanishUi() ? "Editar grupo" : "Edit group";
        OpenAllButton.ToolTip = SpanishUi() ? "Abrir todas" : "Open all";
    }

    // The flyout carries no string tables of its own for one tooltip, so it follows
    // the same rule as the app (see UiPreferences).
    private static bool SpanishUi() => UiPreferences.IsSpanish();

    // Opens every app in the group, for the times the whole set is wanted at once:
    // a work session, a game launcher and its chat, and so on.
    private void OpenAll_Click(object sender, RoutedEventArgs e)
    {
        if (_category.ShortcutList is not null)
            foreach (var ps in _category.ShortcutList)
                Launch(ps);
        CloseOnce();
    }

    // The pencil in the header: open this group's editor in the main app, and get
    // out of the way.
    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var psi = new ProcessStartInfo(Paths.MainApplication) { UseShellExecute = false };
            psi.ArgumentList.Add("--edit");
            psi.ArgumentList.Add(_category.Name);
            Process.Start(psi);
        }
        catch
        {
            // No app to open: the panel still closes, as any click in it does.
        }
        CloseOnce();
    }

    // A per-pixel transparent (AllowsTransparency) window is software-rendered and
    // can show a frozen frame until something forces a repaint. Instead this is a
    // normal GPU-composited window with the glass painted behind it by DWM, and
    // rounded corners on Windows 11; elsewhere, or with transparency effects
    // switched off in Windows, it is an opaque rounded panel. Either way it never
    // freezes, and it follows the system theme.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        bool glass = false;

        if (Environment.OSVersion.Version.Build >= 22000) // Windows 11
        {
            int dark = _isDark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            int round = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
            glass = TransparencyEffectsOn() && ApplyAcrylicBlur(hwnd);
        }

        if (glass)
        {
            // The tint lives in the blur itself, so nothing is painted over it.
            Background = Brushes.Transparent;
            RootBorder.Background = Brushes.Transparent;
        }
        else
        {
            // No glass: opaque so nothing shows through (avoids a black backdrop).
            var solid = new SolidColorBrush(Color.FromArgb(255, _tint.R, _tint.G, _tint.B));
            Background = solid;
            RootBorder.Background = solid;
        }
    }

    // WPF sizes the window once while it is still creating it, and at that moment
    // Windows holds it to the minimum width of a window with a title bar, 136 px at
    // 100 %. A single column of icons is narrower than that, so the vertical panel
    // came out with wide empty sides. Asking for the size again once the window
    // exists lets it shrink to its content; done right before positioning, so the
    // panel is centred on its icon at the width it really has.
    private void FitToContent()
    {
        SizeToContent = SizeToContent.Manual;
        SizeToContent = SizeToContent.WidthAndHeight;
        UpdateLayout();
    }

    // The system backdrop that WPF-UI applies (DWMWA_SYSTEMBACKDROP_TYPE) was
    // measured never painting on this window: a borderless popup gets no acrylic
    // from it, active or not, and the panel came out flat black over a striped
    // test pattern. The accent policy is the route Windows' own taskbar flyouts
    // and apps like EarTrumpet use for borderless glass, and it does not depend on
    // the window being active, which a hover-opened flyout never is.
    private bool ApplyAcrylicBlur(IntPtr hwnd)
    {
        var source = HwndSource.FromHwnd(hwnd);
        if (source?.CompositionTarget is null) return false;
        source.CompositionTarget.BackgroundColor = Colors.Transparent;

        byte alpha = _isDark ? DarkTintAlpha : LightTintAlpha;
        var accent = new AccentPolicy
        {
            AccentState = ACCENT_ENABLE_ACRYLICBLURBEHIND,
            // AABBGGRR, the byte order the accent policy expects.
            GradientColor = (uint)(alpha << 24 | _tint.B << 16 | _tint.G << 8 | _tint.R)
        };

        int size = Marshal.SizeOf<AccentPolicy>();
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WCA_ACCENT_POLICY,
                Data = ptr,
                SizeOfData = size
            };
            return SetWindowCompositionAttribute(hwnd, ref data) != 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    // Windows' own "Transparency effects" switch. With it off, every flyout in the
    // system goes opaque, and this one should not be the exception.
    private static bool TransparencyEffectsOn()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("EnableTransparency") is not int v || v != 0;
        }
        catch { return true; }
    }

    // How much of the theme colour covers the blur, out of 255. Light needs more,
    // or dark wallpaper bleeds through and the black text loses contrast.
    private const byte DarkTintAlpha = 0x99;
    private const byte LightTintAlpha = 0xC0;

    private const int WCA_ACCENT_POLICY = 19;
    private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    // Light or dark as the app is: the user's choice in its quick bar, or Windows'
    // own "apps" mode. TBG_THEME=light|dark still forces it for testing.
    private static bool IsLightTheme() => UiPreferences.IsLight();

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

    /// <summary>
    /// Turns a flyout opened the ordinary way into a warm one, so that closing it
    /// parks it off screen for the next click instead of ending the process.
    /// </summary>
    public void KeepForReuse()
    {
        Reusable = true;
        Title = WarmWindowTitle;
    }

    /// <summary>Parks a warm flyout off screen without unloading it.</summary>
    public void HideForReuse()
    {
        if (!Reusable) return;
        _guard?.Stop();
        StopSlide();
        _closing = false;
        _hadFocus = false;
        _mouseWentUp = false;
        _cursorLeftAt = null;
        _anchor = null;
        Left = OffScreen;
        Top = OffScreen;

        // Parked, a warm flyout only waits, so what it holds is given back now and
        // paged in again the next time it shows. Low priority, so a showing already
        // queued goes first, and skipped if one did.
        Dispatcher.BeginInvoke(() => { if (IsParked) TrimMemory(); }, DispatcherPriority.ContextIdle);
    }

    /// <summary>
    /// Compacts the heap and hands the process's pages back to Windows. They stay
    /// in memory as standby pages, so showing again brings them back cheaply, but
    /// they no longer count against this process while it only waits.
    /// </summary>
    private static void TrimMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        try { SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1); }
        catch { /* nothing to trim is no reason to fail */ }
    }

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint min, nint max);

    /// <summary>
    /// Puts an already warm flyout on screen showing <paramref name="category"/>,
    /// positioned over <paramref name="anchor"/>, or next to the cursor and with
    /// the focus when there is no anchor, which is how a click opens it. Measured
    /// at about fifty milliseconds, against roughly nine hundred to start a fresh
    /// process.
    /// </summary>
    public void ShowFor(Category category, HoverAnchor? anchor)
    {
        // A slide still running from the last showing must not carry on moving the
        // panel, nor finish by putting it away.
        StopSlide();

        _category = category;
        _anchor = anchor;
        _closing = false;
        _hadFocus = false;
        _mouseWentUp = false;
        _cursorLeftAt = null;

        // This process can outlive many trips to the settings, and how the panel
        // comes in is read on every showing.
        Settings.Reload();

        ApplyAppearance();
        LoadItems();
        FitToContent();

        // Clicked: the user meant it, so the panel takes the keyboard like one
        // started from scratch does. The process that received the click handed
        // its right to the foreground over to this one on the way out.
        if (anchor is null) TakeFocus();

        // After taking the focus, which would lift the panel over the taskbar.
        Arrive(TargetOverTaskbar());

        StartDismissGuard();
    }

    private void OnLoadedPosition(object sender, RoutedEventArgs e)
    {
        // A warm flyout positions itself when it is asked to show, not when it is
        // first loaded; at that point it is parked off screen with nothing to say.
        if (Reusable) return;

        FitToContent();

        // A hover-opened flyout must not steal the keyboard. The cursor only brushed
        // past the taskbar, and the user may well be typing somewhere else; yanking
        // the focus away mid-sentence would make the feature unusable. It still
        // takes the focus the moment it is clicked, which is when the user meant it.
        if (_anchor is null) TakeFocus();

        Arrive(TargetOverTaskbar());

        StartDismissGuard();
    }

    /// <summary>How long the panel takes to come in.</summary>
    private static readonly TimeSpan ArriveTime = TimeSpan.FromMilliseconds(250);

    /// <summary>Going away is quicker, as in Windows: by then the user is done with it.</summary>
    private static readonly TimeSpan LeaveTime = TimeSpan.FromMilliseconds(175);

    /// <summary>How far a peeking panel travels, in the units the rest of the panel is laid out in.</summary>
    private const double PeekDistance = 24;

    /// <summary>The most one frame may advance a slide: two frames at 60 Hz.</summary>
    private const double LongestStepMs = 33;

    private EventHandler? _frame;

    // The user's choice, unless Windows' own "Animation effects" are off: then
    // nothing in the system moves, and neither does this.
    private static string Animation()
        => SystemParameters.ClientAreaAnimation
            ? Settings.settingInfo?.panelAnimation ?? PanelAnimation.None
            : PanelAnimation.None;

    /// <summary>
    /// Brings the panel in to <paramref name="target"/>, in the way the user chose.
    /// An animated panel goes straight to where its slide starts: placed at the
    /// target first, a warm panel could show there for a frame before jumping back.
    ///
    /// From the taskbar: it starts fully behind the taskbar and slides out, as the
    /// Start menu does. It stays under the taskbar in the z-order until it arrives,
    /// so the taskbar covers it on the way and the screen edge hides the rest; then
    /// it goes back on top of everything.
    ///
    /// Peek: it rises a short way into place while the apps fade in. The glass
    /// itself cannot fade, since Windows will not make this kind of window see
    /// through (measured: the request is turned down), so it is there whole from
    /// the first frame.
    ///
    /// Either way the apps can be clicked from the first frame. Measured on a 1080p
    /// screen at sixty moves a second or more.
    /// </summary>
    private void Arrive(Point target)
    {
        ContentHost.Opacity = 1;
        string style = Animation();
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        var tb = TaskbarHelper.GetTaskbar();

        RECT now = default;
        bool animated = (style == PanelAnimation.Peek || (style == PanelAnimation.Rise && tb.Handle != IntPtr.Zero))
            && hwnd != IntPtr.Zero && GetWindowRect(hwnd, out now);
        if (!animated)
        {
            Left = target.X;
            Top = target.Y;
            // On top of everything again: a panel that last left by sinking behind
            // the taskbar is still under it in the z-order.
            if (hwnd != IntPtr.Zero)
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            return;
        }

        // Where the target is in screen pixels, at the size the panel already has.
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (scale <= 0) scale = 1;
        int x = (int)Math.Round(target.X * scale), y = (int)Math.Round(target.Y * scale);
        var r = new RECT { Left = x, Top = y, Right = x + (now.Right - now.Left), Bottom = y + (now.Bottom - now.Top) };
        var end = new POINT { X = x, Y = y };

        if (style == PanelAnimation.Rise)
        {
            POINT from = Tucked(r, tb);
            SetWindowPos(hwnd, tb.Handle, from.X, from.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
            Slide(hwnd, tb.Handle, from, end, ArriveTime, EaseOut, null,
                () => SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE));
            return;
        }

        POINT start = Nudged(r, tb);
        ContentHost.Opacity = 0;
        SetWindowPos(hwnd, HWND_TOPMOST, start.X, start.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
        Slide(hwnd, IntPtr.Zero, start, end, ArriveTime, EaseOut, e => ContentHost.Opacity = e, null);
    }

    /// <summary>
    /// Takes the panel out the way it came in, then runs <paramref name="then"/>.
    /// False, with nothing done, when the panel does not animate.
    /// </summary>
    private bool Leave(Action then)
    {
        string style = Animation();
        if (style != PanelAnimation.Rise && style != PanelAnimation.Peek) return false;
        if (IsParked || !IsVisible) return false;

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        var tb = TaskbarHelper.GetTaskbar();
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out RECT r)) return false;
        var now = new POINT { X = r.Left, Y = r.Top };

        if (style == PanelAnimation.Rise)
        {
            if (tb.Handle == IntPtr.Zero) return false;
            SetWindowPos(hwnd, tb.Handle, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            Slide(hwnd, tb.Handle, now, Tucked(r, tb), LeaveTime, EaseIn, null, then);
            return true;
        }

        Slide(hwnd, IntPtr.Zero, now, Nudged(r, tb), LeaveTime, EaseIn, e => ContentHost.Opacity = 1 - e, then);
        return true;
    }

    // Arriving slows down into place; leaving speeds up on the way out.
    private static double EaseOut(double p) => 1 - Math.Pow(1 - p, 4);
    private static double EaseIn(double p) => p * p * p;

    // Where the panel sits fully behind the taskbar: its edge on the taskbar's
    // inner edge, the rest past the screen's edge.
    private static POINT Tucked(RECT r, TaskbarHelper.TaskbarInfo tb) => tb.Edge switch
    {
        TaskbarHelper.Edge.Top => new POINT { X = r.Left, Y = tb.Bottom - (r.Bottom - r.Top) },
        TaskbarHelper.Edge.Left => new POINT { X = tb.Right - (r.Right - r.Left), Y = r.Top },
        TaskbarHelper.Edge.Right => new POINT { X = tb.Left, Y = r.Top },
        _ => new POINT { X = r.Left, Y = tb.Top },
    };

    // Where a peeking panel starts and ends: a short way towards the taskbar.
    private POINT Nudged(RECT r, TaskbarHelper.TaskbarInfo tb)
    {
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int d = (int)Math.Round(PeekDistance * (scale > 0 ? scale : 1));
        return tb.Edge switch
        {
            TaskbarHelper.Edge.Top => new POINT { X = r.Left, Y = r.Top - d },
            TaskbarHelper.Edge.Left => new POINT { X = r.Left - d, Y = r.Top },
            TaskbarHelper.Edge.Right => new POINT { X = r.Left + d, Y = r.Top },
            _ => new POINT { X = r.Left, Y = r.Top + d },
        };
    }

    // One move per frame WPF draws. The clock starts once the window is really on
    // screen, not when the slide is set up: a panel opened from cold is drawn for
    // the first time while still hidden, which was measured taking about a second,
    // and timing from before that finished the slide before anyone could see it.
    // Nor does a frame ever count for more than a short step: a panel opened from
    // cold is still busy for a moment after it appears (measured stalling 0.6 s),
    // and the slide waits that out instead of jumping to its end.
    // With no window to go under, the panel keeps its place in the z-order.
    private void Slide(IntPtr hwnd, IntPtr under, POINT from, POINT to, TimeSpan time,
        Func<double, double> ease, Action<double>? step, Action? done)
    {
        StopSlide();
        uint flags = SWP_NOSIZE | SWP_NOACTIVATE | (under == IntPtr.Zero ? SWP_NOZORDER : 0);
        Stopwatch? clock = null;
        double elapsed = 0, last = 0;
        _frame = (_, _) =>
        {
            if (clock is null)
            {
                if (!IsWindowVisible(hwnd)) return;
                clock = Stopwatch.StartNew();
            }
            double now = clock.Elapsed.TotalMilliseconds;
            elapsed += Math.Min(now - last, LongestStepMs);
            last = now;
            double p = Math.Min(1, elapsed / time.TotalMilliseconds);
            double e = ease(p);
            int x = (int)Math.Round(from.X + (to.X - from.X) * e);
            int y = (int)Math.Round(from.Y + (to.Y - from.Y) * e);
            SetWindowPos(hwnd, under, x, y, 0, 0, flags);
            step?.Invoke(e);
            if (p < 1) return;

            StopSlide();
            done?.Invoke();
        };
        CompositionTarget.Rendering += _frame;
    }

    private void StopSlide()
    {
        if (_frame is not null) CompositionTarget.Rendering -= _frame;
        _frame = null;
    }

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    /// <summary>
    /// Where the panel goes: against the taskbar, above the icon it belongs to. Only
    /// worked out here; <see cref="Arrive"/> puts it there.
    /// </summary>
    private Point TargetOverTaskbar()
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

        return new Point(left, top);
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

            // A right-click menu is open. It reaches outside the panel, so a click
            // on it, or the cursor resting on it, would read as leaving.
            if (_menuOpen) return;

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

        if (Leave(FinishClosing)) return;
        FinishClosing();
    }

    private void FinishClosing()
    {
        // Kept warm: park it off screen instead of closing, so the next showing is
        // the fifty milliseconds it takes to swap the contents rather than the near
        // second it takes to start a process and paint a window for the first time.
        if (Reusable)
        {
            HideForReuse();
            return;
        }

        // A command from the right-click menu can leave a window of this process
        // behind, owned by this panel: the Properties sheet, a delete confirmation.
        // Closing the panel would close those too, so it is hidden instead and
        // closes once they are gone.
        if (ShellContextMenu.CommandRan)
        {
            Hide();
            ShellContextMenu.CloseWhenDone(this);
            return;
        }

        Close();
    }

    private void OnKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape) { CloseOnce(); return; }

        // The menu key, or Shift+F10, on the focused app: the keyboard's right-click.
        bool menuKey = e.Key == System.Windows.Input.Key.Apps
            || (e.SystemKey == System.Windows.Input.Key.F10
                && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Shift);
        if (!menuKey || System.Windows.Input.Keyboard.FocusedElement is not FrameworkElement fe
            || fe.DataContext is not PopupItem item) return;

        e.Handled = true;
        Point corner = fe.PointToScreen(new Point(0, fe.ActualHeight));
        ShowItemMenu(item, (int)corner.X, (int)corner.Y);
    }

    private void Item_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement fe || fe.DataContext is not PopupItem item) return;

        e.Handled = true;
        if (!GetCursorPos(out POINT p)) return;
        ShowItemMenu(item, p.X, p.Y);
    }

    /// <summary>
    /// The app's own right-click menu, as File Explorer or the Start menu shows it.
    /// A command picked from it counts as using the panel, which then closes.
    /// </summary>
    private void ShowItemMenu(PopupItem item, int x, int y)
    {
        string? name = ShellContextMenu.ParsingName(item.Shortcut);
        if (name is null || _menuOpen) return;

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        // A menu that is not in front can need two clicks to go away.
        TakeFocus();

        bool ran;
        _menuOpen = true;
        try { ran = ShellContextMenu.Show(hwnd, name, x, y, _isDark); }
        finally { _menuOpen = false; }

        if (ran) { CloseOnce(); return; }

        // Dismissed: the panel stays, and the click that dismissed the menu, which
        // may have landed outside it, is not taken as a click to dismiss it too.
        _mouseWentUp = false;
        _cursorLeftAt = null;
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

    internal static void Launch(ProgramShortcut ps)
    {
        try
        {
            if (ps.IsGroup)
            {
                // A group inside a group: start another flyout for it, which closes
                // this one on its way in, as any newly opened flyout does.
                var sub = new ProcessStartInfo(Environment.ProcessPath ?? Paths.BackgroundApplication)
                {
                    UseShellExecute = false
                };
                sub.ArgumentList.Add(ps.GroupName);
                Process.Start(sub);
            }
            else if (ps.isWindowsApp)
            {
                // Picked from the app list. A desktop app with arguments or an
                // administrator start goes through its Start menu shortcut, which
                // takes both; Store apps have none and, like any app without
                // either, open through Windows as the Start menu does.
                string? desktop = NeedsOwnStart(ps) ? AppCatalog.DesktopLaunchPath(ps.FilePath) : null;
                if (desktop is not null)
                    StartProgram(desktop, ps);
                else
                    Process.Start(new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{ps.FilePath}")
                    {
                        UseShellExecute = true
                    });
            }
            else
            {
                StartProgram(ps.FilePath, ps);
            }
        }
        catch
        {
            // Launch failures shouldn't crash the flyout.
        }
    }

    private static bool NeedsOwnStart(ProgramShortcut ps)
        => !string.IsNullOrWhiteSpace(ps.Arguments) || ps.RunAsAdmin;

    // A program, or a shortcut to one: Windows adds the arguments after the
    // shortcut's own.
    private static void StartProgram(string path, ProgramShortcut ps)
    {
        var psi = new ProcessStartInfo(path) { UseShellExecute = true };
        if (!string.IsNullOrWhiteSpace(ps.Arguments))
            psi.Arguments = ps.Arguments;
        // "runas" is what asks Windows for the elevated start; declining
        // the prompt throws, which the caller's catch already absorbs.
        if (ps.RunAsAdmin)
            psi.Verb = "runas";
        Process.Start(psi);
    }
}
