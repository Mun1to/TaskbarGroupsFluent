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

    // A group that shows a folder (issue #9): which folder, how much is in it, and a
    // taste of it for the preview. The apps list is kept as it was meanwhile, so
    // going back to it loses nothing.
    private string _folderSource = "";
    private int _folderCount;
    private readonly ObservableCollection<ShortcutEntry> _folderPreview = new();

    // The taskbar icon is the folder's own until the user picks one, and follows
    // the folder if another is chosen.
    private bool _iconFromFolder;

    /// <summary>How much of a folder the preview shows: enough to see the layout.</summary>
    private const int PreviewEntries = 24;

    private bool FolderMode => SourceFolderRadio.IsChecked == true;

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
            if (existing.IsFolderGroup)
            {
                SourceFolderRadio.IsChecked = true;
                if (!ShowFolder(existing.FolderSource))
                {
                    // Gone since it was saved (a drive unplugged, a folder deleted):
                    // kept as it was, so saving the group's other changes keeps it too.
                    _folderSource = existing.FolderSource;
                    FolderNameText.Text = existing.FolderSource;
                    FolderPathText.Text = Loc.Get("Loc_Editor_FolderMissing");
                }
            }
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
        TrayToggle.IsChecked = _wasInTray = category.ShowInTray;
    }

    /// <summary>
    /// Whether the group was in the notification area when the editor opened. The
    /// resident program is only started or stopped when that changes: a save that
    /// leaves it alone must not touch a process the user has running for other
    /// reasons, nor its startup entry.
    /// </summary>
    private bool _wasInTray;

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
            SelectedColumns(), AppCount());
        PreviewItems.ItemsPanel = (System.Windows.Controls.ItemsPanelTemplate)Resources["Preview" + plan.Panel + "Panel"];
        PreviewItems.ItemTemplate = null;
        PreviewItems.ItemTemplateSelector = new EntryTemplates
        {
            App = (DataTemplate)Resources["Preview" + plan.Template],
            Separator = (DataTemplate)Resources[plan.Panel == "Row" ? "PreviewSeparatorDown" : "PreviewSeparatorAcross"]
        };
        PreviewItems.MaxWidth = plan.MaxWidth;
        Resources["SeparatorRowWidth"] = double.IsInfinity(plan.MaxWidth) ? double.NaN : plan.MaxWidth - 1;
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

    /// <summary>
    /// The entries that open something, leaving out the separators; for a group
    /// that shows a folder, what is in the folder.
    /// </summary>
    private int AppCount() => FolderMode ? _folderCount : _shortcuts.Count(s => !s.IsSeparator);

    /// <summary>The entries the taskbar icon can be built from, in order.</summary>
    private IEnumerable<ShortcutEntry> IconSources()
        => FolderMode ? _folderPreview : _shortcuts.Where(s => !s.IsSeparator);

    private void GridIcon_Click(object sender, RoutedEventArgs e)
    {
        if (!IconSources().Any())
        {
            ShowError(Loc.Get("Loc_Editor_GridIconNeedsApps"));
            return;
        }
        _gridIcon = true;
        _iconFromFolder = false;
        RebuildGridIcon();
    }

    private void RebuildGridIcon()
    {
        var icons = new List<DrawingImage>();
        foreach (var entry in IconSources().Take(4))
        {
            try
            {
                DrawingImage? icon = entry.Shortcut.IsShellItem
                    ? ShellFolder.Icon(entry.Shortcut.ShellItem, 128)
                    : Category.ResolveShortcutImage(entry.Shortcut);
                if (icon is not null) icons.Add(icon);
            }
            catch { /* an app without an icon just leaves its cell to the others */ }
        }
        if (icons.Count == 0) return;

        _groupImage = GroupIconGrid.Compose(icons);
        _iconChanged = true;
        GroupIconPreview.Source = _groupImage.ToImageSource();
        GroupIconPlaceholder.Visibility = Visibility.Collapsed;
    }

    // ---- A group that shows a folder (issue #9) -----------------------------

    private void Source_Changed(object sender, RoutedEventArgs e)
    {
        // Raised while the window is still being built, before the rest exists.
        if (AppsSection is null || FolderSection is null || PreviewItems is null) return;

        bool folder = FolderMode;
        AppsSection.Visibility = folder ? Visibility.Collapsed : Visibility.Visible;
        FolderSection.Visibility = folder ? Visibility.Visible : Visibility.Collapsed;
        PreviewItems.ItemsSource = folder ? _folderPreview : _shortcuts;

        // A folder reads best as a list, like a menu: a new group switches to it,
        // unless the user already picked another shape.
        if (folder && !_isEditing && !_loadingPanel && LayoutGridRadio.IsChecked == true)
            LayoutListRadio.IsChecked = true;

        if (_gridIcon) RebuildGridIcon();
        UpdatePreview();
    }

    /// <summary>
    /// Shows <paramref name="source"/> as the group's folder, with a taste of what
    /// is in it in the preview. False, with nothing changed, if it is no folder.
    /// </summary>
    private bool ShowFolder(string source)
    {
        if (!ShellFolder.IsFolder(source)) return false;

        _folderSource = source;
        FolderNameText.Text = FolderTitle(source);
        FolderPathText.Text = Path.IsPathRooted(source) ? source : Loc.Get("Loc_Editor_FolderSpecial");
        using (var icon = ShellFolder.Icon(source, 64))
            FolderIcon.Source = icon.ToImageSource();
        FolderIconPlaceholder.Visibility = FolderIcon.Source is null ? Visibility.Visible : Visibility.Collapsed;

        _folderPreview.Clear();
        var entries = ShellFolder.List(source, out bool more);
        _folderCount = entries.Count + (more ? 1 : 0);
        foreach (var entry in entries.Take(PreviewEntries))
        {
            using var bitmap = ShellFolder.Icon(entry.Id, 48);
            _folderPreview.Add(new ShortcutEntry { Shortcut = ProgramShortcut.ForShellEntry(entry), Icon = bitmap.ToImageSource() });
        }
        return true;
    }

    /// <summary>A folder's name as File Explorer shows it; "All apps" for the one Windows leaves in English.</summary>
    internal static string FolderTitle(string source)
        => string.Equals(source, ShellFolder.AllApps, StringComparison.OrdinalIgnoreCase)
            ? Loc.Get("Loc_Editor_AllApps")
            : ShellFolder.DisplayName(source) ?? source;

    /// <summary>
    /// The user picked a folder. Its name becomes the group's if there is none
    /// yet, and its icon the taskbar icon unless the user chose one of their own.
    /// </summary>
    private void ChooseFolder(string source)
    {
        if (!ShowFolder(source))
        {
            ShowError(Loc.Get("Loc_Editor_FolderMissing"));
            return;
        }

        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            NameTextBox.Text = SafeName(FolderNameText.Text);

        if (_groupImage is null || _iconFromFolder)
        {
            using var big = ShellFolder.Icon(source, 256);
            if (big is not null)
            {
                _groupImage = new Bitmap(big);
                _iconChanged = true;
                _iconFromFolder = true;
                _gridIcon = false;
                GroupIconPreview.Source = _groupImage.ToImageSource();
                GroupIconPlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        if (_gridIcon) RebuildGridIcon();
        UpdatePreview();
    }

    /// <summary>A folder's name made fit to name a group: no characters a file name cannot have, no dot at the end.</summary>
    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string((name ?? "").Where(c => Array.IndexOf(invalid, c) < 0).ToArray()).Trim().TrimEnd('.').Trim();
    }

    private void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Loc.Get("Loc_Editor_PickFolder") };
        if (Directory.Exists(_folderSource)) dialog.InitialDirectory = _folderSource;
        if (dialog.ShowDialog(this) != true) return;
        ChooseFolder(dialog.FolderName);
    }

    // The places of Windows that are no folder on disk, and so cannot be picked
    // with the folder dialog: the Control Panel, This PC, all the apps. A few
    // common folders join them for convenience. Each is named as Windows names it.
    private void SpecialFolders_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button) return;

        var menu = new System.Windows.Controls.ContextMenu
        {
            PlacementTarget = button,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };
        foreach (string source in ShellFolder.Presets)
        {
            if (!ShellFolder.IsFolder(source)) continue;
            var item = new System.Windows.Controls.MenuItem { Header = FolderTitle(source) };
            using (var icon = ShellFolder.Icon(source, 32))
            {
                var image = icon.ToImageSource();
                if (image is not null)
                    item.Icon = new System.Windows.Controls.Image { Source = image, Width = 16, Height = 16 };
            }
            string chosen = source;
            item.Click += (_, _) => ChooseFolder(chosen);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
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
        if (ps.IsSeparator) return new ShortcutEntry { Shortcut = ps, Icon = ShortcutEntry.SeparatorGlyph };

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
    // this one: a group that opens itself would just reopen the same panel. Nor
    // the taskbar's separators, which open nothing and only look out of place in
    // a panel; the panel has its own, below.
    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button) return;

        string self = _originalName ?? (NameTextBox.Text ?? "").Trim();
        var others = Services.GroupService.LoadAll()
            .Where(g => !g.IsSeparator)
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

    // A line across the panel. It goes in at the end, and is dragged from there to
    // where it belongs; its title is set from its options, like an app's name.
    private void AddSeparator_Click(object sender, RoutedEventArgs e)
    {
        _shortcuts.Add(BuildEntry(ProgramShortcut.ForSeparator()));
        Dispatcher.BeginInvoke(() =>
            (ShortcutsItemsControl.ItemContainerGenerator.ContainerFromIndex(_shortcuts.Count - 1) as FrameworkElement)
                ?.BringIntoView(), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private bool AddShortcut(ProgramShortcut ps, int at = -1)
    {
        // The same app twice is a slip; the same separator twice is two sections.
        if (!ps.IsSeparator
            && _shortcuts.Any(s => s.Shortcut.FilePath.Equals(ps.FilePath, StringComparison.OrdinalIgnoreCase)))
            return false;

        var entry = BuildEntry(ps);
        if (at < 0 || at > _shortcuts.Count) _shortcuts.Add(entry);
        else _shortcuts.Insert(at, entry);
        return true;
    }

    // Files, shortcuts and folders dragged in from File Explorer or the desktop,
    // in the order they came, from position at (or at the end).
    private void AddDropped(string[] paths, int at)
    {
        foreach (string path in paths)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) continue;
            if (AddShortcut(ProgramShortcut.ForPath(path), at) && at >= 0) at++;
        }
    }

    // Dropped anywhere but the list: they go at the end.
    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

        // A group that shows a folder takes a folder dropped on it as that folder.
        if (FolderMode)
        {
            string? folder = paths.FirstOrDefault(Directory.Exists);
            if (folder is not null) ChooseFolder(folder);
            return;
        }
        AddDropped(paths, -1);
    }

    // Dragging rows. A row is picked up by pressing anywhere on it but its
    // buttons, and dropped where the line shows. The same line marks where
    // dropped files go in.

    private const string RowFormat = "TaskbarGroupsFluent.EditorRow";
    private System.Windows.Point _pressedAt;
    private ShortcutEntry? _pressed;
    private DropLine? _dropLine;

    private void Row_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _pressed = IsOnButton(e.OriginalSource as DependencyObject, sender as DependencyObject)
            ? null
            : (sender as FrameworkElement)?.DataContext as ShortcutEntry;
        _pressedAt = e.GetPosition(this);
    }

    private void Row_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_pressed is null || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;

        System.Windows.Point now = e.GetPosition(this);
        if (Math.Abs(now.X - _pressedAt.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(now.Y - _pressedAt.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var entry = _pressed;
        _pressed = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(RowFormat, entry), DragDropEffects.Move);
        HideDropLine();
    }

    private static bool IsOnButton(DependencyObject? from, DependencyObject? row)
    {
        for (var d = from; d is not null && d != row; d = System.Windows.Media.VisualTreeHelper.GetParent(d))
            if (d is System.Windows.Controls.Primitives.ButtonBase) return true;
        return false;
    }

    private void List_DragOver(object sender, DragEventArgs e)
    {
        bool row = e.Data.GetDataPresent(RowFormat);
        bool files = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = row ? DragDropEffects.Move : files ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        if (!row && !files) { HideDropLine(); return; }

        ShowDropLine(DropIndex(e));
        ScrollNearEdges(e);
    }

    private void List_DragLeave(object sender, DragEventArgs e) => HideDropLine();

    private void List_Drop(object sender, DragEventArgs e)
    {
        int at = DropIndex(e);
        HideDropLine();
        e.Handled = true;

        if (e.Data.GetData(RowFormat) is ShortcutEntry entry)
        {
            int from = _shortcuts.IndexOf(entry);
            if (from < 0) return;
            // Taking the row out first moves everything after it up by one.
            if (at > from) at--;
            if (at != from) _shortcuts.Move(from, at);
        }
        else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            AddDropped(paths, at);
        }
    }

    /// <summary>The position in the list the cursor points at: before the first row whose middle is below it.</summary>
    private int DropIndex(DragEventArgs e)
    {
        for (int i = 0; i < _shortcuts.Count; i++)
        {
            if (ShortcutsItemsControl.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement row) continue;
            if (e.GetPosition(row).Y < row.ActualHeight / 2) return i;
        }
        return _shortcuts.Count;
    }

    private void ShowDropLine(int index)
    {
        if (_dropLine is null)
        {
            var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(ShortcutsItemsControl);
            if (layer is null) return;
            _dropLine = new DropLine(ShortcutsItemsControl);
            layer.Add(_dropLine);
        }

        // In the gap above the row it goes before, or under the last one. Each row
        // carries an 8 px gap below it, so the middle of that gap is 4 px up.
        double y = 0;
        int count = _shortcuts.Count;
        if (count > 0)
        {
            int reference = Math.Min(index, count - 1);
            if (ShortcutsItemsControl.ItemContainerGenerator.ContainerFromIndex(reference) is FrameworkElement row)
            {
                System.Windows.Point top = row.TranslatePoint(new System.Windows.Point(0, 0), ShortcutsItemsControl);
                y = index < count ? Math.Max(1, top.Y - 4) : top.Y + row.ActualHeight - 4;
            }
        }
        _dropLine.MoveTo(y);
    }

    private void HideDropLine()
    {
        if (_dropLine is null) return;
        System.Windows.Documents.AdornerLayer.GetAdornerLayer(ShortcutsItemsControl)?.Remove(_dropLine);
        _dropLine = null;
    }

    // A long list runs past the window: holding the drag near the top or the
    // bottom edge scrolls it.
    private void ScrollNearEdges(DragEventArgs e)
    {
        const double edge = 48;
        double y = e.GetPosition(EditorScroll).Y;
        if (y < edge) EditorScroll.ScrollToVerticalOffset(EditorScroll.VerticalOffset - 12);
        else if (y > EditorScroll.ActualHeight - edge) EditorScroll.ScrollToVerticalOffset(EditorScroll.VerticalOffset + 12);
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
        // redraw its row and the preview with the new name and the icon the
        // options may have changed.
        int i = _shortcuts.IndexOf(entry);
        if (i >= 0) _shortcuts[i] = BuildEntry(entry.Shortcut);
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
        _iconFromFolder = false;
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
        _iconFromFolder = false;
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
        if (FolderMode)
        {
            if (string.IsNullOrWhiteSpace(_folderSource))
            {
                ShowError(Loc.Get("Loc_Editor_NeedFolder"));
                return;
            }
        }
        else if (AppCount() == 0)
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
            GridIcon = _gridIcon,
            FolderSource = FolderMode ? _folderSource : "",
            ShowInTray = TrayToggle.IsChecked == true
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

        // The pinned button's right-click menu: "Open all", "Edit group" and the app.
        GroupJumpList.Register(name, Loc.Get(FolderMode ? "Loc_Jump_OpenFolder" : "Loc_Jump_OpenAll"),
            Loc.Get("Loc_Jump_Edit"), Loc.Get("Loc_Jump_OpenApp"));

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

        // Ticking or unticking "show by the clock" may be the only reason the resident
        // program has to run, or to stop. One already running notices the change by
        // itself; this starts it when it is not, and keeps the startup entry right.
        // After a rename's cleanup, so the old folder no longer counts.
        if (category.ShowInTray != _wasInTray) HoverService.Sync();

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
            // On a thread that keeps the app alive until the taskbar is back: opened
            // from the pinned group, the app closes as soon as this window does.
            TaskbarPin.RestartExplorerInBackground();
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
