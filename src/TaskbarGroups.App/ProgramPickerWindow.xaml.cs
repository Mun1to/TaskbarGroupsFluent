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
/// Unified app picker backed by the shell AppsFolder catalog (UWP + Win32, already
/// curated) plus a "Browse…" escape hatch for executables not in the catalog.
/// </summary>
public partial class ProgramPickerWindow : FluentWindow
{
    private readonly ObservableCollection<AppPickerItem> _allItems = new();
    private ICollectionView? _view;

    /// <summary>The shortcuts to add for the apps the user selected.</summary>
    public List<ProgramShortcut> SelectedShortcuts =>
        _allItems.Where(i => i.IsSelected).Select(i => i.Shortcut).ToList();

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

        var entries = await Task.Run(AppCatalog.Enumerate);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var item = new AppPickerItem
            {
                Shortcut = new ProgramShortcut
                {
                    FilePath = entry.LaunchId,
                    name = entry.DisplayName,
                    isWindowsApp = true
                }
            };
            if (!seen.Add(item.Key)) continue;
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
        return obj is AppPickerItem item
               && item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void LoadIcons(IEnumerable<AppPickerItem> items)
    {
        foreach (var item in items)
        {
            try
            {
                using var img = Category.ResolveShortcutImage(item.Shortcut);
                var source = img.ToImageSource();
                Dispatcher.Invoke(() => item.Icon = source);
            }
            catch { /* leave the placeholder for apps whose icon won't resolve */ }
        }
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppPickerItem.IsSelected))
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

        var added = new List<AppPickerItem>();
        foreach (string path in dialog.FileNames)
        {
            var item = new AppPickerItem
            {
                Shortcut = new ProgramShortcut
                {
                    FilePath = path,
                    name = Path.GetFileNameWithoutExtension(path),
                    isWindowsApp = false
                },
                IsSelected = true
            };
            if (_allItems.Any(i => i.Key.Equals(item.Key, StringComparison.OrdinalIgnoreCase)))
                continue;
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
        DialogResult = SelectedShortcuts.Count > 0;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
