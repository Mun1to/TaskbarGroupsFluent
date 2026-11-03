using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TaskbarGroups.Background.Helpers;

/// <summary>
/// Locates the Windows taskbars and the cursor so the flyout can be positioned just
/// above the taskbar of the screen it was opened on, aligned to where the user clicked.
///
/// Everything here is in this process's own pixels, and so is everything else it
/// reads from Windows: the cursor, the windows, the screens. WPF runs it system DPI
/// aware, which means a screen at another scale reads as if it were at the system's,
/// so those pixels are not always real ones. They do all agree with each other. The
/// one thing that comes from outside is the watcher's rectangle, in real pixels, and
/// <see cref="FromRealPixels"/> brings it in.
/// </summary>
public static class TaskbarHelper
{
    public enum Edge { Left, Top, Right, Bottom }

    public struct TaskbarInfo
    {
        public int Left, Top, Right, Bottom;
        public Edge Edge;
        public IntPtr Handle;                  // zero if that screen has no taskbar
        public int WorkLeft, WorkTop, WorkRight, WorkBottom;   // of the taskbar's screen
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const uint MONITOR_DEFAULTTONULL = 0;
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>The share of a taskbar that has to be on a screen for it to be that screen's.</summary>
    private const double OnScreenShare = 0.9;

    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

