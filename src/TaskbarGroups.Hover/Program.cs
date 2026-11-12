using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using TaskbarGroups.Core;

namespace TaskbarGroups.Hover;

/// <summary>
/// Entry point of the resident program by the clock. It began as the hover
/// watcher; since issue #11 it also shows the groups ticked to sit in the
/// notification area and can keep a panel loaded for good, and it runs while any
/// of the three is on. It has no window of its own: it lives in the notification
/// area, so a process the user cannot see is still a process the user can find,
/// understand and switch off.
/// </summary>
internal static class Program
{
    private const string MutexName = @"Local\TaskbarGroupsFluent.Hover";

    [STAThread]
    private static void Main()
    {
        // Before anything reads a coordinate. UI Automation reports physical pixels
        // and so does the cursor, but only if nobody is virtualising them for us.
        try { Native.SetProcessDpiAwarenessContext(Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
        catch { /* pre-1703 Windows: the manifest default has to do */ }

        // One watcher is enough, and a second one would open every group twice.
        using var single = new Mutex(initiallyOwned: true, MutexName, out bool mine);
        if (!mine) return;

        // Launched by hand, or by a startup entry left behind, with everything it
        // runs for switched off: honour that and leave, rather than quietly running
        // something the user has turned down. The entry itself is the app's to
        // clear: a copy started from somewhere else must never delete it.
        if (!HoverService.Wanted()) return;

        ApplicationConfiguration.Initialize();

        using var watcher = new TaskbarWatcher();
        watcher.TurnedOff += Application.Exit;

        using var tray = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = Trim(TrayText.Tooltip),
            Visible = !Settings.settingInfo.hideTrayIcon
        };

        // Issue #14: the icon can be hidden, from its own menu or the app's settings,
        // and brought back from the app's settings without restarting anything.
        watcher.SettingsChanged += () => tray.Visible = !Settings.settingInfo.hideTrayIcon;

        // Issue #10: a double click opens the app, as it does for most programs that
        // sit by the clock. The menu is built as it opens, so it only offers to turn
        // off what is on, in the current language.
        tray.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) TrayGroups.StartApp();
        };
        NativeMenu.Attach(tray, FillMenu);

        using var groups = new TrayGroups();
        groups.Emptied += () =>
        {
            if (!HoverService.Wanted()) Application.Exit();
        };

        groups.Start();
        watcher.Start();
        Application.Run();

        tray.Visible = false;
    }

    private static void FillMenu(NativeMenu menu)
    {
        menu.Add(TrayText.OpenApp, () => TrayGroups.StartApp(), isDefault: true);
        menu.Separator();

        if (Settings.settingInfo.hoverToOpen)
            menu.Add(TrayText.TurnOff, () => TurnOff(hover: true));
        if (Settings.settingInfo.instantOpen)
            menu.Add(TrayText.TurnOffInstant, () => TurnOff(hover: false));

        menu.Add(TrayText.HideIcon, HideIcon);
        menu.Add(TrayText.Quit, Application.Exit);
    }

    /// <summary>
    /// Hides this icon for good, as the switch in the app's settings does; the
    /// watcher sees the settings file change and puts the icon away.
    /// </summary>
    private static void HideIcon()
    {
        try
        {
            Settings.settingInfo.hideTrayIcon = true;
            Settings.writeXML();
        }
        catch { /* an unwritable settings file leaves the icon where it is */ }
    }

    /// <summary>
    /// Turns a feature off for good, not just for this session: without clearing
    /// the setting, and the startup entry once nothing else needs this program, it
    /// would come back on the next sign-in and look like it ignored the user. The
    /// program stays for whatever else is still on.
    /// </summary>
    private static void TurnOff(bool hover)
    {
        try
        {
            if (hover) Settings.settingInfo.hoverToOpen = false;
            else Settings.settingInfo.instantOpen = false;
            Settings.writeXML();
        }
        catch { /* an unwritable settings file must not trap the user in the feature */ }

        if (HoverService.Wanted()) return;

        HoverService.RegisterStartup(false);
        Application.Exit();
    }

    private static Icon LoadIcon()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            string? name = Array.Find(asm.GetManifestResourceNames(),
                n => n.EndsWith("Icon.ico", StringComparison.OrdinalIgnoreCase));

            if (name is not null)
            {
                using Stream? s = asm.GetManifestResourceStream(name);
                if (s is not null) return new Icon(s);
            }
        }
        catch { /* fall through to the stock icon */ }

        return SystemIcons.Application;
    }

    /// <summary>NotifyIcon.Text throws above 63 characters, and translations grow.</summary>
    private static string Trim(string text)
        => text.Length <= 63 ? text : text[..63];
}
