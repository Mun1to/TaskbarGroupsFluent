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
            Title = "Eliminar grupo",
            Content = $"¿Seguro que quieres eliminar el grupo \"{group.Name}\"?\n\n" +
                      "Si lo tenías anclado, desánclalo de la barra de tareas aparte.",
            PrimaryButtonText = "Eliminar",
            PrimaryButtonAppearance = ControlAppearance.Danger,
            CloseButtonText = "Cancelar"
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
                Title = "No se pudo eliminar",
                Content = ex.Message,
                CloseButtonText = "Cerrar"
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
                Title = "Acceso directo no encontrado",
                Content = "No se encontró el acceso directo del grupo. Vuelve a guardarlo desde el editor.",
                CloseButtonText = "Cerrar"
            }.ShowDialogAsync();
            return;
        }

        // Open the folder with the .lnk selected. Windows 11 blocks programmatic
        // taskbar pinning, so the final right-click is the user's to make.
        ShellHelper.SelectInExplorer(group.ShortcutPath);

        await new MessageBox
        {
            Title = "Anclar a la barra de tareas",
            Content =
                "Abrí la carpeta con el acceso directo resaltado. Windows pide un último paso manual:\n\n" +
                "1.  Haz clic derecho en el archivo resaltado.\n" +
                "2.  Pulsa \"Mostrar más opciones\" (o Mayús + F10).\n" +
                "3.  Elige \"Anclar a la barra de tareas\".\n\n" +
                "Luego haz clic en ese icono de la barra para abrir el grupo.",
            CloseButtonText = "Entendido"
        }.ShowDialogAsync();
    }
}
