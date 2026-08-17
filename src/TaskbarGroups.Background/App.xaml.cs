using System;
using System.IO;
using System.Windows;
using TaskbarGroups.Core;
using Wpf.Ui.Appearance;

namespace TaskbarGroups.Background;

/// <summary>
/// Background client entry point. Launched (by a pinned taskbar shortcut) with
/// the group name as its argument; shows that group's flyout above the taskbar
/// and exits when the flyout closes.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Match the system light/dark theme instead of the fixed "Dark" in App.xaml.
        ApplicationThemeManager.ApplySystemTheme();

        if (e.Args.Length == 0)
        {
            Shutdown();
            return;
        }

        // The pinned shortcut passes the group name unquoted, so a name with
        // spaces arrives split across several args. Rejoin to rebuild it.
        string groupName = string.Join(" ", e.Args);
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

        new PopupWindow(category).Show();
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
