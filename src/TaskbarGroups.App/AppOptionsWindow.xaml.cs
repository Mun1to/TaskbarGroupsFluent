using System.IO;
using System.Windows;
using System.Windows.Media;
using TaskbarGroups.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TaskbarGroups.App;

/// <summary>
/// The per-app settings inside a group: the name shown in the panel, the
/// arguments a program is started with, and whether it starts as administrator.
/// Works on the <see cref="ProgramShortcut"/> it is given, and only writes to it
/// when the user saves.
/// </summary>
public partial class AppOptionsWindow : FluentWindow
{
    private readonly ProgramShortcut _shortcut;

    public AppOptionsWindow(ProgramShortcut shortcut, ImageSource? icon, string defaultName)
    {
        InitializeComponent();
        Helpers.AppTheme.Watch(this);
        _shortcut = shortcut;

        AppIcon.Source = icon;
        AppTitle.Text = defaultName;
        NameBox.Text = shortcut.name ?? "";
        NameBox.PlaceholderText = defaultName;

        // Store apps start through the shell and folders just open, so neither
        // takes arguments or an elevated start. Rather than offer switches that
        // would do nothing, say so.
        bool program = !shortcut.isWindowsApp && !shortcut.IsGroup && !Directory.Exists(shortcut.FilePath);
        ProgramOnly.Visibility = program ? Visibility.Visible : Visibility.Collapsed;
        NotForThisKind.Visibility = program ? Visibility.Collapsed : Visibility.Visible;
        ArgsBox.Text = shortcut.Arguments ?? "";
        AdminToggle.IsChecked = shortcut.RunAsAdmin;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _shortcut.name = (NameBox.Text ?? "").Trim();
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
}
