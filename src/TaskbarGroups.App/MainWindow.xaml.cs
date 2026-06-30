using System;
using System.Diagnostics;
using System.IO;
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
        Loaded += (_, _) => RefreshGroups();
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
