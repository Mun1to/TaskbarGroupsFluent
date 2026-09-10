using System.Windows;
using System.Windows.Controls;
using TaskbarGroups.App.Helpers;
using TaskbarGroups.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;

namespace TaskbarGroups.App;

/// <summary>
/// Application settings. Currently one feature lives here, hover-to-open, which
/// needs more than a stored flag: it runs as a resident process, so the switch
/// starts and stops it and registers it to run at sign-in.
/// </summary>
public partial class SettingsWindow : FluentWindow
{
    /// <summary>
    /// The dwell choices, in milliseconds. Anything much under a fifth of a second
    /// fires while merely crossing the taskbar; much over a second stops feeling
    /// like a response to what the user did.
    /// </summary>
    private static readonly int[] Delays = { 200, 400, 700 };

    private bool _loading = true;

    public SettingsWindow()
    {
        InitializeComponent();
        SystemThemeWatcher.Watch(this);

        DelayBox.Items.Add(Loc.Get("Loc_Settings_DelayFast"));
        DelayBox.Items.Add(Loc.Get("Loc_Settings_DelayNormal"));
        DelayBox.Items.Add(Loc.Get("Loc_Settings_DelayRelaxed"));

        HoverToggle.IsChecked = Settings.settingInfo.hoverToOpen;
        DelayBox.SelectedIndex = ClosestDelayIndex(Settings.settingInfo.hoverDelayMs);
        UpdateHoverUi();

        _loading = false;
    }

    private static int ClosestDelayIndex(int ms)
    {
        int best = 1;
        int bestGap = int.MaxValue;
        for (int i = 0; i < Delays.Length; i++)
        {
            int gap = System.Math.Abs(Delays[i] - ms);
            if (gap < bestGap) { bestGap = gap; best = i; }
        }
        return best;
    }

    private async void HoverToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        bool wanted = HoverToggle.IsChecked == true;
        bool ok = HoverService.Apply(wanted);

        // The only way this fails is the watcher's executable not being there,
        // which happens when the app runs from a build tree. Say so instead of
        // leaving a switch that looks on while nothing is watching.
        if (wanted && !ok)
        {
            _loading = true;
            HoverToggle.IsChecked = false;
            _loading = false;

            await new MessageBox
            {
                Title = Loc.Get("Loc_Settings_Title"),
                Content = Loc.Get("Loc_Settings_HoverMissing"),
                CloseButtonText = Loc.Get("Loc_Common_Close")
            }.ShowDialogAsync();
        }

        UpdateHoverUi();
    }

    private void DelayBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;

        int index = DelayBox.SelectedIndex;
        if (index < 0 || index >= Delays.Length) return;

        Settings.settingInfo.hoverDelayMs = Delays[index];
        Settings.writeXML();

        // A running watcher sees the file change and picks the new delay up itself,
        // so nothing is stopped here. This used to stop and start it, and losing
        // that race left the setting switched on with nothing watching: a killed
        // process is still listed for a few milliseconds, long enough for the start
        // to decide one was already running. Start() alone is a no-op when it is,
        // and revives it if it somehow died.
        if (Settings.settingInfo.hoverToOpen) HoverService.Start();
    }

    private void UpdateHoverUi()
    {
        bool on = HoverToggle.IsChecked == true;
        DelayCard.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        HoverNotice.IsOpen = on;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
