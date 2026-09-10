using System.Globalization;

namespace TaskbarGroups.Hover;

/// <summary>
/// The four strings this process shows. The main app keeps its translations in
/// XAML resource dictionaries, but loading WPF's resource machinery to read four
/// menu labels would defeat the point of a small resident process, so they live
/// here instead. TBG_LANG overrides the system language, mirroring the app.
/// </summary>
internal static class TrayText
{
    private static readonly bool Spanish = IsSpanish();

    internal static string Tooltip => Spanish
        ? "Taskbar Groups: abrir al pasar el cursor"
        : "Taskbar Groups: open on hover";

    internal static string OpenApp => Spanish
        ? "Abrir Taskbar Groups"
        : "Open Taskbar Groups";

    internal static string TurnOff => Spanish
        ? "Desactivar apertura al pasar el cursor"
        : "Turn off open on hover";

    internal static string Quit => Spanish
        ? "Salir hasta el próximo inicio"
        : "Quit until next sign-in";

    private static bool IsSpanish()
    {
        string? forced = Environment.GetEnvironmentVariable("TBG_LANG");
        if (!string.IsNullOrWhiteSpace(forced))
            return forced.Trim().StartsWith("es", StringComparison.OrdinalIgnoreCase);

        try
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                .Equals("es", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
}
