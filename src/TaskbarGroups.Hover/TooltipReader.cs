using System.Text;

namespace TaskbarGroups.Hover;

/// <summary>
/// Tells which button the cursor rests on by the tooltip the taskbar shows for it,
/// on a taskbar that hands none of its buttons over to UI Automation (issue #12).
///
/// StartAllBack has drawn a taskbar of its own since its version 3.8. Measured on
/// a user's PC (StartAllBack 3.9.28, Windows build 26300), its list of apps is
/// there and on screen, but UI Automation finds not a single button in it, and
/// asking what is under the cursor gives back the whole list. What it does show,
/// like any taskbar, is the name of the button in a tooltip once the cursor has
/// rested on it, and that tooltip is an ordinary window whose text anyone can read.
/// </summary>
internal static class TooltipReader
{
    /// <summary>Tallest a tooltip can be, as in <see cref="TaskbarTooltip"/>.</summary>
    private const int MaxHeight = 120;

    /// <summary>How far from the cursor a tooltip may appear, in pixels.</summary>
    private const int Reach = 200;

    /// <summary>Longest a window may take to say its text.</summary>
    private const uint TextTimeoutMs = 50;

    private static readonly StringBuilder Text = new(260);

    /// <summary>
    /// The group whose name is in a tooltip of <paramref name="bar"/>'s program near
    /// the cursor, going by <paramref name="resolve"/>, or null if there is none.
    /// Every tooltip there is tried, so a stray one cannot hide the right one.
    /// </summary>
    public static string? GroupNear(nint bar, POINT cursor, Func<string, string?> resolve)
    {
        Native.GetWindowThreadProcessId(bar, out int owner);
        if (owner == 0) return null;

        string? group = null;
        Native.EnumWindows((hwnd, unused) =>
        {
            if (!IsTooltipNear(hwnd, owner, cursor)) return true;
            string text = TextOf(hwnd);
            if (text.Length == 0) return true;
            group = resolve(text);
            return group is null;
        }, 0);
        return group;
    }

    /// <summary>
    /// A visible window of the taskbar's program, kept on top and no taller than a
    /// tooltip, close to the cursor. The class is not checked: each taskbar may use
    /// its own, and only a name that matches a pinned group is ever acted on.
    /// </summary>
    private static bool IsTooltipNear(nint hwnd, int owner, POINT cursor)
    {
        if (!Native.IsWindowVisible(hwnd)) return false;
        Native.GetWindowThreadProcessId(hwnd, out int pid);
        if (pid != owner) return false;
        if (((long)Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE) & Native.WS_EX_TOPMOST) == 0) return false;
        if (!Native.GetWindowRect(hwnd, out RECT r) || r.Width <= 0 || r.Height <= 0 || r.Height > MaxHeight) return false;
        return cursor.X >= r.Left - Reach && cursor.X < r.Right + Reach
            && cursor.Y >= r.Top - Reach && cursor.Y < r.Bottom + Reach;
    }

    private static string TextOf(nint hwnd)
    {
        Text.Clear();
        nint sent = Native.SendMessageTimeout(hwnd, Native.WM_GETTEXT, Text.Capacity, Text,
            Native.SMTO_ABORTIFHUNG, TextTimeoutMs, out _);
        return sent == 0 ? string.Empty : Text.ToString().Trim();
    }
}
