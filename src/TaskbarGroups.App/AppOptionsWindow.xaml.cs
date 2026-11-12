using System.IO;
using System.Windows;
using System.Windows.Media;
using TaskbarGroups.App.Helpers;
using TaskbarGroups.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TaskbarGroups.App;

/// <summary>
/// The per-app settings inside a group: the name shown in the panel, its icon,
/// the arguments a program is started with, and whether it starts as
/// administrator. Works on the <see cref="ProgramShortcut"/> it is given, and
/// only writes to it when the user saves.
/// </summary>
public partial class AppOptionsWindow : FluentWindow
{
    private readonly ProgramShortcut _shortcut;

    // The icon picked here, kept as ProgramShortcut.CustomIcon keeps it, or null
    // for the app's own.
    private string? _customIcon;

    public AppOptionsWindow(ProgramShortcut shortcut, ImageSource? icon, string defaultName)
    {
        InitializeComponent();
        Helpers.AppTheme.Watch(this);
        _shortcut = shortcut;
        _customIcon = shortcut.CustomIcon;

        AppIcon.Source = icon;
        AppTitle.Text = defaultName;
        NameBox.Text = shortcut.name ?? "";
        NameBox.PlaceholderText = defaultName;

        // Store apps and web or game links only open through Windows, and folders
        // just open, so none takes arguments or an elevated start. Rather than
        // offer switches that would do nothing, say so. A desktop app picked from
        // the app list does take them: the panel then starts it through its Start
        // menu shortcut.
        bool program = !shortcut.IsGroup && !Directory.Exists(shortcut.FilePath)
            && (!shortcut.isWindowsApp || AppCatalog.DesktopLaunchPath(shortcut.FilePath) is not null);
        ProgramOnly.Visibility = program ? Visibility.Visible : Visibility.Collapsed;
        NotForThisKind.Visibility = program ? Visibility.Collapsed : Visibility.Visible;
        ArgsBox.Text = shortcut.Arguments ?? "";
        AdminToggle.IsChecked = shortcut.RunAsAdmin;

        // A separator only has its title, and with none it is just the line.
        if (shortcut.IsSeparator)
        {
            NameBox.PlaceholderText = Helpers.Loc.Get("Loc_AppOpt_SeparatorPlaceholder");
            NameHint.Text = Helpers.Loc.Get("Loc_AppOpt_SeparatorHint");
            ProgramOnly.Visibility = Visibility.Collapsed;
            NotForThisKind.Visibility = Visibility.Collapsed;
            IconSection.Visibility = Visibility.Collapsed;
        }

        UpdateIconButtons();
    }

    private string CurrentArguments()
        => ProgramOnly.Visibility == Visibility.Visible ? (ArgsBox.Text ?? "").Trim() : (_shortcut.Arguments ?? "");

    // What the entry wears without a picked icon, with the arguments as they are now.
    private ImageSource? AutomaticIcon()
    {
        var probe = new ProgramShortcut
        {
            FilePath = _shortcut.FilePath,
            isWindowsApp = _shortcut.isWindowsApp,
            Arguments = CurrentArguments()
        };
        try
        {
            using var image = Category.ResolveShortcutImage(probe);
            return image.ToImageSource();
        }
        catch { return null; }
    }

    private void UpdateIconButtons()
        => AutoIconButton.Visibility = _customIcon is null ? Visibility.Collapsed : Visibility.Visible;

    private void PickIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.Get("Loc_AppOpt_PickIconTitle"),
            Filter = Loc.Get("Loc_AppOpt_IconFilter") + "|" + PickedIcon.FileFilter
        };
        if (dialog.ShowDialog(this) != true) return;
        string path = dialog.FileName;

        // A program or a library may hold many icons: the user picks which.
        int index = 0;
        if (PickedIcon.HoldsIcons(path))
        {
            int count = PickedIcon.CountIcons(path);
            if (count > 1)
            {
                var picker = new IconPickerWindow(path, count) { Owner = this };
                if (picker.ShowDialog() != true) return;
                index = picker.SelectedIconIndex;
            }
            else if (count == 0)
            {
                ShowError(Loc.Get("Loc_AppOpt_IconLoadFail"));
                return;
            }
        }

        using var picture = PickedIcon.Load(path, index);
        if (picture is null)
        {
            ShowError(Loc.Get("Loc_AppOpt_IconLoadFail"));
            return;
        }
        _customIcon = PickedIcon.Encode(picture);
        AppIcon.Source = picture.ToImageSource();
        UpdateIconButtons();
    }

    private void AutoIcon_Click(object sender, RoutedEventArgs e)
    {
        _customIcon = null;
        AppIcon.Source = AutomaticIcon();
        UpdateIconButtons();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _shortcut.name = (NameBox.Text ?? "").Trim();
        if (!_shortcut.IsSeparator) _shortcut.CustomIcon = _customIcon;
        if (ProgramOnly.Visibility == Visibility.Visible)
        {
            _shortcut.Arguments = (ArgsBox.Text ?? "").Trim();
            _shortcut.RunAsAdmin = AdminToggle.IsChecked == true;
        }
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
        => _ = new Wpf.Ui.Controls.MessageBox
        {
            Title = Loc.Get("Loc_Common_Attention"),
            Content = message,
            CloseButtonText = Loc.Get("Loc_Common_Understood")
        }.ShowDialogAsync();
}
