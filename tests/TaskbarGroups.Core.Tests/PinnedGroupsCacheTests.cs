using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaskbarGroups.Core;
using Xunit;

// Issue #28: a group dragged onto the taskbar was unknown to hover-to-open for
// half a minute, because the reading of the pinned groups was kept for that long
// whatever happened to the taskbar's shortcuts in between.
[Collection("Paths")]
public class PinnedGroupsCacheTests : IDisposable
{
    private readonly string _pins = Path.Combine(Path.GetTempPath(), "tbgf-pins-" + Guid.NewGuid().ToString("N"));
    private DateTime _clock = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
    private int _scans;

    public PinnedGroupsCacheTests() => Directory.CreateDirectory(_pins);

    public void Dispose()
    {
        try { Directory.Delete(_pins, true); } catch { }
    }

    /// <summary>A cache over the temp folder, where a group is any .lnk and is named after it.</summary>
    private PinnedGroupsCache Cache() => new(
        TimeSpan.FromSeconds(30),
        () => PinnedGroups.Stamp(_pins),
        () =>
        {
            _scans++;
            return Directory.GetFiles(_pins, "*.lnk")
                .Select(f => new PinnedGroup { GroupName = Path.GetFileNameWithoutExtension(f) })
                .ToList();
        },
        () => _clock);

    private void Pin(string group, string content = "x")
        => File.WriteAllText(Path.Combine(_pins, group + ".lnk"), content);

    [Fact] // The bug itself: read, pin, read again within the same second.
    public void AGroupPinnedJustAfterAReading_IsSeenAtOnce()
    {
        var cache = Cache();
        Pin("Browsers");
        Assert.Equal(new[] { "Browsers" }, cache.Read().Select(p => p.GroupName));

        Pin("Media");
        _clock += TimeSpan.FromSeconds(1);

        Assert.Contains("Media", cache.Read().Select(p => p.GroupName));
    }

    [Fact] // What tells the watcher to look again at the icon under a cursor that has not moved.
    public void Changes_CountsShortcutsChanging_NotAReadingAgeing()
    {
        var cache = Cache();
        Pin("Browsers");
        cache.Read();
        int first = cache.Changes;

        _clock += TimeSpan.FromSeconds(31);
        cache.Read();
        Assert.Equal(first, cache.Changes);

        Pin("Media");
        cache.Read();
        Assert.Equal(first + 1, cache.Changes);
    }

    [Fact]
    public void AGroupUnpinned_IsGoneAtOnce()
    {
        var cache = Cache();
        Pin("Browsers");
        Pin("Media");
        Assert.Equal(2, cache.Read().Count);

        File.Delete(Path.Combine(_pins, "Media.lnk"));

        Assert.Equal(new[] { "Browsers" }, cache.Read().Select(p => p.GroupName));
    }

    [Fact] // Nothing changed: the shortcuts are not opened again on every look.
    public void WithNothingChanged_TheShortcutsAreReadOnce()
    {
        var cache = Cache();
        Pin("Browsers");
        for (int i = 0; i < 50; i++)
        {
            cache.Read();
            _clock += TimeSpan.FromMilliseconds(500);
        }
        Assert.Equal(1, _scans);
    }

    [Fact] // A group deleted or made again changes no shortcut, so a reading still ages.
    public void AReading_IsTakenAgainAfterItsLife()
    {
        var cache = Cache();
        Pin("Browsers");
        cache.Read();
        _clock += TimeSpan.FromSeconds(31);
        cache.Read();
        Assert.Equal(2, _scans);
    }

    [Fact] // A rename rewrites the shortcut under the same file name.
    public void TheStamp_ChangesWhenAShortcutIsRewritten()
    {
        Pin("Browsers", "points at the old name");
        string before = PinnedGroups.Stamp(_pins);
        Pin("Browsers", "points at the new name, which is longer");
        Assert.NotEqual(before, PinnedGroups.Stamp(_pins));
    }

    [Fact]
    public void TheStamp_IgnoresWhatIsNotAShortcut()
    {
        Pin("Browsers");
        string before = PinnedGroups.Stamp(_pins);
        File.WriteAllText(Path.Combine(_pins, "desktop.ini"), "x");
        Assert.Equal(before, PinnedGroups.Stamp(_pins));
    }

    [Fact]
    public void TheStamp_OfAFolderThatIsNotThere_IsEmpty()
        => Assert.Equal(string.Empty, PinnedGroups.Stamp(Path.Combine(_pins, "nowhere")));

    [Fact] // The whole way, with the real reading: a shortcut the shell wrote, in the folder the app reads.
    public void ARealShortcutDroppedOnTheTaskbar_IsFoundOnTheNextRead()
    {
        string config = Path.Combine(_pins, "config");
        Directory.CreateDirectory(Path.Combine(config, "Media"));
        File.WriteAllText(Path.Combine(config, "Media", "ObjectData.xml"), "<Category />");
        string panel = Path.Combine(_pins, "TaskbarGroups.Background.exe");
        File.WriteAllText(panel, "");

        string configBefore = Paths.ConfigPath;
        string pinsBefore = Environment.GetEnvironmentVariable("TBGF_PINNED_DIR");
        Paths.ConfigPath = config;
        Environment.SetEnvironmentVariable("TBGF_PINNED_DIR", _pins);
        try
        {
            var cache = new PinnedGroupsCache(TimeSpan.FromSeconds(30));
            Assert.Empty(cache.Read());

            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            dynamic link = shell.CreateShortcut(Path.Combine(_pins, "Media.lnk"));
            link.TargetPath = panel;
            link.Arguments = "Media";
            link.Save();

            Assert.Equal(new[] { "Media" }, cache.Read().Select(p => p.GroupName));
        }
        finally
        {
            Paths.ConfigPath = configBefore;
            Environment.SetEnvironmentVariable("TBGF_PINNED_DIR", pinsBefore);
        }
    }

    [Fact] // A reading that fails keeps the groups already known.
    public void AFailedReading_KeepsTheLastOne()
    {
        bool fail = false;
        var cache = new PinnedGroupsCache(TimeSpan.FromSeconds(30),
            () => PinnedGroups.Stamp(_pins),
            () => fail ? throw new IOException("locked") : new List<PinnedGroup> { new() { GroupName = "Browsers" } },
            () => _clock);
        Assert.Single(cache.Read());

        fail = true;
        Pin("Media");

        Assert.Single(cache.Read());
    }
}
