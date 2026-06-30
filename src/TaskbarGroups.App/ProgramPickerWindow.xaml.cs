using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TaskbarGroups.App.Helpers;
using TaskbarGroups.App.Models;
using TaskbarGroups.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TaskbarGroups.App;

/// <summary>
/// Lets the user pick already-installed desktop programs from a searchable list
/// (built from the Start Menu) instead of hunting for the right .exe by hand.
/// An "Examinar…" button still allows picking an executable manually.
/// </summary>
public partial class ProgramPickerWindow : FluentWindow
{
    private readonly ObservableCollection<InstalledAppItem> _allItems = new();
    private ICollectionView? _view;

    public List<InstalledAppInfo> SelectedApps =>
        _allItems.Where(i => i.IsSelected).Select(i => i.Info).ToList();

    public ProgramPickerWindow()
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        UpdateCount();
        Loaded += async (_, _) => await LoadAppsAsync();
    }

    private async Task LoadAppsAsync()
    {
        LoadingRing.Visibility = Visibility.Visible;

        var infos = await Task.Run(InstalledApps.EnumerateInstalled);
        foreach (var info in infos)
        {
            var item = new InstalledAppItem { Info = info };
            item.PropertyChanged += Item_PropertyChanged;
            _allItems.Add(item);
        }

        _view = CollectionViewSource.GetDefaultView(_allItems);
        _view.Filter = FilterApp;
        AppsList.ItemsSource = _view;

        LoadingRing.Visibility = Visibility.Collapsed;
        _ = Task.Run(() => LoadIcons(_allItems.ToList()));
    }

    private bool FilterApp(object obj)
    {
        string query = SearchBox.Text?.Trim() ?? "";
        if (query.Length == 0) return true;
        return obj is InstalledAppItem item
               && item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void LoadIcons(IEnumerable<InstalledAppItem> items)
    {
        foreach (var item in items)
        {
            try
            {
                // Resolve the icon from the shortcut itself (not the resolved .exe),
                // so stub-launched apps like Discord keep their real logo.
                using var img = Category.ResolveShortcutImage(
                    new ProgramShortcut { FilePath = item.Info.ShortcutPath, isWindowsApp = false });
                var source = img.ToImageSource();
                Dispatcher.Invoke(() => item.Icon = source);
            }
            catch
            {
                // Leave the placeholder background for programs whose icon won't resolve.
            }
        }
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstalledAppItem.IsSelected))
            UpdateCount();
    }

    private void UpdateCount()
    {
        int n = _allItems.Count(i => i.IsSelected);
        CountText.Text = n == 1
            ? Loc.Get("Loc_Prog_CountOne")
            : Loc.Format("Loc_Prog_CountMany", n);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => _view?.Refresh();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.Get("Loc_Prog_PickTitle"),
            Filter = Loc.Get("Loc_Prog_FilterPrograms") + "|*.exe;*.lnk|"
                     + Loc.Get("Loc_Prog_FilterAll") + "|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true) return;

        var added = new List<InstalledAppItem>();
        foreach (string path in dialog.FileNames)
        {
            if (_allItems.Any(i => i.Info.ShortcutPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                continue;

            var item = new InstalledAppItem
            {
                Info = new InstalledAppInfo
                {
                    DisplayName = Path.GetFileNameWithoutExtension(path),
                    ShortcutPath = path,
                    TargetPath = path
                },
                IsSelected = true
            };
            item.PropertyChanged += Item_PropertyChanged;
            _allItems.Insert(0, item);
            added.Add(item);
        }

        if (added.Count == 0) return;

        _view?.Refresh();
        UpdateCount();
        _ = Task.Run(() => LoadIcons(added));
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = SelectedApps.Count > 0;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
