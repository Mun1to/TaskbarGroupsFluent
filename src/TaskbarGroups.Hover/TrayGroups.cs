using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using TaskbarGroups.Core;

namespace TaskbarGroups.Hover;

/// <summary>
/// The groups the user ticked to show by the clock (issue #11), one icon each in
/// the notification area. A click opens the group's panel right there, the same
/// panel a pinned group opens; the right click offers what the pinned group's
/// menu offers.
///
/// The icons follow the groups on disk without being told: the config folder is
/// watched, and a change anywhere in it (a group saved, renamed, deleted, its icon
/// replaced) has the list read again once it has gone quiet. Reading it is cheap,
/// a small XML file per group, and only happens after a change.
/// </summary>
internal sealed class TrayGroups : IDisposable
{
    /// <summary>
    /// How long to let a save finish before reading. The editor writes the group
    /// file, then its icon, then the icon cache, and reading halfway through would
    /// catch an icon about to be replaced.
    /// </summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(600);

    private sealed class Entry
    {
        public required NotifyIcon Icon;
        public DateTime IconWritten;
    }

    private readonly Dictionary<string, Entry> _icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileSystemWatcher? _watcher;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly object _gate = new();
    private DateTime? _changedAt;

    /// <summary>Raised when the last group has left the notification area.</summary>
    public event Action? Emptied;

    public TrayGroups()
    {
        try
        {
            _watcher = new FileSystemWatcher(Paths.ConfigPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                // Saving a big group rewrites dozens of cached icons at once.
                InternalBufferSize = 64 * 1024
            };
            _watcher.Changed += (_, e) => Touch(e.FullPath);
            _watcher.Created += (_, e) => Touch(e.FullPath);
            _watcher.Deleted += (_, e) => Touch(e.FullPath);
            _watcher.Renamed += (_, e) => Touch(e.FullPath);
            // Too much at once and Windows drops the details: read everything again.
            _watcher.Error += (_, _) => { lock (_gate) _changedAt = DateTime.UtcNow; };
            _watcher.EnableRaisingEvents = true;
        }
        catch
        {
            // Without it the icons only stay as they were at start, which the next
            // sign-in puts right. Nothing else depends on it.
            _watcher = null;
        }

        _timer = new System.Windows.Forms.Timer { Interval = 250 };
        _timer.Tick += (_, _) => TakeChange();
    }

    public int Count => _icons.Count;

    public void Start()
    {
        Sync();
        _timer.Start();
    }

    /// <summary>
    /// Only what can change an icon counts: a group's file, its icon, or a group
    /// folder itself. The icon cache under each group is rewritten on every save
    /// and would otherwise keep pushing the reading back.
    /// </summary>
    private void Touch(string path)
    {
        string name = Path.GetFileName(path);
        string? parent = Path.GetDirectoryName(path);
        bool groupFolder = parent is not null
            && string.Equals(Path.GetFullPath(parent).TrimEnd('\\'), Path.GetFullPath(Paths.ConfigPath).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);

        if (!groupFolder
            && !name.Equals("ObjectData.xml", StringComparison.OrdinalIgnoreCase)
            && !name.Equals("GroupIcon.ico", StringComparison.OrdinalIgnoreCase))
            return;

        lock (_gate) _changedAt = DateTime.UtcNow;
    }

    private void TakeChange()
    {
        lock (_gate)
        {
            if (_changedAt is null || DateTime.UtcNow - _changedAt < Settle) return;
            _changedAt = null;
        }

        Sync();
        if (_icons.Count == 0) Emptied?.Invoke();
    }

    /// <summary>Puts the icons in line with the groups ticked on disk. UI thread only.</summary>
    private void Sync()
    {
        var wanted = HoverService.TrayGroupNames();

        foreach (string name in _icons.Keys.ToList())
        {
            if (wanted.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            Remove(name);
        }

        foreach (string name in wanted)
        {
            DateTime written = IconWritten(name);
            if (_icons.TryGetValue(name, out Entry? entry))
            {
                // A new icon picked in the editor: swap it in place, so the icon keeps
                // its spot by the clock.
                if (written != entry.IconWritten)
                {
                    Icon? old = entry.Icon.Icon;
                    entry.Icon.Icon = LoadIcon(name);
                    entry.IconWritten = written;
                    if (old is not null && !ReferenceEquals(old, SystemIcons.Application)) old.Dispose();
                }
                continue;
            }

            _icons[name] = new Entry { Icon = Create(name), IconWritten = written };
        }
    }

    private NotifyIcon Create(string name)
    {
        var icon = new NotifyIcon
        {
            Icon = LoadIcon(name),
            Text = Trim(name),
            ContextMenuStrip = FluentMenu.Create()
        };

        // A plain click, as on the taskbar. The second click of a double click does
        // not raise MouseClick, so a double click opens it once.
        icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) OpenGroup(name);
        };

