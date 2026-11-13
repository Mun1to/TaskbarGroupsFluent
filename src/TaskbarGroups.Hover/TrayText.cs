using TaskbarGroups.Core;

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
    internal static string Tooltip => "Taskbar Groups Fluent";

    internal static string OpenApp => UiPreferences.Say(
        "Open Taskbar Groups Fluent",
        "Abrir Taskbar Groups Fluent",
        "Открыть Taskbar Groups Fluent");

    internal static string TurnOff => UiPreferences.Say(
        "Turn off open on hover",
        "Desactivar apertura al pasar el cursor",
        "Отключить открытие при наведении");

    internal static string TurnOffInstant => UiPreferences.Say(
        "Turn off instant opening",
        "Desactivar la apertura al instante",
        "Отключить мгновенное открытие");

    internal static string HideIcon => UiPreferences.Say(
        "Hide this icon (bring it back in the app's settings)",
        "Ocultar este icono (vuelve desde los ajustes de la app)",
        "Скрыть этот значок (вернуть можно в настройках приложения)");

    internal static string Quit => UiPreferences.Say(
        "Quit until next sign-in",
        "Salir hasta el próximo inicio",
        "Выйти до следующего входа в систему");

    internal static string OpenGroup => UiPreferences.Say("Open", "Abrir", "Открыть");

    internal static string OpenAll => UiPreferences.Say("Open all", "Abrir todas", "Открыть все");

    internal static string OpenFolder => UiPreferences.Say("Open the folder", "Abrir la carpeta", "Открыть папку");

    internal static string EditGroup => UiPreferences.Say("Edit group", "Editar grupo", "Изменить группу");

    // A group showing the Recycle Bin (issue #20), in the words File Explorer uses.
    internal static string EmptyBin => RecycleBinText.Empty;
    internal static string RestoreAll => RecycleBinText.RestoreAll;
    internal static string RestoreAllQuestion => RecycleBinText.RestoreAllQuestion;
    internal static string RecycleBinTitle => RecycleBinText.Title;
}
