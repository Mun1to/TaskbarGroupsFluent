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

    internal delegate bool EnumWindowsProc(nint hWnd, nint param);

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc callback, nint param);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(nint hWnd, System.Text.StringBuilder name, int max);

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(nint hWnd, out int processId);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(nint hWnd, int command);

    internal const int SW_HIDE = 0;

    internal delegate void WinEventProc(nint hook, uint evt, nint hWnd,
        int objectId, int childId, uint thread, uint time);

    [DllImport("user32.dll")]
    internal static extern nint SetWinEventHook(uint first, uint last, nint module,
        WinEventProc callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    internal static extern bool UnhookWinEvent(nint hook);

    internal const uint EVENT_OBJECT_SHOW = 0x8002;
    internal const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    internal const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    internal const int OBJID_WINDOW = 0;

    // Rects from UI Automation and GetCursorPos are both physical screen pixels,
    // so they only line up if this process is per-monitor DPI aware. Without it
    // Windows would virtualise our coordinates and every hit test would be off on
    // a scaled display.
    [DllImport("user32.dll")]
    internal static extern bool SetProcessDpiAwarenessContext(nint value);

    internal static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;
}
