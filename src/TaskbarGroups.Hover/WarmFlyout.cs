using System.Diagnostics;
using TaskbarGroups.Core;

namespace TaskbarGroups.Hover;

/// <summary>
/// Keeps a flyout loaded and waiting while the cursor is near the taskbar, and
/// lets it go once the cursor has been away for a while.
///
/// This is where nearly all the speed comes from. Starting a flyout from nothing
/// means loading .NET, then WPF, then laying out and painting a window for the
/// first time: measured at about nine hundred milliseconds, and almost none of it
/// is work anyone chose to do. A flyout that has already been through all that
/// can be put on screen in about fifty.
///
/// The cost is that a loaded flyout holds around 160 MB, which is far too much to
/// keep for a feature that might be used twice a day. So it is only loaded while
/// the cursor is near the taskbar, and dropped after a spell away: present when it
/// is about to be needed, absent the rest of the time.
/// </summary>
internal sealed class WarmFlyout : IDisposable
{
    /// <summary>
    /// How long the cursor has to stay away from the taskbar before the loaded
    /// flyout is let go. Long enough to survive reaching for a window and coming
    /// back, short enough that wandering off frees the memory promptly.
    /// </summary>
    private static readonly TimeSpan IdleLife = TimeSpan.FromSeconds(20);

    /// <summary>
    /// A warm flyout that ends sooner than this, without being asked to, did not
    /// manage to stay up: it found nothing to load, or it crashed on the way.
    /// </summary>
    private static readonly TimeSpan ShortLife = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The wait after the first flyout that did not stay up, doubled after each
    /// one that follows, up to <see cref="LongestRetry"/>. Starting the next one
    /// straight away is what turned a flyout that could not stay up into a new
    /// process every half second for as long as the cursor was on the taskbar.
    /// </summary>
    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LongestRetry = TimeSpan.FromMinutes(5);

    private DateTime? _awaySince;

    private Process? _process;
    private DateTime _startedAt;
    private int _shortLives;
    private DateTime _retryAt = DateTime.MinValue;

    /// <summary>
    /// True if a warm flyout is loaded AND listening. The process being alive is not
    /// enough: for the best part of a second after starting it is still loading, and
    /// a request sent then is accepted and never acted on.
    ///
    /// It does not have to be ours. A flyout opened by a click stays loaded for a
    /// while to take the next clicks, and it answers the watcher just the same.
    /// </summary>
    public bool IsReady => FlyoutChannel.IsFlyoutReady();

    /// <summary>
    /// Called while the cursor is on or near the taskbar. Starts the warm flyout if
    /// there is not one already, ours or one a click left loaded.
    /// </summary>
    public void KeepReady()
    {
        _awaySince = null;
        if (_process is not null && !_process.HasExited) return;
        Forget();
        if (DateTime.UtcNow < _retryAt) return;
        if (FlyoutChannel.IsFlyoutReady()) return;

        _startedAt = DateTime.UtcNow;
        try
        {
            _process = QuietStart.Start(Paths.BackgroundApplication, new[] { "--warm" });
        }
        catch
        {
            // Without a warm flyout the watcher still works, it is just back to
            // starting one from scratch each time.
            _process = null;
            CountShortLife();
        }
    }

    /// <summary>
    /// Lets go of a warm flyout that has ended by itself, and holds back the next
    /// one if this one did not last.
    /// </summary>
    private void Forget()
    {
        if (_process is null) return;

        if (DateTime.UtcNow - _startedAt < ShortLife) CountShortLife();
        else _shortLives = 0;

        _process.Dispose();
        _process = null;
    }

    private void CountShortLife()
    {
        _shortLives++;
        double seconds = FirstRetry.TotalSeconds * Math.Pow(2, Math.Min(_shortLives - 1, 10));
        _retryAt = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Min(seconds, LongestRetry.TotalSeconds));
    }

    /// <summary>
    /// Called while the cursor is away from the taskbar. Lets the warm flyout go
    /// once it has been away long enough.
    /// </summary>
    public void ReleaseIfIdle()
    {
        if (_process is null || _process.HasExited) { Forget(); _awaySince = null; return; }

        _awaySince ??= DateTime.UtcNow;
        if (DateTime.UtcNow - _awaySince < IdleLife) return;

        Stop();
    }

    /// <summary>
    /// Asks the warm flyout to show a group. Returns false if there was nobody to
    /// ask, so the caller can start one the ordinary way.
    /// </summary>
    public bool Show(string group, RECT rect)
    {
        if (!IsReady) return false;
        return FlyoutChannel.Show(group, rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    public void Stop()
    {
        _awaySince = null;
        var p = _process;
        _process = null;
        if (p is null) return;

        try
        {
            // Ask first: it can put its own window away tidily. Killing it outright
            // can leave the panel painted on screen for a moment.
            if (!p.HasExited) FlyoutChannel.Quit();
            if (!p.WaitForExit(700) && !p.HasExited) p.Kill();
        }
        catch { }
        finally { p.Dispose(); }
    }

    public void Dispose() => Stop();
}
