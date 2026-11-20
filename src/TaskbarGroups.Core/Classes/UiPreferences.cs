using System;
using System.Globalization;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Which language and which light or dark look every part of the app uses: the
    /// main window, the flyout and the tray menu of the hover watcher. One rule for
    /// all three, so the flyout never comes up in English over a Spanish app.
    ///
    /// The order is the same for both: the TBG_LANG / TBG_THEME environment
    /// variables first (they exist to test a look without touching Windows), then
    /// the user's choice in the quick bar, then whatever Windows itself uses.
    /// </summary>
    public static class UiPreferences
    {
        public const string System = "system";
        public const string Light = "light";
        public const string Dark = "dark";
        public const string Spanish = "es";
        public const string English = "en";
        public const string Russian = "ru";
        public const string SimplifiedChinese = "zh-Hans";

        /// <summary>Resolves the display language, falling back to English.</summary>
        public static string Language()
        {
            string forced = Environment.GetEnvironmentVariable("TBG_LANG");
            if (!string.IsNullOrWhiteSpace(forced)) return Known(forced.Trim());

            string chosen = Settings.settingInfo?.language;
            if (chosen == Spanish || chosen == English || chosen == Russian || chosen == SimplifiedChinese) return chosen;

            try { return Known(CultureInfo.CurrentUICulture.Name); }
            catch { return English; }
        }

        internal static string Known(string code)
        {
            if (code.StartsWith(Spanish, StringComparison.OrdinalIgnoreCase)) return Spanish;
            if (code.StartsWith(Russian, StringComparison.OrdinalIgnoreCase)) return Russian;
            if (code.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                // Regional cultures such as zh-CN and zh-SG share the zh-Hans parent.
                try
                {
                    for (var culture = CultureInfo.GetCultureInfo(code); culture.Name.Length > 0; culture = culture.Parent)
                        if (culture.Name.Equals(SimplifiedChinese, StringComparison.OrdinalIgnoreCase)) return SimplifiedChinese;
                }
                catch (CultureNotFoundException) { }
            }
            return English;
        }

        public static bool IsSpanish() => Language() == Spanish;

        /// <summary>
        /// A label in the language the app speaks now. For the few words
        /// the flyout and the program by the clock show, which carry no string tables
        /// of their own (issue #14 brought Russian).
        /// </summary>
        public static string Say(string english, string spanish, string russian, string simplifiedChinese)
            => Language() switch { Spanish => spanish, Russian => russian, SimplifiedChinese => simplifiedChinese, _ => english };

        /// <summary>"light", "dark" or "system" when nothing forces either.</summary>
        public static string Theme()
        {
            string forced = Environment.GetEnvironmentVariable("TBG_THEME");
            if (!string.IsNullOrWhiteSpace(forced))
                return forced.Trim().StartsWith("l", StringComparison.OrdinalIgnoreCase) ? Light : Dark;

            string chosen = Settings.settingInfo?.theme;
            return chosen == Light || chosen == Dark ? chosen : System;
        }

        /// <summary>True for the light look, resolving "system" through Windows' own setting.</summary>
        public static bool IsLight()
        {
            string theme = Theme();
            if (theme != System) return theme == Light;

            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int v && v != 0;
            }
            catch { return false; }
        }
    }
}
