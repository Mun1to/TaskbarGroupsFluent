using System;
using System.Drawing;
using TaskbarGroups.Core;
using Xunit;

public class TaskbarButtonCacheTests
{
    private static readonly Rectangle Bar = new(0, 1032, 1920, 48);
    private static readonly Rectangle AiButton = new(600, 1032, 44, 48);
    private static readonly Rectangle DevButton = new(648, 1032, 44, 48);

    [Fact]
    public void ClickInsideCachedButton_ReturnsButtonCenterWithoutWaiting()
    {
        DateTime clock = new(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);
        var cache = new TaskbarButtonCache(TimeSpan.FromSeconds(2), () => clock);
        cache.Store(101, Bar, "stamp-1", [("AI", AiButton), ("Dev", DevButton)]);

        // Click near the right edge of the button: aligns to the button's exact centre.
        Assert.True(cache.TryHit(101, Bar, "stamp-1", "AI", new Point(640, 1055), out Point center, out bool stale));
        Assert.Equal(new Point(622, 1056), center);
        Assert.False(stale);

        // After the fresh window, still serves the centre immediately while flagging a background refresh.
        clock += TimeSpan.FromSeconds(5);
        Assert.True(cache.TryHit(101, Bar, "stamp-1", "AI", new Point(602, 1040), out center, out stale));
        Assert.Equal(new Point(622, 1056), center);
        Assert.True(stale);
    }

    [Fact]
    public void PointerSlidingSidewaysOnSameTaskbar_MissesCacheSoLiveLookupRuns()
    {
        DateTime clock = new(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);
        var cache = new TaskbarButtonCache(TimeSpan.FromSeconds(2), () => clock);
        cache.Store(101, Bar, "stamp-1", [("AI", AiButton), ("Dev", DevButton)]);

        // Pointer is still on the taskbar, but slid 160 px right onto empty taskbar space.
        Assert.False(cache.TryHit(101, Bar, "stamp-1", "AI", new Point(780, 1055), out _, out _));

        // Pointer slid onto a neighbouring group's button on the same taskbar.
        Assert.False(cache.TryHit(101, Bar, "stamp-1", "AI", new Point(665, 1055), out _, out _));
    }

    [Fact]
    public void CacheInvalidatesWhenBarHandleBoundsOrPinsStampChange()
    {
        DateTime clock = new(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);
        var cache = new TaskbarButtonCache(TimeSpan.FromSeconds(2), () => clock);
        cache.Store(101, Bar, "stamp-1", [("AI", AiButton)]);

        Assert.False(cache.TryHit(202, Bar, "stamp-1", "AI", new Point(620, 1055), out _, out _));
        Assert.False(cache.TryHit(101, new Rectangle(0, 1020, 1920, 60), "stamp-1", "AI", new Point(620, 1055), out _, out _));
        Assert.False(cache.TryHit(101, Bar, "stamp-2", "AI", new Point(620, 1055), out _, out _));
    }

    [Fact]
    public void DuplicatePinsPickTheButtonNearestToLaunchPoint()
    {
        var oldPin = new Rectangle(500, 1032, 44, 48);
        var newPin = new Rectangle(900, 1032, 44, 48);
        var offBar = new Rectangle(910, 200, 44, 48);

        Point? nearOld = TaskbarButtonCache.NearestCenter([oldPin, newPin, offBar], Bar, new Point(560, 900));
        Point? nearNew = TaskbarButtonCache.NearestCenter([oldPin, newPin, offBar], Bar, new Point(950, 900));

        Assert.Equal(new Point(522, 1056), nearOld);
        Assert.Equal(new Point(922, 1056), nearNew);
    }

    [Fact]
    public void StoreGroupPreservesOtherGroupsUntilSnapshotChanges()
    {
        DateTime clock = new(2026, 11, 27, 10, 0, 0, DateTimeKind.Utc);
        var cache = new TaskbarButtonCache(TimeSpan.FromSeconds(2), () => clock);
        cache.Store(101, Bar, "stamp-1", [("AI", AiButton), ("Dev", DevButton)]);

        var movedAi = new Rectangle(560, 1032, 44, 48);
        cache.StoreGroup(101, Bar, "stamp-1", "AI", [movedAi]);

        Assert.True(cache.TryHit(101, Bar, "stamp-1", "AI", new Point(580, 1055), out Point aiCenter, out _));
        Assert.Equal(new Point(582, 1056), aiCenter);
        Assert.True(cache.TryHit(101, Bar, "stamp-1", "Dev", new Point(665, 1055), out Point devCenter, out _));
        Assert.Equal(new Point(670, 1056), devCenter);

        // New stamp clears stale siblings.
        cache.StoreGroup(101, Bar, "stamp-2", "AI", [movedAi]);
        Assert.False(cache.TryHit(101, Bar, "stamp-2", "Dev", new Point(665, 1055), out _, out _));
    }
}
