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

        // Bring the hover watcher back if the user wants it and nothing is running:
        // an update stops it to unlock its files, and the startup entry only fires at
        // sign-in, so without this the feature would look switched on and do nothing
        // until the next reboot.
        if (Settings.settingInfo.hoverToOpen) Task.Run(() => HoverService.Start());

        // Groups pinned before 1.6.1 never had a right-click menu, and the list
        // Windows keeps can be wiped by a cleanup tool, so every start registers
        // them all again. It is a few COM calls per group, on a thread of its own
        // because the shell's list object wants a single-threaded apartment.
        RegisterJumpListsInBackground();

        // The pencil in a flyout's header asks for one group's editor, not the whole
        // app, which is why the main window is opened here rather than by a
        // StartupUri in App.xaml.
        string? editName = EditRequest(e.Args);
        if (editName is not null) EditOnly(editName);
        else new MainWindow().Show();
    }

    private static void RegisterJumpListsInBackground()
    {
        string openAll = Loc.Get("Loc_Jump_OpenAll"), edit = Loc.Get("Loc_Jump_Edit");
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                foreach (var group in Services.GroupService.LoadAll())
                    GroupJumpList.Register(group.Name, openAll, edit);
            }
            catch { /* best effort: the menus only add convenience */ }
        }) { IsBackground = true };
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
    }

    /// <summary>The group named after "--edit", or null for a normal start.</summary>
    private static string? EditRequest(string[] args)
    {
        int i = Array.FindIndex(args, a => a.Equals("--edit", StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]) ? args[i + 1] : null;
    }

    // Opens the editor on its own and leaves when it closes. A group that is gone
    // (deleted since it was pinned) falls back to the main window, which is where
    // the user would go next anyway.
    private void EditOnly(string name)
    {
        var group = Services.GroupService.LoadAll()
            .Find(g => string.Equals(g.Source.Name, name, StringComparison.OrdinalIgnoreCase));
        if (group is null)
        {
            new MainWindow().Show();
            return;
        }

        var editor = new GroupEditorWindow(group.Source) { WindowStartupLocation = WindowStartupLocation.CenterScreen };
        editor.ShowDialog();
        Shutdown();
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
