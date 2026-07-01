using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using TaskbarGroups.App.Helpers;
using TaskbarGroups.App.Models;
using TaskbarGroups.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using DrawingImage = System.Drawing.Image;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace TaskbarGroups.App;

/// <summary>
/// Create/edit a taskbar group: name, icon and its list of shortcuts.
/// </summary>
public partial class GroupEditorWindow : FluentWindow
{
    private readonly ObservableCollection<ShortcutEntry> _shortcuts = new();
    private DrawingImage? _groupImage;
    private readonly bool _isEditing;
    private readonly string? _originalName;
    private bool _iconChanged;

    public GroupEditorWindow(Category? existing = null)
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);

        ShortcutsItemsControl.ItemsSource = _shortcuts;
        _shortcuts.CollectionChanged += (_, _) => UpdateEmptyHint();

        if (existing is not null)
        {
            _isEditing = true;
            _originalName = existing.Name;
            HeaderText.Text = Loc.Get("Loc_Editor_TitleEdit");
            Title = Loc.Get("Loc_Editor_TitleEdit");
            EditorTitleBar.Title = Loc.Get("Loc_Editor_TitleEdit");
            NameTextBox.Text = existing.Name;
            LoadExistingIcon(existing);
            LoadExistingShortcuts(existing);
        }

        UpdateEmptyHint();
    }

    private void LoadExistingIcon(Category category)
    {
        try
        {
            _groupImage = category.LoadIconImage();
            GroupIconPreview.Source = _groupImage.ToImageSource();
            GroupIconPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch { /* keep placeholder */ }
    }

    private void LoadExistingShortcuts(Category category)
    {
        if (category.ShortcutList is null) return;
        foreach (var ps in category.ShortcutList)
            _shortcuts.Add(BuildEntry(ps));
    }

    private static ShortcutEntry BuildEntry(ProgramShortcut ps)
    {
        ImageSource_TryResolve(ps, out var icon);
        return new ShortcutEntry { Shortcut = ps, Icon = icon };
    }

    private static void ImageSource_TryResolve(ProgramShortcut ps, out System.Windows.Media.ImageSource? icon)
    {
        try { icon = Category.ResolveShortcutImage(ps).ToImageSource(); }
        catch { icon = null; }
    }

    private void AddProgram_Click(object sender, RoutedEventArgs e)
    {
        // Unified app picker: the shell AppsFolder catalog (UWP + Win32) plus a
        // Browse… escape hatch. Each selection already carries the right launch id
        // and icon path, so we just add it.
        var picker = new ProgramPickerWindow { Owner = this };
        if (picker.ShowDialog() != true) return;

        foreach (var shortcut in picker.SelectedShortcuts)
            AddShortcut(shortcut);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Loc.Get("Loc_Editor_PickFolder"),
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true) return;

        foreach (var path in dialog.FolderNames)
            AddShortcut(new ProgramShortcut { FilePath = path, isWindowsApp = false });
    }

    private void AddShortcut(ProgramShortcut ps)
    {
        if (_shortcuts.Any(s => s.Shortcut.FilePath.Equals(ps.FilePath, StringComparison.OrdinalIgnoreCase)))
            return;
        _shortcuts.Add(BuildEntry(ps));
    }

    private void RemoveShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ShortcutEntry entry)
            _shortcuts.Remove(entry);
    }

    private void ChangeIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.Get("Loc_Editor_PickImage"),
            Filter = Loc.Get("Loc_Editor_ImagesFilter") + "|*.png;*.jpg;*.jpeg;*.bmp;*.ico"
        };
        if (dialog.ShowDialog(this) != true) return;

        BitmapImage source;
        try
        {
            source = new BitmapImage();
            source.BeginInit();
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.UriSource = new Uri(dialog.FileName);
            source.EndInit();
            source.Freeze();
        }
        catch
        {
            ShowError(Loc.Get("Loc_Editor_ImageLoadFail"));
            return;
        }

        // Let the user crop/position the image before applying it.
        var editor = new IconEditorWindow(source) { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is null) return;

        _groupImage = editor.Result;
        _iconChanged = true;
        GroupIconPreview.Source = _groupImage.ToImageSource();
        GroupIconPlaceholder.Visibility = Visibility.Collapsed;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = (NameTextBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError(Loc.Get("Loc_Editor_NeedName"));
            return;
        }
        // Windows folder names can't end in a dot or contain these characters.
        if (name.EndsWith(".") || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ShowError(Loc.Get("Loc_Editor_BadName"));
            return;
        }
        if (_shortcuts.Count == 0)
        {
            ShowError(Loc.Get("Loc_Editor_NeedShortcut"));
            return;
        }

        var category = new Category
        {
            Name = name,
            ShortcutList = _shortcuts.Select(s => s.Shortcut).ToList()
        };

        DrawingImage groupImage = _groupImage ?? GenerateDefaultGroupImage(name);
        try
        {
            category.CreateConfig(groupImage);
        }
        catch (Exception ex)
        {
            await new MessageBox
            {
                Title = Loc.Get("Loc_Editor_SaveFailTitle"),
                Content = ex.Message,
                CloseButtonText = Loc.Get("Loc_Common_Close")
            }.ShowDialogAsync();
            return;
        }

        if (_isEditing)
        {
            bool renamed = _originalName is not null
                && !string.Equals(_originalName, name, StringComparison.Ordinal);

            // Refresh the pinned taskbar button when the icon or the name changed
            // (Windows caches it). Per the user's choice this restarts Explorer.
            if (_iconChanged || renamed)
                RefreshTaskbarIcon(name);

            // A rename writes a new config folder + .lnk; delete the old ones so the
            // group isn't duplicated.
            if (renamed)
                DeleteOldGroup(_originalName!);
        }

        DialogResult = true;
        Close();
    }

    // Remove the previous group's config folder and shortcut after a rename.
    private static void DeleteOldGroup(string oldName)
    {
        try
        {
            string oldConfig = Path.Combine(Paths.ConfigPath, oldName);
            if (Directory.Exists(oldConfig)) Directory.Delete(oldConfig, true);

            string oldLnk = Paths.ShortcutFileFor(oldName);
            if (File.Exists(oldLnk)) File.Delete(oldLnk);
        }
        catch { /* a leftover old group is non-fatal; the user can delete it */ }
    }

    private void RefreshTaskbarIcon(string currentName)
    {
        try
        {
            // The pin was created under the original name; find it by that, then
            // overwrite it with the freshly built shortcut (carrying the new icon).
            string? pinnedLnk = TaskbarPin.FindPinnedShortcut(
                _originalName ?? currentName, Paths.BackgroundApplication);
            if (pinnedLnk is null) return; // not pinned — nothing to refresh

            TaskbarPin.UpdatePinnedShortcut(pinnedLnk, Paths.ShortcutFileFor(currentName));
            System.Threading.Tasks.Task.Run(TaskbarPin.RestartExplorer);
        }
        catch { /* never block saving on a refresh failure */ }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void UpdateEmptyHint()
        => NoShortcutsText.Visibility = _shortcuts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private static DrawingImage GenerateDefaultGroupImage(string name)
    {
        var bmp = new Bitmap(128, 128);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(0, 120, 212));
        string initial = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();
        using var font = new Font("Segoe UI", 56, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        var size = g.MeasureString(initial, font);
        g.DrawString(initial, font, System.Drawing.Brushes.White,
            (128 - size.Width) / 2, (128 - size.Height) / 2);
        return bmp;
    }

    private void ShowError(string message)
        => _ = new MessageBox
        {
            Title = Loc.Get("Loc_Common_Attention"),
            Content = message,
            CloseButtonText = Loc.Get("Loc_Common_Understood")
        }.ShowDialogAsync();
}
