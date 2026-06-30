using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
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
        // Follow the system's light/dark mode and accent colour instead of the
        // fixed "Dark" theme declared in App.xaml. SystemThemeWatcher on each
        // window then keeps it in sync if the user switches theme while open.
        ApplicationThemeManager.ApplySystemTheme();
        base.OnStartup(e);
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
            "Ocurrió un error inesperado:\n\n" + e.Exception.Message,
            "Taskbar Groups", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
