using TaskbarGroups.Core;
using Xunit;

// Issue #20: a click gave up on the panel kept loaded after 700 ms and started
// one from nothing, which on a PC busy with its disk took 6.4 seconds.
public class HandOffWaitTests
{
    private long _clock;

    private bool Wait(long shownAtMs, long goneAtMs = long.MaxValue)
        => HandOffWait.ForPanel(
            onScreen: () => _clock >= shownAtMs,
            flyoutThere: () => _clock < goneAtMs,
            elapsedMs: () => _clock,
            pause: () => _clock += 5);

    [Fact] // The bug itself: slower than the old limit, and still on its way.
    public void APanelSlowerThanTheOldLimit_IsStillWaitedFor()
    {
        Assert.True(Wait(shownAtMs: 1500));
        Assert.InRange(_clock, 1500, 1510);
    }

    [Fact]
    public void APanelThatIsQuick_CostsNoWait()
    {
        Assert.True(Wait(shownAtMs: 50));
        Assert.InRange(_clock, 50, 55);
    }

    [Fact] // Nothing to wait for: the click shows the panel itself straight away.
    public void AFlyoutThatGoesAway_EndsTheWaitAtOnce()
    {
        Assert.False(Wait(shownAtMs: long.MaxValue, goneAtMs: 200));
        Assert.InRange(_clock, 200, 210);
    }

    [Fact] // Stuck for good: given up on, as before, only later.
    public void AFlyoutThatNeverShows_IsGivenUpOn()
    {
        Assert.False(Wait(shownAtMs: long.MaxValue));
        Assert.InRange(_clock, HandOffWait.PatienceMs, HandOffWait.PatienceMs + 10);
    }
}
