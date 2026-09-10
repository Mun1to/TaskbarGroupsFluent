using System.Diagnostics;
using System.IO;
using System.Windows.Automation;
using TaskbarGroups.Core;

namespace TaskbarGroups.Hover;

/// <summary>
/// Watches the cursor and opens a group's flyout when it rests on that group's
/// taskbar icon.
///
/// Windows never tells anyone that the cursor is over a pinned icon: hovering a
/// taskbar button launches nothing, and there is no setting that changes it. The
/// only way to offer hover-to-open is to look for ourselves, which is why this
/// process has to stay resident.
///
/// The cost is kept where it belongs. Asking UI Automation for the taskbar's
/// buttons takes tens of milliseconds, far too much to do on a loop, so it runs
/// only while the cursor is actually inside the taskbar and at most every couple
/// of seconds; the answer is cached. Every other tick is two P/Invoke calls, which
/// is what lets this sit idle all day without being felt.
/// </summary>
internal sealed class TaskbarWatcher : IDisposable
{
    private sealed record GroupButton(string Group, RECT Rect);

    /// <summary>
    /// How often the cursor is checked. Two P/Invoke calls on all but a few ticks,
    /// so this is set by how much delay it adds to noticing, not by its cost.
    /// </summary>
    private const int TickMs = 60;

    /// <summary>
    /// How long the cached button rectangles stay valid while the cursor is on the
    /// taskbar. Icons shift as apps open and close, so the cache has to be short
    /// lived, but it still spares us the lookup on all but one tick in twenty.
    /// </summary>
    private static readonly TimeSpan CacheLife = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long the pinned-shortcut lookup stays valid. Far longer than the button
    /// rectangles because it only changes when a group is pinned, unpinned or
    /// renamed, and reading it means opening every shortcut on the taskbar.
    /// </summary>
    private static readonly TimeSpan PinCacheLife = TimeSpan.FromSeconds(30);

    private readonly System.Windows.Forms.Timer _timer;
    private readonly SettingsListener _settings = new();

    /// <summary>Raised when the user has switched the feature off while we ran.</summary>
    public event Action? TurnedOff;

    private List<GroupButton> _buttons = new();
    private DateTime _cachedAt = DateTime.MinValue;
    private nint _cachedBar;
    private List<PinnedGroup> _pins = new();
    private DateTime _pinsAt = DateTime.MinValue;

    private string? _hovered;
    private DateTime _hoverStart;
    private string? _opened;

    public TaskbarWatcher()
    {
        _timer = new System.Windows.Forms.Timer { Interval = TickMs };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start() => _timer.Start();

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        _settings.Dispose();
    }

    private void Tick()
    {
        try { Poll(); }
        catch { /* a watcher that crashes is worse than one that misses a tick */ }
    }

    private void Poll()
    {
        // Picks up a changed dwell time without anyone restarting us, and lets us
        // bow out if the feature was switched off behind our back.
        if (_settings.TakeChange() && !Settings.settingInfo.hoverToOpen)
        {
            TurnedOff?.Invoke();
            return;
        }

        if (!Native.GetCursorPos(out POINT cursor)) return;

        // The cheap gate. Nothing else runs unless the cursor is on a taskbar, so
        // the common case costs two P/Invoke calls and nothing else.
        var bars = TaskbarWindows();
        nint bar = 0;
        foreach (nint h in bars)
        {
            if (Native.GetWindowRect(h, out RECT r) && r.Contains(cursor)) { bar = h; break; }
        }

        if (bar == 0)
        {
            _hovered = null;
            _opened = null;

            // Not on the taskbar, but close enough to be heading for it. Refreshing
            // now takes the cost off the critical path: icons shift while the cursor
            // is away, so the rectangles do have to be re-read, and doing it on
            // arrival would add its own delay to the one thing that has to feel
            // immediate. Approaching and turning away costs one reading a second.
            if (NearAnyTaskbar(cursor, bars) && DateTime.UtcNow - _cachedAt > ApproachRefresh)
                RefreshButtons(bars.Count > 0 ? bars[0] : 0);

            return;
        }

        // Only the taskbar under the cursor is read. Windows leaves a
        // Shell_SecondaryTrayWnd behind for monitors that are no longer attached,
        // still flagged visible and parked off-screen, and reading those would
        // triple the cost to learn nothing.
        if (bar != _cachedBar || DateTime.UtcNow - _cachedAt > CacheLife) RefreshButtons(bar);

        GroupButton? hit = null;
        foreach (var b in _buttons)
        {
            if (b.Rect.Contains(cursor)) { hit = b; break; }
        }

        if (hit is null)
        {
            _hovered = null;
            _opened = null;
            return;
        }

        if (hit.Group != _hovered)
        {
            _hovered = hit.Group;
            _hoverStart = DateTime.UtcNow;
            _opened = null;
            return;
        }

        // Already opened for this visit. Without this the flyout would be relaunched
        // ten times a second for as long as the cursor stayed still.
        if (_opened == hit.Group) return;

        int delay = Math.Max(0, Settings.settingInfo.hoverDelayMs);
        double waited = (DateTime.UtcNow - _hoverStart).TotalMilliseconds;

        // The flyout is launched well before the dwell is up, and waits out the rest
        // itself. Starting it takes far longer than the dwell does, so running the
        // two at the same time is most of the wait the user actually feels; done in
        // sequence they simply add up. The flyout checks the cursor is still on the
        // icon before it shows, so nothing appears that should not have.
        if (waited < Math.Min(delay, PrelaunchMs)) return;

        Open(hit, delay);
        _opened = hit.Group;
    }

