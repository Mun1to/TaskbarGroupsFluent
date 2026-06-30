using System;
using System.Globalization;
using System.Windows;

namespace TaskbarGroups.App.Helpers;

/// <summary>
/// Runtime access to localized strings for code-behind (XAML uses DynamicResource
/// directly). Strings live in Localization/Strings.{es,en}.xaml; the matching
/// dictionary is merged at startup based on the Windows display language.
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
    /// Merges the language dictionary that matches the system UI culture. Spanish
    /// systems get Spanish; everything else falls back to English.
    /// </summary>
    public static void ApplySystemLanguage(Application app)
    {
        string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .Equals("es", StringComparison.OrdinalIgnoreCase) ? "es" : "en";

        try
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Localization/Strings.{lang}.xaml", UriKind.Absolute)
            };
            app.Resources.MergedDictionaries.Add(dict);
        }
        catch
        {
            // Never block startup on a missing/locale dictionary; the UI falls back
            // to the resource keys, which is ugly but keeps the app usable.
        }
    }
}
