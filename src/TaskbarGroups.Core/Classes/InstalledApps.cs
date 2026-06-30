using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// A desktop (Win32) program the user already has installed, discovered from
    /// its Start Menu shortcut. Carries the friendly name and the resolved .exe so
    /// the user never has to hunt for the right executable under Program Files.
    /// </summary>
    public class InstalledAppInfo
    {
        public string DisplayName { get; set; } = "";

        // The Start Menu .lnk itself. We launch and draw the icon from this, not
        // from the resolved .exe: the shortcut carries the real logo and the
        // correct launch command even when its target is a stub (e.g. Discord's
        // Update.exe), which a bare .exe would render as a generic icon.
        public string ShortcutPath { get; set; } = "";

        // The executable the shortcut resolves to — shown to the user and used to
        // deduplicate apps that have more than one Start Menu shortcut.
        public string TargetPath { get; set; } = "";
    }

    /// <summary>
    /// Enumerates installed desktop programs by scanning the Start Menu shortcuts
    /// (the same list Windows shows in its app list) and resolving each .lnk to the
    /// executable its installer chose. This is the easy alternative to making the
    /// user browse for the correct .exe by hand.
    /// </summary>
    public static class InstalledApps
    {
        public static List<InstalledAppInfo> EnumerateInstalled()
        {
            // Shell.Application is a classic COM object; resolving shortcuts with it
            // must happen on an STA thread, so run the whole scan on one.
            List<InstalledAppInfo> result = new();
            var staThread = new Thread(() => result = EnumerateCore())
            {
                IsBackground = true
            };
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
            return result;
        }

        private static List<InstalledAppInfo> EnumerateCore()
        {
            // Deduplicate by target executable (case-insensitive).
            var found = new Dictionary<string, InstalledAppInfo>(StringComparer.OrdinalIgnoreCase);

            string[] roots =
            {
                SafeStartMenu(Environment.SpecialFolder.CommonStartMenu),
                SafeStartMenu(Environment.SpecialFolder.StartMenu),
            };

            // WScript.Shell resolves shortcut targets reliably. Shell.Application's
            // GetLink.Path returns "" for many installer-made shortcuts (Brave,
            // Chrome, Edge, Office…), which silently dropped those programs.
            dynamic wsh;
            try { wsh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")); }
            catch { return new List<InstalledAppInfo>(); }

            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;

                IEnumerable<string> lnks;
                try { lnks = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories); }
                catch { continue; }

                foreach (string lnk in lnks)
                {
                    try
                    {
                        string name = Path.GetFileNameWithoutExtension(lnk);
                        if (LooksLikeJunkName(name)) continue;

                        dynamic shortcut = wsh.CreateShortcut(lnk);
                        string target = (shortcut.TargetPath as string) ?? "";
                        if (string.IsNullOrWhiteSpace(target)) continue;
                        if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                        if (IsExecutableJunk(target)) continue;
                        if (!File.Exists(target)) continue;

                        if (!found.ContainsKey(target))
                            found[target] = new InstalledAppInfo
                            {
                                DisplayName = name,
                                ShortcutPath = lnk,
                                TargetPath = target
                            };
                    }
                    catch { /* skip shortcuts we can't read */ }
                }
            }

            return found.Values
                .OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static string SafeStartMenu(Environment.SpecialFolder folder)
        {
            try { return Path.Combine(Environment.GetFolderPath(folder), "Programs"); }
            catch { return ""; }
        }

        // Drop obvious non-app shortcuts by their friendly name.
        private static bool LooksLikeJunkName(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("uninstall") || n.Contains("desinstal");
        }

        // Drop installer stubs and generic uninstallers that slip past the name filter.
        private static bool IsExecutableJunk(string target)
        {
            string file = Path.GetFileName(target).ToLowerInvariant();
            return file.StartsWith("unins") || file == "setup.exe" || file == "installer.exe";
        }
    }
}
