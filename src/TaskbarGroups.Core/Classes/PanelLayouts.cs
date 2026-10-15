using System;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// The shapes a group's flyout can take, and the one rule that turns a group's
    /// settings into a layout. The flyout and the editor's preview both go through
    /// <see cref="Plan"/>, so the preview cannot drift from what actually opens.
    ///
    /// Everything is stored as text in ObjectData.xml so the file stays readable,
    /// and an unknown value from a newer version degrades to the grid instead of
    /// failing to load.
    /// </summary>
    public static class PanelLayouts
    {
        public const string Grid = "Grid";
        public const string Row = "Row";
        public const string Column = "Column";
        public const string List = "List";

        public const string HeaderTop = "Top";
        public const string HeaderBottom = "Bottom";

        /// <summary>Icon sizes offered to the user, as a percentage of the normal one.</summary>
        public static readonly int[] IconScales = { 80, 100, 125 };

        // Width of one cell in each template, margins included. They have to match
        // the sizes in the flyout's XAML, or columns wrap a cell too early or late.
        private const double LabelledCell = 84;
        private const double IconCell = 56;
        private const double ListCell = 202;

        /// <summary>Which items panel and item template to use, and how wide the items may get.</summary>
        public readonly struct PanelPlan
        {
            public PanelPlan(string panel, string template, double maxWidth, bool usesColumns, bool usesLabels)
            {
                Panel = panel;
                Template = template;
                MaxWidth = maxWidth;
                UsesColumns = usesColumns;
                UsesLabels = usesLabels;
            }

            /// <summary>"Grid" (wrapping), "Row" or "Column".</summary>
            public string Panel { get; }

            /// <summary>"TileWithLabel", "TileIconOnly", "LineWithLabel" or "ListCell".</summary>
            public string Template { get; }

            public double MaxWidth { get; }

            /// <summary>Whether the column count means anything for this layout.</summary>
            public bool UsesColumns { get; }

            /// <summary>Whether showing or hiding names means anything for this layout.</summary>
            public bool UsesLabels { get; }
        }

        public static PanelPlan Plan(string layout, bool showLabels, int columns, int appCount)
        {
            int count = Math.Max(appCount, 1);

            if (Is(layout, Row))
                return new PanelPlan("Row", showLabels ? "TileWithLabel" : "TileIconOnly",
                    double.PositiveInfinity, false, true);

            if (Is(layout, Column))
                return new PanelPlan("Column", showLabels ? "LineWithLabel" : "TileIconOnly",
                    double.PositiveInfinity, false, true);

            if (Is(layout, List))
            {
                // A list always shows names: that is what sets it apart from a grid.
                // Past six apps it splits in two, so it never runs up the screen.
                int listColumns = columns > 0 ? columns : (count > 6 ? 2 : 1);
                return new PanelPlan("Grid", "ListCell", listColumns * ListCell + 1, true, false);
            }

            int gridColumns = columns > 0 ? columns : Math.Clamp(count, 1, showLabels ? 5 : 4);
            // A hair over the exact width, so rounding at odd DPI scales never pushes
            // the last column onto a row of its own.
            double cell = showLabels ? LabelledCell : IconCell;
            return new PanelPlan("Grid", showLabels ? "TileWithLabel" : "TileIconOnly",
                gridColumns * cell + 1, true, true);
        }

        public static bool IsRow(string layout) => Is(layout, Row);
        public static bool IsColumn(string layout) => Is(layout, Column);
        public static bool IsList(string layout) => Is(layout, List);
        public static bool IsBottom(string headerPosition) => Is(headerPosition, HeaderBottom);

        /// <summary>A stored icon scale, snapped to one of the offered sizes.</summary>
        public static double Scale(int iconScale)
            => Array.IndexOf(IconScales, iconScale) >= 0 ? iconScale / 100.0 : 1.0;

        private static bool Is(string value, string expected)
            => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }
}
