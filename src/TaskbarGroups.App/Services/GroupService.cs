using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TaskbarGroups.App.Models;
using TaskbarGroups.Core;

namespace TaskbarGroups.App.Services;

/// <summary>
/// Loads taskbar groups from the on-disk config folder. Each subfolder of
/// <see cref="Paths.ConfigPath"/> containing an ObjectData.xml is one group.
/// </summary>
public static class GroupService
{
    public static List<GroupItem> LoadAll()
    {
        var groups = new List<GroupItem>();
        string configPath = Paths.ConfigPath;
        if (!Directory.Exists(configPath))
            return groups;

        foreach (var dir in Directory.GetDirectories(configPath))
        {
            if (!File.Exists(Path.Combine(dir, "ObjectData.xml")))
                continue;

            try
            {
                var category = new Category(dir);
                groups.Add(new GroupItem
                {
                    Name = category.Name,
                    ShortcutCount = category.ShortcutList?.Count ?? 0,
                    Icon = LoadGroupIcon(dir),
                    Source = category,
                    ShortcutPath = Path.Combine(
                        Paths.ShortcutsPath,
                        Regex.Replace(category.Name, @"(_)+", " ") + ".lnk")
                });
            }
            catch
            {
                // Skip malformed groups rather than failing the whole list.
            }
        }

        return groups;
    }

    private static ImageSource? LoadGroupIcon(string groupDir)
    {
        string png = Path.Combine(groupDir, "GroupImage.png");
        if (!File.Exists(png))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.UriSource = new System.Uri(png);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
