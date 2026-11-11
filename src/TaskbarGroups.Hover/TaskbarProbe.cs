using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TaskbarGroups.Hover;

/// <summary>
/// Finds a taskbar that is not the one of Windows under the cursor (issue #12).
///
/// The watcher only reads the windows Windows keeps for its taskbars. Start11
/// draws its taskbar with its own code, and if that taskbar is a window of its
/// own, the cursor resting on a group there was never on a taskbar at all as far
/// as the watcher could tell. Such a window is told by its shape and place, the
/// way anyone would: kept on top of everything, along an edge of its monitor and
/// across most of it.
/// </summary>
internal static class TaskbarProbe
{
    /// <summary>
    /// Thickest a taskbar can be, in pixels: a large one at a high scale, or a
    /// vertical one with room for names.
    /// </summary>
    private const int MaxThickness = 320;

    /// <summary>How far from the edge of the screen a floating taskbar may sit.</summary>
    private const int EdgeGap = 32;

    private static int _checkedPid;
    private static bool _checkedOurs;

    /// <summary>
    /// The window under the cursor when it looks like a taskbar that is not one of
    /// Windows, with its rectangle, or zero for anything else.
    /// </summary>
    public static nint TaskbarAt(POINT cursor, out RECT rect)
    {
        rect = default;
        nint under = Native.WindowFromPoint(cursor);
        if (under == 0) return 0;
        nint root = Native.GetAncestor(under, Native.GA_ROOT);
        if (root == 0 || !Native.IsWindowVisible(root)) return 0;
        if (((long)Native.GetWindowLongPtr(root, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) == 0) return 0;
        if (!Native.GetWindowRect(root, out RECT r) || r.Width <= 0 || r.Height <= 0) return 0;

        var info = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
        if (!Native.GetMonitorInfo(Native.MonitorFromPoint(cursor, Native.MONITOR_DEFAULTTONEAREST), ref info))
            return 0;
        RECT m = info.rcMonitor;

        bool across = r.Width >= m.Width * 6 / 10 && r.Height <= MaxThickness
                      && (Near(r.Bottom, m.Bottom) || Near(r.Top, m.Top));
        bool down = r.Height >= m.Height * 6 / 10 && r.Width <= MaxThickness
                    && (Near(r.Left, m.Left) || Near(r.Right, m.Right));
        if (!across && !down) return 0;

        // Our own panel is kept on top as well, and is never a taskbar.
        Native.GetWindowThreadProcessId(root, out int pid);
        if (IsOurs(pid)) return 0;

        rect = r;
        return root;
    }

    private static bool Near(int a, int b) => Math.Abs(a - b) <= EdgeGap;

    private static bool IsOurs(int pid)
    {
        if (pid == _checkedPid) return _checkedOurs;
        bool ours;
        try
        {
            using var process = Process.GetProcessById(pid);
            ours = process.ProcessName.StartsWith("TaskbarGroups", StringComparison.OrdinalIgnoreCase);
        }
        catch { ours = false; }
        _checkedPid = pid;
        _checkedOurs = ours;
        return ours;
    }
}
