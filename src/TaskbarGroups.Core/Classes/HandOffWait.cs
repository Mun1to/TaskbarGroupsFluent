#nullable enable

using System;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// The wait of a click that has passed its group on to the warm flyout, for
    /// that flyout to put the panel on screen.
    ///
    /// A warm flyout shows in about fifty milliseconds while its memory is at hand.
    /// One kept loaded for hours has handed that memory back to Windows, and on a
    /// PC busy with its disk getting it back took longer than the 700 ms this wait
    /// used to last (issue #20). The click then gave the flyout up and started a
    /// panel from nothing, on the same busy disk: 6.4 seconds in all, where waiting
    /// would have been a second or two. Starting from nothing is never the quicker
    /// way while the warm flyout is still there, so it is waited for as long as
    /// Windows itself waits before it calls a window "not responding", and the wait
    /// ends at once if the flyout goes away.
    /// </summary>
    public static class HandOffWait
    {
        /// <summary>How long a warm flyout that is still there is waited for.</summary>
        public const int PatienceMs = 5000;

        /// <summary>
        /// True once <paramref name="onScreen"/> says the panel is up. False if the
        /// flyout is gone (<paramref name="flyoutThere"/>) or the patience runs out,
        /// which is the caller's cue to show the panel itself.
        /// </summary>
        public static bool ForPanel(Func<bool> onScreen, Func<bool> flyoutThere,
            Func<long> elapsedMs, Action pause, int patienceMs = PatienceMs)
        {
            while (elapsedMs() < patienceMs)
            {
                if (onScreen()) return true;
                if (!flyoutThere()) return false;
                pause();
            }
            return onScreen();
        }
    }
}
