using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

/// <summary>
/// Every language file of the main window has to carry the same texts as the
/// English one. A key missing from one of them is not caught anywhere else:
/// <c>Loc.Get</c> hands back the key itself, so a Chinese or Russian user would
/// read "Loc_Pin_Tooltip" on screen and nothing would fail.
/// </summary>
public class LocalizationParityTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly Regex Placeholder = new(@"\{(\d+)(?:[,:][^}]*)?\}");

    public static IEnumerable<object[]> Languages()
        => Directory.GetFiles(LocalizationFolder(), "Strings.*.xaml")
            .Select(f => Path.GetFileName(f)[8..^5])
            .Where(l => l != "en")
            .OrderBy(l => l, StringComparer.Ordinal)
            .Select(l => new object[] { l });

    [Theory]
    [MemberData(nameof(Languages))]
    public void HasTheSameKeysAsEnglish(string language)
    {
        var english = Read("en");
        var other = Read(language);

        var missing = english.Keys.Except(other.Keys).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var extra = other.Keys.Except(english.Keys).OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0 && extra.Count == 0,
            $"Strings.{language}.xaml is out of step with Strings.en.xaml." +
            (missing.Count > 0 ? $" Missing: {string.Join(", ", missing)}." : "") +
            (extra.Count > 0 ? $" Not in English: {string.Join(", ", extra)}." : ""));
    }

    /// <summary>
    /// A translation that asks for an argument the English text does not have
    /// makes <c>Loc.Format</c> throw, and that is a crash in the user's language only.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public void UsesTheSamePlaceholdersAsEnglish(string language)
    {
        var english = Read("en");
        var other = Read(language);

        var wrong = english.Keys.Intersect(other.Keys)
            .Where(k => !Placeholders(english[k]).SetEquals(Placeholders(other[k])))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(wrong.Count == 0,
            $"Strings.{language}.xaml uses other {{0}} placeholders than English in: {string.Join(", ", wrong)}.");
    }

    private static HashSet<string> Placeholders(string text)
        => Placeholder.Matches(text).Select(m => m.Groups[1].Value).ToHashSet();

    private static Dictionary<string, string> Read(string language)
    {
        var doc = XDocument.Load(Path.Combine(LocalizationFolder(), $"Strings.{language}.xaml"));
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in doc.Root.Elements())
        {
            string key = (string)element.Attribute(Xaml + "Key");
            if (key is null) continue;
            Assert.False(keys.ContainsKey(key), $"Strings.{language}.xaml has {key} twice.");
            keys[key] = element.Value;
        }
        return keys;
    }

    private static string LocalizationFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string folder = Path.Combine(dir.FullName, "src", "TaskbarGroups.App", "Localization");
            if (Directory.Exists(folder)) return folder;
        }
        throw new DirectoryNotFoundException("src/TaskbarGroups.App/Localization not found above the test output.");
    }
}
