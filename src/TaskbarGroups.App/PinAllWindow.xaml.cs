using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TaskbarGroups.App.Helpers;
using TaskbarGroups.App.Models;
using TaskbarGroups.Core;
using Wpf.Ui.Controls;

namespace TaskbarGroups.App;

/// <summary>
/// Pins the groups inside a group to the taskbar, one after another, in its order
/// and with taskbar separators where it has lines (issue #8). Windows lets no app
/// pin by itself, so the user still makes each right-click; this window keeps the
/// next shortcut selected in File Explorer, sees each pin land in the folder where
/// Windows keeps them, and moves on by itself. See <see cref="PinPlan"/>.
/// </summary>
public partial class PinAllWindow : FluentWindow
{
    private sealed record Row(ImageSource? Icon, string Name, string State, FontWeight Weight);

    private readonly List<PinStep> _steps;
    private readonly DispatcherTimer _watch = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private PinStep? _current;

    public PinAllWindow(Category main)
    {
        InitializeComponent();
        AppTheme.Watch(this);

        HeadingText.Text = Loc.Format("Loc_PinAll_Heading", main.Name);
        _steps = PinPlan.Build(main, Loc.Get("Loc_Separator_DefaultName"));

        // Over at the right, clear of the File Explorer window, which opens in the
        // middle. Placed once it has its real size: the window style holds it to a
        // minimum width of its own.
        Rect area = SystemParameters.WorkArea;
        Left = area.Right - Width - 24;
        Top = area.Top + 24;

        _watch.Tick += (_, _) => Check();
        Loaded += (_, _) =>
        {
            Left = area.Right - ActualWidth - 24;
            Next();
            _watch.Start();
        };
        Closed += (_, _) => _watch.Stop();
    }

    // Pinned, the shortcut shows up in the folder of pins within a moment, and then
    // the next one is put in front of the user.
    private void Check()
    {
        if (_current is null) return;
        PinPlan.Refresh(_steps);
        if (_current.Pinned) Next();
        else Render();
    }

    private void Next()
    {
        _current = _steps.FirstOrDefault(s => s.Pending);
        if (_current is not null) Point();
        Render();
    }

    // The folder with the shortcut to pin selected. Opened again on every step, as
    // File Explorer moves the selection in the window it already has open there.
    private void Point()
    {
        if (_current is not null && File.Exists(_current.ShortcutPath))
            ShellHelper.SelectInExplorer(_current.ShortcutPath);
    }

    private void Render()
    {
        var toPin = _steps.Where(s => !s.WasPinned).ToList();
        StepsList.ItemsSource = _steps.Select(s => new Row(
            IconOf(s),
            NameOf(s),
            s.WasPinned ? Loc.Get("Loc_PinAll_Already")
                : s.Pinned ? Loc.Get("Loc_PinAll_Done")
                : s.Skipped ? Loc.Get("Loc_PinAll_Skipped")
                : ReferenceEquals(s, _current) ? Loc.Get("Loc_PinAll_Now")
                : "",
            ReferenceEquals(s, _current) ? FontWeights.SemiBold : FontWeights.Normal)).ToList();

        bool done = _current is null;
        SkipButton.Visibility = done ? Visibility.Collapsed : Visibility.Visible;
        ReopenButton.Visibility = done ? Visibility.Collapsed : Visibility.Visible;
        CloseButton.Appearance = done ? ControlAppearance.Primary : ControlAppearance.Secondary;

        if (_steps.Count == 0)
        {
            NowTitle.Text = Loc.Get("Loc_PinAll_Nothing");
            NowSteps.Visibility = Visibility.Collapsed;
        }
        else if (done)
        {
            NowTitle.Text = Loc.Get(_steps.Any(s => s.Skipped) ? "Loc_PinAll_FinishedSkipped" : "Loc_PinAll_Finished");
            NowSteps.Visibility = Visibility.Collapsed;
        }
        else
        {
            NowTitle.Text = Loc.Format("Loc_PinAll_NowTitle", toPin.IndexOf(_current!) + 1, toPin.Count, NameOf(_current!));
            NowSteps.Text = Loc.Get(WindowsVersion.IsWindows11 ? "Loc_PinAll_Steps" : "Loc_PinAll_Steps10");
            NowSteps.Visibility = Visibility.Visible;
        }

        NoteText.Text = Loc.Get(_steps.Any(s => s.WasPinned) ? "Loc_PinAll_NoteKept" : "Loc_PinAll_NoteOrder");
    }

    private static string NameOf(PinStep step) => step.IsSeparator ? Loc.Get("Loc_Group_Separator") : step.GroupName;

    private static ImageSource? IconOf(PinStep step)
    {
        if (step.IsSeparator) return ShortcutEntry.SeparatorGlyph;
        string png = Path.Combine(Paths.ConfigPath, step.GroupName, "GroupImage.png");
        if (!File.Exists(png)) return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.UriSource = new Uri(png);
            bitmap.DecodePixelWidth = 44;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        _current.Skipped = true;
        Next();
    }

    private void Reopen_Click(object sender, RoutedEventArgs e) => Point();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
