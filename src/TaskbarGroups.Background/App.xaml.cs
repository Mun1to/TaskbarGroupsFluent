using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using TaskbarGroups.Background.Models;
using TaskbarGroups.Core;

namespace TaskbarGroups.Background;

/// <summary>
/// Background client entry point. Launched (by a pinned taskbar shortcut) with
/// the group name as its argument; shows that group's flyout above the taskbar
/// and exits when the flyout closes.
/// </summary>
public partial class App : Application
{
    /// <summary>Milliseconds since Windows created this process.</summary>
    private static double SinceStart()
    {
        try { return (DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalMilliseconds; }
        catch { return 0; }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // No theme to apply: the flyout paints itself from IsLightTheme(), and the
        // WPF-UI dictionaries it used to need are gone.

        if (e.Args.Length == 0)
        {
            Shutdown();
            return;
        }

        // Started to sit ready rather than to show anything: load, paint off screen,
        // and wait to be told which group to put up. Everything slow is paid for
        // now, while the cursor is only heading towards the taskbar.
        foreach (string a in e.Args)
        {
            if (a.Equals("--warm", StringComparison.OrdinalIgnoreCase)) { RunWarm(); return; }
        }

        // The pinned shortcut passes the group name unquoted, so a name with
        // spaces arrives split across several args. Rejoin to rebuild it, keeping
        // any "--" switch (the hover watcher adds one) out of the name.
        var words = new List<string>();
        HoverAnchor? anchor = null;
        int dwellMs = 0;
        foreach (string arg in e.Args)
        {
            if (!arg.StartsWith("--", StringComparison.Ordinal)) { words.Add(arg); continue; }
            anchor ??= HoverAnchor.Parse(arg);
            if (arg.StartsWith("--dwell=", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(arg["--dwell=".Length..], out int d)) dwellMs = d;
        }

        string groupName = string.Join(" ", words);
        string groupDir = Path.Combine(Paths.ConfigPath, groupName);

        if (!File.Exists(Path.Combine(groupDir, "ObjectData.xml")))
        {
            Shutdown();
            return;
        }

        Category category;
        try
        {
            category = new Category(groupDir);
        }
        catch
        {
            Shutdown();
            return;
        }

        // Only once we know we have a flyout to show, so a bad group name never
        // dismisses the one already on screen.
        CloseOtherFlyouts();

        var w = new PopupWindow(category, anchor);

        // The watcher starts us before the dwell is up so that loading and waiting
        // happen at the same time; whatever is left of the wait is seen out here.
        // Loading almost always takes longer, so this usually sleeps for nothing.
        if (anchor is not null && dwellMs > 0)
        {
            int left = dwellMs - (int)SinceStart();
            if (left > 0) System.Threading.Thread.Sleep(Math.Min(left, dwellMs));

            // The cursor moved on while we were loading, so this panel is no longer
            // wanted. Leaving without showing anything is the whole point of being
            // allowed to start early.
            // Checked against the taskbar rather than the icon. The icon rectangle
            // comes from the watcher's cache and can be a moment stale, and a panel
            // that refuses to appear is far more annoying than one that appears and
            // dismisses itself. Off the taskbar entirely, though, is unambiguous.
            if (!Helpers.TaskbarHelper.CursorOnTaskbar())
            {
                Shutdown();
                return;
            }
        }

        w.Show();
    }


    /// <summary>
    /// The warm mode. One flyout is loaded and painted off screen, then shows and
    /// hides itself on request for as long as the watcher keeps it around.
    ///
    /// It has to be loaded with something, since a window with no content cannot be
    /// laid out, so it takes the first group it finds; whatever it is actually asked
    /// for replaces that in about fifty milliseconds.
    /// </summary>
    private void RunWarm()
    {
        Category? seed = FirstGroup();
        if (seed is null)
        {
            Shutdown();
            return;
        }

        var window = new PopupWindow(seed);
        window.Prewarm();

        var stop = new CancellationTokenSource();
        Exit += (_, _) => stop.Cancel();

        // A warm flyout holds a lot of memory and has no window anyone can see, so
        // it must never outlive the watcher that asked for it. The watcher stops it
        // on the way out, but a watcher that is killed outright cannot, and an
        // invisible orphan of 160 MB is the worst thing this feature could leave
        // behind. So it also watches for that itself and goes quietly.
        StartOrphanGuard(window, stop);

        var listener = new Thread(() => FlyoutChannel.Listen(
            message => Dispatcher.Invoke(() => Handle(window, message)), stop.Token))
        {
            IsBackground = true,
            Name = "flyout-channel"
        };
        listener.Start();

        // Only now, with the window painted and the pipe being served, do we say we
        // are ready. Saying it any earlier is what lost the first showing.
        _ready = FlyoutChannel.CreateReady();
        Exit += (_, _) => { try { _ready?.Dispose(); } catch { } };
    }

    private EventWaitHandle? _ready;


    /// <summary>
    /// Ends the warm flyout if its watcher is gone, or if nothing has been asked of
    /// it for a long time. Either means nobody is coming back for it.
    /// </summary>
    private void StartOrphanGuard(PopupWindow window, CancellationTokenSource stop)
    {
        var idle = TimeSpan.FromMinutes(3);
        var lastAsk = DateTime.UtcNow;
        TouchMessage = () => lastAsk = DateTime.UtcNow;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        timer.Tick += (_, _) =>
        {
            bool watcherGone;
            try { watcherGone = Process.GetProcessesByName(HoverService.ProcessName).Length == 0; }
            catch { watcherGone = false; }

            bool forgotten = DateTime.UtcNow - lastAsk > idle;
            if (!watcherGone && !forgotten) return;

            timer.Stop();
            stop.Cancel();
            Shutdown();
        };
        timer.Start();
    }

    private static Action? TouchMessage;

    /// <summary>Carries out one request from the watcher.</summary>
    private void Handle(PopupWindow window, string message)
    {
        TouchMessage?.Invoke();

        if (message == "quit")
        {
            Shutdown();
            return;
        }

        if (message == "hide")
        {
            window.HideForReuse();
            return;
        }

        if (!FlyoutChannel.TryParseShow(message, out string group,
                out int left, out int top, out int right, out int bottom))
            return;

        string dir = Path.Combine(Paths.ConfigPath, group);
        if (!File.Exists(Path.Combine(dir, "ObjectData.xml"))) return;

        try
        {
            window.ShowFor(new Category(dir), new HoverAnchor(left, top, right, bottom));
        }
        catch
        {
            // A group that will not load must not take the warm flyout down with it;
            // the next group asked for may well be fine.
        }
    }

    /// <summary>Any readable group, used only to give the warm window something to lay out.</summary>
    private static Category? FirstGroup()
    {
        try
        {
            foreach (string dir in Directory.GetDirectories(Paths.ConfigPath))
            {
                if (!File.Exists(Path.Combine(dir, "ObjectData.xml"))) continue;
                try { return new Category(dir); } catch { }
            }
        }
        catch { }
        return null;
    }

    // Only one flyout should ever be on screen. Opening a second group used to
    // stack another panel on top of the first, and any flyout that had gone stale
    // stayed there for good. Killing the previous instances is safe: the process
    // owns nothing but the panel, and whatever it launched is already its own
    // process by then.
    private static void CloseOtherFlyouts()
    {
        int self = Environment.ProcessId;
        int warm = WarmProcessId();

        foreach (var p in System.Diagnostics.Process.GetProcessesByName("TaskbarGroups.Background"))
        {
            using (p)
            {
                if (p.Id == self) continue;

                // The warm flyout is not a stale panel, it is the one waiting to be
                // fast. Ask it to put its panel away and leave the process alone,
                // otherwise every click would throw away the head start.
                if (p.Id == warm) { FlyoutChannel.Hide(); continue; }

                try { p.Kill(); } catch { /* already gone, or not ours to kill */ }
            }
        }
    }

    /// <summary>
    /// The warm flyout's process id, or 0 if none is running. Found through its
    /// window title rather than by reading command lines, which needs WMI and is
    /// far too slow to sit in the path of showing a panel.
    /// </summary>
    private static int WarmProcessId()
    {
        try
        {
            IntPtr hwnd = FindWindow(null, PopupWindow.WarmWindowTitle);
            if (hwnd == IntPtr.Zero) return 0;
            GetWindowThreadProcessId(hwnd, out int pid);
            return pid;
        }
        catch { return 0; }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? cls, string? title);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int pid);
}
