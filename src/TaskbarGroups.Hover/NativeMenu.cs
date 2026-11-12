using System.Runtime.InteropServices;
using System.Windows.Forms;
using TaskbarGroups.Core;

namespace TaskbarGroups.Hover;

/// <summary>
/// The right-click menus of the icons by the clock, as Windows' own menus: the
/// same ones the other icons there show, with Windows' fonts, spacing, rounded
/// corners and shadow, light or dark as the app is (issue #17).
///
/// The 1.9 menus were WinForms menus redrawn to look like Windows 11, and they
/// never quite did: next to the menus of the icons beside them they read as
/// something else. A real Windows menu cannot look wrong there, and it costs less.
/// </summary>
internal sealed class NativeMenu
{
    private readonly List<(string? Text, Action? Run, bool Default)> _items = new();

    /// <summary>A line of the menu. The default one is in bold, as Windows does it.</summary>
    public void Add(string text, Action run, bool isDefault = false) => _items.Add((text, run, isDefault));

    public void Separator() => _items.Add((null, null, false));

    /// <summary>
    /// Shows the menu that <paramref name="fill"/> builds whenever the icon is
    /// right-clicked. It is built at that moment, so it is always in the current
    /// language and offers what makes sense right then.
    /// </summary>
    public static void Attach(NotifyIcon icon, Action<NativeMenu> fill)
    {
        icon.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var menu = new NativeMenu();
            fill(menu);
            menu.Show();
        };
    }

    /// <summary>Shows the menu at the cursor and runs the line picked, if any.</summary>
    public void Show()
    {
        IntPtr handle = CreatePopupMenu();
        if (handle == IntPtr.Zero) return;
        try
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.Text is null) AppendMenu(handle, MF_SEPARATOR, UIntPtr.Zero, null);
                else
                {
                    AppendMenu(handle, MF_STRING, (UIntPtr)(i + 1), item.Text);
                    if (item.Default) SetMenuDefaultItem(handle, (uint)(i + 1), 0);
                }
            }

            FollowAppTheme();

            IntPtr owner = Owner.Handle;
            // Without the foreground the menu would not close on a click elsewhere.
            SetForegroundWindow(owner);
            GetCursorPos(out POINT p);
            uint align = GetSystemMetrics(SM_MENUDROPALIGNMENT) != 0 ? TPM_RIGHTALIGN : TPM_LEFTALIGN;
            int picked = TrackPopupMenuEx(handle,
                TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN | align, p.X, p.Y, owner, IntPtr.Zero);
            // What Microsoft's notes on TrackPopupMenu ask for, so the menu also
            // closes cleanly when it is opened twice in a row.
            PostMessage(owner, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (picked > 0 && picked <= _items.Count) _items[picked - 1].Run?.Invoke();
        }
        finally
        {
            DestroyMenu(handle);
        }
    }

    /// <summary>
    /// Menus are drawn light or dark for the whole process, through a uxtheme
    /// export Windows has no documented name for (ordinal 135, in Windows 10 1903
    /// and later: Explorer and Notepad use it the same way). Before 1903 it means
    /// something else, so it is left alone there and the menu stays light.
    /// </summary>
    private static void FollowAppTheme()
    {
        if (Environment.OSVersion.Version.Build < 18362) return;
        try
        {
            SetPreferredAppMode(UiPreferences.IsLight() ? ForceLight : ForceDark);
            FlushMenuThemes();
        }
        catch { /* without it the menu just follows Windows' default look */ }
    }

    /// <summary>
    /// A window of our own, never shown, for the menu to belong to: it needs one
    /// to take the foreground and to read the clicks through.
    /// </summary>
    private static NativeWindow Owner
    {
        get
        {
            if (_owner is null)
            {
                _owner = new NativeWindow();
                _owner.CreateHandle(new CreateParams { Caption = "Taskbar Groups Fluent menu" });
            }
            return _owner;
        }
    }

    private static NativeWindow? _owner;

    private const int ForceDark = 2, ForceLight = 3;
    private const uint MF_STRING = 0x0, MF_SEPARATOR = 0x800;
    private const uint TPM_LEFTALIGN = 0x0, TPM_RIGHTALIGN = 0x8, TPM_BOTTOMALIGN = 0x20;
    private const uint TPM_RIGHTBUTTON = 0x2, TPM_RETURNCMD = 0x100;
    private const int SM_MENUDROPALIGNMENT = 40;
    private const uint WM_NULL = 0x0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string? text);
    [DllImport("user32.dll")] private static extern bool SetMenuDefaultItem(IntPtr menu, uint item, uint byPosition);
    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr owner, IntPtr parameters);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("uxtheme.dll", EntryPoint = "#135")] private static extern int SetPreferredAppMode(int mode);
    [DllImport("uxtheme.dll", EntryPoint = "#136")] private static extern void FlushMenuThemes();
}
