using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Moves groups in and out: a group exported to a single file, the same file
    /// brought back on another PC, a copy of an existing group, and the groups of
    /// the original Taskbar Groups, which keeps them in the very same format.
    ///
    /// Every way in ends at <see cref="ImportFolder"/>, which rebuilds the group
    /// through <see cref="Category.CreateConfig"/> rather than copying files over.
    /// The shortcut and icon a group carries point at the program files of the PC
    /// it was saved on, so they have to be made again on this one.
    /// </summary>
    public static class GroupTransfer
    {
        /// <summary>The extension of an exported group: a zip of its folder.</summary>
        public const string Extension = ".tbgroup";

        /// <summary>Writes the group's folder to a single file at <paramref name="filePath"/>.</summary>
        public static void Export(string groupName, string filePath)
        {
            string dir = Path.Combine(Paths.ConfigPath, groupName);
            if (File.Exists(filePath)) File.Delete(filePath);
            ZipFile.CreateFromDirectory(dir, filePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }

        /// <summary>Brings an exported group in, and returns the name it ended up with.</summary>
        public static string ImportFile(string filePath)
        {
            string temp = Path.Combine(Path.GetTempPath(), "tbgf-import-" + Guid.NewGuid().ToString("N"));
            try
            {
                ZipFile.ExtractToDirectory(filePath, temp);
                return ImportFolder(temp);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { /* the temp folder is not worth failing over */ }
            }
        }

        /// <summary>A copy of an existing group under a free name built from <paramref name="suffix"/>.</summary>
        public static string Duplicate(string groupName, string suffix)
            => ImportFolder(Path.Combine(Paths.ConfigPath, groupName), groupName + suffix);

        /// <summary>
        /// Every group found under <paramref name="root"/>, the folder where an old
        /// Taskbar Groups lived. It accepts that folder, its "config" folder or a
        /// folder one level above, since people rarely remember which one it was.
        /// </summary>
        public static List<string> ImportTaskbarGroups(string root)
        {
            var imported = new List<string>();
            foreach (string groupDir in FindGroupFolders(root))
            {
                try { imported.Add(ImportFolder(groupDir)); }
                catch { /* one broken group should not stop the rest */ }
            }
            return imported;
        }

        /// <summary>
        /// The first time the Store package runs on a PC that had the classic
        /// installer, brings that copy's groups over, so moving to the Store does not
        /// mean starting again. Only once, marked by a file: deleting every group
        /// later must not make them come back.
        /// </summary>
        public static void ImportClassicInstallOnce()
        {
            if (!AppPackage.IsPackaged) return;

            string marker = Path.Combine(AppPackage.DataRoot, ".classic-imported");
            if (File.Exists(marker)) return;

            try { ImportTaskbarGroups(AppPackage.ClassicDataRoot); }
            catch { /* nothing to import, or nothing readable: start empty */ }

            try { File.WriteAllText(marker, DateTime.Now.ToString("o")); }
            catch { /* worst case it tries again next start and finds the names taken */ }
        }

        /// <summary>
        /// Rebuilds the group stored in <paramref name="groupDir"/> into this PC's
        /// config, under <paramref name="preferredName"/> or its own name, made
        /// unique if a group with that name already exists.
        /// </summary>
        public static string ImportFolder(string groupDir, string preferredName = null)
        {
            var category = new Category(groupDir);
            category.Name = UniqueName(string.IsNullOrWhiteSpace(preferredName) ? category.Name : preferredName);

            using Image image = LoadGroupImage(groupDir) ?? Category.GetErrorImage();
            category.CreateConfig(image);
            return category.Name;
        }

        private static IEnumerable<string> FindGroupFolders(string root)
        {
            if (!Directory.Exists(root)) yield break;

            var candidates = new List<string> { root, Path.Combine(root, "config") };
            foreach (string sub in SafeDirectories(root))
                candidates.Add(Path.Combine(sub, "config"));

            foreach (string candidate in candidates.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var groups = SafeDirectories(candidate)
                    .Where(d => File.Exists(Path.Combine(d, "ObjectData.xml")))
                    .ToList();
                if (groups.Count == 0) continue;
                foreach (string g in groups) yield return g;
                yield break;
            }
        }

        private static IEnumerable<string> SafeDirectories(string dir)
        {
            try { return Directory.GetDirectories(dir); }
            catch { return Array.Empty<string>(); }
        }

        // Read into memory so the file is not left locked while the group's own
        // GroupImage.png is rewritten, which happens when a group is duplicated.
        private static Image LoadGroupImage(string groupDir)
        {
            string png = Path.Combine(groupDir, "GroupImage.png");
            if (!File.Exists(png)) return null;
            try
            {
                using var stream = new MemoryStream(File.ReadAllBytes(png));
                using var loaded = Image.FromStream(stream);
                return new Bitmap(loaded);
            }
            catch { return null; }
        }

        /// <summary>The name itself if free, else "name (2)", "name (3)" and so on.</summary>
        internal static string UniqueName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) name = "Group";
            if (!Directory.Exists(Path.Combine(Paths.ConfigPath, name))) return name;

            for (int n = 2; ; n++)
            {
                string candidate = $"{name} ({n})";
                if (!Directory.Exists(Path.Combine(Paths.ConfigPath, candidate))) return candidate;
            }
        }
    }
}
