using System;
using System.Diagnostics;
using System.IO;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Turns the hover-to-open watcher on and off. The watcher is a separate,
    /// resident process, so switching the setting has to do three things that must
    /// stay in step: persist the choice, register or clear the Windows startup
    /// entry, and start or stop the process that is running right now.
    /// </summary>
    public static class HoverService
    {
        /// <summary>Process name of the watcher, without the extension.</summary>
        public const string ProcessName = "TaskbarGroups.Hover";

        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "TaskbarGroupsFluentHover";

        /// <summary>True if at least one watcher process is alive.</summary>
        public static bool IsRunning()
        {
            try { return Process.GetProcessesByName(ProcessName).Length > 0; }
            catch { return false; }
        }

        /// <summary>
        /// Applies the setting end to end. Returns false if the watcher was meant to
        /// start but its executable is missing, which happens when the app runs from
        /// a build tree that has not deployed it yet.
        /// </summary>
        public static bool Apply(bool enabled)
        {
            Settings.settingInfo.hoverToOpen = enabled;
            Settings.writeXML();

            RegisterStartup(enabled);

            if (!enabled)
            {
                Stop();
                return true;
            }

            return Start();
        }

        /// <summary>Launches the watcher unless one is already running.</summary>
        public static bool Start()
        {
            if (IsRunning()) return true;
            if (!File.Exists(Paths.HoverApplication)) return false;

            try
            {
                Process.Start(new ProcessStartInfo(Paths.HoverApplication)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(Paths.HoverApplication) ?? string.Empty
                });
                return true;
            }
            catch { return false; }
        }

        /// <summary>Stops every watcher process. Safe to call when none is running.</summary>
        public static void Stop()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(ProcessName))
                    using (p)
                    {
                        try { p.Kill(); } catch { /* already gone, or not ours to kill */ }
                    }
            }
            catch { /* never let stopping the watcher take the app down with it */ }
        }

        /// <summary>
        /// Adds or removes the HKCU startup entry. Per-user only: the installer runs
        /// without elevation, so the machine-wide key is not ours to write.
        /// </summary>
        public static void RegisterStartup(bool enabled)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (key == null) return;

                if (enabled)
                    key.SetValue(RunValue, "\"" + Paths.HoverApplication + "\"");
                else
                    key.DeleteValue(RunValue, throwOnMissingValue: false);
            }
            catch { /* a locked-down registry must not break the setting */ }
        }
    }
}
