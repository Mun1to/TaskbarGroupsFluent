using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// What the hover watcher saw, for when open on hover does nothing on a PC we
    /// can't see.
    ///
    /// Issue #30 came with no more than "not working", and until then the watcher
    /// kept nothing at all: whether it was running, which taskbar it found, whether
    /// any button there was one of our groups, what lay under the cursor. Each of
    /// those points at a different fix, so each leaves a line here, and the user
    /// can paste the file in the issue.
    ///
    /// The watcher looks ten times a second, so a line is only written when what it
    /// says under its topic has changed: resting on one icon writes once, not
    /// every tick. The file sits in the temp folder next to the open times and keeps
    /// the last <see cref="Keep"/> lines. It holds group names, the taskbar's window
    /// classes and button IDs, and nothing else.
    /// </summary>
    public sealed class HoverTrace
    {
        public const int Keep = 300;

        public static string FilePath
            => Path.Combine(Path.GetTempPath(), "TaskbarGroupsFluent-hover.txt");

        private readonly Dictionary<string, string> _last = new(StringComparer.Ordinal);
        private readonly Action<string> _write;
        private readonly Func<DateTime> _now;

        public HoverTrace() : this(line => Append(FilePath, line, Keep), () => DateTime.Now) { }

        public HoverTrace(Action<string> write, Func<DateTime> now)
        {
            _write = write;
            _now = now;
        }

        /// <summary>
        /// Writes <paramref name="text"/> under <paramref name="topic"/>, unless it is
        /// what was last written under that topic. Returns whether it was written.
        /// </summary>
        public bool Note(string topic, string text)
        {
            if (_last.TryGetValue(topic, out string last) && last == text) return false;
            _last[topic] = text;
            Write(topic, text);
            return true;
        }

        /// <summary>Writes a line whatever was written before, for things that happen once each time.</summary>
        public void Event(string topic, string text) => Write(topic, text);

        /// <summary>Lets the next <see cref="Note"/> under <paramref name="topic"/> through even if it repeats.</summary>
        public void Forget(string topic) => _last.Remove(topic);

        private void Write(string topic, string text)
        {
            try
            {
                _write(string.Format(CultureInfo.InvariantCulture,
                    "{0:yyyy-MM-dd HH:mm:ss.fff} {1}: {2}", _now(), topic, text));
            }
            catch { /* a line we could not write changes nothing for the user */ }
        }

        /// <summary>Adds a line to the file at <paramref name="path"/> and keeps only its last <paramref name="keep"/> lines.</summary>
        public static void Append(string path, string line, int keep)
        {
            try
            {
                var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
                lines.Add(line);
                if (lines.Count > keep) lines.RemoveRange(0, lines.Count - keep);
                File.WriteAllLines(path, lines);
            }
            catch { /* the temp folder is not ours to insist on */ }
        }
    }
}
