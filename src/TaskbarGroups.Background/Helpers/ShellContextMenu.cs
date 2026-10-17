using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Threading;
using TaskbarGroups.Core;

namespace TaskbarGroups.Background.Helpers;

/// <summary>
/// The menu Windows shows on a right-click: for a program, a shortcut or a folder
/// the one in File Explorer, for an app picked from the list the one in the Start
/// menu. It is the classic menu, the one behind "Show more options", because the
/// new Windows 11 menu is not open to other apps.
/// </summary>
internal static class ShellContextMenu
{
    /// <summary>
    /// True once a command from the menu has run in this process. Some of them
    /// leave something of this process behind: a Properties sheet, a delete
    /// confirmation, files put on the clipboard by Copy. Ending the process would
    /// take those with it.
    /// </summary>
    public static bool CommandRan { get; private set; }

    private static EventWaitHandle? _busy;

    /// <summary>
    /// The name Windows knows the entry by, or null for one that has no menu of its
    /// own: a group inside a group lives only in this app.
    /// </summary>
    public static string? ParsingName(ProgramShortcut ps)
    {
        if (ps.IsGroup || string.IsNullOrWhiteSpace(ps.FilePath)) return null;
        return ps.isWindowsApp ? @"shell:AppsFolder\" + ps.FilePath : ps.FilePath;
    }

    /// <summary>
    /// Shows the menu for <paramref name="parsingName"/> at screen point
    /// (<paramref name="x"/>, <paramref name="y"/>), in physical pixels, and runs
    /// what the user picks. Returns true if a command ran, false if the menu was
    /// dismissed or could not be built.
    /// </summary>
    public static bool Show(IntPtr owner, string parsingName, int x, int y, bool dark)
    {
        Guid iidItem = typeof(IShellItem).GUID;
        if (SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iidItem, out IShellItem? item) != 0 || item is null)
            return false;

