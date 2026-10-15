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

        // Back up the user's groups before updating. Updates don't touch this data
        // (it lives under %APPDATA%, separate from the app), but this is a safety net.
        ConfigBackup.Create();

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

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
        => new SettingsWindow { Owner = this }.ShowDialog();

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

    // The card's "…" button: the things a group needs now and then, kept off the
    // card itself so the three everyday buttons stay as they were.
    private void MoreGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: GroupItem group } button) return;
        ShowMenu(button,
            (Loc.Get("Loc_Group_Duplicate"), () => DuplicateGroup(group)),
            (Loc.Get("Loc_Group_Export"), () => ExportGroup(group)));
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button) return;
        ShowMenu(button,
            (Loc.Get("Loc_Main_ImportFile"), ImportGroupFile),
            (Loc.Get("Loc_Main_ImportTbg"), ImportTaskbarGroups),
            (Loc.Get("Loc_Main_ImportAppGroup"), ImportAppGroup));
    }

    private static void ShowMenu(FrameworkElement anchor, params (string Text, Action Run)[] items)
    {
        var menu = new System.Windows.Controls.ContextMenu
        {
            PlacementTarget = anchor,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };
        foreach (var (text, run) in items)
        {
            var item = new System.Windows.Controls.MenuItem { Header = text };
            item.Click += (_, _) => run();
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void DuplicateGroup(GroupItem group)
    {
        try
        {
            GroupTransfer.Duplicate(group.Name, Loc.Get("Loc_Group_CopySuffix"));
        }
        catch (Exception ex)
        {
            ShowMessage(Loc.Get("Loc_Main_ImportFailTitle"), ex.Message);
        }
        RefreshGroups();
    }

    private void ExportGroup(GroupItem group)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = group.Name + GroupTransfer.Extension,
            Filter = Loc.Get("Loc_Main_GroupFileFilter") + "|*" + GroupTransfer.Extension,
            DefaultExt = GroupTransfer.Extension
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            GroupTransfer.Export(group.Name, dialog.FileName);
            ShellHelper.SelectInExplorer(dialog.FileName);
        }
        catch (Exception ex)
        {
            ShowMessage(Loc.Get("Loc_Group_ExportFailTitle"), ex.Message);
        }
    }

    private void ImportGroupFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = Loc.Get("Loc_Main_GroupFileFilter") + "|*" + GroupTransfer.Extension,
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true) return;

        var imported = new System.Collections.Generic.List<string>();
        try
        {
            foreach (string file in dialog.FileNames)
                imported.Add(GroupTransfer.ImportFile(file));
        }
        catch (Exception ex)
        {
            ShowMessage(Loc.Get("Loc_Main_ImportFailTitle"), ex.Message);
        }
        RefreshGroups();
        ReportImported(imported);
    }

    // Taskbar Groups kept its groups in a "config" folder next to the program,
    // in the same format this app uses, so they come across whole: apps, names
    // and icons.
    private void ImportTaskbarGroups()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Loc.Get("Loc_Main_ImportTbgPick") };
        if (dialog.ShowDialog(this) != true) return;

        var imported = GroupTransfer.ImportTaskbarGroups(dialog.FolderName);
        RefreshGroups();
        if (imported.Count == 0)
            ShowMessage(Loc.Get("Loc_Main_ImportFailTitle"), Loc.Get("Loc_Main_ImportNone"));
        else
            ReportImported(imported);
    }

    // AppGroup installed on this PC is read straight from its data folder; if it
    // is not, the user points at one of its ".agz" backups instead.
    private void ImportAppGroup()
    {
        var imported = new System.Collections.Generic.List<string>();
        try
        {
            if (AppGroupImport.IsInstalled)
            {
                imported = AppGroupImport.ImportInstalled();
            }
            else
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = Loc.Get("Loc_Main_AppGroupFilter") + "|*.agz"
                };
                if (dialog.ShowDialog(this) != true) return;
                imported = AppGroupImport.ImportBackup(dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            ShowMessage(Loc.Get("Loc_Main_ImportFailTitle"), ex.Message);
            return;
        }

        RefreshGroups();
        if (imported.Count == 0)
            ShowMessage(Loc.Get("Loc_Main_ImportFailTitle"), Loc.Get("Loc_Main_AppGroupNone"));
        else
            ReportImported(imported);
    }

    private void ReportImported(System.Collections.Generic.List<string> imported)
    {
        if (imported.Count == 0) return;
        ShowMessage(Loc.Get("Loc_Main_ImportedTitle"), imported.Count == 1
            ? Loc.Format("Loc_Main_ImportedOne", imported[0])
            : Loc.Format("Loc_Main_ImportedMany", imported.Count));
    }

    private static void ShowMessage(string title, string message)
        => _ = new MessageBox
        {
            Title = title,
            Content = message,
            CloseButtonText = Loc.Get("Loc_Common_Close")
        }.ShowDialogAsync();

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
