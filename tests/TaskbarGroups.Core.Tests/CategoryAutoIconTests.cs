using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using TaskbarGroups.Core;
using Xunit;

[Collection("Paths")]
public class CategoryAutoIconTests : IDisposable
{
    private readonly string _original;
    private readonly string _root;

    public CategoryAutoIconTests()
    {
        _original = Paths.ConfigPath;
        _root = Path.Combine(Path.GetTempPath(), "tbgf-auto-" + Guid.NewGuid().ToString("N"));
        Paths.ConfigPath = _root;
    }

    public void Dispose()
    {
        Paths.ConfigPath = _original;
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    // A group whose Name is already set, with an empty shortcut list.
    private static Category Group() => new Category
    {
        Name = "g",
        ShortcutList = new List<ProgramShortcut>()
    };

    private static Bitmap Solid(int size, Color color)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, 0, 0, size, size);
        return bmp;
    }

    // An app whose 256 px icon is already in the group's cache.
    private static ProgramShortcut Cached(Category group, string path, Color color)
    {
        var ps = new ProgramShortcut { FilePath = path, name = path, isWindowsApp = true };
        string file = group.generateCachePath(ps);
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        using var bmp = Solid(256, color);
        bmp.Save(file, ImageFormat.Png);
        return ps;
    }

    // An app with nothing cached (and a target that does not exist).
    private static ProgramShortcut Uncached(string path)
        => new ProgramShortcut { FilePath = path, name = path, isWindowsApp = true };

    private static bool Near(Color actual, Color expected)
        => actual.A > 200
           && Math.Abs(actual.R - expected.R) <= 12
           && Math.Abs(actual.G - expected.G) <= 12
           && Math.Abs(actual.B - expected.B) <= 12;

    [Fact] // Four apps fill the four cells, at 256x256.
    public void CachedApps_FillTheFourCells()
    {
        var group = Group();
        group.ShortcutList.Add(Cached(group, "a", Color.Red));
        group.ShortcutList.Add(Cached(group, "b", Color.Lime));
        group.ShortcutList.Add(Cached(group, "c", Color.Blue));
        group.ShortcutList.Add(Cached(group, "d", Color.Yellow));

        using var built = group.BuildAutoIcon();

        Assert.NotNull(built);
        Assert.Equal(256, built.Width);
        Assert.Equal(256, built.Height);
        Assert.True(Near(built.GetPixel(50, 50), Color.Red));
        Assert.True(Near(built.GetPixel(200, 50), Color.Lime));
        Assert.True(Near(built.GetPixel(50, 200), Color.Blue));
        Assert.True(Near(built.GetPixel(200, 200), Color.Yellow));
    }

    [Fact] // Review Focus 3: a separator is not an app and takes no cell.
    public void Separators_DoNotTakeACell()
    {
        var group = Group();
        group.ShortcutList.Add(ProgramShortcut.ForSeparator("line"));
        group.ShortcutList.Add(Cached(group, "a", Color.Red));
        group.ShortcutList.Add(Cached(group, "b", Color.Lime));
        group.ShortcutList.Add(Cached(group, "c", Color.Blue));
        group.ShortcutList.Add(Cached(group, "d", Color.Yellow));

        using var built = group.BuildAutoIcon();

        Assert.NotNull(built);
        Assert.True(Near(built.GetPixel(200, 200), Color.Yellow)); // the fourth app made it
    }

    [Fact] // Review Focus 5: an app with no cached icon is left out; the rest still compose.
    public void AppsWithoutACache_AreLeftOut()
    {
        var group = Group();
        group.ShortcutList.Add(Cached(group, "a", Color.Red));
        group.ShortcutList.Add(Uncached(@"C:\nope\missing.exe"));
        group.ShortcutList.Add(Cached(group, "c", Color.Blue));

        using var built = group.BuildAutoIcon();

        Assert.NotNull(built);
        Assert.True(Near(built.GetPixel(50, 128), Color.Red));   // two cells, side by side, centred
        Assert.True(Near(built.GetPixel(200, 128), Color.Blue));
    }

    [Fact] // Review Focus 2: nothing cached -> leave the existing image alone.
    public void NoCachedIcon_ReturnsNull()
    {
        var group = Group();
        group.ShortcutList.Add(Uncached(@"C:\nope\missing.exe"));
        group.ShortcutList.Add(Uncached(@"C:\nope\missing2.exe"));

        Assert.Null(group.BuildAutoIcon());
    }

    [Fact] // Review Focus 4: a folder group wears the folder's own icon.
    public void FolderGroup_ReturnsNull()
    {
        var group = Group();
        group.FolderSource = "shell:Downloads";
        group.ShortcutList.Add(Cached(group, "a", Color.Red));

        Assert.Null(group.BuildAutoIcon());
    }

    [Fact]
    public void OnlySeparators_ReturnsNull()
    {
        var group = Group();
        group.ShortcutList.Add(ProgramShortcut.ForSeparator("one"));
        group.ShortcutList.Add(ProgramShortcut.ForSeparator("two"));

        Assert.Null(group.BuildAutoIcon());
    }
}
