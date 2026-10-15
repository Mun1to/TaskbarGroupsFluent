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

    // The taskbar icon is the group's own apps, two by two, and follows them: every
    // add or remove rebuilds it until the user picks an image or an emoji instead.
    private bool _gridIcon;

    // Set while the panel controls are filled in from a saved group, so their change
    // events don't redraw the preview a dozen times on the way.
    private bool _loadingPanel = true;

    public GroupEditorWindow(Category? existing = null)
    {
        InitializeComponent();
        AppTheme.Watch(this);

        ShortcutsItemsControl.ItemsSource = _shortcuts;
        PreviewItems.ItemsSource = _shortcuts;
        _shortcuts.CollectionChanged += (_, _) =>
        {
            UpdateEmptyHint();
            if (_gridIcon) RebuildGridIcon();
            UpdatePreview();
        };
        NameTextBox.TextChanged += (_, _) => UpdatePreview();

        ColumnsCombo.Items.Add(Loc.Get("Loc_Editor_ColumnsAuto"));
        for (int n = 1; n <= 8; n++) ColumnsCombo.Items.Add(n.ToString());
        // Same order as PanelLayouts.IconScales.
        IconSizeCombo.Items.Add(Loc.Get("Loc_Editor_IconSmall"));
        IconSizeCombo.Items.Add(Loc.Get("Loc_Editor_IconNormal"));
        IconSizeCombo.Items.Add(Loc.Get("Loc_Editor_IconLarge"));
        HeaderPositionCombo.Items.Add(Loc.Get("Loc_Editor_HeaderTop"));
        HeaderPositionCombo.Items.Add(Loc.Get("Loc_Editor_HeaderBottom"));

        // A new group starts with its name and pencil showing, which is the look
        // Munir picked for 1.6. Existing groups keep whatever they had: the field
        // defaults to off, so a file saved before 1.6 opens exactly as it used to.
        var panel = existing ?? new Category { ShowHeader = true };
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
            _gridIcon = existing.GridIcon;
        }
        LoadPanelOptions(panel);

        UpdateEmptyHint();
        _loadingPanel = false;
        UpdatePreview();
    }

    private void LoadPanelOptions(Category category)
    {
        if (PanelLayouts.IsRow(category.PanelLayout)) LayoutRowRadio.IsChecked = true;
        else if (PanelLayouts.IsColumn(category.PanelLayout)) LayoutColumnRadio.IsChecked = true;
        else if (PanelLayouts.IsList(category.PanelLayout)) LayoutListRadio.IsChecked = true;
        else LayoutGridRadio.IsChecked = true;

        ColumnsCombo.SelectedIndex = Math.Clamp(category.Width, 0, ColumnsCombo.Items.Count - 1);
        int size = Array.IndexOf(PanelLayouts.IconScales, category.IconScale);
        IconSizeCombo.SelectedIndex = size >= 0 ? size : 1;
        LabelsToggle.IsChecked = category.ShowLabels;
        HeaderToggle.IsChecked = category.ShowHeader;
        HeaderPositionCombo.SelectedIndex = PanelLayouts.IsBottom(category.HeaderPosition) ? 1 : 0;
    }

    private string SelectedLayout()
        => LayoutRowRadio.IsChecked == true ? PanelLayouts.Row
         : LayoutColumnRadio.IsChecked == true ? PanelLayouts.Column
         : LayoutListRadio.IsChecked == true ? PanelLayouts.List
         : PanelLayouts.Grid;

    private int SelectedIconScale()
        => PanelLayouts.IconScales[Math.Clamp(IconSizeCombo.SelectedIndex, 0, PanelLayouts.IconScales.Length - 1)];

    private string SelectedHeaderPosition()
        => HeaderPositionCombo.SelectedIndex == 1 ? PanelLayouts.HeaderBottom : PanelLayouts.HeaderTop;

    // Index 0 is "automatic"; after that the index is the column count itself.
    private int SelectedColumns() => Math.Max(ColumnsCombo.SelectedIndex, 0);

    private void PanelOption_Changed(object sender, RoutedEventArgs e) => UpdatePreview();

    // Lays the preview out with exactly the rules the flyout uses, so what the user
    // sees here is what opens from the taskbar.
    private void UpdatePreview()
    {
        if (_loadingPanel) return;

        var plan = PanelLayouts.Plan(SelectedLayout(), LabelsToggle.IsChecked == true,
            SelectedColumns(), _shortcuts.Count);
        PreviewItems.ItemsPanel = (System.Windows.Controls.ItemsPanelTemplate)Resources["Preview" + plan.Panel + "Panel"];
        PreviewItems.ItemTemplate = (DataTemplate)Resources["Preview" + plan.Template];
        PreviewItems.MaxWidth = plan.MaxWidth;
        double scale = PanelLayouts.Scale(SelectedIconScale());
        PreviewItems.LayoutTransform = scale == 1.0
            ? System.Windows.Media.Transform.Identity
            : new System.Windows.Media.ScaleTransform(scale, scale);

        // Only the options that change something for this layout are shown.
        ColumnsRow.Visibility = plan.UsesColumns ? Visibility.Visible : Visibility.Collapsed;
        LabelsToggle.Visibility = plan.UsesLabels ? Visibility.Visible : Visibility.Collapsed;
        bool header = HeaderToggle.IsChecked == true;
        HeaderPositionRow.Visibility = header ? Visibility.Visible : Visibility.Collapsed;

        bool bottom = SelectedHeaderPosition() == PanelLayouts.HeaderBottom;
        System.Windows.Controls.DockPanel.SetDock(PreviewHeader,
            bottom ? System.Windows.Controls.Dock.Bottom : System.Windows.Controls.Dock.Top);
        PreviewHeader.Margin = bottom ? new Thickness(8, 4, 0, 0) : new Thickness(8, 0, 0, 4);

        // Dark or light glass, following the app's theme the way the flyout follows
        // the system's.
        bool dark = ApplicationThemeManager.GetAppTheme() != ApplicationTheme.Light;
        PreviewPanel.Background = new System.Windows.Media.SolidColorBrush(dark
            ? System.Windows.Media.Color.FromArgb(0xB4, 0x20, 0x20, 0x20)
            : System.Windows.Media.Color.FromArgb(0xC8, 0xF3, 0xF3, 0xF3));
        PreviewPanel.BorderBrush = new System.Windows.Media.SolidColorBrush(dark
            ? System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)
            : System.Windows.Media.Color.FromArgb(0x22, 0x00, 0x00, 0x00));
        PreviewHeaderText.Foreground = new System.Windows.Media.SolidColorBrush(dark
            ? System.Windows.Media.Colors.White
            : System.Windows.Media.Color.FromRgb(0x1A, 0x1A, 0x1A));

        PreviewHeader.Visibility = header ? Visibility.Visible : Visibility.Collapsed;
        string name = (NameTextBox.Text ?? "").Trim();
        PreviewHeaderText.Text = name.Length > 0 ? name : Loc.Get("Loc_Editor_NamePlaceholder");
    }

    private void GridIcon_Click(object sender, RoutedEventArgs e)
    {
        if (_shortcuts.Count == 0)
        {
            ShowError(Loc.Get("Loc_Editor_GridIconNeedsApps"));
            return;
        }
        _gridIcon = true;
        RebuildGridIcon();
    }

    private void RebuildGridIcon()
    {
        var icons = new List<DrawingImage>();
        foreach (var entry in _shortcuts.Take(4))
        {
            try { icons.Add(Category.ResolveShortcutImage(entry.Shortcut)); }
            catch { /* an app without an icon just leaves its cell to the others */ }
        }
        if (icons.Count == 0) return;

        _groupImage = GroupIconGrid.Compose(icons);
        _iconChanged = true;
        GroupIconPreview.Source = _groupImage.ToImageSource();
        GroupIconPlaceholder.Visibility = Visibility.Collapsed;
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

    // A group inside this one. Offered from the groups that already exist, minus
    // this one: a group that opens itself would just reopen the same panel.
    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button) return;

        string self = _originalName ?? (NameTextBox.Text ?? "").Trim();
        var others = Services.GroupService.LoadAll()
            .Select(g => g.Name)
            .Where(n => !string.Equals(n, self, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (others.Count == 0)
        {
            ShowError(Loc.Get("Loc_Editor_NoOtherGroups"));
            return;
        }

        var menu = new System.Windows.Controls.ContextMenu
        {
            PlacementTarget = button,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };
        foreach (string name in others)
        {
            var item = new System.Windows.Controls.MenuItem { Header = name };
            item.Click += (_, _) => AddShortcut(ProgramShortcut.ForGroup(name));
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void AddShortcut(ProgramShortcut ps)
    {
        if (_shortcuts.Any(s => s.Shortcut.FilePath.Equals(ps.FilePath, StringComparison.OrdinalIgnoreCase)))
            return;
        _shortcuts.Add(BuildEntry(ps));
    }

    private void AppOptions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ShortcutEntry entry }) return;

        // The name the app would have without a custom one, for the placeholder.
        string saved = entry.Shortcut.name;
        entry.Shortcut.name = "";
        string defaultName = entry.DisplayName;
        entry.Shortcut.name = saved;

        var dialog = new AppOptionsWindow(entry.Shortcut, entry.Icon, defaultName) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        // ShortcutEntry doesn't raise change notifications, so swap the item to
        // redraw its row and the preview with the new name.
        int i = _shortcuts.IndexOf(entry);
        if (i >= 0) _shortcuts[i] = new ShortcutEntry { Shortcut = entry.Shortcut, Icon = entry.Icon };
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
        _gridIcon = false;
        GroupIconPreview.Source = _groupImage.ToImageSource();
        GroupIconPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void ChangeEmoji_Click(object sender, RoutedEventArgs e)
    {
        var picker = new EmojiPickerWindow { Owner = this };
        if (picker.ShowDialog() != true || picker.Result is null) return;

        _groupImage = picker.Result;
        _iconChanged = true;
        _gridIcon = false;
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
            ShortcutList = _shortcuts.Select(s => s.Shortcut).ToList(),
            PanelLayout = SelectedLayout(),
            Width = SelectedColumns(),
            ShowLabels = LabelsToggle.IsChecked == true,
            ShowHeader = HeaderToggle.IsChecked == true,
            HeaderPosition = SelectedHeaderPosition(),
            IconScale = SelectedIconScale(),
            GridIcon = _gridIcon
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

        // The pinned button's right-click menu: "Open all" and "Edit group".
        GroupJumpList.Register(name, Loc.Get("Loc_Jump_OpenAll"), Loc.Get("Loc_Jump_Edit"));

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
            {
                DeleteOldGroup(_originalName!);
                GroupJumpList.Remove(_originalName!);
            }
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
                _originalName ?? currentName, Paths.PanelLauncher);
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
