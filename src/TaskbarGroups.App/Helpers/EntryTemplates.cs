using System.Windows;
using System.Windows.Controls;
using TaskbarGroups.App.Models;

namespace TaskbarGroups.App.Helpers;

/// <summary>
/// Picks how each entry of the editor's preview is drawn, the same way the
/// flyout does: the layout's own look for an app, and a line for a separator.
/// </summary>
public sealed class EntryTemplates : DataTemplateSelector
{
    public DataTemplate? App { get; init; }
    public DataTemplate? Separator { get; init; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item is ShortcutEntry { IsSeparator: true } ? Separator : App;
}
