using System;
using System.IO;
using System.Linq;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Safety net for the user's data (groups + icons) around app updates. That data
    /// lives under %APPDATA% — separate from the installed program — so an update
    /// never touches it; this keeps a rolling copy anyway, in case an update (or the
    /// user) ever wipes it. Backups sit next to the config folder under "backups".
    /// </summary>
    public static class ConfigBackup
    {
        private const int KeepCount = 3;

        private static string BackupsRoot =>
            Path.Combine(Path.GetDirectoryName(Paths.ConfigPath), "backups");

        /// <summary>
        /// Copies the whole config folder into a timestamped backup and prunes old
        /// ones. Best-effort: never throws, so it can't block an update.
        /// </summary>
        public static void Create()
        {
            try
            {
                if (!Directory.Exists(Paths.ConfigPath)) return;
                // Nothing to protect if there are no groups yet.
                if (Directory.GetDirectories(Paths.ConfigPath).Length == 0) return;

                Directory.CreateDirectory(BackupsRoot);
                string dest = Path.Combine(BackupsRoot,
                    "config-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                CopyTree(Paths.ConfigPath, dest);
                Prune();
            }
            catch { /* a backup is a safety net, never a blocker */ }
        }

        private static void CopyTree(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (string dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dir.Replace(src, dest));
            foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                File.Copy(file, file.Replace(src, dest), overwrite: true);
        }

        private static void Prune()
        {
            foreach (string old in Directory.GetDirectories(BackupsRoot, "config-*")
                         .OrderByDescending(d => d)
                         .Skip(KeepCount))
            {
                try { Directory.Delete(old, recursive: true); } catch { /* ignore */ }
            }
        }
    }
}
