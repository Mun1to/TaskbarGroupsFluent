using System;
using System.Collections.Generic;
using System.IO;

namespace TaskbarGroups.Core
{
    /// <summary>A group's shortcut as Windows actually stored it on the taskbar.</summary>
    public sealed class PinnedGroup
    {
        /// <summary>File name of the shortcut, without the .lnk extension.</summary>
        public string LinkName { get; init; } = string.Empty;

        /// <summary>The AppUserModelID saved inside the shortcut file.</summary>
        public string AppId { get; init; } = string.Empty;

        /// <summary>The group the shortcut launches, which is the name with a config folder.</summary>
        public string GroupName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Reads the groups currently pinned to the taskbar, straight from the shortcuts
    /// Windows keeps for them.
    ///
    /// This is more roundabout than it looks, because nothing on a taskbar button
    /// carries the group's name. The AppUserModelID comes close, but it gets frozen
    /// twice over: the pin keeps the ID it was filed under, and the shortcut file
    /// keeps whatever it was last written with, so after a rename all three can
    /// disagree. Measured on a real taskbar, a group renamed from "Notas y
    /// Productividad" to "Notes and Productivity" showed the old ID on its button,
    /// the new ID inside its .lnk, and the new name in that .lnk's launch argument.
    ///
    /// The launch argument is the one that is always right, because it is exactly
    /// what the click path passes. So the job here is to find the shortcut a button
    /// belongs to, and <see cref="Resolve"/> tries the ways they can be tied together
    /// strongest first.
    /// </summary>
    public static class PinnedGroups
    {
        /// <summary>Where Windows keeps a shortcut for every taskbar pin.</summary>
        private static string PinFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Internet Explorer", "Quick Launch", "User Pinned", "TaskBar");

        /// <summary>
        /// Every pinned group with a configuration still on disk. Pins can outlive the
        /// group they point at, and opening one of those would only flash a process
        /// that exits immediately.
        /// </summary>
        public static List<PinnedGroup> Scan()
        {
            var found = new List<PinnedGroup>();

            string[] links;
            try
            {
                string folder = PinFolder;
                if (!Directory.Exists(folder)) return found;
                links = Directory.GetFiles(folder, "*.lnk");
            }
            catch { return found; }

            foreach (string link in links)
            {
                string appId, target, arguments;
                try { ShellLink.ReadShortcut(link, out target, out arguments, out appId); }
                catch { continue; }

                if (!IsOurFlyout(target)) continue;

                string group = (arguments ?? string.Empty).Trim().Trim('"');
                if (group.Length == 0 || !GroupExists(group)) continue;

                found.Add(new PinnedGroup
                {
                    LinkName = Path.GetFileNameWithoutExtension(link),
                    AppId = appId ?? string.Empty,
                    GroupName = group
                });
            }

            return found;
        }

        /// <summary>
        /// The group a taskbar button opens, given the AppUserModelID that button
        /// reports, or null if it is not one of ours.
        ///
        /// The four attempts run strongest first. Matching the button's ID against a
        /// shortcut's own ID is exact and covers every group that was never renamed.
        /// The two after it each cover one side of a rename: the shortcut file keeps
        /// its old name while its argument is updated, or the file is renamed while
        /// the argument stays. The last is the button's ID taken at face value, and
        /// it is only accepted if a group by that name really exists, so a stale ID
        /// can never open something the user did not mean.
        /// </summary>
        public static string? Resolve(string buttonAppId, IReadOnlyList<PinnedGroup> pins)
        {
            if (string.IsNullOrEmpty(buttonAppId)) return null;
            if (!buttonAppId.StartsWith(Category.AppIdPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            string suffix = buttonAppId[Category.AppIdPrefix.Length..].Trim();

            foreach (var p in pins)
            {
                if (string.Equals(p.AppId, buttonAppId, StringComparison.OrdinalIgnoreCase))
                    return p.GroupName;
            }

            foreach (var p in pins)
            {
                if (string.Equals(p.GroupName, suffix, StringComparison.OrdinalIgnoreCase))
                    return p.GroupName;
            }

            foreach (var p in pins)
            {
                if (string.Equals(p.LinkName, suffix, StringComparison.OrdinalIgnoreCase))
                    return p.GroupName;
            }

            return suffix.Length > 0 && GroupExists(suffix) ? suffix : null;
        }

        private static bool IsOurFlyout(string target)
        {
            if (string.IsNullOrEmpty(target)) return false;
            try
            {
                return string.Equals(Path.GetFileName(target),
                    "TaskbarGroups.Background.exe", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>True if the group still has a readable configuration on disk.</summary>
        public static bool GroupExists(string groupName)
        {
            try
            {
                return File.Exists(Path.Combine(Paths.ConfigPath, groupName, "ObjectData.xml"));
            }
            catch { return false; }
        }
    }
}
