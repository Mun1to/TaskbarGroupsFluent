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

        new PopupWindow(category).Show();
    }
}
