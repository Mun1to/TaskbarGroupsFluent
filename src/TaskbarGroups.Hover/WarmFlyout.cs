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

    private DateTime? _awaySince;

    private Process? _process;

    /// <summary>
    /// True if a warm flyout is loaded AND listening. The process being alive is not
    /// enough: for the best part of a second after starting it is still loading, and
    /// a request sent then is accepted and never acted on.
    /// </summary>
    public bool IsReady =>
        _process is not null && !_process.HasExited && FlyoutChannel.IsFlyoutReady();

    /// <summary>
    /// Called while the cursor is on or near the taskbar. Starts the warm flyout if
    /// there is not one already.
    /// </summary>
    public void KeepReady()
    {
        _awaySince = null;
        if (_process is not null && !_process.HasExited) return;

        try
        {
            var psi = new ProcessStartInfo(Paths.BackgroundApplication) { UseShellExecute = false };
            psi.ArgumentList.Add("--warm");
            _process = Process.Start(psi);
        }
        catch
        {
            // Without a warm flyout the watcher still works, it is just back to
            // starting one from scratch each time.
            _process = null;
        }
    }

    /// <summary>
    /// Called while the cursor is away from the taskbar. Lets the warm flyout go
    /// once it has been away long enough.
    /// </summary>
    public void ReleaseIfIdle()
    {
        if (_process is null || _process.HasExited) { _process = null; _awaySince = null; return; }

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
        if (_process is null || _process.HasExited) return false;
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
