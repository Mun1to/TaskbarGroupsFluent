using System;
using System.IO;
using TaskbarGroups.Core;
using Xunit;

[Collection("Paths")]
public class TaskbarPinRenameTests
{
    [Fact]
    public void AnOldPinStillNeedsRepinningAfterItsLaunchArgumentHasBeenUpdated()
    {
        string root = Directory.CreateTempSubdirectory("tbg-rename-").FullName;
        string pins = Directory.CreateDirectory(Path.Combine(root, "pins")).FullName;
        string oldPins = Environment.GetEnvironmentVariable("TBGF_PINNED_DIR");
        try
        {
            Environment.SetEnvironmentVariable("TBGF_PINNED_DIR", pins);
            string target = Path.Combine(root, "TaskbarGroups.Background.exe");
            File.WriteAllText(target, "");
            string pinned = Path.Combine(pins, "Game.lnk");
            string fresh = Path.Combine(root, "Fun.lnk");
            ShellLink.InstallShortcut(target, Category.AppIdPrefix + "Game", "Game", root, target, pinned, "Game");
            ShellLink.InstallShortcut(target, Category.AppIdPrefix + "Fun", "Fun", root, target, fresh, "Fun");

            Assert.Equal(pinned, TaskbarPin.FindPinnedShortcut("Game", target));
            File.Copy(fresh, pinned, true); // What previous versions did when saving a rename.
            Assert.Equal(pinned, TaskbarPin.FindPinnedShortcut("Fun", target));
            Assert.True(TaskbarPin.NeedsRepin(pinned, fresh));

            File.Delete(pinned);
            File.Copy(fresh, Path.Combine(pins, "Fun.lnk"));
            string replacement = TaskbarPin.FindPinnedShortcut("Fun", target);
            Assert.NotNull(replacement);
            Assert.False(TaskbarPin.NeedsRepin(replacement, fresh));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TBGF_PINNED_DIR", oldPins);
            Directory.Delete(root, true);
        }
    }
}
