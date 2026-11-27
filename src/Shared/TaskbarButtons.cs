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
    internal static List<(string Group, System.Windows.Rect Bounds)> Read(nint bar, IReadOnlyList<PinnedGroup> pins, Action<string>? trace = null)
    {
        var found = new List<(string Group, System.Windows.Rect Bounds)>();

        AutomationElement? root;
        try { root = AutomationElement.FromHandle(bar); }
        catch (Exception e)
        {
            trace?.Invoke($"{ClassOf(bar)}: UI Automation could not open it ({e.GetType().Name})");
            return found;
        }
        if (root is null)
        {
            trace?.Invoke($"{ClassOf(bar)}: UI Automation gave nothing for it");
            return found;
        }

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
        catch (Exception e)
        {
            trace?.Invoke($"{ClassOf(bar)}: UI Automation could not list its buttons ({e.GetType().Name})");
            return found;
        }

        // Where the pinned and running apps sit on a Windows 10 taskbar. A button is
        // only matched by its name in there, so Start, the search box or a clock
        // that happens to read like a group never opens one.
        System.Windows.Rect? taskList = WindowsVersion.IsWindows11 ? null : TaskListOf(bar);

        var seenIds = trace is null ? null : new List<string>();
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

            seenIds?.Add(id.Length > 0 ? id : "name: " + name);
            bool inTaskList = taskList is { } list && list.Contains(box.Left + box.Width / 2, box.Top + box.Height / 2);
            string? group = GroupFor(id, inTaskList ? name : null, pins);
            if (group is null) continue;

            found.Add((group, box));
        }

        if (trace is not null)
        {
            string seen = $"{ClassOf(bar)}: {buttons.Count} buttons, {found.Count} of them groups";
            if (found.Count > 0) seen += ": " + string.Join(", ", found.Select(b => b.Group).Distinct());
            else if (seenIds is { Count: > 0 })
            {
                // Ours first: a button carrying a group's ID that no pinned shortcut
                // claims says the shortcuts are where the trouble is, not the buttons.
                var sample = seenIds
                    .OrderBy(i => i.Contains(Category.AppIdPrefix, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .Take(8)
                    .Select(ForTrace);
                seen += "; buttons read: " + string.Join(" | ", sample);
            }
            trace(seen);
        }
        return found;
    }

    /// <summary>
    /// Identifies the pinned group button directly under a physical screen point
    /// without walking the whole taskbar tree. Returns false if the point lies on
    /// empty taskbar space, another app, or another window covering the taskbar.
    /// </summary>
    internal static bool TryReadAtPoint(
        nint bar,
        int x,
        int y,
        IReadOnlyList<PinnedGroup> pins,
        out string group,
        out System.Windows.Rect bounds)
    {
        group = string.Empty;
        bounds = System.Windows.Rect.Empty;

        if (!GetWindowRect(bar, out RECT barRect)) return false;
        if (x < barRect.Left || x >= barRect.Right || y < barRect.Top || y >= barRect.Bottom) return false;

        int barWidth = barRect.Right - barRect.Left;
        int barHeight = barRect.Bottom - barRect.Top;
        System.Windows.Rect? taskList = WindowsVersion.IsWindows11 ? null : TaskListOf(bar);

        var cache = new CacheRequest { AutomationElementMode = AutomationElementMode.Full };
        cache.Add(AutomationElement.AutomationIdProperty);
        cache.Add(AutomationElement.BoundingRectangleProperty);
        cache.Add(AutomationElement.NameProperty);

        AutomationElement? element;
        try
        {
            using (cache.Activate())
                element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
        }
        catch { return false; }

        // FromPoint can land on an inner glyph/label inside a taskbar button, so
        // climb at most two ancestors while staying inside the taskbar bounds.
        for (int depth = 0; depth < 3 && element is not null; depth++)
        {
            string id, name;
            System.Windows.Rect box;
            try
            {
                id = element.Cached.AutomationId ?? string.Empty;
                name = element.Cached.Name ?? string.Empty;
                box = element.Cached.BoundingRectangle;
            }
            catch { return false; }

            if (!box.IsEmpty && box.Width > 0 && box.Height > 0)
            {
                if (box.Width >= barWidth * 0.9 && box.Height >= barHeight * 0.9) break;

                double cx = box.Left + box.Width / 2;
                double cy = box.Top + box.Height / 2;
                if (cx >= barRect.Left && cx < barRect.Right && cy >= barRect.Top && cy < barRect.Bottom)
                {
                    bool inTaskList = taskList is { } list && list.Contains(cx, cy);
                    string? resolved = GroupFor(id, inTaskList ? name : null, pins);
                    if (resolved is not null)
                    {
                        group = resolved;
                        bounds = box;
                        return true;
                    }
                }
            }

            if (depth == 2) break;
            try
            {
                element = TreeWalker.ControlViewWalker.GetParent(element, cache);
            }
            catch { return false; }
        }

        return false;
    }

    /// <summary>
    /// Finds every button on <paramref name="bar"/> belonging to <paramref name="group"/>.
    /// On Windows 11 this filters server-side by the group's candidate AppUserModelIDs
    /// instead of pulling every taskbar button across processes, falling back to a full
    /// <see cref="Read"/> when needed (Windows 10 name-matched pins or legacy frozen IDs).
    /// </summary>
    internal static List<System.Windows.Rect> FindGroup(
        nint bar,
        string group,
        IReadOnlyList<PinnedGroup> pins,
        out List<(string Group, System.Windows.Rect Bounds)>? allButtons)
    {
        allButtons = null;
        var found = new List<System.Windows.Rect>();

        if (WindowsVersion.IsWindows11)
        {
            List<string> candidates = PinnedGroups.CandidateAppIds(group, pins);
            if (candidates.Count > 0)
            {
                AutomationElement? root = null;
                try { root = AutomationElement.FromHandle(bar); }
                catch { /* fall back below */ }

                if (root is not null)
                {
                    Condition cond = candidates.Count == 1
                        ? new PropertyCondition(
                            AutomationElement.AutomationIdProperty,
                            "Appid: " + candidates[0],
                            PropertyConditionFlags.IgnoreCase)
                        : new OrCondition(candidates
                            .Select(id => (Condition)new PropertyCondition(
                                AutomationElement.AutomationIdProperty,
                                "Appid: " + id,
                                PropertyConditionFlags.IgnoreCase))
                            .ToArray());

                    var cache = new CacheRequest { AutomationElementMode = AutomationElementMode.None };
                    cache.Add(AutomationElement.AutomationIdProperty);
                    cache.Add(AutomationElement.BoundingRectangleProperty);

                    try
                    {
                        AutomationElementCollection matches;
                        using (cache.Activate())
                            matches = root.FindAll(TreeScope.Descendants, cond);

                        foreach (AutomationElement button in matches)
                        {
                            string id;
                            System.Windows.Rect box;
                            try
                            {
                                id = button.Cached.AutomationId ?? string.Empty;
                                box = button.Cached.BoundingRectangle;
                            }
                            catch { continue; }
                            if (box.IsEmpty || box.Width <= 0 || box.Height <= 0) continue;

                            string? resolved = GroupFor(id, null, pins);
                            if (string.Equals(resolved, group, StringComparison.OrdinalIgnoreCase))
                                found.Add(box);
                        }
                        if (found.Count > 0) return found;
                    }
                    catch { /* fall back to full read below */ }
                }
            }
        }

        allButtons = Read(bar, pins);
        foreach (var button in allButtons)
        {
            if (string.Equals(button.Group, group, StringComparison.OrdinalIgnoreCase))
                found.Add(button.Bounds);
        }
        return found;
    }

    private static string ClassOf(nint window)
    {
        var name = new System.Text.StringBuilder(64);
        return GetClassName(window, name, name.Capacity) > 0 ? name.ToString() : "?";
    }

    /// <summary>
    /// A button's ID as the trace shows it: short, and with the user's own folder
    /// put as %USERPROFILE%, since an app's ID can be its path and the file is
    /// meant to be pasted in an issue.
    /// </summary>
    private static string ForTrace(string text)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length > 0) text = text.Replace(home, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        return text.Length <= 80 ? text : text[..77] + "...";
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
