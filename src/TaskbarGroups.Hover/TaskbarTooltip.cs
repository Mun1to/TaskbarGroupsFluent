using System.Text;

namespace TaskbarGroups.Hover;

/// <summary>
/// Puts away the taskbar's own tooltip while a hovered group's panel is open.
///
/// Resting the cursor on a taskbar button is precisely what makes Windows show
/// that button's name in a small bubble, and it is due about eight hundred
/// milliseconds in. Clicking never ran into this, because a click moves the
/// cursor on long before the bubble is owed; hovering leaves it exactly where the
/// bubble wants to appear, so it arrives half a second after the panel and sits
/// on top of it for its full two and a half seconds, covering an app or two.
///
/// Windows offers no way to decline it, so it is hidden the moment it shows.
/// That is safe: the bubble is a disposable XAML popup, and the taskbar builds
/// another for the next hover, with a new window handle each time.
///
/// Two details were learned the hard way and are what make this work at all.
/// First, the bubble is caught on the system's own notification rather than by
/// looking every so often: polling at the watcher's sixty millisecond tick let it
/// flash for a frame before it went. Second, a bubble is shown before it is
/// placed, so the notification arrives while its rectangle is still empty and
/// there is no way yet to tell it from the taskbar's right-click menu, which is
/// the same class of window. So one that turns up unplaced is followed until
/// Windows puts it somewhere, and only then judged.
/// </summary>
internal static class TaskbarTooltip
{
    private const string PopupClass = "Xaml_WindowedPopupClass";

    /// <summary>
    /// Tallest a tooltip can be. The taskbar's right-click menu is the same class
    /// of window and several times this, which is what keeps one from being
    /// hidden out from under the user.
    /// </summary>
    private const int MaxHeight = 120;

    /// <summary>How far from the taskbar a tooltip of ours may sit, in pixels.</summary>
    private const int Reach = 200;

    // Held for the life of the process: a delegate handed to Windows that the
    // garbage collector is free to collect is a crash waiting for a quiet moment.
    private static readonly Native.WinEventProc OnShown = Shown;
    private static readonly Native.WinEventProc OnPlaced = Placed;
    private static readonly Native.EnumWindowsProc OnWindow = Consider;

    private static readonly StringBuilder ClassName = new(64);

    private static nint _shownHook;
    private static nint _placedHook;
    private static nint _unplaced;
    private static RECT _near;
    private static int _owner;

    /// <summary>
    /// Starts hiding taskbar tooltips that appear beside <paramref name="bar"/>.
    /// Called when a hovered panel opens; harmless to call again.
    /// </summary>
    public static void Watch(nint bar, RECT barRect)
    {
        _near = new RECT
        {
            Left = barRect.Left - Reach,
            Top = barRect.Top - Reach,
            Right = barRect.Right + Reach,
            Bottom = barRect.Bottom + Reach
        };

        if (_shownHook != 0) return;

        Native.GetWindowThreadProcessId(bar, out _owner);
        if (_owner == 0) return;

        _shownHook = Native.SetWinEventHook(
            Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_SHOW, 0, OnShown,
            (uint)_owner, 0, Native.WINEVENT_OUTOFCONTEXT);

        _placedHook = Native.SetWinEventHook(
            Native.EVENT_OBJECT_LOCATIONCHANGE, Native.EVENT_OBJECT_LOCATIONCHANGE, 0,
            OnPlaced, (uint)_owner, 0, Native.WINEVENT_OUTOFCONTEXT);

        // One sweep on the way in, for a bubble that was already up before the
        // panel opened. After this the notifications do the work.
        try { Native.EnumWindows(OnWindow, 0); } catch { }
    }

    /// <summary>Stops watching. Called as soon as no hovered panel is open.</summary>
    public static void Stop()
    {
        _unplaced = 0;
        if (_shownHook == 0) return;

        try { Native.UnhookWinEvent(_shownHook); } catch { }
        try { Native.UnhookWinEvent(_placedHook); } catch { }
        _shownHook = 0;
        _placedHook = 0;
    }

    private static void Shown(nint hook, uint evt, nint hwnd,
        int objectId, int childId, uint thread, uint time)
    {
        // Only whole windows appearing. The taskbar raises this for its inner
        // parts too, and those are not windows anyone could hide.
        if (objectId != Native.OBJID_WINDOW || childId != 0 || hwnd == 0) return;

        try
        {
            if (!IsTaskbarPopup(hwnd)) return;
            if (!Settle(hwnd)) _unplaced = hwnd;
        }
        catch { /* one missed tooltip is not worth taking the watcher down for */ }
    }

    private static void Placed(nint hook, uint evt, nint hwnd,
        int objectId, int childId, uint thread, uint time)
    {
        if (hwnd != _unplaced || objectId != Native.OBJID_WINDOW || childId != 0) return;

        try { if (Settle(hwnd)) _unplaced = 0; }
        catch { _unplaced = 0; }
    }

    private static bool Consider(nint hwnd, nint _)
    {
        if (IsTaskbarPopup(hwnd)) Settle(hwnd);
        return true;
    }

    /// <summary>A popup window belonging to the taskbar's own process.</summary>
    private static bool IsTaskbarPopup(nint hwnd)
    {
        ClassName.Clear();
        if (Native.GetClassName(hwnd, ClassName, ClassName.Capacity) == 0) return false;
        if (!ClassName.Equals(PopupClass.AsSpan())) return false;

        Native.GetWindowThreadProcessId(hwnd, out int pid);
        return pid == _owner;
    }

    /// <summary>
    /// Judges a taskbar popup now that it may have somewhere to be, hiding it if
    /// it is a tooltip next to the taskbar. False means it has no position yet and
    /// is worth waiting for.
    /// </summary>
    private static bool Settle(nint hwnd)
    {
        if (!Native.GetWindowRect(hwnd, out RECT r)) return true;
        if (r.Width <= 0 || r.Height <= 0) return false;

        if (r.Height <= MaxHeight && Overlaps(_near, r) && Native.IsWindowVisible(hwnd))
            Native.ShowWindow(hwnd, Native.SW_HIDE);

        return true;
    }

    private static bool Overlaps(RECT a, RECT b)
        => a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
}
