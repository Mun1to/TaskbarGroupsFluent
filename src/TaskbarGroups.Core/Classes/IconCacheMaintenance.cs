using System.IO;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// One-time icon-cache refresh after an app update that changes how icons are
    /// resolved. Groups store pre-rendered shortcut icons under their config folder;
    /// the flyout reads those, so a group cached by an older build keeps showing the
    /// old (e.g. generic) icon until it's re-saved. Bumping <see cref="CurrentVersion"/>
    /// whenever the icon pipeline changes triggers a single rebuild on next launch.
    /// </summary>
    public static class IconCacheMaintenance
    {
        // Bump this whenever icon resolution changes (AppCatalog, ResolveShortcutImage…).
        //  v2: shell-generic fallback to the executable's PE icon (Obsidian/Brave fix).
        private const int CurrentVersion = 2;

        private static string MarkerPath => Path.Combine(Paths.ConfigPath, ".iconcache-version");

        /// <summary>
        /// Rebuilds every group's icon cache if the last refresh predates the current
        /// icon-pipeline version. Cheap no-op once the marker is up to date. Safe to
        /// call on a background thread at startup; never throws.
        /// </summary>
        public static void RefreshIfStale()
        {
            try
            {
                if (ReadVersion() >= CurrentVersion) return;
                RebuildAll();
                File.WriteAllText(MarkerPath, CurrentVersion.ToString());
            }
            catch { /* cache maintenance must never block or crash startup */ }
        }

        private static int ReadVersion()
        {
            try
            {
                return File.Exists(MarkerPath)
                       && int.TryParse(File.ReadAllText(MarkerPath).Trim(), out int v)
                    ? v : 0;
            }
            catch { return 0; }
        }

        private static void RebuildAll()
        {
            foreach (string dir in Directory.GetDirectories(Paths.ConfigPath))
            {
                if (!File.Exists(Path.Combine(dir, "ObjectData.xml"))) continue;
                try { new Category(dir).cacheIcons(); }
                catch { /* skip a group that can't be read; the rest still refresh */ }
            }
        }
    }
}
