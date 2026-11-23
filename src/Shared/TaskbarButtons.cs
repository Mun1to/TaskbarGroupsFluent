using System.Runtime.InteropServices;
using System.Windows.Automation;
using TaskbarGroups.Core;

namespace TaskbarGroups.Shell;

// Shared by the hover watcher and the click-opened flyout. UI Automation bounds
// are physical pixels. Call with per-monitor awareness when reading the task list.
internal static class TaskbarButtons
{
    /// <summary>
    /// Reads the taskbar's buttons and keeps the ones that belong to us.
    ///
    /// Each pinned group carries the AppUserModelID we stamped on its shortcut, and
    /// UI Automation hands it back as the button's AutomationId. That identifies the
    /// button, but it is not the group's name: the ID is fixed when the shortcut is
    /// built and survives a rename, so it can name a group that no longer exists.
    /// The shortcut's own launch argument is the name that does, which is why the ID
    /// is looked up against the pinned shortcuts rather than used directly.
    /// </summary>
    internal static List<(string Group, System.Windows.Rect Bounds)> Read(nint bar, IReadOnlyList<PinnedGroup> pins)
    {
        var found = new List<(string, System.Windows.Rect)>();

        AutomationElement? root;
        try { root = AutomationElement.FromHandle(bar); }
        catch { return found; }
        if (root is null) { return found; }

        // All the properties are fetched in the same pass. Read one at a time through
        // .Current they would be a cross-process call each, twenty buttons over,
        // which is where nearly all the time was going.
        var cache = new CacheRequest { AutomationElementMode = AutomationElementMode.None };
        cache.Add(AutomationElement.AutomationIdProperty);
        cache.Add(AutomationElement.BoundingRectangleProperty);
        cache.Add(AutomationElement.NameProperty);

        AutomationElementCollection buttons;
        try
        {
            using (cache.Activate())
                buttons = root.FindAll(TreeScope.Descendants, ButtonCondition);
        }
        catch { return found; }

        // Where the pinned and running apps sit on a Windows 10 taskbar. A button is
        // only matched by its name in there, so Start, the search box or a clock
        // that happens to read like a group never opens one.
        System.Windows.Rect? taskList = WindowsVersion.IsWindows11 ? null : TaskListOf(bar);

        foreach (AutomationElement button in buttons)
        {
            string id, name;
            System.Windows.Rect box;
            try
            {
                id = button.Cached.AutomationId ?? string.Empty;
                name = button.Cached.Name ?? string.Empty;
                box = button.Cached.BoundingRectangle;
            }
            catch { continue; }
            if (box.IsEmpty || box.Width <= 0 || box.Height <= 0) continue;

            bool inTaskList = taskList is { } list && list.Contains(box.Left + box.Width / 2, box.Top + box.Height / 2);
            string? group = GroupFor(id, inTaskList ? name : null, pins);
            if (group is null) continue;

            found.Add((group, box));
        }

        return found;
    }

    /// <summary>
    /// What counts as a taskbar button. On Windows 11 its buttons are buttons. The
    /// taskbar of Windows 10 is an older control, and a pinned icon there can also
    /// come through as a menu item or a split button, so those are taken too.
    /// </summary>
    private static readonly Condition ButtonCondition = WindowsVersion.IsWindows11
        ? new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)
        : new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.SplitButton));

    /// <summary>
    /// The group a taskbar button opens, or null if the button is not one of ours.
    ///
    /// The pinned shortcut is the authority, because it holds the same name the
    /// click path uses. Falling back to the ID's own suffix covers the case where
    /// the shortcut cannot be read at all, and it is still checked against a real
    /// config folder so a stale ID never launches a group that is not there.
    ///
    /// A button with no ID at all is matched by its name instead, which is the
    /// name of its pinned shortcut. That is how the taskbar of Windows 10 may hand
    /// its buttons over, and it is never tried on a button that does carry an ID,
    /// nor on one outside the taskbar's list of apps (a null name).
    /// </summary>
    private static string? GroupFor(string automationId, string? name, IReadOnlyList<PinnedGroup> pins)
    {
        string? appId = AppIdFromAutomationId(automationId);
        if (appId is not null) return PinnedGroups.Resolve(appId, pins);
        return automationId.Length == 0 && name is not null
            ? PinnedGroups.ResolveByButtonName(name, pins)
            : null;
    }

    /// <summary>
    /// The rectangle of a Windows 10 taskbar's list of apps (MSTaskListWClass),
    /// somewhere inside the taskbar window, or null if it is not there.
    /// </summary>
    private static System.Windows.Rect? TaskListOf(nint bar)
    {
        System.Windows.Rect? found = null;
        var name = new System.Text.StringBuilder(32);
        EnumChildWindows(bar, (child, _) =>
        {
            name.Clear();
            if (GetClassName(child, name, name.Capacity) > 0
                && name.ToString() == "MSTaskListWClass"
                && GetWindowRect(child, out RECT r))
            {
                found = new System.Windows.Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
                return false;
            }
            return true;
        }, 0);
        return found;
    }

    /// <summary>
    /// The AppUserModelID inside a taskbar button's AutomationId, or null if the
    /// button is not a pinned group. Windows prefixes the id with "Appid: ".
    /// </summary>
    internal static string? AppIdFromAutomationId(string automationId)
    {
        if (string.IsNullOrEmpty(automationId)) return null;

        int at = automationId.IndexOf(Category.AppIdPrefix, StringComparison.OrdinalIgnoreCase);
        return at < 0 ? null : automationId[at..].Trim();
    }


    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    private delegate bool EnumWindowsProc(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, EnumWindowsProc callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, System.Text.StringBuilder name, int capacity);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out RECT rect);
}
