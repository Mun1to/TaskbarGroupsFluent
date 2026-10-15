using System.Windows;
using TaskbarGroups.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TaskbarGroups.App.Helpers;

/// <summary>
/// Light, dark, or whatever Windows uses: the choice from the quick bar, applied
/// to every window. Each window calls <see cref="Watch"/> instead of WPF-UI's
/// SystemThemeWatcher directly, because the watcher puts the system theme back
/// the moment Windows changes it, which would undo a theme the user fixed.
/// </summary>
public static class AppTheme
{
    private static bool FollowsSystem => UiPreferences.Theme() == UiPreferences.System;

    /// <summary>Applies the current choice to the whole app.</summary>
    public static void Apply()
    {
        if (FollowsSystem)
            ApplicationThemeManager.ApplySystemTheme();
        else
            ApplicationThemeManager.Apply(
                UiPreferences.IsLight() ? ApplicationTheme.Light : ApplicationTheme.Dark,
                WindowBackdropType.Mica);
    }

    /// <summary>Keeps a window in step with Windows, only while the user wants that.</summary>
    public static void Watch(Window window)
    {
        if (FollowsSystem) SystemThemeWatcher.Watch(window);
    }

    /// <summary>Saves a new choice ("system", "light" or "dark") and repaints every open window.</summary>
    public static void Set(string theme)
    {
        Settings.settingInfo.theme = theme;
        Settings.writeXML();
        Apply();

        foreach (Window window in Application.Current.Windows)
        {
            SystemThemeWatcher.UnWatch(window);
            Watch(window);
        }
    }
}
