using System;
using System.Linq;
using System.Windows;
using TaskbarGroups.Core;

namespace TaskbarGroups.App.Helpers;

/// <summary>
/// Runtime access to localized strings for code-behind (XAML uses DynamicResource
/// directly). Strings live in Localization/Strings.{es,en}.xaml; the matching
/// dictionary is merged at startup, and swapped when the user picks another
/// language in the quick bar.
/// </summary>
public static class Loc
{
    /// <summary>Returns the localized string for a key, or the key itself if missing.</summary>
    public static string Get(string key)
        => Application.Current?.TryFindResource(key) as string ?? key;

    /// <summary>Localized string with <see cref="string.Format(string, object[])"/> arguments.</summary>
    public static string Format(string key, params object[] args)
        => string.Format(Get(key), args);

    /// <summary>
    /// Merges the dictionary of the language <see cref="UiPreferences"/> settles on:
    /// TBG_LANG, then the user's choice, then Windows' own language. Spanish
    /// systems get Spanish; everything else falls back to English.
    /// </summary>
    public static void ApplyLanguage(Application app)
    {
        try
        {
            var current = app.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString.Contains("/Localization/Strings.") == true);
            if (current is not null) app.Resources.MergedDictionaries.Remove(current);

            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Localization/Strings.{UiPreferences.Language()}.xaml",
                    UriKind.Absolute)
            });
        }
        catch
        {
            // Never block startup on a missing/locale dictionary; the UI falls back
            // to the resource keys, which is ugly but keeps the app usable.
        }
    }
}
