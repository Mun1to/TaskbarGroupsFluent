using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// How long the last clicks on a group took to show its panel, split into the
    /// part Windows spends starting our process and the part we spend after that.
    ///
    /// Issue #17: a group opened in about three seconds after a few minutes without
    /// use, on a PC we can't see. Which half those seconds are in decides the fix,
    /// so each click leaves one line here, and the user can paste the file in the
    /// issue. It holds the time of day and durations only, in the temp folder, and keeps
    /// the last <see cref="Keep"/> lines.
    /// </summary>
    public static class OpenTimes
    {
        private const int Keep = 40;

        public static string FilePath
            => Path.Combine(Path.GetTempPath(), "TaskbarGroupsFluent-open-times.txt");

        /// <summary>Milliseconds from Windows creating this process to now.</summary>
        public static double SinceProcessStart()
        {
            try { return (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMilliseconds; }
            catch { return -1; }
        }

        /// <summary>
        /// Notes one click: how it was served ("warm" by the panel kept loaded, or
        /// "cold" by starting one), how long Windows took to start this process
        /// (<paramref name="startMs"/>) and how long it took in all.
        /// </summary>
        public static void Note(string how, double startMs, double totalMs)
        {
            try
            {
                string line = string.Format(CultureInfo.InvariantCulture,
                    "{0:yyyy-MM-dd HH:mm:ss} {1} start={2:0}ms total={3:0}ms",
                    DateTime.Now, how, startMs, totalMs);
                string path = FilePath;
                var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new System.Collections.Generic.List<string>();
                lines.Add(line);
                if (lines.Count > Keep) lines.RemoveRange(0, lines.Count - Keep);
                File.WriteAllLines(path, lines);
            }
            catch { /* a time we could not note changes nothing for the user */ }
        }
    }
}
