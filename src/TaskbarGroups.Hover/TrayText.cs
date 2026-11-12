namespace TaskbarGroups.Hover;

/// <summary>
/// The strings this process shows. The main app keeps its translations in XAML
/// resource dictionaries, but loading WPF's resource machinery to read a few menu
/// labels would defeat the point of a small resident process, so they live here
/// instead. The language follows the same rule as the app and the flyout
/// (UiPreferences), read each time so a change in the app reaches the menus.
/// The group menu says what a pinned group's right-click menu says, in the same
/// words, so the two read as one thing.
/// </summary>
internal static class TrayText
{
    private static bool Spanish => TaskbarGroups.Core.UiPreferences.IsSpanish();

    internal static string Tooltip => "Taskbar Groups Fluent";

    internal static string OpenApp => Spanish
        ? "Abrir Taskbar Groups Fluent"
        : "Open Taskbar Groups Fluent";

    internal static string TurnOff => Spanish
        ? "Desactivar apertura al pasar el cursor"
        : "Turn off open on hover";

    internal static string TurnOffInstant => Spanish
        ? "Desactivar la apertura al instante"
        : "Turn off instant opening";

    internal static string HideIcon => Spanish
        ? "Ocultar este icono (vuelve desde los ajustes de la app)"
        : "Hide this icon (bring it back in the app's settings)";

    internal static string Quit => Spanish
        ? "Salir hasta el próximo inicio"
        : "Quit until next sign-in";

    internal static string OpenGroup => Spanish ? "Abrir" : "Open";

    internal static string OpenAll => Spanish ? "Abrir todas" : "Open all";

    internal static string OpenFolder => Spanish ? "Abrir la carpeta" : "Open the folder";

    internal static string EditGroup => Spanish ? "Editar grupo" : "Edit group";
}
