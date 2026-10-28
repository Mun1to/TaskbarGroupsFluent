using System.Windows;
using System.Windows.Controls;
using TaskbarGroups.Background.Models;

namespace TaskbarGroups.Background.Helpers;

/// <summary>
/// Picks how each entry of the flyout is drawn: the layout's own look for an app,
/// and a line for a separator, across the panel or, in a row, up and down.
/// </summary>
public sealed class PopupItemTemplates : DataTemplateSelector
{
    public DataTemplate? App { get; init; }
    public DataTemplate? Separator { get; init; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        => item is PopupItem { IsSeparator: true } ? Separator : App;
}
