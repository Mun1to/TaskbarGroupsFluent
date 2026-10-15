using System;
using System.IO;
using System.Reflection;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Centralised access to the application's folders and key file paths.
    /// Pure path logic only — deploying the background executable and creating
    /// editor shortcuts is the responsibility of the host app (App.Bootstrapper),
    /// keeping the Core free of embedded resources and UI concerns.
    /// </summary>
    public static class Paths
    {
        /// <summary>The full path to the currently executing assembly.</summary>
        public static string exeString = Assembly.GetEntryAssembly()?.Location
                                         ?? Assembly.GetExecutingAssembly().Location;

        /// <summary>The folder in which the host executable resides.</summary>
        public static string exeFolder = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>The folder of the executing assembly.</summary>
        public static string path = Path.GetDirectoryName(exeString);

        public static string defaultConfigPath;
        public static string defaultShortcutsPath;
        public static string defaultBackgroundPath;

        public static string ConfigPath = setupConfigPath();
        public static string ShortcutsPath = setupShortcutsPath();

        /// <summary>
        /// The .lnk file for a group inside <see cref="ShortcutsPath"/>. The "_" to
        /// space substitution mirrors how the shortcut is named when it is created.
        /// </summary>
        public static string ShortcutFileFor(string groupName)
            => Path.Combine(ShortcutsPath,
                System.Text.RegularExpressions.Regex.Replace(groupName, @"(_)+", " ") + ".lnk");

        /// <summary>
        /// Path to the background flyout executable. The installer deploys it under
        /// a "Background" subfolder; the Store package keeps all three programs in
        /// one folder, because each carries its own copy of .NET and side by side
        /// they share it instead of doubling the download.
        /// </summary>
        public static string BackgroundApplication = FindBackgroundApplication();

        private static string FindBackgroundApplication()
        {
            string inSubfolder = Path.Combine(exeFolder, "Background", "TaskbarGroups.Background.exe");
            string beside = Path.Combine(exeFolder, "TaskbarGroups.Background.exe");
            return !File.Exists(inSubfolder) && File.Exists(beside) ? beside : inSubfolder;
        }

        /// <summary>
        /// Path to the resident hover watcher, deployed alongside the main app.
        /// It is the only part of the product that keeps running on its own, and
        /// only while the hover-to-open setting is on.
        /// </summary>
        public static string HoverApplication =
            Path.Combine(exeFolder, "TaskbarGroups.Hover.exe");

        /// <summary>
        /// Path to the main app, from whichever piece is asking: the app and the
        /// watcher sit next to it, while the flyout lives one folder down in
        /// "Background" and has to look in its parent.
        /// </summary>
        public static string MainApplication
        {
            get
            {
                string here = Path.Combine(exeFolder, "TaskbarGroups.App.exe");
                return File.Exists(here)
                    ? here
                    : Path.GetFullPath(Path.Combine(exeFolder, "..", "TaskbarGroups.App.exe"));
            }
        }

        /// <summary>
        /// What a pinned group's shortcut starts: the flyout itself, or inside the
        /// Store package its execution alias, since the real path changes with
        /// every update and would leave the pin pointing at nothing.
        /// </summary>
        public static string PanelLauncher =>
            AppPackage.IsPackaged ? AppPackage.PanelAlias : BackgroundApplication;

        /// <summary>The same for the main app, which "Edit group" starts.</summary>
        public static string AppLauncher =>
            AppPackage.IsPackaged ? AppPackage.AppAlias : MainApplication;

        private static string setupConfigPath()
        {
            defaultConfigPath = Path.Combine(AppPackage.DataRoot, "config");

            string directoryPath = Settings.settingInfo.portableMode
                ? Path.Combine(exeFolder, "config")
                : defaultConfigPath;

            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        private static string setupShortcutsPath()
        {
            defaultShortcutsPath = Path.Combine(AppPackage.DataRoot, "Shortcuts");

            string directoryPath = Settings.settingInfo.portableMode
                ? Path.Combine(exeFolder, "Shortcuts")
                : defaultShortcutsPath;

            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        public static string OptimizationProfilePath
        {
            get
            {
                string directoryPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Jack Schierbeck", "taskbar-groups", "JITComp");
                Directory.CreateDirectory(directoryPath);
                return directoryPath;
            }
        }

    }
}
