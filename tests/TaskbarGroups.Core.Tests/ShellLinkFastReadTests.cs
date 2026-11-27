using System;
using System.Collections.Generic;
using System.IO;
using TaskbarGroups.Core;
using Xunit;

public class ShellLinkFastReadTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("tbg-lnk-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    [Theory]
    [InlineData("中文目录", "AI 助手")]
    [InlineData("Папка_🚀_Español", "Ñoño & Русский")]
    public void FastParserMatchesInstalledShortcutWithUnicodePathAndAppId(string folderName, string groupName)
    {
        string dir = Directory.CreateDirectory(Path.Combine(_root, folderName)).FullName;
        string target = Path.Combine(dir, "TaskbarGroups.Background.exe");
        File.WriteAllText(target, "");
        string lnk = Path.Combine(_root, groupName + ".lnk");
        string expectedAppId = Category.AppIdPrefix + groupName;

        ShellLink.InstallShortcut(target, expectedAppId, groupName, dir, target, lnk, groupName);

        byte[] bytes = File.ReadAllBytes(lnk);
        Assert.True(ShellLink.TryParseShortcut(bytes, out string parsedTarget, out string parsedArgs, out string parsedAppId, out _));
        Assert.Equal(target, parsedTarget, ignoreCase: true);
        Assert.Equal(groupName, parsedArgs);
        Assert.Equal(expectedAppId, parsedAppId);

        ShellLink.ReadShortcut(lnk, out string readTarget, out string readArgs, out string readAppId);
        Assert.Equal(parsedTarget, readTarget);
        Assert.Equal(parsedArgs, readArgs);
        Assert.Equal(parsedAppId, readAppId);
    }

    [Fact]
    public void FastParserHandlesWshShortcutWithoutAppId()
    {
        string target = Path.Combine(_root, "TaskbarGroups.Background.exe");
        File.WriteAllText(target, "");
        string lnk = Path.Combine(_root, "Media.lnk");

        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.CreateShortcut(lnk);
        link.TargetPath = target;
        link.Arguments = "Media";
        link.Save();

        byte[] bytes = File.ReadAllBytes(lnk);
        Assert.True(ShellLink.TryParseShortcut(bytes, out string parsedTarget, out string parsedArgs, out string parsedAppId, out _));
        Assert.Equal(target, parsedTarget, ignoreCase: true);
        Assert.Equal("Media", parsedArgs);
        Assert.Equal(string.Empty, parsedAppId);
    }

    [Fact]
    public void FastParserRejectsCorruptOrTruncatedBytes()
    {
        Assert.False(ShellLink.TryParseShortcut(ReadOnlySpan<byte>.Empty, out _, out _, out _, out _));
        Assert.False(ShellLink.TryParseShortcut(new byte[80], out _, out _, out _, out _));
    }

    [Fact]
    public void CandidateAppIdsCoverDefaultRenamedAndDuplicatePins()
    {
        var pins = new List<PinnedGroup>
        {
            new() { LinkName = "Contact", AppId = Category.AppIdPrefix + "Game", GroupName = "Game" },
            new() { LinkName = "Game", AppId = Category.AppIdPrefix + "Game", GroupName = "Game" },
            new() { LinkName = "AI", AppId = Category.AppIdPrefix + "AI", GroupName = "AI" }
        };

        List<string> gameIds = PinnedGroups.CandidateAppIds("Game", pins);
        Assert.Equal(2, gameIds.Count);
        Assert.Contains(Category.AppIdPrefix + "Game", gameIds, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Category.AppIdPrefix + "Contact", gameIds, StringComparer.OrdinalIgnoreCase);

        List<string> aiIds = PinnedGroups.CandidateAppIds("AI", pins);
        Assert.Single(aiIds);
        Assert.Equal(Category.AppIdPrefix + "AI", aiIds[0]);
    }
}
