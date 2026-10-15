using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Brings groups over from AppGroup, so moving here does not mean building
    /// every group again. AppGroup keeps them all in one appgroups.json, either in
    /// its data folder or inside a ".agz" backup, which is a zip of that file and
    /// its icons. What maps across: the name, the apps with their arguments and
    /// tooltips, the column count, the header and where it sits, the labels and
    /// the group icon. Anything without an equivalent here is left behind.
    /// </summary>
    public static class AppGroupImport
    {
        private const string ConfigFile = "appgroups.json";

        /// <summary>Where an installed (non-portable) AppGroup keeps its data.</summary>
        public static string InstalledFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppGroup");

        public static bool IsInstalled => File.Exists(Path.Combine(InstalledFolder, ConfigFile));

        /// <summary>Imports the groups of the AppGroup installed on this PC.</summary>
        public static List<string> ImportInstalled()
        {
            string json = File.ReadAllText(Path.Combine(InstalledFolder, ConfigFile));
            return Import(json, iconPath => File.Exists(iconPath) ? iconPath : null);
        }

        /// <summary>Imports the groups inside an AppGroup ".agz" backup.</summary>
        public static List<string> ImportBackup(string backupPath)
        {
            string temp = Path.Combine(Path.GetTempPath(), "tbgf-appgroup-" + Guid.NewGuid().ToString("N"));
            try
            {
                ZipFile.ExtractToDirectory(backupPath, temp);
                string json = File.ReadAllText(Path.Combine(temp, ConfigFile));

                // The icon paths in the file point at the PC the backup came from;
                // the icons themselves travel in the zip, so find them by file name.
                var icons = Directory.GetFiles(temp, "*", SearchOption.AllDirectories)
                    .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                return Import(json, iconPath =>
                    !string.IsNullOrEmpty(iconPath) && icons.TryGetValue(Path.GetFileName(iconPath), out var found) ? found : null);
            }
            finally
            {
                try { Directory.Delete(temp, true); } catch { /* the temp folder is not worth failing over */ }
            }
        }

        private static List<string> Import(string json, Func<string, string> findIcon)
        {
            var imported = new List<string>();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return imported;

            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                try
                {
                    string name = Text(entry.Value, "groupName");
                    var shortcuts = Shortcuts(entry.Value);
                    if (string.IsNullOrWhiteSpace(name) || shortcuts.Count == 0) continue;

                    var category = new Category
                    {
                        Name = GroupTransfer.UniqueName(SafeName(name)),
                        ShortcutList = shortcuts,
                        ShowHeader = Flag(entry.Value, "groupHeader", false),
                        ShowLabels = Flag(entry.Value, "showLabels", false),
                        Width = Math.Clamp(Number(entry.Value, "groupCol"), 0, 8),
                        HeaderPosition = string.Equals(Text(entry.Value, "headerPosition"), "Bottom", StringComparison.OrdinalIgnoreCase)
                            ? PanelLayouts.HeaderBottom : PanelLayouts.HeaderTop
                    };

                    Image icon = LoadIcon(findIcon(Text(entry.Value, "groupIcon")));
                    if (icon is null)
                    {
                        // No icon came with it: build one from its apps, as AppGroup's
                        // own grid icons are.
                        category.GridIcon = true;
                        var images = shortcuts.Take(4).Select(Category.ResolveShortcutImage).ToList();
                        icon = GroupIconGrid.Compose(images);
                        images.ForEach(i => i?.Dispose());
                    }

                    using (icon) category.CreateConfig(icon);
                    imported.Add(category.Name);
                }
                catch { /* one broken group should not stop the rest */ }
            }
            return imported;
        }

        // "path" is an object keyed by the file to open, each with its tooltip and
        // arguments. Shortcuts (.lnk), programs and Steam's .url files all open the
        // same way here, through the shell.
        private static List<ProgramShortcut> Shortcuts(JsonElement group)
        {
            var list = new List<ProgramShortcut>();
            if (!group.TryGetProperty("path", out var paths) || paths.ValueKind != JsonValueKind.Object) return list;

            foreach (var p in paths.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(p.Name)) continue;
                list.Add(new ProgramShortcut
                {
                    FilePath = p.Name,
                    Arguments = Text(p.Value, "args") ?? "",
                    name = Text(p.Value, "tooltip") ?? ""
                });
            }
            return list;
        }

        private static Image LoadIcon(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                if (path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using var ico = new Icon(path, 256, 256);
                    return ico.ToBitmap();
                }
                using var stream = new MemoryStream(File.ReadAllBytes(path));
                using var loaded = Image.FromStream(stream);
                return new Bitmap(loaded);
            }
            catch { return null; }
        }

        // A group name becomes a folder name here, so drop what Windows refuses.
        private static string SafeName(string name)
        {
            var bad = Path.GetInvalidFileNameChars();
            string clean = new string(name.Where(c => !bad.Contains(c)).ToArray()).Trim().TrimEnd('.');
            return clean.Length > 0 ? clean : "AppGroup";
        }

        private static string Text(JsonElement e, string key)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

        private static bool Flag(JsonElement e, string key, bool fallback)
            => e.TryGetProperty(key, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                ? v.GetBoolean() : fallback;

        private static int Number(JsonElement e, string key)
            => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : 0;
    }
}