        IntPtr unknown = IntPtr.Zero, menu = IntPtr.Zero;
        object? handler = null;
        HwndSource? source = null;
        HwndSourceHook? hook = null;
        try
        {
            Guid bhid = BHID_SFUIObject, iid = typeof(IContextMenu).GUID;
            if (item.BindToHandler(IntPtr.Zero, ref bhid, ref iid, out unknown) != 0 || unknown == IntPtr.Zero)
                return false;

            handler = Marshal.GetObjectForIUnknown(unknown);
            var cm = (IContextMenu)handler;
            menu = CreatePopupMenu();
            if (menu == IntPtr.Zero) return false;

            // Shift held adds the extended commands, as it does in File Explorer.
            uint flags = CMF_NORMAL;
            if ((GetKeyState(VK_SHIFT) & 0x8000) != 0) flags |= CMF_EXTENDEDVERBS;
            if (cm.QueryContextMenu(menu, 0, FirstCommand, LastCommand, flags) < 0) return false;

            // "Send to", "Open with" and a few others fill their submenus only when
            // they open, and they hear about it through these messages.
            var cm2 = handler as IContextMenu2;
            var cm3 = handler as IContextMenu3;
            source = HwndSource.FromHwnd(owner);
            hook = (IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (msg != WM_INITMENUPOPUP && msg != WM_DRAWITEM && msg != WM_MEASUREITEM && msg != WM_MENUCHAR)
                    return IntPtr.Zero;
                try
                {
                    if (cm3 is not null && cm3.HandleMenuMsg2((uint)msg, wParam, lParam, out IntPtr result) == 0)
                    {
                        handled = true;
                        return result;
                    }
                    if (cm2 is not null && msg != WM_MENUCHAR && cm2.HandleMenuMsg((uint)msg, wParam, lParam) == 0)
                        handled = true;
                }
                catch { /* a menu handler that fails just leaves its submenu empty */ }
                return IntPtr.Zero;
            };
            source?.AddHook(hook);

            FollowTheme(dark);
            int command = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, x, y, owner, IntPtr.Zero);
            // Without this, a menu opened from a window that was not in front can
            // need two clicks to go away (documented under TrackPopupMenu).
            PostMessage(owner, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            if (command < FirstCommand) return false;

            var info = new CMINVOKECOMMANDINFOEX
            {
                cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                fMask = CMIC_MASK_UNICODE | CMIC_MASK_PTINVOKE,
                hwnd = owner,
                lpVerb = (IntPtr)(command - FirstCommand),
                lpVerbW = (IntPtr)(command - FirstCommand),
                nShow = SW_SHOWNORMAL,
                ptInvoke = new POINT { X = x, Y = y }
            };
            if ((GetKeyState(VK_SHIFT) & 0x8000) != 0) info.fMask |= CMIC_MASK_SHIFT_DOWN;
            if ((GetKeyState(VK_CONTROL) & 0x8000) != 0) info.fMask |= CMIC_MASK_CONTROL_DOWN;

            MarkBusy();
            cm.InvokeCommand(ref info);
            return true;
        }
        catch
        {
            // A broken menu handler from some other program must not take the
            // panel down with it.
            return false;
        }
        finally
        {
            if (source is not null && hook is not null) source.RemoveHook(hook);
            if (menu != IntPtr.Zero) DestroyMenu(menu);
            if (handler is not null) Marshal.ReleaseComObject(handler);
            if (unknown != IntPtr.Zero) Marshal.Release(unknown);
            Marshal.ReleaseComObject(item);
        }
    }

    /// <summary>
    /// True if this process has a window on screen other than
    /// <paramref name="panel"/>: something a menu command opened and the user is
    /// still looking at.
    /// </summary>
    public static bool HasWindowsBesides(IntPtr panel)
    {
        if (!CommandRan) return false;

        int self = Environment.ProcessId;
        bool found = false;
        EnumWindows((hwnd, _) =>
        {
            if (hwnd == panel || !IsWindowVisible(hwnd)) return true;
            GetWindowThreadProcessId(hwnd, out int pid);
            if (pid != self) return true;
            found = true;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    /// <summary>
    /// Ends a panel that ran a menu command once nothing that command opened is
    /// left. The panel is already hidden; it is only closed (which ends the
    /// process) when no other window of the process remains, and never in the
    /// first seconds, which is when a command started in the background shows its
    /// window.
    /// </summary>
    public static void CloseWhenDone(System.Windows.Window panel)
    {
        IntPtr hwnd = new WindowInteropHelper(panel).Handle;
        DateTime since = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) =>
        {
            if (DateTime.UtcNow - since < GraceAfterCommand || HasWindowsBesides(hwnd)) return;
            timer.Stop();
            panel.Close();
        };
        timer.Start();
    }

    private static readonly TimeSpan GraceAfterCommand = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Hands the clipboard over to Windows before the process ends. Copy and Cut
    /// put this process's own data on it, which would otherwise vanish with it.
    /// </summary>
    public static void ReleaseClipboard()
    {
        if (!CommandRan) return;
        try { OleFlushClipboard(); } catch { }
    }

    /// <summary>
    /// The signal a newer panel looks for before closing this one: the process is
    /// no longer just a panel, it may be holding a window the user has open.
    /// </summary>
    public static string BusyName(int processId) => "TaskbarGroupsFluent.MenuCommand." + processId;

    private static void MarkBusy()
    {
        CommandRan = true;
        try { _busy ??= new EventWaitHandle(true, EventResetMode.ManualReset, BusyName(Environment.ProcessId)); }
        catch { }
    }

    // Classic menus are drawn light unless the process asks for dark. These two
    // entry points have no name, only a number, but they are the ones File
    // Explorer and many other apps use, and they have been stable since Windows 10
    // version 1903, older than anything this app runs on.
    private static void FollowTheme(bool dark)
    {
        try
        {
            SetPreferredAppMode(dark ? AppModeForceDark : AppModeForceLight);
            FlushMenuThemes();
        }
        catch { /* no theming: the menu still works, in light */ }
    }

    private const int AppModeForceDark = 2;
    private const int AppModeForceLight = 3;

    [DllImport("uxtheme.dll", EntryPoint = "#135")] private static extern int SetPreferredAppMode(int mode);
    [DllImport("uxtheme.dll", EntryPoint = "#136")] private static extern void FlushMenuThemes();

    private const uint FirstCommand = 1;
    private const uint LastCommand = 0x7FFF;
    private const uint CMF_NORMAL = 0x0;
    private const uint CMF_EXTENDEDVERBS = 0x100;
    private const uint CMIC_MASK_UNICODE = 0x4000;
    private const uint CMIC_MASK_SHIFT_DOWN = 0x10000000;
    private const uint CMIC_MASK_PTINVOKE = 0x20000000;
    private const uint CMIC_MASK_CONTROL_DOWN = 0x40000000;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;
    private const int SW_SHOWNORMAL = 1;
    private const int WM_NULL = 0x0000;
    private const int WM_DRAWITEM = 0x002B;
    private const int WM_MEASUREITEM = 0x002C;
    private const int WM_MENUCHAR = 0x0120;
    private const int WM_INITMENUPOPUP = 0x0117;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;

    private static readonly Guid BHID_SFUIObject = new("3981e225-f559-11d3-8e3a-00c04f6837d5");

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    // Every string field is left empty, so they are plain pointers: nothing to marshal.
    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFOEX
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr lpTitle;
        public IntPtr lpVerbW;
        public IntPtr lpParametersW;
        public IntPtr lpDirectoryW;
        public IntPtr lpTitleW;
        public POINT ptInvoke;
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetParent(out IShellItem ppsi);
        [PreserveSig] int GetDisplayName(uint sigdnName, out IntPtr ppszName);
        [PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        [PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("000214e4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig] int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
    }

    [ComImport, Guid("000214f4-0000-0000-c000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu2
    {
        [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig] int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
        [PreserveSig] int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
    }

    [ComImport, Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu3
    {
        [PreserveSig] int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig] int InvokeCommand(ref CMINVOKECOMMANDINFOEX pici);
        [PreserveSig] int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
        [PreserveSig] int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
        [PreserveSig] int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, out IShellItem? item);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr tpm);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern short GetKeyState(int key);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int pid);
    [DllImport("ole32.dll")] private static extern int OleFlushClipboard();
}
