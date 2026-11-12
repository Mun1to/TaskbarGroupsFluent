using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TaskbarGroups.Core;

namespace TaskbarGroups.Hover;

/// <summary>
/// The right-click menus of the icons by the clock, drawn like the menus of
/// Windows 11 and of the app's own panel instead of the old Windows look (issue
/// #14): rounded corners, room around each line, a soft rounded highlight, and
/// light or dark as the app is. It stays a WinForms menu, redrawn: loading WPF
/// to show five lines would make this small resident program a large one.
/// </summary>
internal static class FluentMenu
{
    /// <summary>
    /// A menu dressed this way. Its colours and font are chosen each time it opens,
    /// so a change of theme or language in the app reaches it without a restart;
    /// handlers the caller adds to Opening run after that, so the items it adds
    /// there pick up the font.
    /// </summary>
    public static ContextMenuStrip Create()
    {
        var renderer = new Renderer();
        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Renderer = renderer
        };
        menu.Opening += (_, _) => Dress(menu, renderer);
        menu.ItemAdded += (_, e) => { if (e.Item is not null) Space(menu, e.Item); };
        menu.HandleCreated += (_, _) => Round(menu);
        menu.Opened += (_, _) => Border(menu, renderer.Colors);
        return menu;
    }

    private static float Scale(ToolStrip strip) => strip.DeviceDpi / 96f;

    private static void Dress(ContextMenuStrip menu, Renderer renderer)
    {
        renderer.Colors = UiPreferences.IsLight() ? Palette.Light : Palette.Dark;
        float s = Scale(menu);
        menu.Font = MenuFont;
        menu.BackColor = renderer.Colors.Back;
        menu.ForeColor = renderer.Colors.Text;
        menu.Padding = new Padding((int)Math.Round(4 * s));
        menu.MinimumSize = new Size((int)Math.Round(200 * s), 0);
        // Windows 11 rounds the menu and casts its shadow; the square shadow WinForms
        // adds on its own would stick out past the rounded corners.
        menu.DropShadowEnabled = !RoundedByWindows;
    }

    /// <summary>Room around each line, as in the menus of Windows 11.</summary>
    private static void Space(ToolStrip menu, ToolStripItem item)
    {
        float s = Scale(menu);
        int px(float v) => (int)Math.Round(v * s);
        if (item is ToolStripSeparator)
        {
            item.Margin = new Padding(0, px(2), 0, px(2));
            return;
        }
        item.Padding = new Padding(px(4), px(6), px(12), px(6));
    }

    /// <summary>Segoe UI Variable where Windows has it (11), Segoe UI elsewhere, at 14 px.</summary>
    private static readonly Font MenuFont = PickFont();

    private static Font PickFont()
    {
        var variable = new Font("Segoe UI Variable Text", 10.5f);
        if (variable.Name == "Segoe UI Variable Text") return variable;
        variable.Dispose();
        return new Font("Segoe UI", 10.5f);
    }

    /// <summary>Windows 11 rounds the corners of windows that ask for it, and draws their border.</summary>
    private static readonly bool RoundedByWindows = Environment.OSVersion.Version.Build >= 22000;

    private static void Round(ContextMenuStrip menu)
    {
        if (!RoundedByWindows) return;
        int corner = DWMWCP_ROUNDSMALL;
        DwmSetWindowAttribute(menu.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
    }

    private static void Border(ContextMenuStrip menu, Palette colors)
    {
        if (!RoundedByWindows || !menu.IsHandleCreated) return;
        int color = ColorTranslator.ToWin32(colors.Border);
        DwmSetWindowAttribute(menu.Handle, DWMWA_BORDER_COLOR, ref color, sizeof(int));
    }

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWCP_ROUNDSMALL = 3;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

    /// <summary>The colours of the menus of Windows 11, light and dark.</summary>
    internal sealed record Palette(Color Back, Color Text, Color Disabled, Color Highlight, Color Separator, Color Border)
    {
        public static readonly Palette Light = new(
            Color.FromArgb(0xF9, 0xF9, 0xF9), Color.FromArgb(0x1A, 0x1A, 0x1A), Color.FromArgb(0x8A, 0x8A, 0x8A),
            Color.FromArgb(0xEC, 0xEC, 0xEC), Color.FromArgb(0xE5, 0xE5, 0xE5), Color.FromArgb(0xDD, 0xDD, 0xDD));

        public static readonly Palette Dark = new(
            Color.FromArgb(0x2C, 0x2C, 0x2C), Color.FromArgb(0xFF, 0xFF, 0xFF), Color.FromArgb(0x9A, 0x9A, 0x9A),
            Color.FromArgb(0x3A, 0x3A, 0x3A), Color.FromArgb(0x40, 0x40, 0x40), Color.FromArgb(0x48, 0x48, 0x48));
    }

    private sealed class Renderer : ToolStripRenderer
    {
        public Palette Colors = Palette.Light;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            => e.Graphics.Clear(Colors.Back);

        // Windows 11 draws the border round the rounded corners; anywhere else it is drawn here.
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (RoundedByWindows) return;
            using var pen = new Pen(Colors.Border);
            var r = new Rectangle(Point.Empty, e.ToolStrip.Size);
            r.Width--;
            r.Height--;
            e.Graphics.DrawRectangle(pen, r);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            float s = e.ToolStrip?.DeviceDpi / 96f ?? 1f;
            var r = new RectangleF(0, 0, e.Item.Width - 1, e.Item.Height - 1);
            using var path = RoundedRect(r, 4 * s);
            using var fill = new SolidBrush(Colors.Highlight);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Colors.Text : Colors.Disabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using var pen = new Pen(Colors.Separator);
            e.Graphics.DrawLine(pen, 0, y, e.Item.Width, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Colors.Text;
            base.OnRenderArrow(e);
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
