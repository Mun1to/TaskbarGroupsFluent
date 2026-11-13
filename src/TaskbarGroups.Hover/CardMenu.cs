using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using TaskbarGroups.Core;

namespace TaskbarGroups.Hover;

/// <summary>
/// The right-click menus of the icons by the clock, drawn by the app as a card in
/// the same style as winshotx and VoCript: a header that says whose menu it is, an
/// icon on every line, a short note under a line where the label alone would not
/// say what it does, and a switch for what stays on instead of a line that turns
/// it off.
///
/// A Windows menu is a list of words: it cannot show a switch, a version or a
/// group's own icon. This one is drawn here, light or dark as the app is, with the
/// Windows accent on what is switched on, and still behaves like a menu: it opens
/// at the cursor, closes on a click anywhere else or on Escape, and the arrow keys
/// and Enter work.
/// </summary>
internal sealed class CardMenu
{
    internal enum Kind { Header, Item, Toggle, Separator }

    internal sealed class Line
    {
        public Kind Kind;
        public string Glyph = "";
        public string Text = "";
        public string? Note;
        public Icon? Icon;
        public bool On;
        public Action? Run;
        public Action<bool>? Set;
    }

    private readonly List<Line> _lines = new();

    internal IReadOnlyList<Line> Lines => _lines;

    /// <summary>The top of the card: whose menu this is, with its icon and, if given, a detail on the right.</summary>
    public void Header(Icon? icon, string title, string? detail = null)
        => _lines.Add(new Line { Kind = Kind.Header, Icon = icon, Text = title, Note = detail });

    /// <summary>A line that does something and closes the menu.</summary>
    public void Add(string glyph, string text, Action run, string? note = null)
        => _lines.Add(new Line { Kind = Kind.Item, Glyph = glyph, Text = text, Note = note, Run = run });

    /// <summary>A line with a switch. It flips in place and the menu stays open.</summary>
    public void Toggle(string glyph, string text, bool on, Action<bool> set, string? note = null)
        => _lines.Add(new Line { Kind = Kind.Toggle, Glyph = glyph, Text = text, Note = note, On = on, Set = set });

    public void Separator()
    {
        // Never two in a row, and never one first: a gap with nothing between reads as a mistake.
        if (_lines.Count == 0 || _lines[^1].Kind is Kind.Separator or Kind.Header) return;
        _lines.Add(new Line { Kind = Kind.Separator });
    }

    /// <summary>
    /// Shows the menu that <paramref name="fill"/> builds whenever the icon is
    /// right-clicked. It is built at that moment, so it is always in the current
    /// language and shows things as they are right then.
    /// </summary>
    public static void Attach(NotifyIcon icon, Action<CardMenu> fill)
    {
        icon.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var menu = new CardMenu();
            fill(menu);
            menu.Show();
        };
    }

    public void Show()
    {
        if (_lines.Count > 0 && _lines[^1].Kind == Kind.Separator) _lines.RemoveAt(_lines.Count - 1);
        Native.GetCursorPos(out POINT cursor);
        CardMenuWindow.Open(_lines, new Point(cursor.X, cursor.Y));
    }

    /// <summary>Segoe Fluent Icons in Windows 11, Segoe MDL2 Assets in Windows 10: the same code points in both.</summary>
    internal static class Glyphs
    {
        public const string App = "";
        public const string Open = "";
        public const string OpenAll = "";
        public const string Folder = "";
        public const string Edit = "";
        public const string Hover = "";
        public const string Instant = "";
        public const string Hide = "";
        public const string Quit = "";
        public const string Empty = "";
        public const string Restore = "";
    }
}

/// <summary>The window that draws a <see cref="CardMenu"/>. One at a time.</summary>
internal sealed class CardMenuWindow : Form
{
    private static CardMenuWindow? _open;

    private readonly List<CardMenu.Line> _lines;
    private readonly List<Rectangle> _rects = new();
    private readonly float _s;
    private readonly Palette _p;
    private readonly bool _rounded;
    private readonly Font _label, _note, _title, _detail, _glyph;
    private readonly System.Windows.Forms.Timer _watch = new() { Interval = 50 };
    private readonly System.Windows.Forms.Timer _fade = new() { Interval = 15 };
    private readonly Stopwatch _clock = new();
    private int _hot = -1;
    private bool _closing;

