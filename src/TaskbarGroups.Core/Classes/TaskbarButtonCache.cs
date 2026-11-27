#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Remembers where each pinned group's taskbar button sits in physical pixels so
    /// a click that stayed on its button opens at the button's centre right away,
    /// without waiting on UI Automation.
    ///
    /// Any change to the taskbar's handle, size/position, or the pinned shortcuts
    /// folder invalidates the entry; a click whose launch point no longer falls on
    /// the cached button (the cursor slid away, apps shifted the icons, or the
    /// shortcut came from Win+number) misses and triggers a live UIA lookup.
    /// </summary>
    public sealed class TaskbarButtonCache
    {
        private sealed class BarSnapshot
        {
            public Rectangle BarBounds;
            public string PinsStamp = string.Empty;
            public Dictionary<string, Entry> Groups = new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class Entry
        {
            public List<Rectangle> Bounds = new();
            public DateTime CachedAt;
        }

        private readonly object _gate = new();
        private readonly Dictionary<nint, BarSnapshot> _bars = new();
        private readonly TimeSpan _freshLife;
        private readonly Func<DateTime> _now;

        /// <summary>Tolerance around a cached button rectangle when checking a click point.</summary>
        private const int HitSlack = 2;

        public TaskbarButtonCache(TimeSpan freshLife)
            : this(freshLife, () => DateTime.UtcNow) { }

        public TaskbarButtonCache(TimeSpan freshLife, Func<DateTime> now)
        {
            _freshLife = freshLife;
            _now = now;
        }

        /// <summary>Replaces the cached buttons for one taskbar from a full read.</summary>
        public void Store(
            nint barHandle,
            Rectangle barBounds,
            string pinsStamp,
            IReadOnlyList<(string Group, Rectangle Bounds)> buttons)
        {
            if (barHandle == 0 || barBounds.Width <= 0 || barBounds.Height <= 0) return;

            DateTime now = _now();
            var groups = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (var (group, bounds) in buttons)
            {
                if (string.IsNullOrEmpty(group) || bounds.Width <= 0 || bounds.Height <= 0) continue;
                if (!groups.TryGetValue(group, out Entry? entry))
                {
                    entry = new Entry { CachedAt = now };
                    groups[group] = entry;
                }
                entry.Bounds.Add(bounds);
            }

            lock (_gate)
            {
                _bars[barHandle] = new BarSnapshot
                {
                    BarBounds = barBounds,
                    PinsStamp = pinsStamp,
                    Groups = groups
                };
            }
        }

        /// <summary>Updates the cached buttons for a single group on one taskbar.</summary>
        public void StoreGroup(
            nint barHandle,
            Rectangle barBounds,
            string pinsStamp,
            string group,
            IReadOnlyList<Rectangle> bounds)
        {
            if (barHandle == 0 || barBounds.Width <= 0 || barBounds.Height <= 0
                || string.IsNullOrEmpty(group) || bounds.Count == 0)
                return;

            var valid = new List<Rectangle>(bounds.Count);
            foreach (Rectangle r in bounds)
                if (r.Width > 0 && r.Height > 0) valid.Add(r);
            if (valid.Count == 0) return;

            DateTime now = _now();
            lock (_gate)
            {
                if (!_bars.TryGetValue(barHandle, out BarSnapshot? bar)
                    || bar.BarBounds != barBounds
                    || !string.Equals(bar.PinsStamp, pinsStamp, StringComparison.Ordinal))
                {
                    bar = new BarSnapshot { BarBounds = barBounds, PinsStamp = pinsStamp };
                    _bars[barHandle] = bar;
                }
                bar.Groups[group] = new Entry { Bounds = valid, CachedAt = now };
            }
        }

        /// <summary>
        /// True if <paramref name="launch"/> falls on a cached button of <paramref name="group"/>
        /// on <paramref name="barHandle"/>. Sets <paramref name="center"/> to that button's centre
        /// and <paramref name="stale"/> when the entry is older than the fresh window so the
        /// caller can refresh it in the background after the panel is shown.
        /// </summary>
        public bool TryHit(
            nint barHandle,
            Rectangle barBounds,
            string pinsStamp,
            string group,
            Point launch,
            out Point center,
            out bool stale)
        {
            center = default;
            stale = false;
            if (barHandle == 0 || string.IsNullOrEmpty(group)) return false;

            DateTime now = _now();
            lock (_gate)
            {
                if (!_bars.TryGetValue(barHandle, out BarSnapshot? bar)) return false;
                if (bar.BarBounds != barBounds || !string.Equals(bar.PinsStamp, pinsStamp, StringComparison.Ordinal))
                    return false;
                if (!bar.Groups.TryGetValue(group, out Entry? entry) || entry.Bounds.Count == 0)
                    return false;

                Point? best = null;
                long bestDistance = long.MaxValue;
                foreach (Rectangle box in entry.Bounds)
                {
                    if (!Contains(box, launch, HitSlack)) continue;
                    Point c = CenterOf(box);
                    if (!barBounds.Contains(c)) continue;
                    long dx = c.X - launch.X, dy = c.Y - launch.Y, distance = dx * dx + dy * dy;
                    if (distance < bestDistance) { bestDistance = distance; best = c; }
                }

                if (best is not { } found) return false;
                center = found;
                stale = now - entry.CachedAt > _freshLife;
                return true;
            }
        }

        /// <summary>
        /// Picks the button centre inside <paramref name="barBounds"/> nearest to
        /// <paramref name="launch"/>, or null if none lies on that taskbar.
        /// </summary>
        public static Point? NearestCenter(IEnumerable<Rectangle> bounds, Rectangle barBounds, Point launch)
        {
            Point? best = null;
            long bestDistance = long.MaxValue;
            foreach (Rectangle box in bounds)
            {
                if (box.Width <= 0 || box.Height <= 0) continue;
                Point c = CenterOf(box);
                if (c.X < barBounds.Left || c.X >= barBounds.Right || c.Y < barBounds.Top || c.Y >= barBounds.Bottom)
                    continue;
                long dx = c.X - launch.X, dy = c.Y - launch.Y, distance = dx * dx + dy * dy;
                if (distance < bestDistance) { bestDistance = distance; best = c; }
            }
            return best;
        }

        public static Point CenterOf(Rectangle box)
            => new(box.Left + box.Width / 2, box.Top + box.Height / 2);

        private static bool Contains(Rectangle box, Point p, int slack)
            => p.X >= box.Left - slack && p.X < box.Right + slack
            && p.Y >= box.Top - slack && p.Y < box.Bottom + slack;
    }
}