        // Built as it opens, so it is always in the current language and knows
        // whether the group shows a folder now. An empty menu starts its Opening with
        // Cancel already set, so once filled it is told to open after all.
        icon.ContextMenuStrip.Opening += (_, e) =>
        {
            FillMenu(icon.ContextMenuStrip, name);
            e.Cancel = false;
        };
        icon.Visible = true;
        return icon;
    }

    private static void FillMenu(ContextMenuStrip menu, string name)
    {
        foreach (ToolStripItem old in menu.Items.Cast<ToolStripItem>().ToList()) old.Dispose();
        menu.Items.Clear();

        var open = new ToolStripMenuItem(TrayText.OpenGroup, null, (_, _) => OpenGroup(name));
        open.Font = new Font(menu.Font, FontStyle.Bold);
        menu.Items.Add(open);

        bool folder = IsFolderGroup(name);
        menu.Items.Add(folder ? TrayText.OpenFolder : TrayText.OpenAll, null,
            (_, _) => StartPanel(name, "--open-all"));
        menu.Items.Add(TrayText.EditGroup, null, (_, _) => StartApp("--edit", name));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(TrayText.OpenApp, null, (_, _) => StartApp());
    }

    private static bool IsFolderGroup(string name)
    {
        try { return new Category(Path.Combine(Paths.ConfigPath, name)).IsFolderGroup; }
        catch { return false; }
    }

    /// <summary>
    /// Opens the group's panel next to the cursor, which is on the icon by the clock.
    /// It goes through the flyout's own start, as a click on a pinned group does, so
    /// a warm flyout takes it in about fifty milliseconds and a cold start still
    /// works. That process is lent the right to take the foreground, which the click
    /// on our icon gave us: without it the panel could not take the keyboard, nor
    /// close by losing it.
    /// </summary>
    internal static void OpenGroup(string name) => StartPanel(name);

    private static void StartPanel(string name, params string[] switches)
    {
        try
        {
            var psi = new ProcessStartInfo(Paths.BackgroundApplication)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(Paths.BackgroundApplication) ?? string.Empty
            };
            psi.ArgumentList.Add(name);
            foreach (string s in switches) psi.ArgumentList.Add(s);

            using var p = Process.Start(psi);
            if (p is not null) Native.AllowSetForegroundWindow(p.Id);
        }
        catch { /* nothing to open it with; the next click may fare better */ }
    }

    /// <summary>Starts the main app, with <paramref name="args"/> if any.</summary>
    internal static void StartApp(params string[] args)
    {
        try
        {
            string exe = Paths.MainApplication;
            if (!File.Exists(exe)) return;

            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty
            };
            foreach (string a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is not null) Native.AllowSetForegroundWindow(p.Id);
        }
        catch { /* nothing useful to say from a tray menu */ }
    }

    private static DateTime IconWritten(string name)
    {
        try { return File.GetLastWriteTimeUtc(IconPath(name)); }
        catch { return DateTime.MinValue; }
    }

    private static string IconPath(string name) => Path.Combine(Paths.ConfigPath, name, "GroupIcon.ico");

    /// <summary>
    /// The group's own icon at the size the notification area draws, read into
    /// memory so the file stays free for the editor to replace.
    /// </summary>
    private static Icon LoadIcon(string name)
    {
        try
        {
            string path = IconPath(name);
            if (File.Exists(path)) return new Icon(path, SystemInformation.SmallIconSize);
        }
        catch { /* fall through to the app's own */ }

        return SystemIcons.Application;
    }

    private void Remove(string name)
    {
        if (!_icons.Remove(name, out Entry? entry)) return;
        entry.Icon.Visible = false;
        Icon? icon = entry.Icon.Icon;
        entry.Icon.ContextMenuStrip?.Dispose();
        entry.Icon.Dispose();
        if (icon is not null && !ReferenceEquals(icon, SystemIcons.Application)) icon.Dispose();
    }

    /// <summary>NotifyIcon.Text throws above its limit; a group name can be long.</summary>
    private static string Trim(string text)
        => text.Length <= 63 ? text : text[..63];

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
        try { _watcher?.Dispose(); } catch { }
        foreach (string name in _icons.Keys.ToList()) Remove(name);
    }
}
