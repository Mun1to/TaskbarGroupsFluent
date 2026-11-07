#nullable enable annotations
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TaskbarGroups.Core
{
    /// <summary>One icon the pin assistant puts on the taskbar: a group, or a taskbar separator.</summary>
    public sealed class PinStep
    {
        public string GroupName { get; init; } = "";
        public bool IsSeparator { get; init; }

        /// <summary>On the taskbar already when the assistant started; it stays where it was.</summary>
        public bool WasPinned { get; set; }

        /// <summary>On the taskbar now.</summary>
        public bool Pinned { get; set; }

        /// <summary>Passed over by the user.</summary>
        public bool Skipped { get; set; }

        /// <summary>The ID its shortcut carries, which is how a pin of it is recognised.</summary>
        public string AppId => (IsSeparator ? Category.SeparatorAppIdPrefix : Category.AppIdPrefix) + GroupName;

        /// <summary>The shortcut the user pins.</summary>
        public string ShortcutPath => Paths.ShortcutFileFor(GroupName);

        public bool Pending => !Pinned && !Skipped;
    }

    /// <summary>
    /// Pins the groups inside a group to the taskbar, one after another, in its order
    /// and with its separators (issue #8): one group to set out a whole taskbar.
    ///
    /// Windows lets no app pin anything by itself, so each pin is still the user's
    /// right-click. What this does is the rest: it works out what has to go on the
    /// taskbar and in which order, makes a taskbar separator for each line between
    /// the groups, and tells when each one is pinned, by the shortcut Windows
    /// copies into its folder of pins the moment it is. Windows adds every new pin
    /// at the end of the taskbar, so pinning them in order is what keeps the order.
    /// </summary>
    public static class PinPlan
    {
        /// <summary>The groups inside <paramref name="main"/> that still exist, each once.</summary>
        public static List<string> Groups(Category main)
        {
            var names = new List<string>();
            if (main.ShortcutList is null) return names;
            foreach (var ps in main.ShortcutList)
            {
                if (!ps.IsGroup || string.IsNullOrWhiteSpace(ps.GroupName)) continue;
                if (!PinnedGroups.GroupExists(ps.GroupName)) continue;
                if (names.Contains(ps.GroupName, StringComparer.OrdinalIgnoreCase)) continue;
                names.Add(ps.GroupName);
            }
            return names;
        }

        /// <summary>
        /// What goes on the taskbar for <paramref name="main"/>, in order. Each line
        /// in it becomes a taskbar separator: one already made and not on the
        /// taskbar is used first, and new ones are made for the rest, named like
        /// the ones the main window makes. Two lines in a row make one separator.
        /// </summary>
        public static List<PinStep> Build(Category main, string separatorName)
        {
            var steps = new List<PinStep>();
            if (main.ShortcutList is null) return steps;

            HashSet<string> pinned = PinnedGroups.PinnedAppIds();
            var spare = new Queue<string>(SeparatorGroups()
                .Where(n => !pinned.Contains(Category.SeparatorAppIdPrefix + n)));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ps in main.ShortcutList)
            {
                if (ps.IsSeparator)
                {
                    // A line at the very start, or right after another, separates nothing.
                    if (steps.Count == 0 || steps[^1].IsSeparator) continue;
                    steps.Add(new PinStep { GroupName = "", IsSeparator = true });
                    continue;
                }
                if (!ps.IsGroup || !PinnedGroups.GroupExists(ps.GroupName) || !seen.Add(ps.GroupName)) continue;
                steps.Add(new PinStep { GroupName = ps.GroupName });
            }

            // Nor does one at the end.
            while (steps.Count > 0 && steps[^1].IsSeparator) steps.RemoveAt(steps.Count - 1);

            for (int i = 0; i < steps.Count; i++)
            {
                if (!steps[i].IsSeparator) continue;
                string? name = spare.Count > 0 ? spare.Dequeue() : NewSeparator(separatorName);
                // One that could not be made is left out: the groups still go on.
                if (name is null) { steps.RemoveAt(i--); continue; }
                steps[i] = new PinStep { GroupName = name, IsSeparator = true };
            }

            Refresh(steps);
            foreach (var step in steps) step.WasPinned = step.Pinned;
            return steps;
        }

        /// <summary>Marks the steps that are on the taskbar now.</summary>
        public static void Refresh(IEnumerable<PinStep> steps)
        {
            HashSet<string> ids = PinnedGroups.PinnedAppIds();
            List<PinnedGroup> groups = PinnedGroups.Scan();
            foreach (var step in steps)
            {
                if (step.Pinned) continue;
                // A group renamed since it was pinned keeps its old ID on the taskbar,
                // and its pinned shortcut the new name: either one counts.
                step.Pinned = ids.Contains(step.AppId)
                    || (!step.IsSeparator && groups.Exists(g =>
                        string.Equals(g.GroupName, step.GroupName, StringComparison.OrdinalIgnoreCase)));
            }
        }

        /// <summary>The taskbar separators already made, in name order.</summary>
        private static List<string> SeparatorGroups()
        {
            var names = new List<string>();
            try
            {
                foreach (string dir in Directory.GetDirectories(Paths.ConfigPath))
                {
                    if (!File.Exists(Path.Combine(dir, "ObjectData.xml"))) continue;
                    try
                    {
                        var c = new Category(dir);
                        if (c.IsSeparator && File.Exists(Paths.ShortcutFileFor(c.Name))) names.Add(c.Name);
                    }
                    catch { /* a group that will not load is no separator to use */ }
                }
            }
            catch { }
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }

        /// <summary>Makes a taskbar separator under a name no group has yet, or null if it cannot be made.</summary>
        private static string? NewSeparator(string baseName)
        {
            string name = baseName;
            for (int n = 2; Directory.Exists(Path.Combine(Paths.ConfigPath, name)); n++)
                name = baseName + " " + n;
            try
            {
                Category.CreateSeparator(name);
                return name;
            }
            catch { return null; }
        }
    }
}
