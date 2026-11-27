#nullable enable

using System;
using System.Collections.Generic;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// The pinned groups, read again only when they may have changed.
    ///
    /// Reading them means opening every shortcut on the taskbar, far too much for
    /// each look at the taskbar, so a reading is kept. It used to be kept for thirty
    /// seconds whatever happened, and that is issue #28: dragging a group onto the
    /// taskbar takes the cursor over the taskbar, which took a reading a moment
    /// before the drop, so the group just pinned stayed unknown for half a minute.
    /// On a taskbar where a group is told by its icon (StartAllBack, Start11) an
    /// unknown group cannot be found at all, and resting on it opened nothing.
    ///
    /// So the reading is thrown away as soon as the folder of taskbar shortcuts
    /// looks any different (<see cref="PinnedGroups.Stamp()"/>), and after
    /// <c>life</c> regardless: a group can also be deleted, or made again, without
    /// any shortcut changing.
    /// </summary>
    public sealed class PinnedGroupsCache
    {
        private readonly TimeSpan _life;
        private readonly Func<string> _stamp;
        private readonly Func<List<PinnedGroup>> _scan;
        private readonly Func<DateTime> _now;

        private List<PinnedGroup> _pins = new();
        private string? _stampRead;
        private DateTime _readAt = DateTime.MinValue;

        public PinnedGroupsCache(TimeSpan life)
            : this(life, PinnedGroups.Stamp, PinnedGroups.Scan, () => DateTime.UtcNow) { }

        /// <summary>With the folder's stamp, the reading and the clock handed in, for tests.</summary>
        public PinnedGroupsCache(TimeSpan life, Func<string> stamp, Func<List<PinnedGroup>> scan, Func<DateTime> now)
        {
            _life = life;
            _stamp = stamp;
            _scan = scan;
            _now = now;
        }

        /// <summary>
        /// How many times the taskbar's shortcuts have been found changed. A
        /// reading taken again only because it had aged does not count.
        /// </summary>
        public int Changes { get; private set; }

        /// <summary>The groups pinned to the taskbar now.</summary>
        public IReadOnlyList<PinnedGroup> Read()
        {
            // Taken before the reading: a shortcut that changes while it is being
            // read leaves a stamp that is already old, and the next call reads again.
            // A negative life (such as Timeout.InfiniteTimeSpan) keeps the reading
            // until the shortcut folder's stamp actually changes.
            string stamp = _stamp();
            DateTime now = _now();
            if (stamp == _stampRead && (_life < TimeSpan.Zero || now - _readAt <= _life)) return _pins;

            try { _pins = _scan(); }
            catch { /* keep the previous reading rather than losing every group */ }
            if (stamp != _stampRead) Changes++;
            _stampRead = stamp;
            _readAt = now;
            return _pins;
        }
    }
}
