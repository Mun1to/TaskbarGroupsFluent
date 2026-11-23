using System.Diagnostics;
using System.IO;
using TaskbarGroups.Shell;
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
    /// How long the pinned-shortcut lookup stays valid with the taskbar's shortcuts
    /// untouched. Far longer than the button rectangles because reading it means
    /// opening every shortcut on the taskbar; a group pinned, unpinned or renamed
    /// is noticed at once all the same (see <see cref="PinnedGroupsCache"/>).
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
    private readonly PinnedGroupsCache _pinCache = new(PinCacheLife);
    private IReadOnlyList<PinnedGroup> _pins = Array.Empty<PinnedGroup>();

    private string? _hovered;
    private DateTime _hoverStart;
    private string? _opened;

    /// <summary>A right click started during this visit to <see cref="_hovered"/>.</summary>
    private bool _clicked;

    private bool _rightWasDown;

    /// <summary>
    /// Whether a right click started since the last tick: the button went down, or
    /// went down and up again in between, as a quick click does. A button that was
    /// already held does not count, so dragging onto a group still opens it. The
    /// left button is left alone for the same reason: dragging a file onto a group
    /// to drop it in the panel (issue #4) holds it down the whole way, and a left
    /// click opens the panel by its own path anyway.
    /// </summary>
    private bool ButtonPressed()
    {
        short state = Native.GetAsyncKeyState(Native.VK_RBUTTON);
        bool down = (state & 0x8000) != 0;
        bool started = down ? !_rightWasDown : (state & 1) != 0;
        _rightWasDown = down;
        return started;
    }

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

        // Read on every tick, so that what it says is about this tick and not about
        // a click somewhere else a while ago.
        bool pressed = ButtonPressed();

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

        GroupButton? hit = null;
        string under = ClassUnder(cursor, bar);
        if (under == Covered)
        {
            // Another program's window lies over the taskbar here, and the cursor
            // is on that window, not on any icon of the taskbar.
        }
        else if (DrawnByAnotherProgram(under, bar))
        {
            // Issue #12: Start11 draws its own taskbar over the one of Windows 11,
            // which stays underneath with its buttons squeezed out of place, so its
            // buttons would name the wrong group. The icon on screen is what counts.
            hit = MatchByIcon(cursor, bar, barRect);
        }
        else
        {
            // Only the taskbar under the cursor is read. Windows leaves a
            // Shell_SecondaryTrayWnd behind for monitors that are no longer attached,
            // still flagged visible and parked off-screen, and reading those would
            // triple the cost to learn nothing.
            if (bar != _cachedBar || DateTime.UtcNow - _cachedAt > CacheLife) RefreshButtons(bar);

            foreach (var b in _buttons)
            {
                if (b.Rect.Contains(cursor)) { hit = b; break; }
            }

            // The older taskbar's list of apps, as Windows 10 has it and as
            // StartAllBack brings back, may not say which button is which: then the
            // icon on screen does.
            if (hit is null && under == TaskListClass) hit = MatchByIcon(cursor, bar, barRect);
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
            _clicked = pressed;
            Forget();
            return;
        }

        // Already opened for this visit. Without this the flyout would be relaunched
        // ten times a second for as long as the cursor stayed still.
        if (_opened == hit.Group) return;

        // Issue #12 (mihha): a right click on the icon before the panel is up brings
        // up the taskbar's own menu, and the panel showing up a moment later took it
        // away. The user asked for that menu, so hovering has nothing left to do
        // until the cursor leaves this icon and comes back.
        if (pressed) _clicked = true;
        if (_clicked) return;

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

    private const string TaskListClass = "MSTaskListWClass";

    /// <summary>
    /// The windows the taskbar of Windows 11 is made of, where its buttons can be
    /// read. Anything else under the cursor inside a taskbar was put there by
    /// another program, as Start11 does, or is the older taskbar's list of apps.
    /// Measured on Windows 11 26200: over the buttons of apps, Windows says the
    /// window under the cursor is MSTaskSwWClass; over Start, search and the clock,
    /// Shell_TrayWnd or TrayNotifyWnd.
    /// </summary>
    private static readonly HashSet<string> Windows11Parts = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "Windows.UI.Composition.DesktopWindowContentBridge",
        "Windows.UI.Input.InputSite.WindowClass",
        "MSTaskSwWClass", "ReBarWindow32",
        "TrayNotifyWnd"
    };

    /// <summary>What <see cref="ClassUnder"/> says when the window under the cursor is not the taskbar's.</summary>
    private const string Covered = "(covered)";

    /// <summary>
    /// The class of the window right under the cursor, "" if it can't be told, or
    /// <see cref="Covered"/> if that window is not part of the taskbar
    /// <paramref name="bar"/> at all: some window lying on top of it.
    /// </summary>
    internal static string ClassUnder(POINT cursor, nint bar)
    {
        nint window = Native.WindowFromPoint(cursor);
        if (window == 0) return "";
        if (Native.GetAncestor(window, Native.GA_ROOT) != bar) return Covered;
        var name = new System.Text.StringBuilder(128);
        return Native.GetClassName(window, name, name.Capacity) > 0 ? name.ToString() : "";
    }

    /// <summary>
    /// Whether the taskbar under the cursor is drawn by some other program on top
    /// of Windows 11's, so the buttons of Windows 11 beneath can't be trusted.
    ///
    /// The older taskbar's list of apps is a case of its own. Windows 11 shows it,
    /// a button or two wide, on the taskbars of other screens, and there its
    /// buttons read fine (measured on 26200). On the main taskbar Windows 11 never
    /// shows it: when it is under the cursor there, StartAllBack or Start11 has
    /// brought the older taskbar back.
    /// </summary>
    internal static bool DrawnByAnotherProgram(string under, nint bar)
    {
        if (!WindowsVersion.IsWindows11 || under.Length == 0) return false;
        if (under == TaskListClass) return bar == Native.FindWindow("Shell_TrayWnd", null);
        return !Windows11Parts.Contains(under);
    }

    private POINT _matchPoint;
    private DateTime _matchSince;
    private bool _matchDone;
    private GroupButton? _matched;
    private int _matchPins = -1;

    /// <summary>
    /// The group whose icon the cursor rests on, told by the icon itself (see
    /// <see cref="IconMatcher"/>). Looking costs tens of milliseconds, so it is
    /// done once the cursor has stopped, and once per stop.
    /// </summary>
    private GroupButton? MatchByIcon(POINT cursor, nint barWindow, RECT bar)
    {
        if (Math.Abs(cursor.X - _matchPoint.X) > 3 || Math.Abs(cursor.Y - _matchPoint.Y) > 3)
        {
            _matchPoint = cursor;
            _matchSince = DateTime.UtcNow;
            _matchDone = false;
            _matched = null;
            return null;
        }
        // Looked at already for this stop, unless the pinned groups have changed
        // since: a group dropped on the taskbar leaves the cursor resting right on
        // its new icon (issue #28), and that icon was nobody's a moment ago.
        RefreshPins();
        if (_matchDone && _pinCache.Changes == _matchPins) return _matched;
        if (DateTime.UtcNow - _matchSince < MatchAfter) return null;

        _matchDone = true;
        _matchPins = _pinCache.Changes;
        _matched = null;
        int dpi = 96;
        try { dpi = (int)Native.GetDpiForWindow(barWindow); } catch { }
        var found = IconMatcher.GroupAt(cursor, bar, _pins, dpi == 0 ? 96 : dpi);
        if (found is not { } f) return null;

        // The button stretches across its row of the bar around its icon (the whole
        // bar unless it holds several rows), so the panel lines up with it as it
        // does with a button read from Windows.
        RECT row = f.Row;
        bool across = row.Width >= row.Height;
        int thick = across ? row.Height : row.Width;
        int mid = across ? (f.Icon.Left + f.Icon.Right) / 2 : (f.Icon.Top + f.Icon.Bottom) / 2;
        RECT button = across
            ? new RECT { Left = mid - thick / 2, Right = mid + thick / 2, Top = row.Top, Bottom = row.Bottom }
            : new RECT { Top = mid - thick / 2, Bottom = mid + thick / 2, Left = row.Left, Right = row.Right };
        _matched = new GroupButton(f.Group, button);
        return _matched;
    }

    /// <summary>How still the cursor has to be before its icon is looked at.</summary>
    private static readonly TimeSpan MatchAfter = TimeSpan.FromMilliseconds(80);

    private void RefreshPins() => _pins = _pinCache.Read();

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

    private void RefreshButtons(nint bar)
    {
        _cachedAt = DateTime.UtcNow;
        _cachedBar = bar;
        RefreshPins();
        _buttons = TaskbarButtons.Read(bar, _pins).Select(button => new GroupButton(button.Group, new RECT
        {
            Left = (int)Math.Round(button.Bounds.Left), Top = (int)Math.Round(button.Bounds.Top),
            Right = (int)Math.Round(button.Bounds.Right), Bottom = (int)Math.Round(button.Bounds.Bottom)
        })).ToList();
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
