using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Helpers for keeping a group's pinned-taskbar button in sync after editing.
    /// Windows pins a *copy* of the .lnk and caches its icon aggressively, so
    /// changing the original isn't reflected until the pinned copy is rewritten
    /// and the shell reloads. <see cref="RestartExplorer"/> forces that reload.
    /// </summary>
    public static class TaskbarPin
    {
        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam,
            IntPtr lParam, uint flags, uint timeout, out IntPtr result);

        private const int SHCNE_ASSOCCHANGED = 0x08000000;

        // Undocumented message the "Exit Explorer" menu posts to the taskbar window.
        // It shuts the shell down cleanly (flushing the icon cache) instead of a
        // hard kill, which can corrupt the cache and blank out other pinned icons.
        private const uint WM_EXIT_EXPLORER = 0x5B4; // WM_USER + 436
        private const uint SMTO_BLOCK = 0x0001;

        private static string PinnedDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");

        /// <summary>
        /// Finds the pinned-taskbar shortcut for a group (matched by its background
        /// target and the group-name argument), or null if the group isn't pinned.
        /// Must run on an STA thread — it uses the Shell.Application COM object.
        /// </summary>
        public static string FindPinnedShortcut(string groupName, string backgroundExe)
        {
            string dir = PinnedDir;
            if (!Directory.Exists(dir)) return null;

            dynamic shell;
            try { shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")); }
            catch { return null; }

            dynamic folder = shell.NameSpace(dir);
            if (folder == null) return null;

            string bgName = Path.GetFileName(backgroundExe);

            foreach (string lnk in Directory.EnumerateFiles(dir, "*.lnk"))
            {
                try
                {
                    dynamic item = folder.ParseName(Path.GetFileName(lnk));
                    if (item == null) continue;

                    dynamic link = item.GetLink;
                    string target = link.Path as string ?? "";
                    string args = link.Arguments as string ?? "";

                    if (!string.Equals(Path.GetFileName(target), bgName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string argName = args.Replace("\"", "").Trim();
                    if (string.Equals(argName, groupName, StringComparison.OrdinalIgnoreCase))
                        return lnk;
                }
                catch { /* skip shortcuts we can't read */ }
            }

            return null;
        }

        /// <summary>
        /// Overwrites the pinned copy with the freshly built shortcut (new icon /
        /// target) and notifies the shell of the change.
        /// </summary>
        public static void UpdatePinnedShortcut(string pinnedLnk, string freshLnk)
        {
            try
            {
                if (File.Exists(freshLnk) && File.Exists(pinnedLnk))
                    File.Copy(freshLnk, pinnedLnk, true);
                SHChangeNotify(SHCNE_ASSOCCHANGED, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch { /* best effort */ }
        }

        /// <summary>
        /// Restarts the Windows shell so the taskbar reloads its pinned icons,
        /// using a clean shutdown that preserves the icon cache (a hard kill can
        /// corrupt it and blank out other pinned icons). Safe from a background
        /// thread.
        /// </summary>
        public static void RestartExplorer()
        {
            bool exited = false;
            try
            {
                IntPtr tray = FindWindow("Shell_TrayWnd", null);
                if (tray != IntPtr.Zero)
                {
                    // Ask the shell to exit cleanly; it flushes its caches first.
                    SendMessageTimeout(tray, WM_EXIT_EXPLORER, IntPtr.Zero, IntPtr.Zero,
                        SMTO_BLOCK, 5000, out _);

                    for (int i = 0; i < 40 && Process.GetProcessesByName("explorer").Any(); i++)
                        Thread.Sleep(150);
                    exited = !Process.GetProcessesByName("explorer").Any();
                }
            }
            catch { /* fall through to the hard fallback */ }

            // Fallback only if the clean exit didn't work: hard-kill the shell.
            if (!exited)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName("explorer"))
                        try { p.Kill(); } catch { }
                    Thread.Sleep(400);
                }
                catch { }
            }

            // A clean shell exit does NOT auto-respawn, so relaunch it. (Starting
            // explorer.exe with no shell running becomes the shell; no window opens.)
            try
            {
                if (!Process.GetProcessesByName("explorer").Any())
                    Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
            }
            catch { }
        }
    }
}
