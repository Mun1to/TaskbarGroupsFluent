namespace TaskbarGroups.Hover;

/// <summary>
/// Which taskbar a taskbar window is showing, read from the window itself rather
/// than from the version of Windows (issue #12).
///
/// Windows 11 draws its taskbar in XAML, but it still carries the list of apps of
/// the Windows 10 taskbar (MSTaskListWClass) inside it, hidden. Start11 and
/// StartAllBack bring that older taskbar back on Windows 11, and then that list is
/// the one on screen, handing its buttons over the way Windows 10 does. Going by
/// the version of Windows, the watcher looked there for the buttons of the new
/// taskbar and never found a single group, so resting on one opened nothing.
/// </summary>
internal static class TaskbarStyle
{
    /// <summary>
    /// Where the older taskbar's list of apps sits, when <paramref name="bar"/> is
    /// showing that taskbar (Windows 10, or Windows 11 with Start11 or
    /// StartAllBack), or null when it is the taskbar of Windows 11.
    /// </summary>
    public static RECT? ClassicTaskList(nint bar)
    {
        RECT? list = null;
        bool xaml = false;
        var name = new System.Text.StringBuilder(64);
        Native.EnumChildWindows(bar, (child, unused) =>
        {
            name.Clear();
            if (Native.GetClassName(child, name, name.Capacity) == 0) return true;
            string cls = name.ToString();

            // The taskbar of Windows 11 is a XAML island across the whole bar. Its
            // own list of apps is left in there too, hidden on the main taskbar but
            // shown, a single icon wide, on the taskbars of other monitors (measured
            // on Windows 11 26200), so the list alone cannot tell the two apart.
            if (cls == XamlIsland && OnScreen(child, out RECT island)) xaml = true;
            else if (cls == TaskList && list is null && OnScreen(child, out RECT r)) list = r;
            return true;
        }, 0);
        return xaml ? null : list;
    }

    /// <summary>
    /// Whether <paramref name="bar"/> is drawn the way the taskbar of Windows 11
    /// is, with a XAML island across it.
    /// </summary>
    public static bool HasXamlIsland(nint bar)
    {
        bool xaml = false;
        var name = new System.Text.StringBuilder(64);
        Native.EnumChildWindows(bar, (child, unused) =>
        {
            name.Clear();
            if (Native.GetClassName(child, name, name.Capacity) > 0
                && name.ToString() == XamlIsland && OnScreen(child, out RECT island))
            {
                xaml = true;
                return false;
            }
            return true;
        }, 0);
        return xaml;
    }

    private const string XamlIsland = "Windows.UI.Composition.DesktopWindowContentBridge";
    private const string TaskList = "MSTaskListWClass";

    private static bool OnScreen(nint window, out RECT rect)
        => Native.IsWindowVisible(window)
           & Native.GetWindowRect(window, out rect)
           && rect.Width > 0 && rect.Height > 0;
}
