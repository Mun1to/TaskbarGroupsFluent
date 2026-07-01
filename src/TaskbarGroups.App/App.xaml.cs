using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TaskbarGroups.App.Helpers;
using TaskbarGroups.Core;
using Wpf.Ui.Appearance;

namespace TaskbarGroups.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
        // Match the Windows display language (Spanish, else English) before any
        // window is created, so the first paint is already localized.
        Loc.ApplySystemLanguage(this);
        // Follow the system's light/dark mode and accent colour instead of the
        // fixed "Dark" theme declared in App.xaml. SystemThemeWatcher on each
        // window then keeps it in sync if the user switches theme while open.
        ApplicationThemeManager.ApplySystemTheme();
        base.OnStartup(e);

        // After an update that changes icon resolution, refresh group caches once so
        // pinned flyouts don't keep showing icons rendered by the previous build.
        // Off the UI thread — it re-resolves every shortcut's icon.
        Task.Run(IconCacheMaintenance.RefreshIfStale);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            string log = Path.Combine(Path.GetTempPath(), "taskbargroups_error.log");
            File.WriteAllText(log, DateTime.Now + Environment.NewLine + e.Exception);
        }
        catch { /* logging must never throw */ }

        MessageBox.Show(
            Loc.Format("Loc_Err_Unexpected", e.Exception.Message),
            Loc.Get("Loc_Common_AppName"), MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
