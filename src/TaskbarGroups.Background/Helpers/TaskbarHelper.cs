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

    public static (int X, int Y) GetCursor()
    {
        GetCursorPos(out POINT p);
        return (p.X, p.Y);
    }
}