    /// <summary>
    /// How long the cursor has to rest before the flyout is started in the
    /// background. Long enough that sweeping across the taskbar does not spawn a
    /// process per icon, short enough to leave most of the startup overlapping the
    /// dwell rather than following it.
    /// </summary>
    private const int PrelaunchMs = 120;


    /// <summary>
    /// How close to the taskbar counts as heading for it, in pixels. Wide enough to
    /// give the reading time to finish before the cursor arrives.
    /// </summary>
    private const int ApproachMargin = 140;

    /// <summary>How often an approach may trigger a reading.</summary>
    private static readonly TimeSpan ApproachRefresh = TimeSpan.FromMilliseconds(900);

    /// <summary>True if the cursor is just outside one of the taskbars.</summary>
    private static bool NearAnyTaskbar(POINT cursor, List<nint> bars)
    {
        foreach (nint h in bars)
        {
            if (!Native.GetWindowRect(h, out RECT r)) continue;
            if (cursor.X >= r.Left - ApproachMargin && cursor.X < r.Right + ApproachMargin
             && cursor.Y >= r.Top - ApproachMargin && cursor.Y < r.Bottom + ApproachMargin)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Handles of every visible taskbar: the primary one plus one per extra monitor.
    /// </summary>
    private static List<nint> TaskbarWindows()
    {
        var list = new List<nint>(2);
        nint primary = Native.FindWindow("Shell_TrayWnd", null);
        if (primary != 0 && Native.IsWindowVisible(primary)) list.Add(primary);

        nint secondary = 0;
        while ((secondary = Native.FindWindowEx(0, secondary, "Shell_SecondaryTrayWnd", null)) != 0)
        {
            if (Native.IsWindowVisible(secondary)) list.Add(secondary);
        }
        return list;
    }

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
    private void RefreshButtons(nint bar)
    {
        _cachedAt = DateTime.UtcNow;
        _cachedBar = bar;
        var found = new List<GroupButton>();

        if (DateTime.UtcNow - _pinsAt > PinCacheLife)
        {
            try { _pins = PinnedGroups.Scan(); }
            catch { /* keep the previous map rather than losing every group */ }
            _pinsAt = DateTime.UtcNow;
        }

        AutomationElement? root;
        try { root = AutomationElement.FromHandle(bar); }
        catch { _buttons = found; return; }
        if (root is null) { _buttons = found; return; }

        // Both properties are fetched in the same pass. Read one at a time through
        // .Current they would be a cross-process call each, twenty buttons over,
        // which is where nearly all the time was going.
        var cache = new CacheRequest { AutomationElementMode = AutomationElementMode.None };
        cache.Add(AutomationElement.AutomationIdProperty);
        cache.Add(AutomationElement.BoundingRectangleProperty);

        var isButton = new PropertyCondition(
            AutomationElement.ControlTypeProperty, ControlType.Button);

        AutomationElementCollection buttons;
        try
        {
            using (cache.Activate())
                buttons = root.FindAll(TreeScope.Descendants, isButton);
        }
        catch { _buttons = found; return; }

        foreach (AutomationElement button in buttons)
        {
            string id;
            System.Windows.Rect box;
            try
            {
                id = button.Cached.AutomationId ?? string.Empty;
                box = button.Cached.BoundingRectangle;
            }
            catch { continue; }

            string? group = GroupFor(id);
            if (group is null || box.IsEmpty || box.Width <= 0) continue;

            found.Add(new GroupButton(group, new RECT
            {
                Left = (int)Math.Round(box.Left),
                Top = (int)Math.Round(box.Top),
                Right = (int)Math.Round(box.Right),
                Bottom = (int)Math.Round(box.Bottom)
            }));
        }

        _buttons = found;
    }

    /// <summary>
    /// The group a taskbar button opens, or null if the button is not one of ours.
    ///
    /// The pinned shortcut is the authority, because it holds the same name the
    /// click path uses. Falling back to the ID's own suffix covers the case where
    /// the shortcut cannot be read at all, and it is still checked against a real
    /// config folder so a stale ID never launches a group that is not there.
    /// </summary>
    private string? GroupFor(string automationId)
    {
        string? appId = AppIdFromAutomationId(automationId);
        return appId is null ? null : PinnedGroups.Resolve(appId, _pins);
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


    /// <summary>
    /// Launches the flyout in hover mode, telling it which rectangle opened it so it
    /// can treat the icon as part of itself and not vanish the moment the cursor
    /// crosses the gap between the two.
    /// </summary>
    private static void Open(GroupButton button, int delayMs)
    {
        try
        {
            var psi = new ProcessStartInfo(Paths.BackgroundApplication) { UseShellExecute = false };
            psi.ArgumentList.Add(
                $"--hover={button.Rect.Left},{button.Rect.Top},{button.Rect.Right},{button.Rect.Bottom}");
            // What is left of the dwell once the head start is subtracted. The flyout
            // sees it through, so the wait happens while it loads instead of before.
            psi.ArgumentList.Add($"--dwell={Math.Max(0, delayMs - PrelaunchMs)}");
            psi.ArgumentList.Add(button.Group);
            Process.Start(psi);
        }
        catch { /* a group whose flyout will not start must not stop the watcher */ }
    }
}