    public static void Open(List<CardMenu.Line> lines, Point cursor)
    {
        _open?.Close();
        var window = new CardMenuWindow(lines, cursor);
        _open = window;
        window.Show();
        // A menu has to own the foreground, or a click elsewhere would not close it.
        // The right-click on our icon by the clock is what lets this process take it.
        SetForegroundWindow(window.Handle);
        window.Activate();
    }

    private CardMenuWindow(List<CardMenu.Line> lines, Point cursor)
    {
        _lines = lines;
        _s = ScaleAt(cursor);
        _p = Palette.Current();
        _rounded = Environment.OSVersion.Version.Build >= 22000;

        _label = FontOf(15, FontStyle.Regular, "Segoe UI Variable Text Semibold", "Segoe UI Semibold");
        _note = FontOf(13, FontStyle.Regular, "Segoe UI Variable Text", "Segoe UI");
        _title = FontOf(15, FontStyle.Bold, "Segoe UI Variable Text", "Segoe UI");
        _detail = FontOf(13, FontStyle.Regular, "Segoe UI Variable Text", "Segoe UI");
        _glyph = FontOf(16, FontStyle.Regular, "Segoe Fluent Icons", "Segoe MDL2 Assets");

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = _p.Back;
        Text = "Taskbar Groups Fluent";

        Size size = Arrange();
        Bounds = Place(cursor, size);

        _watch.Tick += (_, _) => CloseOnClickOutside();
        _fade.Tick += (_, _) => FadeStep();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_LAYERED;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => false;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Born see-through, so the first frame Windows shows is the empty one, and
        // faded in from there.
        SetLayeredWindowAttributes(Handle, 0, 0, LWA_ALPHA);
        if (_rounded)
        {
            int corner = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            int border = ColorTranslator.ToWin32(_p.Border);
            DwmSetWindowAttribute(Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _clock.Start();
        _fade.Start();
        _watch.Start();
    }

    /// <summary>
    /// About 0.15 s from nothing to the card, as a menu opens in Windows 11. At the
    /// end the window stops being layered, which is what lets Windows 11 round its
    /// corners and draw its border on it.
    /// </summary>
    private void FadeStep()
    {
        double t = Math.Min(1, _clock.Elapsed.TotalMilliseconds / 150.0);
        double eased = 1 - Math.Pow(1 - t, 3);
        if (t >= 1)
        {
            _fade.Stop();
            int ex = GetWindowLong(Handle, GWL_EXSTYLE);
            SetWindowLong(Handle, GWL_EXSTYLE, ex & ~WS_EX_LAYERED);
            RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, RDW_FRAME | RDW_INVALIDATE | RDW_ALLCHILDREN);
            return;
        }
        SetLayeredWindowAttributes(Handle, 0, (byte)(eased * 255), LWA_ALPHA);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        Dismiss();
    }

    /// <summary>
    /// The foreground is what normally closes a menu, but if Windows refused it to
    /// us a click elsewhere would leave the card hanging. So a press outside it
    /// closes it too, checked only while it is open.
    /// </summary>
    private void CloseOnClickOutside()
    {
        if (MouseButtons != MouseButtons.None && !Bounds.Contains(Cursor.Position)) Dismiss();
    }

    private void Dismiss()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _watch.Stop();
        _fade.Stop();
        if (_open == this) _open = null;
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watch.Dispose();
            _fade.Dispose();
            _label.Dispose();
            _note.Dispose();
            _title.Dispose();
            _detail.Dispose();
            _glyph.Dispose();
        }
        base.Dispose(disposing);
    }

    // ---- Layout ------------------------------------------------------------------

    private int S(float px) => (int)Math.Round(px * _s);

    private const float Pad = 4, RowX = 10, GlyphBox = 20, Gap = 12, SwitchW = 36, SwitchH = 20;
    private const float RowH = 38, RowNoteH = 54, HeaderH = 48, SepH = 9, MinW = 280, MaxW = 440;

    /// <summary>Measures every line, sets where each one goes and returns the size of the card.</summary>
    private Size Arrange()
    {
        int width = S(MinW);
        foreach (var line in _lines)
        {
            int w = line.Kind switch
            {
                CardMenu.Kind.Header => S(12 + 24 + 10) + Measure(line.Text, _title)
                    + (line.Note is null ? 0 : S(16) + Measure(line.Note, _detail)) + S(14),
                CardMenu.Kind.Item or CardMenu.Kind.Toggle => S(Pad + RowX + GlyphBox + Gap)
                    + Math.Max(Measure(line.Text, _label), line.Note is null ? 0 : Measure(line.Note, _note))
                    + (line.Kind == CardMenu.Kind.Toggle ? S(16 + SwitchW) : 0) + S(RowX + 6 + Pad),
                _ => 0
            };
            width = Math.Max(width, w);
        }
        width = Math.Min(width, S(MaxW));

        int y = 0;
        bool afterHeader = false;
        foreach (var line in _lines)
        {
            int h = line.Kind switch
            {
                CardMenu.Kind.Header => S(HeaderH),
                CardMenu.Kind.Separator => S(SepH),
                _ => S(line.Note is null ? RowH : RowNoteH)
            };
            if (line.Kind != CardMenu.Kind.Header && (y == 0 || afterHeader)) y += S(Pad);
            afterHeader = line.Kind == CardMenu.Kind.Header;
            _rects.Add(line.Kind == CardMenu.Kind.Header
                ? new Rectangle(0, y, width, h)
                : new Rectangle(S(Pad), y, width - 2 * S(Pad), h));
            y += h;
        }
        y += S(Pad);
        return new Size(width, y);
    }

    private static int Measure(string text, Font font)
        => TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;

    /// <summary>
    /// Bottom-left corner at the cursor, as Windows opens the menus of the icons by
    /// the clock, moved inside the screen's work area so it never sits on the
    /// taskbar or runs off an edge, whichever side the taskbar is on.
    /// </summary>
    private static Rectangle Place(Point cursor, Size size)
    {
        Rectangle area = Screen.FromPoint(cursor).WorkingArea;
        int x = cursor.X;
        if (x + size.Width > area.Right) x = cursor.X - size.Width;
        int y = cursor.Y - size.Height;
        if (y < area.Top) y = cursor.Y;
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - size.Width));
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - size.Height));
        return new Rectangle(new Point(x, y), size);
    }

    private static float ScaleAt(Point cursor)
    {
        try
        {
            IntPtr monitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);
            if (GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpi, out _) == 0 && dpi > 0)
                return dpi / 96f;
        }
        catch { /* Windows before 8.1 */ }
        return 1f;
    }

    private Font FontOf(float px, FontStyle style, params string[] families)
    {
        foreach (string family in families)
        {
            var font = new Font(family, px * _s, style, GraphicsUnit.Pixel);
            if (string.Equals(font.Name, family, StringComparison.OrdinalIgnoreCase)
                || family.StartsWith(font.Name, StringComparison.OrdinalIgnoreCase)) return font;
            font.Dispose();
        }
        return new Font(SystemFonts.MenuFont?.FontFamily ?? FontFamily.GenericSansSerif, px * _s, style, GraphicsUnit.Pixel);
    }

    // ---- Drawing -----------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(_p.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        for (int i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            Rectangle r = _rects[i];
            switch (line.Kind)
            {
                case CardMenu.Kind.Header: DrawHeader(g, line, r); break;
                case CardMenu.Kind.Separator:
                    using (var pen = new Pen(_p.Line, Math.Max(1, S(1))))
                    {
                        int mid = r.Top + r.Height / 2;
                        g.SmoothingMode = SmoothingMode.None;
                        g.DrawLine(pen, r.Left + S(RowX), mid, r.Right - S(RowX), mid);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                    }
                    break;
                default: DrawRow(g, line, r, i == _hot); break;
            }
        }

        // Windows 11 draws the border round its rounded corners; Windows 10 gets one drawn here.
        if (!_rounded)
        {
            using var pen = new Pen(_p.Border);
            g.SmoothingMode = SmoothingMode.None;
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    private void DrawHeader(Graphics g, CardMenu.Line line, Rectangle r)
    {
        int icon = S(24);
        int x = r.Left + S(14);
        if (line.Icon is not null)
        {
            using var sized = new Icon(line.Icon, icon, icon);
            g.DrawIcon(sized, new Rectangle(x, r.Top + (r.Height - icon) / 2, icon, icon));
        }
        x += icon + S(10);

        int detailW = 0;
        if (line.Note is not null)
        {
            detailW = Measure(line.Note, _detail);
            var dr = new Rectangle(r.Right - S(14) - detailW, r.Top, detailW, r.Height);
            TextRenderer.DrawText(g, line.Note, _detail, dr, _p.Note, _p.Back, Flags | TextFormatFlags.Right);
        }
        var tr = new Rectangle(x, r.Top, r.Right - S(14) - (detailW > 0 ? detailW + S(12) : 0) - x, r.Height);
        TextRenderer.DrawText(g, line.Text, _title, tr, _p.Text, _p.Back, Flags | TextFormatFlags.EndEllipsis);

        using var pen = new Pen(_p.Line, Math.Max(1, S(1)));
        g.SmoothingMode = SmoothingMode.None;
        g.DrawLine(pen, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
        g.SmoothingMode = SmoothingMode.AntiAlias;
    }

    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine
        | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

    private void DrawRow(Graphics g, CardMenu.Line line, Rectangle r, bool hot)
    {
        Color back = hot ? _p.Hot : _p.Back;
        if (hot)
        {
            using var brush = new SolidBrush(_p.Hot);
            using var path = Rounded(r, S(6));
            g.FillPath(brush, path);
        }

        int x = r.Left + S(RowX);
        bool lit = line.Kind == CardMenu.Kind.Toggle && line.On;
        var gr = new Rectangle(x, r.Top, S(GlyphBox), r.Height);
        TextRenderer.DrawText(g, line.Glyph, _glyph, gr, lit ? _p.Accent : _p.Glyph, back,
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        x += S(GlyphBox + Gap);

        int right = r.Right - S(RowX);
        if (line.Kind == CardMenu.Kind.Toggle)
        {
            var sw = new Rectangle(right - S(SwitchW), r.Top + (r.Height - S(SwitchH)) / 2, S(SwitchW), S(SwitchH));
            DrawSwitch(g, sw, line.On);
            right = sw.Left - S(16);
        }

        if (line.Note is null)
        {
            TextRenderer.DrawText(g, line.Text, _label, new Rectangle(x, r.Top, right - x, r.Height),
                _p.Text, back, Flags | TextFormatFlags.EndEllipsis);
            return;
        }

        int labelH = _label.Height, noteH = _note.Height, both = labelH + S(1) + noteH;
        int top = r.Top + (r.Height - both) / 2;
        TextRenderer.DrawText(g, line.Text, _label, new Rectangle(x, top, right - x, labelH),
            _p.Text, back, Flags | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, line.Note, _note, new Rectangle(x, top + labelH + S(1), right - x, noteH),
            _p.Note, back, Flags | TextFormatFlags.EndEllipsis);
    }

    /// <summary>The switch of the app's settings: the accent with a white knob when on, an outline when off.</summary>
    private void DrawSwitch(Graphics g, Rectangle r, bool on)
    {
        using var track = Rounded(r, r.Height / 2);
        if (on)
        {
            using var fill = new SolidBrush(_p.Accent);
            g.FillPath(fill, track);
        }
        else
        {
            using var pen = new Pen(_p.SwitchOff, Math.Max(1f, 1.25f * _s));
            g.DrawPath(pen, track);
        }

        int knob = on ? S(12) : S(10);
        int cy = r.Top + r.Height / 2;
        int cx = on ? r.Right - S(4) - knob / 2 - S(1) : r.Left + S(5) + knob / 2;
        using var brush = new SolidBrush(on ? Color.White : _p.SwitchOff);
        g.FillEllipse(brush, cx - knob / 2f, cy - knob / 2f, knob, knob);
    }

    private static GraphicsPath Rounded(Rectangle r, int radius)
    {
        int d = Math.Max(1, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ---- Mouse and keyboard ------------------------------------------------------

    private bool Pickable(int i) => i >= 0 && i < _lines.Count && _lines[i].Kind is CardMenu.Kind.Item or CardMenu.Kind.Toggle;

    private int LineAt(Point p)
    {
        for (int i = 0; i < _rects.Count; i++)
            if (Pickable(i) && _rects[i].Contains(p)) return i;
        return -1;
    }

    private void SetHot(int i)
    {
        if (i == _hot) return;
        _hot = i;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHot(LineAt(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHot(-1);
    }

    // Either button picks a line, as in Windows' own menus.
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        int i = LineAt(e.Location);
        if (i >= 0) Pick(i);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Escape: Dismiss(); return true;
            case Keys.Down: Step(+1); return true;
            case Keys.Up: Step(-1); return true;
            case Keys.Enter:
            case Keys.Space:
                if (Pickable(_hot)) Pick(_hot);
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void Step(int direction)
    {
        int n = _lines.Count;
        int i = _hot;
        for (int k = 0; k < n; k++)
        {
            i = ((i < 0 ? (direction > 0 ? -1 : n) : i) + direction + n) % n;
            if (Pickable(i)) { SetHot(i); return; }
        }
    }

    private void Pick(int i)
    {
        var line = _lines[i];
        if (line.Kind == CardMenu.Kind.Toggle)
        {
            line.On = !line.On;
            Invalidate();
            try { line.Set?.Invoke(line.On); }
            catch { /* the switch shows what was asked; the setting is the app's to keep */ }
            return;
        }

        Dismiss();
        try { line.Run?.Invoke(); }
        catch { /* a line that fails must not take the program by the clock down with it */ }
    }

    // ---- Colours -----------------------------------------------------------------

    internal sealed record Palette(Color Back, Color Text, Color Note, Color Glyph, Color Hot, Color Line,
        Color Border, Color SwitchOff, Color Accent)
    {
        /// <summary>Light or dark as the app is, with the Windows accent for what is on.</summary>
        public static Palette Current()
        {
            bool light = UiPreferences.IsLight();
            Color accent = AccentFor(light);
            return light
                ? new Palette(
                    Back: Color.FromArgb(0xF9, 0xF9, 0xF9), Text: Color.FromArgb(0x1A, 0x1A, 0x1A),
                    Note: Color.FromArgb(0x55, 0x55, 0x55), Glyph: Color.FromArgb(0x40, 0x40, 0x40),
                    Hot: Color.FromArgb(0xEA, 0xEA, 0xEA), Line: Color.FromArgb(0xE3, 0xE3, 0xE3),
                    Border: Color.FromArgb(0xD5, 0xD5, 0xD5), SwitchOff: Color.FromArgb(0x6E, 0x6E, 0x6E),
                    Accent: accent)
                : new Palette(
                    Back: Color.FromArgb(0x2B, 0x2B, 0x2B), Text: Color.FromArgb(0xFF, 0xFF, 0xFF),
                    Note: Color.FromArgb(0xC2, 0xC2, 0xC2), Glyph: Color.FromArgb(0xD6, 0xD6, 0xD6),
                    Hot: Color.FromArgb(0x3A, 0x3A, 0x3A), Line: Color.FromArgb(0x3D, 0x3D, 0x3D),
                    Border: Color.FromArgb(0x46, 0x46, 0x46), SwitchOff: Color.FromArgb(0xA8, 0xA8, 0xA8),
                    Accent: accent);
        }

        /// <summary>
        /// The accent from Windows' palette: one step lighter than the base on the
        /// dark card and one step darker on the light one, as Windows' own switches
        /// pick it, so the white knob reads on it either way.
        /// </summary>
        private static Color AccentFor(bool light)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
                if (key?.GetValue("AccentPalette") is byte[] palette && palette.Length >= 32)
                {
                    int i = (light ? 4 : 2) * 4;
                    return Color.FromArgb(palette[i], palette[i + 1], palette[i + 2]);
                }
            }
            catch { /* no palette: Windows' default blue below */ }
            return light ? Color.FromArgb(0x00, 0x67, 0xC0) : Color.FromArgb(0x00, 0x91, 0xF8);
        }
    }

    // ---- Windows -----------------------------------------------------------------

    private const int WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, CS_DROPSHADOW = 0x20000;
    private const int GWL_EXSTYLE = -20;
    private const uint LWA_ALPHA = 0x2;
    private const uint RDW_INVALIDATE = 0x1, RDW_ALLCHILDREN = 0x80, RDW_FRAME = 0x400;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWA_BORDER_COLOR = 34, DWMWCP_ROUND = 2;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] private static extern bool RedrawWindow(IntPtr hwnd, IntPtr rect, IntPtr region, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point pt, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
