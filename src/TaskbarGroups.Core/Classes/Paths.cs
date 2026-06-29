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

        private static string AppDataRelativePath =>
            Path.Combine("Jack Schierbeck", "taskbar-groups");

        public static string ConfigPath = setupConfigPath();
        public static string ShortcutsPath = setupShortcutsPath();

        /// <summary>
        /// Path to the background flyout executable, deployed alongside the main
        /// app under a "Background" subfolder. Pinned shortcuts target this exe.
        /// </summary>
        public static string BackgroundApplication =
            Path.Combine(exeFolder, "Background", "TaskbarGroups.Background.exe");

        private static string setupConfigPath()
        {
            defaultConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppDataRelativePath, "config");

            string directoryPath = Settings.settingInfo.portableMode
                ? Path.Combine(exeFolder, "config")
                : defaultConfigPath;

            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        private static string setupShortcutsPath()
        {
            defaultShortcutsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppDataRelativePath, "Shortcuts");

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
                    AppDataRelativePath, "JITComp");
                Directory.CreateDirectory(directoryPath);
                return directoryPath;
            }
        }

    }
}