    [DllImport("user32.dll")] private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);
    [DllImport("user32.dll")] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string lpClassName, string? lpWindowName);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT p, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    /// <summary>
    /// The taskbar a panel opening at this point belongs to (issue #7). Each screen
    /// can have its own, and asking for Shell_TrayWnd alone only ever finds the one on
    /// the main screen.
    /// </summary>
    public static TaskbarInfo GetTaskbarAt(int x, int y)
    {
        var point = new POINT { X = x, Y = y };
        MONITORINFO screen = ScreenOf(MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST));

        // The one under the point, which is the usual case and the only way to tell
        // for one that hides itself: only while it is showing is it on its screen.
        foreach (IntPtr bar in Taskbars())
        {
            if (GetWindowRect(bar, out RECT r) && Inside(r, point, 0))
                return Describe(bar, r, screen);
        }

        // Opened some other way, with the cursor up on the screen. Windows leaves a
        // taskbar behind for each screen that was unplugged, parked off screen and
        // still flagged visible, which this leaves out.
        foreach (IntPtr bar in Taskbars())
        {
            if (GetWindowRect(bar, out RECT r) && MostlyOn(r, screen.rcMonitor))
                return Describe(bar, r, screen);
        }

        return WithoutTaskbar(screen);
    }

    /// <summary>
    /// A screen with no taskbar of its own, or one that has hidden itself: the panel
    /// sits against the edge of the screen where the main taskbar is on its own.
    /// </summary>
    private static TaskbarInfo WithoutTaskbar(MONITORINFO screen)
    {
        Edge edge = Edge.Bottom;
        IntPtr main = FindWindow("Shell_TrayWnd", null);
        if (main != IntPtr.Zero && GetWindowRect(main, out RECT r))
            edge = ResolveEdge(r, ScreenOf(MonitorFromPoint(default, MONITOR_DEFAULTTOPRIMARY)).rcMonitor);

        RECT work = screen.rcWork;
        RECT strip = edge switch
        {
            Edge.Top => new RECT { Left = work.Left, Top = work.Top, Right = work.Right, Bottom = work.Top },
            Edge.Left => new RECT { Left = work.Left, Top = work.Top, Right = work.Left, Bottom = work.Bottom },
            Edge.Right => new RECT { Left = work.Right, Top = work.Top, Right = work.Right, Bottom = work.Bottom },
            _ => new RECT { Left = work.Left, Top = work.Bottom, Right = work.Right, Bottom = work.Bottom },
        };
        TaskbarInfo info = Describe(IntPtr.Zero, strip, screen);
        info.Edge = edge;
        return info;
    }

    private static TaskbarInfo Describe(IntPtr bar, RECT r, MONITORINFO screen) => new()
    {
        Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom,
        Edge = ResolveEdge(r, screen.rcMonitor),
        Handle = bar,
        WorkLeft = screen.rcWork.Left, WorkTop = screen.rcWork.Top,
        WorkRight = screen.rcWork.Right, WorkBottom = screen.rcWork.Bottom
    };

    private static MONITORINFO ScreenOf(IntPtr monitor)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref info);
        return info;
    }

    // Against the middle of its own screen: only the main screen starts at zero, and
    // one above it or to its left is all negative whichever edge the taskbar is on.
    private static Edge ResolveEdge(RECT bar, RECT screen)
    {
        if (bar.Right - bar.Left >= bar.Bottom - bar.Top)
            return bar.Top + bar.Bottom < screen.Top + screen.Bottom ? Edge.Top : Edge.Bottom;
        return bar.Left + bar.Right < screen.Left + screen.Right ? Edge.Left : Edge.Right;
    }

    private static bool MostlyOn(RECT bar, RECT screen)
    {
        long wide = Math.Min(bar.Right, screen.Right) - Math.Max(bar.Left, screen.Left);
        long high = Math.Min(bar.Bottom, screen.Bottom) - Math.Max(bar.Top, screen.Top);
        if (wide <= 0 || high <= 0) return false;
        long area = (long)(bar.Right - bar.Left) * (bar.Bottom - bar.Top);
        return wide * high >= area * OnScreenShare;
    }

    private static IEnumerable<IntPtr> Taskbars()
    {
        IntPtr main = FindWindow("Shell_TrayWnd", null);
        if (main != IntPtr.Zero && IsWindowVisible(main)) yield return main;

        IntPtr secondary = IntPtr.Zero;
        while ((secondary = FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
        {
            if (IsWindowVisible(secondary)) yield return secondary;
        }
    }

    /// <summary>
    /// Brings a rectangle from real screen pixels into this process's own. The watcher
    /// reads the taskbar in real pixels, as it has to, and on a screen at another scale
    /// than the system's the two differ: taken as they came, the panel would open away
    /// from the icon and the icon would not count as part of the panel. The screen is
    /// the same to both, so the rectangle keeps its place within it. The middle of the
    /// rectangle decides the screen, as its far edge may already be on the next one.
    /// </summary>
    public static (int Left, int Top, int Right, int Bottom) FromRealPixels(int left, int top, int right, int bottom)
    {
        var middle = new POINT { X = left + (right - left) / 2, Y = top + (bottom - top) / 2 };

        IntPtr monitor;
        MONITORINFO real;
        IntPtr previous = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        try
        {
            monitor = MonitorFromPoint(middle, MONITOR_DEFAULTTONEAREST);
            real = ScreenOf(monitor);
        }
        finally
        {
            SetThreadDpiAwarenessContext(previous);
        }

        RECT from = real.rcMonitor, to = ScreenOf(monitor).rcMonitor;
        if (from.Right <= from.Left || from.Bottom <= from.Top) return (left, top, right, bottom);

        double scaleX = (double)(to.Right - to.Left) / (from.Right - from.Left);
        double scaleY = (double)(to.Bottom - to.Top) / (from.Bottom - from.Top);
        return ((int)Math.Round(to.Left + (left - from.Left) * scaleX),
                (int)Math.Round(to.Top + (top - from.Top) * scaleY),
                (int)Math.Round(to.Left + (right - from.Left) * scaleX),
                (int)Math.Round(to.Top + (bottom - from.Top) * scaleY));
    }

    /// <summary>
    /// True if a rectangle in this process's own pixels, on the screen of a point in
    /// them, reaches another screen. Asked in real pixels: at another scale a screen
    /// reads narrower here than it really is, so a place that reads as empty can be
    /// right on top of it.
    /// </summary>
    public static bool ReachesAnotherScreen(int x, int y, int left, int top, int right, int bottom)
    {
        IntPtr own = MonitorFromPoint(new POINT { X = x, Y = y }, MONITOR_DEFAULTTONEAREST);
        RECT to = ScreenOf(own).rcMonitor;

        IntPtr previous = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        try
        {
            RECT from = ScreenOf(own).rcMonitor;
            if (to.Right <= to.Left || to.Bottom <= to.Top) return false;

            double scaleX = (double)(from.Right - from.Left) / (to.Right - to.Left);
            double scaleY = (double)(from.Bottom - from.Top) / (to.Bottom - to.Top);
            var real = new RECT
            {
                Left = (int)Math.Round(from.Left + (left - to.Left) * scaleX),
                Top = (int)Math.Round(from.Top + (top - to.Top) * scaleY),
                Right = (int)Math.Round(from.Left + (right - to.Left) * scaleX),
                Bottom = (int)Math.Round(from.Top + (bottom - to.Top) * scaleY)
            };
            IntPtr found = MonitorFromRect(ref real, MONITOR_DEFAULTTONULL);
            return found != IntPtr.Zero && found != own;
        }
        finally
        {
            SetThreadDpiAwarenessContext(previous);
        }
    }

    /// <summary>
    /// True if the cursor is anywhere on a taskbar. A hover-opened flyout checks
    /// this before showing, rather than checking the icon it came from: the icon's
    /// rectangle can be a moment out of date, and refusing to appear over a rounding
    /// error is worse than appearing once when the user has already moved along the
    /// taskbar. Leaving the taskbar altogether is the clear signal they are gone.
    /// </summary>
    public static bool CursorOnTaskbar(int margin = 0)
    {
        if (!GetCursorPos(out POINT p)) return true;

        foreach (IntPtr bar in Taskbars())
        {
            if (GetWindowRect(bar, out RECT r) && Inside(r, p, margin)) return true;
        }
        return false;
    }

    private static bool Inside(RECT r, POINT p, int margin)
        => p.X >= r.Left - margin && p.X < r.Right + margin
        && p.Y >= r.Top - margin && p.Y < r.Bottom + margin;

    public static (int X, int Y) GetCursor()
    {
        GetCursorPos(out POINT p);
        return (p.X, p.Y);
    }
}
