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
    /// How long the hand-off waits to see the panel on screen before giving up on
    /// the warm flyout and opening one the ordinary way. A warm flyout shows in
    /// about fifty milliseconds, so this only runs out if it is stuck.
    /// </summary>
    private const int HandOffTimeoutMs = 700;

    /// <summary>Anything left of this is the parking spot of a warm flyout.</summary>
    private const int Parked = -5000;

    [STAThread]
    private static void Main(string[] args)
    {
        if (Array.Exists(args, arg => string.Equals(arg, Category.SeparatorArgument, StringComparison.OrdinalIgnoreCase)))
            return;

        if (HandOff(args)) return;
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
        // Only a click passes just the group name. The watcher, the right-click
        // menu and the warm start all add a "--" switch and keep their own paths.
        if (args.Length == 0) return false;
        foreach (string a in args)
            if (a.StartsWith("--", StringComparison.Ordinal)) return false;

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
        if (!FlyoutChannel.Click(string.Join(" ", args))) return false;

        // Already showing a group (opened by hover): it takes the new one in place,
        // and there is nothing to wait for.
        if (wasOnScreen) return true;

        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < HandOffTimeoutMs)
        {
            if (OnScreen(warm)) return true;
            Thread.Sleep(5);
        }
        return false;
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
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
}
