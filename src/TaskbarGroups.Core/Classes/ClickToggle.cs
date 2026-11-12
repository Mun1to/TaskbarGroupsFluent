using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Lets a second click on a group's taskbar button close its panel, as a second
    /// click on Start closes the Start menu (issue #17).
    ///
    /// Without it, that click did two things in a row: pressing the button took the
    /// focus from the panel, which closed it, and releasing it launched the group
    /// again, which opened it right back. So the panel notes here why it closed, and
    /// the click that follows asks before opening anything.
    ///
    /// A file, because the click arrives in a new process, and the panel it closed
    /// may already be gone. It holds one line and is read once per click.
    /// </summary>
    public static class ClickToggle
    {
        /// <summary>
        /// How long after the panel closed the launch still counts as the same click.
        /// Starting the process can take a while on a slow PC, and the cursor test
        /// below is what keeps a later click from being swallowed.
        /// </summary>
        private const int WindowMs = 3000;

        /// <summary>How far the cursor may be from where the panel closed, in pixels.</summary>
        private const int Slack = 30;

        private static string FilePath
            => Path.Combine(Path.GetTempPath(), "TaskbarGroupsFluent-closed-by-click.txt");

        /// <summary>
        /// Called by a panel that is closing because the user pressed the mouse on
        /// the taskbar. Notes the group and where the cursor is.
        /// </summary>
        public static void Remember(string group)
        {
            try
            {
                if (string.IsNullOrEmpty(group) || !GetCursorPos(out POINT p)) return;
                File.WriteAllText(FilePath, string.Join("|",
                    DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture),
                    p.X.ToString(CultureInfo.InvariantCulture),
                    p.Y.ToString(CultureInfo.InvariantCulture),
                    group));
            }
            catch { /* without the note, the click just opens the panel again */ }
        }

        /// <summary>
        /// Whether a click on <paramref name="group"/> is the one that just closed
        /// its panel, in which case nothing should open. The note is used up either
        /// way, so it can only ever swallow one click.
        /// </summary>
        public static bool JustClosed(string group)
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return false;
                string[] parts = File.ReadAllText(path).Split(new[] { '|' }, 4);
                File.Delete(path);
                if (parts.Length != 4) return false;

                long ticks = long.Parse(parts[0], CultureInfo.InvariantCulture);
                int x = int.Parse(parts[1], CultureInfo.InvariantCulture);
                int y = int.Parse(parts[2], CultureInfo.InvariantCulture);

                double ago = (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalMilliseconds;
                if (ago < 0 || ago > WindowMs) return false;
                if (!string.Equals(parts[3], group, StringComparison.OrdinalIgnoreCase)) return false;
                if (!GetCursorPos(out POINT p)) return false;
                return Math.Abs(p.X - x) <= Slack && Math.Abs(p.Y - y) <= Slack;
            }
            catch { return false; }
        }

        /// <summary>Whether the cursor is on a taskbar, the main one or another screen's.</summary>
        public static bool CursorOnTaskbar()
        {
            try
            {
                if (!GetCursorPos(out POINT p)) return false;
                IntPtr root = GetAncestor(WindowFromPoint(p), GA_ROOT);
                if (root == IntPtr.Zero) return false;
                var name = new System.Text.StringBuilder(64);
                GetClassName(root, name, name.Capacity);
                string cls = name.ToString();
                return cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd";
            }
            catch { return false; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        private const uint GA_ROOT = 2;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT p);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT p);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int max);
    }
}
