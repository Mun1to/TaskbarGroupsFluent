using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using TaskbarGroups.App.Helpers;
using TaskbarGroups.App.Models;
using TaskbarGroups.App.Services;
using TaskbarGroups.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace TaskbarGroups.App;

/// <summary>
/// Main editor window: lists existing taskbar groups and entry point to create new ones.
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        Loaded += (_, _) =>
        {
            RefreshGroups();
            _ = CheckForUpdatesAsync();
        };
    }

    // Offer to self-update when a newer release is on GitHub. Best-effort: any
    // failure (offline, no installer asset…) silently leaves the app as-is.
    private async Task CheckForUpdatesAsync()
    {
        var info = await UpdateChecker.CheckAsync();
        if (info is null) return;

        var choice = await new MessageBox
        {
            Title = Loc.Get("Loc_Update_Title"),
            Content = Loc.Format("Loc_Update_Message", info.Tag),
            PrimaryButtonText = Loc.Get("Loc_Update_Now"),
            PrimaryButtonAppearance = ControlAppearance.Primary,
            CloseButtonText = Loc.Get("Loc_Update_Later")
        }.ShowDialogAsync();

        if (choice != Wpf.Ui.Controls.MessageBoxResult.Primary) return;

        string? installer = await UpdateChecker.DownloadInstallerAsync(info.DownloadUrl);
        if (installer is null)
        {
            await new MessageBox
            {
                Title = Loc.Get("Loc_Update_Title"),
                Content = Loc.Get("Loc_Update_Failed"),
                CloseButtonText = Loc.Get("Loc_Common_Close")
            }.ShowDialogAsync();
            return;
        }

        try
        {
            // The installer closes the running app, overwrites in place and relaunches.
            Process.Start(new ProcessStartInfo(installer, "/SILENT /NORESTART") { UseShellExecute = true });
            Application.Current.Shutdown();
        }
        catch { /* if the installer can't launch, keep running the current version */ }
    }

    private void RefreshGroups()
    {
        var groups = GroupService.LoadAll();
        GroupsItemsControl.ItemsSource = groups;

        bool hasGroups = groups.Count > 0;
        GroupsItemsControl.Visibility = hasGroups ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = hasGroups ? Visibility.Collapsed : Visibility.Visible;
    }

    private void AddGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var editor = new GroupEditorWindow { Owner = this };
        if (editor.ShowDialog() == true)
            RefreshGroups();
    }

    private void EditGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: GroupItem group }) return;

        var editor = new GroupEditorWindow(group.Source) { Owner = this };
        if (editor.ShowDialog() == true)
            RefreshGroups();
    }

    private async void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: GroupItem group }) return;

        var confirm = await new MessageBox
        {
            Title = Loc.Get("Loc_Del_Title"),
            Content = Loc.Format("Loc_Del_Message", group.Name),
            PrimaryButtonText = Loc.Get("Loc_Common_Delete"),
            PrimaryButtonAppearance = ControlAppearance.Danger,
            CloseButtonText = Loc.Get("Loc_Common_Cancel")
        }.ShowDialogAsync();

        if (confirm != Wpf.Ui.Controls.MessageBoxResult.Primary) return;

        try
        {
            string configDir = Path.Combine(Paths.ConfigPath, group.Name);
            if (Directory.Exists(configDir)) Directory.Delete(configDir, true);
            if (File.Exists(group.ShortcutPath)) File.Delete(group.ShortcutPath);
        }
        catch (Exception ex)
        {
            await new MessageBox
            {
                Title = Loc.Get("Loc_Del_FailTitle"),
                Content = ex.Message,
                CloseButtonText = Loc.Get("Loc_Common_Close")
            }.ShowDialogAsync();
        }

        RefreshGroups();
    }

    private async void PinToTaskbar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: GroupItem group }) return;

        if (!File.Exists(group.ShortcutPath))
        {
            await new MessageBox
            {
                Title = Loc.Get("Loc_Pin_NotFoundTitle"),
                Content = Loc.Get("Loc_Pin_NotFoundBody"),
                CloseButtonText = Loc.Get("Loc_Common_Close")
            }.ShowDialogAsync();
            return;
        }

        // Open the folder with the .lnk selected. Windows 11 blocks programmatic
        // taskbar pinning, so the final right-click is the user's to make.
        ShellHelper.SelectInExplorer(group.ShortcutPath);

        await new MessageBox
        {
            Title = Loc.Get("Loc_Pin_Title"),
            Content = Loc.Get("Loc_Pin_Body"),
            CloseButtonText = Loc.Get("Loc_Common_Understood")
        }.ShowDialogAsync();
    }
}
