using System;
using System.Collections.Generic;
using System.IO;
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

    // Only one flyout should ever be on screen. Opening a second group used to
    // stack another panel on top of the first, and any flyout that had gone stale
    // stayed there for good. Killing the previous instances is safe: the process
    // owns nothing but the panel, and whatever it launched is already its own
    // process by then.
    private static void CloseOtherFlyouts()
    {
        int self = Environment.ProcessId;
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("TaskbarGroups.Background"))
        {
            using (p)
            {
                if (p.Id == self) continue;
                try { p.Kill(); } catch { /* already gone, or not ours to kill */ }
            }
        }
    }
}
