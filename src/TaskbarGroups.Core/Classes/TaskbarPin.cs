using System;
using System.Diagnostics;
using System.IO;
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

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

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
        /// Runs <see cref="RestartExplorer"/> on a thread that keeps this process
        /// alive until the taskbar is back. The editor opened from a pinned group
        /// (the pencil in its panel, or "Edit group" in its right-click menu) closes
        /// the app as soon as it saves; on a pool thread the restart died with the
        /// app, usually after the shell had already exited, and left no taskbar.
        /// </summary>
        public static void RestartExplorerInBackground()
            => new Thread(RestartExplorer) { IsBackground = false, Name = "Restart Explorer" }.Start();

        /// <summary>
        /// Restarts the Windows shell so the taskbar reloads its pinned icons,
        /// using a clean shutdown that preserves the icon cache (a hard kill can
        /// corrupt it and blank out other pinned icons). Only the process that owns
        /// the taskbar is touched: folder windows can run in explorer.exe processes
        /// of their own, and other signed-in users have theirs. Safe from a
        /// background thread; returns once the taskbar is back.
        /// </summary>
        public static void RestartExplorer()
        {
            bool killed = false;
            try
            {
                IntPtr tray = FindWindow("Shell_TrayWnd", null);
                using Process shell = ProcessOf(tray);
                if (shell != null)
                {
                    // Ask the shell to exit cleanly; it flushes its caches first.
                    SendMessageTimeout(tray, WM_EXIT_EXPLORER, IntPtr.Zero, IntPtr.Zero,
                        SMTO_BLOCK, 5000, out _);

                    // Fallback only if the clean exit didn't work: hard-kill the shell.
                    if (!shell.WaitForExit(8000))
                    {
                        try { shell.Kill(); killed = true; } catch { }
                        shell.WaitForExit(3000);
                    }
                }
            }
            catch { /* whatever happened, a shell has to come back below */ }

            BringShellBack(killed);
        }

        // A clean exit doesn't bring the shell back by itself; a kill does, a moment
        // later, so after one Windows gets that moment first. Starting explorer.exe
        // while no shell runs makes it the shell, and no window opens. Each try is
        // checked by the taskbar actually showing up, not by the start succeeding.
        private static void BringShellBack(bool windowsMayRestartIt)
        {
            if (WaitForTaskbar(windowsMayRestartIt ? 3000 : 0)) return;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true })?.Dispose(); }
                catch { }
                if (WaitForTaskbar(15000)) return;
            }
        }

        private static bool WaitForTaskbar(int timeoutMs)
        {
            var clock = Stopwatch.StartNew();
            while (FindWindow("Shell_TrayWnd", null) == IntPtr.Zero)
            {
                if (clock.ElapsedMilliseconds >= timeoutMs) return false;
                Thread.Sleep(100);
            }
            return true;
        }

        // The process that owns a window, or null.
        private static Process ProcessOf(IntPtr window)
        {
            if (window == IntPtr.Zero) return null;
            GetWindowThreadProcessId(window, out uint pid);
            try { return pid == 0 ? null : Process.GetProcessById((int)pid); }
            catch { return null; }
        }
    }
}
