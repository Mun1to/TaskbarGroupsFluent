using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace TaskbarGroups.App.Helpers;

/// <summary>
/// The line that shows where a dragged row, or a dropped file, will land in a
/// list. Drawn over the list rather than inside it, so the rows do not shift
/// about under the cursor while it moves.
/// </summary>
internal sealed class DropLine : Adorner
{
    private double _y = double.NaN;

    public DropLine(UIElement list) : base(list)
    {
        IsHitTestVisible = false;
    }

    /// <summary>Puts the line at <paramref name="y"/>, in the list's own coordinates.</summary>
    public void MoveTo(double y)
    {
        if (y == _y) return;
        _y = y;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (double.IsNaN(_y)) return;

        var brush = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DodgerBlue;
        var pen = new Pen(brush, 2.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        double width = ((FrameworkElement)AdornedElement).ActualWidth;
        dc.DrawLine(pen, new Point(4, _y), new Point(width - 4, _y));
    }
}
