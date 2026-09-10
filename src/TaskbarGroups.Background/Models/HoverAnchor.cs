using System;
using System.Globalization;

namespace TaskbarGroups.Background.Models;

/// <summary>
/// The taskbar button that opened this flyout by hover, in physical screen pixels.
///
/// It matters because a flyout opened by hover has to close by hover too, and
/// "the cursor left the panel" is the wrong test: the panel floats a few pixels
/// above the taskbar, so moving from the icon into the panel crosses a strip that
/// belongs to neither and would slam it shut on the way in. Knowing where the icon
/// is lets the flyout treat it, and the gap, as part of itself.
/// </summary>
public sealed record HoverAnchor(int Left, int Top, int Right, int Bottom)
{
    /// <summary>
    /// Reads the "--hover=left,top,right,bottom" argument, or returns null if it is
    /// absent or malformed, which just means the flyout behaves as a clicked one.
    /// </summary>
    public static HoverAnchor? Parse(string? argument)
    {
        const string flag = "--hover=";
        if (string.IsNullOrWhiteSpace(argument) ||
            !argument.StartsWith(flag, StringComparison.OrdinalIgnoreCase))
            return null;

        string[] parts = argument[flag.Length..].Split(',');
        if (parts.Length != 4) return null;

        var n = new int[4];
        for (int i = 0; i < 4; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i]))
                return null;
        }

        return new HoverAnchor(n[0], n[1], n[2], n[3]);
    }

    public bool Contains(int x, int y, int margin = 0)
        => x >= Left - margin && x < Right + margin
        && y >= Top - margin && y < Bottom + margin;

    public int CenterX => Left + (Right - Left) / 2;
    public int CenterY => Top + (Bottom - Top) / 2;
}
