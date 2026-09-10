using System.Runtime.InteropServices;

namespace TaskbarGroups.Hover;

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public bool Contains(POINT p)
        => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;

    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

internal static class Native
{
    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(nint hWnd, out RECT r);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint FindWindow(string? cls, string? title);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint FindWindowEx(nint parent, nint after, string? cls, string? title);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(nint hWnd);

    // Rects from UI Automation and GetCursorPos are both physical screen pixels,
    // so they only line up if this process is per-monitor DPI aware. Without it
    // Windows would virtualise our coordinates and every hit test would be off on
    // a scaled display.
    [DllImport("user32.dll")]
    internal static extern bool SetProcessDpiAwarenessContext(nint value);

    internal static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;
}
