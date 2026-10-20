using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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
    private static readonly CompareInfo CultureCompare = CultureInfo.CurrentCulture.CompareInfo;
    private const CompareOptions SearchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    private readonly ObservableCollection<AppPickerItem> _allItems = new();
    private ICollectionView? _view;
    private CancellationTokenSource? _cts;

    /// <summary>The shortcuts to add for the apps the user selected.</summary>
    public List<ProgramShortcut> SelectedShortcuts =>
        _allItems.Where(i => i.IsSelected).Select(i => i.Shortcut).ToList();

    public ProgramPickerWindow()
    {
        InitializeComponent();
        AppTheme.Watch(this);
        UpdateCount();
        Loaded += async (_, _) => await LoadAppsAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnClosed(e);
    }

    private async Task LoadAppsAsync()
    {
        LoadingRing.Visibility = Visibility.Visible;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var entries = await Task.Run(AppCatalog.Enumerate, token);
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
        _ = Task.Run(() => LoadIcons(_allItems.ToList(), token), token);
    }

    private bool FilterApp(object obj)
    {
        string query = SearchBox.Text?.Trim() ?? "";
        if (query.Length == 0) return true;
        if (obj is not AppPickerItem item) return false;

        string[] terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (string term in terms)
        {
            bool matchName = CultureCompare.IndexOf(item.DisplayName, term, SearchOptions) >= 0;
            bool matchPath = CultureCompare.IndexOf(item.Shortcut.FilePath, term, SearchOptions) >= 0;
            if (!matchName && !matchPath) return false;
        }
        return true;
    }

    private void LoadIcons(IEnumerable<AppPickerItem> items, CancellationToken token)
    {
        foreach (var item in items)
        {
            if (token.IsCancellationRequested) break;
            try
            {
                // Request 48px icons instead of 256px for thumbnail display (saves significant RAM & CPU)
                using var img = Category.ResolveShortcutImage(item.Shortcut, 48);
                if (img is null || token.IsCancellationRequested) continue;
                var source = img.ToImageSource();
                Dispatcher.BeginInvoke(() =>
                {
                    if (!token.IsCancellationRequested)
                        item.Icon = source;
                });
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

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (AppsList.Items.Count > 0)
            {
                AppsList.Focus();
                if (AppsList.SelectedIndex < 0) AppsList.SelectedIndex = 0;
                var container = AppsList.ItemContainerGenerator.ContainerFromIndex(AppsList.SelectedIndex) as ListBoxItem;
                container?.Focus();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Enter)
        {
            var firstItem = _view?.Cast<AppPickerItem>().FirstOrDefault();
            if (firstItem != null)
            {
                if (!_allItems.Any(i => i.IsSelected))
                    firstItem.IsSelected = true;
                Add_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
        }
    }

    private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: AppPickerItem item })
        {
            item.IsSelected = true;
            Add_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void ListBoxItem_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && sender is ListBoxItem { DataContext: AppPickerItem item })
        {
            item.IsSelected = !item.IsSelected;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (sender is ListBoxItem { DataContext: AppPickerItem targetItem })
                targetItem.IsSelected = true;
            Add_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void ItemBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is AppPickerItem item)
        {
            if (e.OriginalSource == fe)
            {
                item.IsSelected = !item.IsSelected;
                e.Handled = true;
            }
        }
    }

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
        var token = _cts?.Token ?? CancellationToken.None;
        _ = Task.Run(() => LoadIcons(added, token), token);
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
