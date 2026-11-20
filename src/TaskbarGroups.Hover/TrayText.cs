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
        "Открыть Taskbar Groups Fluent",
        "打开 Taskbar Groups Fluent");

    // The card's header already names the app, so its first line just says what it does.
    internal static string OpenTheApp => UiPreferences.Say("Open the app", "Abrir la app", "Открыть приложение", "打开应用");

    // The two switches, in the words of the app's settings, so they read as the same thing.
    internal static string Hover => UiPreferences.Say(
        "Open on hover",
        "Abrir al pasar el cursor",
        "Открывать при наведении",
        "悬停展开");

    internal static string Instant => UiPreferences.Say(
        "Open groups instantly",
        "Abrir los grupos al instante",
        "Открывать группы мгновенно",
        "即时展开分组");

    internal static string HideIcon => UiPreferences.Say("Hide this icon", "Ocultar este icono", "Скрыть этот значок", "隐藏此图标");

    internal static string HideIconNote => UiPreferences.Say(
        "Bring it back in the app's settings",
        "Vuelve desde los ajustes de la app",
        "Вернуть можно в настройках приложения",
        "可在应用设置中重新显示");

    internal static string Quit => UiPreferences.Say("Quit", "Salir", "Выйти", "退出");

    internal static string QuitNote => UiPreferences.Say(
        "Starts again at the next sign-in",
        "Vuelve al iniciar sesión otra vez",
        "Запустится при следующем входе в систему",
        "下次登录时会再次启动");

    internal static string OpenGroup => UiPreferences.Say("Open", "Abrir", "Открыть", "打开");

    internal static string OpenAll => UiPreferences.Say("Open all", "Abrir todas", "Открыть все", "全部打开");

    internal static string OpenFolder => UiPreferences.Say("Open the folder", "Abrir la carpeta", "Открыть папку", "打开文件夹");

    internal static string EditGroup => UiPreferences.Say("Edit group", "Editar grupo", "Изменить группу", "编辑分组");

    // A group showing the Recycle Bin (issue #20), in the words File Explorer uses.
    internal static string EmptyBin => RecycleBinText.Empty;
    internal static string RestoreAll => RecycleBinText.RestoreAll;
    internal static string RestoreAllQuestion => RecycleBinText.RestoreAllQuestion;
    internal static string RecycleBinTitle => RecycleBinText.Title;
}
