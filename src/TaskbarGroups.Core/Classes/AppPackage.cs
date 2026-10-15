using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Whether this copy runs from the Microsoft Store package (MSIX) or from the
    /// classic installer, and the few things that have to differ between the two.
    /// It is decided at run time by asking Windows whether the process has a package
    /// identity, so one build serves both and the two can never drift apart.
    ///
    /// Inside a package three things that the installer build relies on stop working
    /// without any error:
    /// - New folders written straight into %APPDATA% or %LOCALAPPDATA% are redirected
    ///   to a private copy only the package sees. The app reads its groups back fine,
    ///   but Explorer and the taskbar look at the real folder and find no shortcut to
    ///   pin and no icon to show. So packaged data lives in the package's own data
    ///   folder, written to by its real path, which everyone can see.
    /// - The install folder changes with every update, so a pinned shortcut aimed at
    ///   the real exe would break on the next one. Shortcuts aim at the app execution
    ///   aliases declared in the package manifest, which stay put.
    /// - The registry's Run key is virtualized too: it is written fine and Windows
    ///   never reads it. Starting with Windows goes through a StartupTask instead.
    /// </summary>
    public static class AppPackage
    {
        /// <summary>The package family name, or null outside a package.</summary>
        public static readonly string FamilyName = ReadFamilyName();

        public static bool IsPackaged => FamilyName != null;

        /// <summary>
        /// The folder every Taskbar Groups fork has kept its groups in, so they carry
        /// over between them. The Store package reads it once, to import them.
        /// </summary>
        public static readonly string ClassicDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Jack Schierbeck", "taskbar-groups");

        /// <summary>
        /// Where the user's groups and settings live: the classic folder, or inside
        /// the package its LocalState, which Windows removes on uninstall.
        /// </summary>
        public static readonly string DataRoot = IsPackaged
            ? Path.Combine(LocalAppData, "Packages", FamilyName, "LocalState")
            : ClassicDataRoot;

        /// <summary>Alias of the main app, as declared in AppxManifest.xml.</summary>
        public static string AppAlias => Path.Combine(AliasFolder, "TaskbarGroupsFluent.exe");

        /// <summary>Alias of the flyout, as declared in AppxManifest.xml.</summary>
        public static string PanelAlias => Path.Combine(AliasFolder, "TaskbarGroupsFluentPanel.exe");

        /// <summary>TaskId of the hover watcher's StartupTask in AppxManifest.xml.</summary>
        public const string HoverStartupTaskId = "TaskbarGroupsFluentHover";

        private static string LocalAppData =>
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        private static string AliasFolder => Path.Combine(LocalAppData, "Microsoft", "WindowsApps");

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFamilyName(ref uint packageFamilyNameLength, StringBuilder packageFamilyName);

        private const int ERROR_INSUFFICIENT_BUFFER = 122;

        private static string ReadFamilyName()
        {
            try
            {
                // Outside a package the first call answers APPMODEL_ERROR_NO_PACKAGE;
                // inside, it asks for a buffer, which is the only success it can give.
                uint length = 0;
                if (GetCurrentPackageFamilyName(ref length, null) != ERROR_INSUFFICIENT_BUFFER) return null;

                var name = new StringBuilder((int)length);
                return GetCurrentPackageFamilyName(ref length, name) == 0 ? name.ToString() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
