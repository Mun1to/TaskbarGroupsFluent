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
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };

        // Issue #10: a double click opens the app, as it does for most programs that
        // sit by the clock. The menu is built as it opens, so it only offers to turn
        // off what is on, in the current language.
        tray.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) TrayGroups.StartApp();
        };
        // An empty menu starts its Opening with Cancel already set, and it would never
        // show: once it is filled, it is told to open after all.
        tray.ContextMenuStrip.Opening += (_, e) =>
        {
            FillMenu(tray.ContextMenuStrip);
            e.Cancel = false;
        };

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

    private static void FillMenu(ContextMenuStrip menu)
    {
        foreach (ToolStripItem old in menu.Items.Cast<ToolStripItem>().ToList()) old.Dispose();
        menu.Items.Clear();

        var open = new ToolStripMenuItem(TrayText.OpenApp, null, (_, _) => TrayGroups.StartApp());
        open.Font = new Font(open.Font, FontStyle.Bold);
        menu.Items.Add(open);
        menu.Items.Add(new ToolStripSeparator());

        if (Settings.settingInfo.hoverToOpen)
            menu.Items.Add(TrayText.TurnOff, null, (_, _) => TurnOff(hover: true));
        if (Settings.settingInfo.instantOpen)
            menu.Items.Add(TrayText.TurnOffInstant, null, (_, _) => TurnOff(hover: false));

        menu.Items.Add(TrayText.Quit, null, (_, _) => Application.Exit());
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
