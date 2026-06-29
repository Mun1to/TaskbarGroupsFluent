using System;
using System.IO;
using System.Windows;
using TaskbarGroups.Core;

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

        if (e.Args.Length == 0)
        {
            Shutdown();
            return;
        }

        string groupName = e.Args[0];
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
