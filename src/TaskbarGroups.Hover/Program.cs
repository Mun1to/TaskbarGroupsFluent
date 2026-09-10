using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using TaskbarGroups.Core;

namespace TaskbarGroups.Hover;

/// <summary>
/// Entry point of the resident hover watcher. It has no window of its own: it
/// lives in the notification area, so a process the user cannot see is still a
/// process the user can find, understand and switch off.
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

        // Launched by hand while the feature is off: honour the setting and leave,
        // rather than quietly running something the user has turned down.
        if (!Settings.settingInfo.hoverToOpen) return;

        ApplicationConfiguration.Initialize();

        using var watcher = new TaskbarWatcher();
        watcher.TurnedOff += Application.Exit;

        using var tray = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = Trim(TrayText.Tooltip),
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        watcher.Start();
        Application.Run();

        tray.Visible = false;
    }

    private static ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(TrayText.OpenApp, null, (_, _) => OpenMainApp());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(TrayText.TurnOff, null, (_, _) => TurnOff());
        menu.Items.Add(TrayText.Quit, null, (_, _) => Application.Exit());

        return menu;
    }

    private static void OpenMainApp()
    {
        try
        {
            string exe = Path.Combine(Paths.exeFolder, "TaskbarGroups.App.exe");
            if (File.Exists(exe))
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch { /* nothing useful to say from a tray menu */ }
    }

    /// <summary>
    /// Turns the feature off for good, not just for this session: without clearing
    /// the setting and the startup entry it would come back on the next sign-in and
    /// look like it ignored the user.
    /// </summary>
    private static void TurnOff()
    {
        try
        {
            Settings.settingInfo.hoverToOpen = false;
            Settings.writeXML();
        }
        catch { /* an unwritable settings file must not trap the user in the feature */ }

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
