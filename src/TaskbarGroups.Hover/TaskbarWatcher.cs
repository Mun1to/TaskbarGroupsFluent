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
    private readonly WarmFlyout _warm = new();

    /// <summary>Raised when the user has switched the feature off while we ran.</summary>
    public event Action? TurnedOff;

    /// <summary>The settings file changed and was read again, with this program still wanted.</summary>
    public event Action? SettingsChanged;

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
        _warm.Dispose();
        TaskbarTooltip.Stop();
    }

    private void Tick()
    {
        try { Poll(); }
        catch { /* a watcher that crashes is worse than one that misses a tick */ }
    }

    private void Poll()
    {
        // Picks up a changed dwell time without anyone restarting us, and lets us
        // bow out if everything we run for was switched off behind our back.
        if (_settings.TakeChange())
        {
            if (!HoverService.Wanted())
            {
                TurnedOff?.Invoke();
                return;
            }
            SettingsChanged?.Invoke();
        }

        // Since issue #11 this process also runs without hover-to-open: for groups
        // in the notification area, or to keep a panel loaded for good. Then only
        // the warm flyout is looked after here, and no taskbar button is ever read.
        bool hover = Settings.settingInfo.hoverToOpen;
        bool keep = Settings.settingInfo.instantOpen;
        if (keep) _warm.KeepReady(keep: true);

        if (!Native.GetCursorPos(out POINT cursor)) return;

        // The cheap gate. Nothing else runs unless the cursor is on a taskbar, so
        // the common case costs two P/Invoke calls and nothing else.
        var bars = TaskbarWindows();
        nint bar = 0;
        RECT barRect = default;
        foreach (nint h in bars)
        {
            if (Native.GetWindowRect(h, out RECT r) && r.Contains(cursor)) { bar = h; barRect = r; break; }
        }

        if (bar == 0)
        {
            _hovered = null;
            Forget();

            nint near = TaskbarNear(cursor, bars);
            if (near != 0)
            {
                // Heading for the taskbar. Two things are got out of the way now, so
                // that neither is paid for once the cursor is resting on an icon and
                // everything has to feel immediate: the button rectangles, which go
                // stale while the cursor is away because icons shift as windows come
                // and go, and the flyout itself, which takes the best part of a
                // second to load from cold. Approaching and turning away costs one
                // reading a second, and a loaded flyout that is let go shortly after.
                // The same head start serves a click on a pinned group or on a group
                // by the clock, so it is taken with hover-to-open off as well.
                if (hover && DateTime.UtcNow - _cachedAt > ApproachRefresh)
                    RefreshButtons(near);
                _warm.KeepReady(keep);
            }
            else if (!keep)
            {
                _warm.ReleaseIfIdle();
            }

            return;
        }

        _warm.KeepReady(keep);
        if (!hover)
        {
            _hovered = null;
            Forget();
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
            Forget();
            return;
        }

        if (hit.Group != _hovered)
        {
            _hovered = hit.Group;
            _hoverStart = DateTime.UtcNow;
            Forget();
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
        // With a warm flyout waiting there is nothing to overlap, so the dwell is
        // honoured in full: it is the only thing between resting on an icon and the
        // panel appearing, and showing costs about fifty milliseconds.
        bool warm = _warm.IsReady;
        if (waited < (warm ? delay : Math.Min(delay, PrelaunchMs))) return;

        // Resting on the icon is also what summons the taskbar's own tooltip, and
        // it is due a good half second after the panel, so it would arrive on top
        // of it. It cannot be declined, only put away as it appears.
        TaskbarTooltip.Watch(bar, barRect);

        if (warm && _warm.Show(hit.Group, hit.Rect))
        {
            _opened = hit.Group;
            return;
        }

        Open(hit, delay);
        _opened = hit.Group;
    }

    /// <summary>
    /// Forgets the open panel, and with it the reason to keep hiding the
    /// taskbar's tooltips.
    /// </summary>
    private void Forget()
    {
        _opened = null;
        TaskbarTooltip.Stop();
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

    /// <summary>The taskbar the cursor is just outside of, or zero if it is near none.</summary>
    private static nint TaskbarNear(POINT cursor, List<nint> bars)
    {
        foreach (nint h in bars)
        {
            if (!Native.GetWindowRect(h, out RECT r)) continue;
            if (cursor.X >= r.Left - ApproachMargin && cursor.X < r.Right + ApproachMargin
             && cursor.Y >= r.Top - ApproachMargin && cursor.Y < r.Bottom + ApproachMargin)
                return h;
        }
        return 0;
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
        catch { _buttons = found; return; }

        // Where the pinned and running apps sit on a Windows 10 taskbar. A button is
        // only matched by its name in there, so Start, the search box or a clock
        // that happens to read like a group never opens one.
        RECT? taskList = WindowsVersion.IsWindows11 ? null : TaskListOf(bar);

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
            if (box.IsEmpty || box.Width <= 0) continue;

            bool inTaskList = taskList is RECT list && list.Contains(new POINT
            {
                X = (int)Math.Round(box.Left + box.Width / 2),
                Y = (int)Math.Round(box.Top + box.Height / 2)
            });
            string? group = GroupFor(id, inTaskList ? name : null);
            if (group is null) continue;

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
    private string? GroupFor(string automationId, string? name)
    {
        string? appId = AppIdFromAutomationId(automationId);
        if (appId is not null) return PinnedGroups.Resolve(appId, _pins);
        return automationId.Length == 0 && name is not null
            ? PinnedGroups.ResolveByButtonName(name, _pins)
            : null;
    }

    /// <summary>
    /// The rectangle of a Windows 10 taskbar's list of apps (MSTaskListWClass),
    /// somewhere inside the taskbar window, or null if it is not there.
    /// </summary>
    private static RECT? TaskListOf(nint bar)
    {
        RECT? found = null;
        var name = new System.Text.StringBuilder(32);
        Native.EnumChildWindows(bar, (child, _) =>
        {
            name.Clear();
            if (Native.GetClassName(child, name, name.Capacity) > 0
                && name.ToString() == "MSTaskListWClass"
                && Native.GetWindowRect(child, out RECT r))
            {
                found = r;
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


    /// <summary>
    /// Launches the flyout in hover mode, telling it which rectangle opened it so it
    /// can treat the icon as part of itself and not vanish the moment the cursor
    /// crosses the gap between the two.
    /// </summary>
    private static void Open(GroupButton button, int delayMs)
    {
        try
        {
            // Without the "app starting" cursor: the panel showing up is the answer
            // to resting on the icon, and one the cursor moved away from in time
            // never shows at all, so there is nothing to flash it for.
            using var _ = QuietStart.Start(Paths.BackgroundApplication, new[]
            {
                $"--hover={button.Rect.Left},{button.Rect.Top},{button.Rect.Right},{button.Rect.Bottom}",
                // What is left of the dwell once the head start is subtracted. The flyout
                // sees it through, so the wait happens while it loads instead of before.
                $"--dwell={Math.Max(0, delayMs - PrelaunchMs)}",
                button.Group
            });
        }
        catch { /* a group whose flyout will not start must not stop the watcher */ }
    }
}
