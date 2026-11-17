using System;
using System.IO;
using TaskbarGroups.Core;
using Xunit;

public class AppCatalogIconFromFileTests
{
    [Fact] // shell32.dll always carries icons; 96 is scaled into a 96px bitmap.
    public void ExistingPeFile_YieldsTheRequestedSize()
    {
        string path = Path.Combine(Environment.SystemDirectory, "shell32.dll");
        using var bmp = AppCatalog.IconFromFile(path, 96);
        Assert.NotNull(bmp);
        Assert.Equal(96, bmp.Width);
        Assert.Equal(96, bmp.Height);
    }

    [Fact] // Review Focus 4: nothing to extract -> null, so the caller can crop.
    public void MissingFile_ReturnsNull()
    {
        Assert.Null(AppCatalog.IconFromFile(@"C:\nope\missing.exe", 256));
    }

    [Fact]
    public void EmptyPath_ReturnsNull()
    {
        Assert.Null(AppCatalog.IconFromFile("", 256));
    }
}
