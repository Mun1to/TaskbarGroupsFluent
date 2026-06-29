using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
/// Lets the user pick installed Microsoft Store apps (by AppUserModelId) to add
/// to a group — the apps with no .exe that the classic file picker can't reach.
/// </summary>
public partial class StoreAppPickerWindow : FluentWindow
{
    private readonly ObservableCollection<StoreAppItem> _allItems = new();
    private ICollectionView? _view;

    public List<StoreAppInfo> SelectedApps =>
        _allItems.Where(i => i.IsSelected).Select(i => i.Info).ToList();

    public StoreAppPickerWindow()
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);
        Loaded += async (_, _) => await LoadAppsAsync();
    }

    private async Task LoadAppsAsync()
    {
        LoadingRing.Visibility = Visibility.Visible;

        var infos = await Task.Run(StoreApps.EnumerateInstalled);
        foreach (var info in infos)
        {
            var item = new StoreAppItem { Info = info };
            item.PropertyChanged += Item_PropertyChanged;
            _allItems.Add(item);
        }

        _view = CollectionViewSource.GetDefaultView(_allItems);
        _view.Filter = FilterApp;
        AppsList.ItemsSource = _view;

        LoadingRing.Visibility = Visibility.Collapsed;
        _ = Task.Run(LoadIcons);
    }

    private bool FilterApp(object obj)
    {
        string query = SearchBox.Text?.Trim() ?? "";
        if (query.Length == 0) return true;
        return obj is StoreAppItem item
               && item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void LoadIcons()
    {
        foreach (var item in _allItems.ToList())
        {
            try
            {
                var bitmap = handleWindowsApp.getWindowsAppIcon(item.Info.AppUserModelId, true);
                var source = bitmap.ToImageSource();
                Dispatcher.Invoke(() => item.Icon = source);
            }
            catch
            {
                // Leave the placeholder background for apps whose icon won't resolve.
            }
        }
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StoreAppItem.IsSelected))
            UpdateCount();
    }

    private void UpdateCount()
    {
        int n = _allItems.Count(i => i.IsSelected);
        CountText.Text = n == 1 ? "1 seleccionada" : $"{n} seleccionadas";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => _view?.Refresh();

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
