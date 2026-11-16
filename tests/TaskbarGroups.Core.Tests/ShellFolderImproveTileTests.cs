using System;
using System.Drawing;
using System.IO;
using TaskbarGroups.Core;
using Xunit;

public class ShellFolderImproveTileTests
{
    // A transparent canvas with a centred opaque square of `solid` px.
    private static Bitmap Canvas(int canvas, int solid)
    {
        var bmp = new Bitmap(canvas, canvas, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        int off = (canvas - solid) / 2;
        using var brush = new SolidBrush(Color.FromArgb(255, 30, 120, 220));
        g.FillRectangle(brush, off, off, solid, solid);
        return bmp;
    }

    [Fact] // Review Focus 1: a clean icon is not copied or cropped.
    public void CleanIcon_IsReturnedUnchanged()
    {
        using var bmp = Canvas(256, 240);
        Assert.Same(bmp, ShellFolder.ImproveTile(bmp, @"C:\some\file.txt", null, 96));
    }

    [Fact] // Review Focus 2: nothing solid -> not a tile, so untouched.
    public void TransparentIcon_IsReturnedUnchanged()
    {
        using var bmp = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Assert.Same(bmp, ShellFolder.ImproveTile(bmp, @"C:\some\file.txt", null, 96));
    }

    [Fact] // No source to re-resolve from -> crop so the logo fills the slot.
    public void PaddedWithoutASource_IsCropped()
    {
        using var bmp = Canvas(256, 48);
        using var result = ShellFolder.ImproveTile(bmp, @"C:\some\file.txt", null, 96);
        Assert.NotSame(bmp, result);
        Assert.Equal(52, result.Width);
    }

    [Fact] // A real .exe wins over the crop; 96 comes back at 96.
    public void PaddedExe_IsReResolved()
    {
        using var bmp = Canvas(256, 48);
        string exe = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        using var result = ShellFolder.ImproveTile(bmp, exe, exe, 96);
        Assert.NotSame(bmp, result);
        Assert.Equal(96, result.Width);
        Assert.Equal(96, result.Height);
    }

    [Fact] // Review Focus 4: the .exe yields nothing -> crop, never null.
    public void PaddedExeWithoutAnIcon_FallsBackToCrop()
    {
        using var bmp = Canvas(256, 48);
        string missing = @"C:\nope\missing.exe";
        using var result = ShellFolder.ImproveTile(bmp, missing, missing, 96);
        Assert.Equal(52, result.Width);
    }

    [Fact] // Review Focus 5: whitespace is trimmed; an unknown app falls back to crop.
    public void AppsFolderPrefixWithWhitespace_FallsBackToCrop()
    {
        using var bmp = Canvas(256, 48);
        using var result = ShellFolder.ImproveTile(bmp, "  shell:AppsFolder\\does.not.exist  ", null, 96);
        Assert.Equal(52, result.Width);
    }
}
