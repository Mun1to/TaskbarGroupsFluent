namespace TaskbarGroups.Hover;

/// <summary>
/// The four strings this process shows. The main app keeps its translations in
/// XAML resource dictionaries, but loading WPF's resource machinery to read four
/// menu labels would defeat the point of a small resident process, so they live
/// here instead. The language follows the same rule as the app and the flyout
/// (UiPreferences), read each time so a change in the app reaches the menu.
/// </summary>
internal static class TrayText
{
    private static bool Spanish => TaskbarGroups.Core.UiPreferences.IsSpanish();

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
}
