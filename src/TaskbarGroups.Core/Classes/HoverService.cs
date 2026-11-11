using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Starts and stops the resident program by the clock (TaskbarGroups.Hover).
    /// It began as the hover-to-open watcher and still carries its name, but since
    /// issue #11 it runs for any of three reasons: opening on hover, keeping a panel
    /// loaded so groups open at once, and showing groups in the notification area.
    /// Whenever one of them changes, three things must stay in step: the choice
    /// saved, the Windows startup entry, and the process running right now.
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
        /// True if anything the user switched on needs the resident program: hover to
        /// open, instant opening, or a group shown in the notification area.
        /// </summary>
        public static bool Wanted()
            => Settings.settingInfo.hoverToOpen
            || Settings.settingInfo.instantOpen
            || TrayGroupNames().Count > 0;

        /// <summary>
        /// The groups ticked to show in the notification area, in the order their
        /// folders are listed. Separators never are: they have no panel to open.
        /// A group that will not load is skipped, as everywhere else.
        /// </summary>
        public static List<string> TrayGroupNames()
        {
            var names = new List<string>();
            try
            {
                foreach (string dir in Directory.GetDirectories(Paths.ConfigPath))
                {
                    if (!File.Exists(Path.Combine(dir, "ObjectData.xml"))) continue;
                    try
                    {
                        var group = new Category(dir);
                        if (group.ShowInTray && !group.IsSeparator) names.Add(group.Name);
                    }
                    catch { /* a broken group has no icon to show */ }
                }
            }
            catch { /* no config folder yet means no groups */ }
            return names;
        }

        /// <summary>
        /// Applies the hover setting end to end. Returns false if the watcher was
        /// meant to start but its executable is missing, which happens when the app
        /// runs from a build tree that has not deployed it yet.
        /// </summary>
        public static bool Apply(bool enabled)
        {
            Settings.settingInfo.hoverToOpen = enabled;
            Settings.writeXML();
            return Sync();
        }

        /// <summary>Applies the instant-open setting end to end, as <see cref="Apply"/> does.</summary>
        public static bool ApplyInstantOpen(bool enabled)
        {
            Settings.settingInfo.instantOpen = enabled;
            Settings.writeXML();
            return Sync();
        }

        /// <summary>
        /// Brings the startup entry and the running process in line with what is
        /// switched on now. A program that is already running is left alone: it reads
        /// the settings and the groups again by itself, and leaves once nothing needs
        /// it, so it is never killed and restarted (that race is what once left the
        /// feature on with nothing running). Returns false only if it was wanted and
        /// could not be started.
        /// </summary>
        public static bool Sync()
        {
            bool wanted = Wanted();
            RegisterStartup(wanted);

            if (!wanted)
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

        /// <summary>
        /// Stops every watcher process and waits for them to actually be gone.
        ///
        /// The waiting is the whole point. Kill only asks; for a few milliseconds
        /// afterwards the process is still listed and still holds the single-instance
        /// mutex. A Stop() followed straight away by a Start() would therefore find
        /// "one is already running", launch nothing, and leave the setting switched on
        /// with nothing watching — which is exactly what changing the delay used to do.
        /// </summary>
        public static void Stop()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(ProcessName))
                    using (p)
                    {
                        try
                        {
                            p.Kill();
                            p.WaitForExit(ExitWaitMs);
                        }
                        catch { /* already gone, or not ours to kill */ }
                    }
            }
            catch { /* never let stopping the watcher take the app down with it */ }
        }

        /// <summary>
        /// Long enough for a process that has been killed to disappear; measured at
        /// about 15ms, so this is generous rather than tight.
        /// </summary>
        private const int ExitWaitMs = 3000;

        /// <summary>
        /// Adds or removes the HKCU startup entry. Per-user only: the installer runs
        /// without elevation, so the machine-wide key is not ours to write.
        /// The Store package uses its StartupTask instead, see <see cref="AppPackage"/>.
        /// </summary>
        public static void RegisterStartup(bool enabled)
        {
            if (AppPackage.IsPackaged)
            {
                SetStartupTask(enabled);
                return;
            }

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

        /// <summary>
        /// Switches the package's StartupTask. It runs on the thread pool because the
        /// WinRT calls are asynchronous and the settings window waits for them on the
        /// UI thread. If the user turned the task off in Task Manager, Windows keeps
        /// it off and only they can turn it back on there; the watcher still starts
        /// now, it just won't come back after a restart.
        /// </summary>
        private static void SetStartupTask(bool enabled)
        {
            try
            {
                System.Threading.Tasks.Task.Run(async () =>
                {
                    var task = await Windows.ApplicationModel.StartupTask.GetAsync(AppPackage.HoverStartupTaskId);
                    if (enabled) await task.RequestEnableAsync();
                    else task.Disable();
                }).Wait(ExitWaitMs);
            }
            catch { /* same as the registry: never let this break the setting */ }
        }
    }
}
