using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using TaskbarGroups.Core;

namespace TaskbarGroups.Background;

/// <summary>
/// Entry point, ahead of WPF.
///
/// A click on a pinned group starts this process with the group's name, and from
/// cold that costs the best part of a second: .NET, then WPF, then a window laid
/// out and painted for the first time. When a flyout is already loaded and waiting
/// (the hover watcher keeps one, and a flyout opened by a click stays a while), all
/// of that has been paid for, so this process only passes the name on and leaves.
/// It does so before WPF is touched at all: loading it is most of the cost being
/// avoided.
/// </summary>
internal static class Program
{
    /// <summary>
    /// How long a click waits to reach the warm flyout. It answers at once unless
    /// it is in the middle of the request before this one, and then it is about to.
    /// </summary>
    private const int ReachMs = 2000;

    /// <summary>
    /// Why this click was not served by a warm flyout, for <see cref="OpenTimes"/>:
    /// "no-panel" (none was loaded), "no-answer" (one was, and could not be
    /// reached), "panel-gone" (it went away while showing) or "panel-slow" (it was
    /// still there and had not shown when the patience ran out).
    /// </summary>
    internal static string ColdWhy = "no-panel";

    /// <summary>
    /// Anything left of this is the parking spot of a warm flyout. Far past any screen:
    /// the ones to the left of the main one are real places with negative coordinates,
    /// and a panel showing there must not read as parked.
    /// </summary>
    private const int Parked = (int)(PopupWindow.OffScreen / 2);

    /// <summary>
    /// For a click, the milliseconds from Windows creating this process to Main;
    /// -1 for anything else. See <see cref="OpenTimes"/>.
    /// </summary>
    internal static double StartMs = -1;

    [STAThread]
    private static void Main(string[] args)
    {
        if (Array.Exists(args, arg => string.Equals(arg, Category.SeparatorArgument, StringComparison.OrdinalIgnoreCase)))
            return;

        // The second click on a group's button closes its panel instead of opening
        // it again: pressing the button already closed it (issue #17).
        bool click = IsPlainClick(args);
        if (click && ClickToggle.JustClosed(string.Join(" ", args))) return;

        // What Windows took to get us here, before any of our own work.
        if (click) StartMs = OpenTimes.SinceProcessStart();

        if (HandOff(args))
        {
            OpenTimes.Note("warm", StartMs, OpenTimes.SinceProcessStart());
            return;
        }
        RunWpf();
    }

    // Kept out of Main so that JIT-compiling Main does not load WPF for the
    // hand-off, which never needs it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunWpf()
    {
        // App.xaml declares nothing, so WPF generates no InitializeComponent for it.
        new App().Run();
    }

    /// <summary>
    /// Passes a plain click on to the warm flyout. Returns false whenever this
    /// process should go on and show the panel itself: there is no warm flyout,
    /// the start is not a plain click, or the panel never came up.
    /// </summary>
    private static bool HandOff(string[] args)
    {
        if (!IsPlainClick(args)) return false;

        ColdWhy = "no-panel";
        if (!FlyoutChannel.IsFlyoutReady()) return false;

        IntPtr warm = FindWindow(null, PopupWindow.WarmWindowTitle);
        if (warm == IntPtr.Zero) return false;

        // Windows let this process take the foreground, because the click that
        // started it was the user's. The panel lives in another process, so that
        // right is handed over; without it the panel would come up unable to take
        // the keyboard, and with it unable to close by losing it.
        GetWindowThreadProcessId(warm, out uint pid);
        if (pid != 0) AllowSetForegroundWindow(pid);

        bool wasOnScreen = OnScreen(warm);

        // The pinned shortcut passes the name unquoted, so a name with spaces
        // arrives split across several args.
        ColdWhy = "no-answer";
        if (!FlyoutChannel.Click(string.Join(" ", args), ReachMs)) return false;

        // Already showing a group (opened by hover): it takes the new one in place,
        // and there is nothing to wait for.
        if (wasOnScreen) return true;

        // Issue #20: waited for as long as it is still there. See HandOffWait.
        var clock = Stopwatch.StartNew();
        if (HandOffWait.ForPanel(() => OnScreen(warm), () => IsWindow(warm),
                () => clock.ElapsedMilliseconds, () => Thread.Sleep(5)))
            return true;

        ColdWhy = IsWindow(warm) ? "panel-slow" : "panel-gone";
        return false;
    }

    /// <summary>
    /// Only a click passes just the group name. The watcher, the right-click menu
    /// and the warm start all add a "--" switch and keep their own paths.
    /// </summary>
    private static bool IsPlainClick(string[] args)
    {
        if (args.Length == 0) return false;
        foreach (string a in args)
            if (a.StartsWith("--", StringComparison.Ordinal)) return false;
        return true;
    }

    private static bool OnScreen(IntPtr hwnd)
        => GetWindowRect(hwnd, out RECT r) && r.Left > Parked && r.Top > Parked;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? cls, string? title);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint pid);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
}
