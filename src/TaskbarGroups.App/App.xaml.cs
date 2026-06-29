using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace TaskbarGroups.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnUnhandledException;
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
