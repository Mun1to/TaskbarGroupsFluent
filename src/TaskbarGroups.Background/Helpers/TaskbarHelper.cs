using System;
using System.Runtime.InteropServices;

namespace TaskbarGroups.Background.Helpers;

/// <summary>
/// Locates the Windows taskbar and the cursor so the flyout can be positioned
/// just above the taskbar, aligned to where the user clicked.
/// </summary>
public static class TaskbarHelper
{
    public enum Edge { Left, Top, Right, Bottom }

    public struct TaskbarInfo
    {
        public int Left, Top, Right, Bottom;   // physical pixels
        public Edge Edge;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")] private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    public static TaskbarInfo GetTaskbar()
    {
        var info = new TaskbarInfo();
        IntPtr handle = FindWindow("Shell_TrayWnd", null);
        if (handle != IntPtr.Zero && GetWindowRect(handle, out RECT r))
        {
            info.Left = r.Left; info.Top = r.Top; info.Right = r.Right; info.Bottom = r.Bottom;
            info.Edge = ResolveEdge(r);
        }
        return info;
    }

    private static Edge ResolveEdge(RECT r)
    {
        int width = r.Right - r.Left;
        int height = r.Bottom - r.Top;
        if (width >= height)
            return r.Top <= 0 ? Edge.Top : Edge.Bottom;
        return r.Left <= 0 ? Edge.Left : Edge.Right;
    }

    /// <summary>
    /// True if the cursor is anywhere on the taskbar. A hover-opened flyout checks
    /// this before showing, rather than checking the icon it came from: the icon's
    /// rectangle can be a moment out of date, and refusing to appear over a rounding
    /// error is worse than appearing once when the user has already moved along the
    /// taskbar. Leaving the taskbar altogether is the clear signal they are gone.
    /// </summary>
    public static bool CursorOnTaskbar(int margin = 0)
    {
        if (!GetCursorPos(out POINT p)) return true;

        IntPtr handle = FindWindow("Shell_TrayWnd", null);
        if (handle != IntPtr.Zero && GetWindowRect(handle, out RECT r) && Inside(r, p, margin))
            return true;

        IntPtr secondary = IntPtr.Zero;
        while ((secondary = FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
        {
            if (GetWindowRect(secondary, out RECT s) && Inside(s, p, margin)) return true;
        }
        return false;
    }

    private static bool Inside(RECT r, POINT p, int margin)
        => p.X >= r.Left - margin && p.X < r.Right + margin
        && p.Y >= r.Top - margin && p.Y < r.Bottom + margin;

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string lpClassName, string? lpWindowName);

    public static (int X, int Y) GetCursor()
    {
        GetCursorPos(out POINT p);
        return (p.X, p.Y);
    }
}
