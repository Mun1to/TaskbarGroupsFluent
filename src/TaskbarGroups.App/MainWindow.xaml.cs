using System;
using System.Collections.Generic;
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
using Windows.Services.Store;
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
        AppTheme.Watch(this);
        LoadQuickBar();
        Loaded += (_, _) =>
        {
            RefreshGroups();
            _ = CheckForUpdatesAsync(manual: false);
        };
    }

    // ---- The quick bar ------------------------------------------------------
    //
    // Theme, language and hover-to-open at hand, and the update state. The
    // settings themselves live where they always did; this only flips them.

    private bool _quickLoading;

    private void LoadQuickBar()
    {
        _quickLoading = true;

        string theme = Settings.settingInfo.theme;
        (theme == UiPreferences.Light ? ThemeLight
            : theme == UiPreferences.Dark ? ThemeDark : ThemeSystem).IsChecked = true;

        string lang = Settings.settingInfo.language;
        (lang == UiPreferences.Spanish ? LangEs
            : lang == UiPreferences.English ? LangEn : LangSystem).IsChecked = true;

        QuickHoverToggle.IsChecked = Settings.settingInfo.hoverToOpen;
        VersionText.Text = UpdateChecker.CurrentTag;
        ShowUpdateState(_phase);

        _quickLoading = false;
    }

    private void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (_quickLoading) return;
        AppTheme.Set(sender == ThemeLight ? UiPreferences.Light
            : sender == ThemeDark ? UiPreferences.Dark : UiPreferences.System);
    }

    // The window's own words switch on the spot (they are dynamic resources);
    // what code builds, the cards and the pinned groups' right-click menus, is
    // built again in the new language.
    private void Lang_Checked(object sender, RoutedEventArgs e)
    {
        if (_quickLoading) return;
        Settings.settingInfo.language = sender == LangEs ? UiPreferences.Spanish
            : sender == LangEn ? UiPreferences.English : UiPreferences.System;
        Settings.writeXML();

        Loc.ApplyLanguage(Application.Current);
        RefreshGroups();
        ShowUpdateState(_phase);
        App.RegisterJumpListsInBackground();
    }

    private async void QuickHover_Changed(object sender, RoutedEventArgs e)
    {
        if (_quickLoading) return;

        bool wanted = QuickHoverToggle.IsChecked == true;
        if (HoverService.Apply(wanted) || !wanted) return;

        // Same as in the settings: the watcher's exe is missing, so say so instead
        // of leaving a switch that looks on while nothing is watching.
        _quickLoading = true;
        QuickHoverToggle.IsChecked = false;
        _quickLoading = false;

        await new MessageBox
        {
            Title = Loc.Get("Loc_Settings_Title"),
            Content = Loc.Get("Loc_Settings_HoverMissing"),
            CloseButtonText = Loc.Get("Loc_Common_Close")
        }.ShowDialogAsync();
    }

    // ---- Updates ------------------------------------------------------------
    //
    // The installer build asks GitHub and runs the new installer; the Store
    // package asks the Store and lets it install, because the installer can't
    // overwrite a package's read-only folder. Either way the bar shows where
    // things stand, and a new version found at start still gets its dialog.

    private enum UpdatePhase { Idle, Checking, UpToDate, Available, Downloading, Failed }

    private UpdatePhase _phase = UpdatePhase.Idle;
    private UpdateChecker.UpdateInfo? _classicUpdate;
    private IReadOnlyList<StorePackageUpdate>? _storeUpdates;
    private string _availableTag = "";

    private void UpdateStatus_Click(object sender, RoutedEventArgs e) => _ = CheckForUpdatesAsync(manual: true);

    private void UpdateNow_Click(object sender, RoutedEventArgs e) => _ = InstallUpdateAsync();

    private void Support_Click(object sender, RoutedEventArgs e) => SupportLink.Open();

    /// <summary>
    /// Looks for a new version. The automatic look at start is silent unless it
    /// finds one; the one asked for with the button always says how it went.
    /// </summary>
    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_phase is UpdatePhase.Checking or UpdatePhase.Downloading) return;
        var before = _phase;
        if (manual) ShowUpdateState(UpdatePhase.Checking);
        else _phase = UpdatePhase.Checking;

        bool ok;
        if (AppPackage.IsPackaged)
        {
            (ok, _storeUpdates) = await StoreUpdateChecker.TryCheckAsync(this);
            _availableTag = _storeUpdates is null ? "" : StoreUpdateChecker.Tag(_storeUpdates);
        }
        else
        {
            (ok, _classicUpdate) = await UpdateChecker.TryCheckAsync();
            _availableTag = _classicUpdate?.Tag ?? "";
        }

        bool available = _availableTag.Length > 0;
        ShowUpdateState(available ? UpdatePhase.Available
            : ok ? UpdatePhase.UpToDate
            : manual ? UpdatePhase.Failed : before);

        if (available && !manual) await OfferUpdateAsync();
    }

    // The dialog a new version gets when the app opens, as it always had.
    private async Task OfferUpdateAsync()
    {
        var choice = await new MessageBox
        {
            Title = Loc.Get("Loc_Update_Title"),
            Content = Loc.Format(AppPackage.IsPackaged ? "Loc_Update_StoreMessage" : "Loc_Update_Message", _availableTag),
            PrimaryButtonText = Loc.Get("Loc_Update_Now"),
            PrimaryButtonAppearance = ControlAppearance.Primary,
            CloseButtonText = Loc.Get("Loc_Update_Later")
        }.ShowDialogAsync();

        if (choice == Wpf.Ui.Controls.MessageBoxResult.Primary) await InstallUpdateAsync();
    }

    private async Task InstallUpdateAsync()
    {
        if (_phase != UpdatePhase.Available) return;
        ShowUpdateState(UpdatePhase.Downloading);

        if (AppPackage.IsPackaged)
        {
            // Windows closes the app when the Store replaces its package, so most
            // of the time nothing after this line runs.
            var state = _storeUpdates is null ? null : await StoreUpdateChecker.InstallAsync(this, _storeUpdates);
            if (state == StorePackageUpdateState.Completed) { ShowUpdateState(UpdatePhase.UpToDate); return; }
            ShowUpdateState(UpdatePhase.Available);
            if (state != StorePackageUpdateState.Canceled) await ShowUpdateFailedAsync();
            return;
        }

        string? installer = _classicUpdate is null ? null : await UpdateChecker.DownloadInstallerAsync(_classicUpdate.DownloadUrl);
        if (installer is null)
        {
            ShowUpdateState(UpdatePhase.Available);
            await ShowUpdateFailedAsync();
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
        catch { ShowUpdateState(UpdatePhase.Available); /* keep running the current version */ }
    }

    private Task ShowUpdateFailedAsync() => new MessageBox
    {
        Title = Loc.Get("Loc_Update_Title"),
        Content = Loc.Get("Loc_Update_Failed"),
        CloseButtonText = Loc.Get("Loc_Common_Close")
    }.ShowDialogAsync();

    /// <summary>
    /// Paints the right end of the bar. Up to date it is only the version and a
    /// green tick; the words come back when there is something to say.
    /// </summary>
    private void ShowUpdateState(UpdatePhase phase)
    {
        _phase = phase;
        bool busy = phase is UpdatePhase.Checking or UpdatePhase.Downloading;

        UpdateRing.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UpdateStatusButton.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        UpdateNowButton.Visibility = phase == UpdatePhase.Available ? Visibility.Visible : Visibility.Collapsed;
        UpdateTick.Visibility = phase == UpdatePhase.UpToDate ? Visibility.Visible : Visibility.Collapsed;
        UpdateSync.Visibility = phase == UpdatePhase.UpToDate ? Visibility.Collapsed : Visibility.Visible;

        string text = phase switch
        {
            UpdatePhase.Checking => Loc.Get("Loc_Update_Checking"),
            UpdatePhase.Available => Loc.Format("Loc_Update_AvailableShort", _availableTag),
            UpdatePhase.Downloading => Loc.Format("Loc_Update_Downloading", _availableTag),
            UpdatePhase.Failed => Loc.Get("Loc_Update_CheckFailed"),
            _ => ""
        };
        UpdateText.Text = text;
        UpdateText.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        string tip = phase == UpdatePhase.UpToDate
            ? Loc.Format("Loc_Update_UpToDate", UpdateChecker.CurrentTag)
            : Loc.Get("Loc_Update_CheckNow");
        UpdateStatusButton.ToolTip = tip;
        System.Windows.Automation.AutomationProperties.SetName(UpdateStatusButton, tip);
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

    // The settings window can switch hover-to-open too, so the bar follows it.
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow { Owner = this }.ShowDialog();
        LoadQuickBar();
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
            GroupJumpList.Remove(group.Name);
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
