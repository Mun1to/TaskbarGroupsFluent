#nullable enable annotations
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TaskbarGroups.Core
{
    /// <summary>One thing in a folder, as the shell lists it.</summary>
    public sealed class ShellEntry
    {
        public string Name { get; init; } = "";

        /// <summary>
        /// What the shell finds it by again, from any thread or process of this
        /// session: its item id list, written out as text (see <see cref="ShellFolder.IdPrefix"/>).
        /// A name to parse does not always work: the Control Panel's Fonts was
        /// measured coming back as a name the shell then refuses, and Windows Tools
        /// hands out names that only mean something inside it.
        /// </summary>
        public string Id { get; init; } = "";

        /// <summary>
        /// A folder in the shell's sense: something that may hold other things. A
        /// zip file is a file here. Holding nothing is common, since most Control
        /// Panel pages call themselves folders, so whether one opens as a submenu is
        /// decided by <see cref="ShellFolder.HasChildren"/>.
        /// </summary>
        public bool IsFolder { get; init; }
    }

    /// <summary>
    /// What a group that shows a folder needs from the shell (issue #9): what is in
    /// the folder, its name and icon, and opening any of it. The folder can be one
    /// on disk or one that only the shell has, like the Control Panel or This PC,
    /// which is why everything goes by shell items rather than by paths.
    ///
    /// Everything here takes a folder or an item either as the user would write it
    /// (a path, "shell:Downloads", "shell:::{class id}") or as an <see cref="ShellEntry.Id"/>.
    ///
    /// Shell objects belong to the thread that made them and want a single-threaded
    /// apartment, so callers run these on the UI thread or on an STA thread of
    /// their own, and only strings and bitmaps cross between threads.
    /// </summary>
    public static class ShellFolder
    {
        /// <summary>
        /// How much of a folder a panel lists. A panel is a menu, not a file window:
        /// past this, the last entry opens the folder itself.
        /// </summary>
        public const int MaxEntries = 400;

        /// <summary>Marks an item id list written out as text, base64 after it.</summary>
        public const string IdPrefix = "pidl:";

        /// <summary>
        /// The special folders offered in the editor, by the name the shell opens
        /// them with. Their names are asked of Windows, so they come in its language.
        /// </summary>
        public static readonly string[] Presets =
        {
            ControlPanel,
            "shell:::{D20EA4E1-3957-11d2-A40B-0C5020524153}", // Windows Tools (Administrative Tools on Windows 10)
            "shell:MyComputerFolder",
            AllApps,
            "shell:Desktop",
            "shell:Downloads",
            "shell:Personal",
            "shell:My Pictures",
            "shell:NetworkPlacesFolder",
            // Issue #17: with "show by the clock", a group showing it is a recycle
            // bin next to the clock, with the bin's own icon and its own menu.
            "shell:RecycleBinFolder",
        };

        /// <summary>All Control Panel items, the view the issue asked for.</summary>
        public const string ControlPanel = "shell:::{21EC2020-3AEA-1069-A2DD-08002B30309D}";

        /// <summary>Every app in the Start menu. Windows calls it "Applications" in every language.</summary>
        public const string AllApps = "shell:AppsFolder";

        /// <summary>
        /// What is in <paramref name="source"/>, folders first and then by name the
        /// way File Explorer sorts them, without what Windows hides. Empty if the
        /// folder cannot be read. <paramref name="more"/> says it held more than
        /// <see cref="MaxEntries"/>.
        /// </summary>
        public static List<ShellEntry> List(string source, out bool more)
        {
            more = false;
            var list = new List<ShellEntry>();
            IShellItem? folder = Create(source);
            if (folder is null) return list;

            IEnumShellItems? items = null;
            try
            {
                items = Enumerate(folder);
                if (items is null) return list;

                while (items.Next(1, out IShellItem? item, out uint fetched) == 0 && fetched == 1 && item is not null)
                {
                    try
                    {
                        if (list.Count == MaxEntries) { more = true; break; }
                        ShellEntry? entry = Describe(item);
                        if (entry is not null) list.Add(entry);
                    }
                    finally { Marshal.ReleaseComObject(item); }
                }
            }
            catch { /* whatever was read so far is still worth showing */ }
            finally
            {
                if (items is not null) Marshal.ReleaseComObject(items);
                Marshal.ReleaseComObject(folder);
            }

            list.Sort((a, b) => a.IsFolder != b.IsFolder
                ? (a.IsFolder ? -1 : 1)
                : StrCmpLogicalW(a.Name, b.Name));
            return list;
        }

        /// <summary>
        /// True if a folder holds anything Windows shows. Can take seconds: the
        /// Fonts folder was measured at three, so panels ask it off their UI thread.
        /// </summary>
        public static bool HasChildren(string source)
        {
            IShellItem? folder = Create(source);
            if (folder is null) return false;

            IEnumShellItems? items = null;
            try
            {
                items = Enumerate(folder);
                if (items is null) return false;

                // A few hidden ones (desktop.ini) can come first.
                for (int i = 0; i < 64; i++)
                {
                    if (items.Next(1, out IShellItem? item, out uint fetched) != 0 || fetched != 1 || item is null)
                        return false;
                    try { if (!IsHidden(item)) return true; }
                    finally { Marshal.ReleaseComObject(item); }
                }
                return true;
            }
            catch { return false; }
            finally
            {
                if (items is not null) Marshal.ReleaseComObject(items);
                Marshal.ReleaseComObject(folder);
            }
        }

        /// <summary>The folder's name as File Explorer shows it, or null if there is no such folder.</summary>
        public static string? DisplayName(string source)
        {
            IShellItem? item = Create(source);
            if (item is null) return null;
            try { return item.GetDisplayName(SIGDN_NORMALDISPLAY, out string? name) == 0 ? name : null; }
            catch { return null; }
            finally { Marshal.ReleaseComObject(item); }
        }

        /// <summary>True if the shell can open <paramref name="source"/> as a folder.</summary>
        public static bool IsFolder(string source)
        {
            IShellItem? item = Create(source);
            if (item is null) return false;
            try
            {
                return item.GetAttributes(SFGAO_FOLDER | SFGAO_STREAM, out uint a) >= 0
                    && (a & SFGAO_FOLDER) != 0 && (a & SFGAO_STREAM) == 0;
            }
            catch { return false; }
            finally { Marshal.ReleaseComObject(item); }
        }

        /// <summary>
        /// The icon the shell gives the item, at exactly that size, and never a
        /// thumbnail of what is in it: for a folder of photos that is the difference
        /// between a panel that fills in at once and one that crawls, and four hundred
        /// files do not hold four hundred jumbo images.
        /// </summary>
        public static Bitmap? Icon(string source, int size)
        {
            IShellItem? item = Create(source);
            if (item is null) return null;
            IntPtr hbitmap = IntPtr.Zero;
            try
            {
                if (item is not IShellItemImageFactory factory) return null;
                if (factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF_ICONONLY, out hbitmap) != 0
                    || hbitmap == IntPtr.Zero) return null;
                return AppCatalog.BitmapFromHBitmap(hbitmap);
            }
            catch { return null; }
            finally
            {
                if (hbitmap != IntPtr.Zero) DeleteObject(hbitmap);
                Marshal.ReleaseComObject(item);
            }
        }

        /// <summary>
        /// Opens the item the way a double-click in File Explorer does: a file with
        /// its program, a folder in a window, a Control Panel page as that page.
        /// </summary>
        public static bool Open(string source)
        {
            IntPtr pidl = IdList(source);
            if (pidl == IntPtr.Zero) return false;
            try
            {
                var info = new SHELLEXECUTEINFO
                {
                    cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
                    fMask = SEE_MASK_INVOKEIDLIST,
                    lpIDList = pidl,
                    nShow = SW_SHOWNORMAL
                };
                return ShellExecuteExW(ref info);
            }
            catch { return false; }
            finally { Marshal.FreeCoTaskMem(pidl); }
        }

        /// <summary>
        /// The item id list behind <paramref name="source"/>, for code that hands it
        /// to the shell itself. The caller frees it with <see cref="Marshal.FreeCoTaskMem"/>.
        /// Zero if there is no such item.
        /// </summary>
        public static IntPtr IdList(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return IntPtr.Zero;
            source = source.Trim();

            if (source.StartsWith(IdPrefix, StringComparison.Ordinal))
            {
                byte[] bytes;
                try { bytes = Convert.FromBase64String(source.Substring(IdPrefix.Length)); }
                catch { return IntPtr.Zero; }
                if (bytes.Length < 2) return IntPtr.Zero;
                IntPtr copy = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, copy, bytes.Length);
                return copy;
            }

            try
            {
                return SHParseDisplayName(source, IntPtr.Zero, out IntPtr pidl, 0, out _) == 0 ? pidl : IntPtr.Zero;
            }
            catch { return IntPtr.Zero; }
        }

        private static ShellEntry? Describe(IShellItem item)
        {
            if (item.GetAttributes(SFGAO_FOLDER | SFGAO_STREAM | SFGAO_HIDDEN, out uint a) < 0) return null;
            if ((a & SFGAO_HIDDEN) != 0) return null;

            string? name = item.GetDisplayName(SIGDN_NORMALDISPLAY, out string? n) == 0 ? n : null;
            // An entry with no name of its own shows its class id instead: one was
            // measured in the Control Panel, and there is nothing to call it.
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("::{", StringComparison.Ordinal)) return null;

            string? id = IdOf(item);
            if (id is null) return null;

            return new ShellEntry
            {
                Name = name,
                Id = id,
                IsFolder = (a & SFGAO_FOLDER) != 0 && (a & SFGAO_STREAM) == 0
            };
        }

        /// <summary>The item's absolute id list as text, or null if the shell will not give it.</summary>
        private static string? IdOf(IShellItem item)
        {
            IntPtr pidl = IntPtr.Zero;
            try
            {
                if (SHGetIDListFromObject(item, out pidl) != 0 || pidl == IntPtr.Zero) return null;
                int size = (int)ILGetSize(pidl);
                if (size < 2) return null;
                var bytes = new byte[size];
                Marshal.Copy(pidl, bytes, 0, size);
                return IdPrefix + Convert.ToBase64String(bytes);
            }
            catch { return null; }
            finally { if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); }
        }

        private static bool IsHidden(IShellItem item)
            => item.GetAttributes(SFGAO_HIDDEN, out uint a) >= 0 && (a & SFGAO_HIDDEN) != 0;

        private static IShellItem? Create(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return null;
            Guid iid = typeof(IShellItem).GUID;
            try
            {
                if (source.StartsWith(IdPrefix, StringComparison.Ordinal))
                {
                    IntPtr pidl = IdList(source);
                    if (pidl == IntPtr.Zero) return null;
                    try { return SHCreateItemFromIDList(pidl, ref iid, out IShellItem? fromId) == 0 ? fromId : null; }
                    finally { Marshal.FreeCoTaskMem(pidl); }
                }
                return SHCreateItemFromParsingName(source.Trim(), IntPtr.Zero, ref iid, out IShellItem? item) == 0
                    ? item : null;
            }
            catch { return null; }
        }

        private static IEnumShellItems? Enumerate(IShellItem folder)
        {
            Guid bhid = BHID_EnumItems, iid = typeof(IEnumShellItems).GUID;
            return folder.BindToHandler(IntPtr.Zero, ref bhid, ref iid, out object? o) == 0 ? o as IEnumShellItems : null;
        }

        private const uint SIGDN_NORMALDISPLAY = 0;
        private const uint SFGAO_FOLDER = 0x20000000;
        private const uint SFGAO_STREAM = 0x00400000;
        private const uint SFGAO_HIDDEN = 0x00080000;
        private const int SIIGBF_ICONONLY = 0x4;
        private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
        private const int SW_SHOWNORMAL = 1;
        private static readonly Guid BHID_EnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object? ppv);
            [PreserveSig] int GetParent(out IShellItem? ppsi);
            [PreserveSig] int GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string? ppszName);
            [PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            [PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
        }

        [ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IEnumShellItems
        {
            [PreserveSig] int Next(uint celt, out IShellItem? rgelt, out uint pceltFetched);
            [PreserveSig] int Skip(uint celt);
            [PreserveSig] int Reset();
            [PreserveSig] int Clone(out IEnumShellItems? ppenum);
        }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx, cy; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            public string? lpVerb;
            public string? lpFile;
            public string? lpParameters;
            public string? lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            public string? lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIconOrMonitor;
            public IntPtr hProcess;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, out IShellItem? item);

        [DllImport("shell32.dll")]
        private static extern int SHCreateItemFromIDList(IntPtr pidl, ref Guid riid, out IShellItem? item);

        [DllImport("shell32.dll")]
        private static extern int SHGetIDListFromObject([MarshalAs(UnmanagedType.IUnknown)] object punk, out IntPtr ppidl);

        [DllImport("shell32.dll")]
        private static extern uint ILGetSize(IntPtr pidl);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHParseDisplayName(string name, IntPtr pbc, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ShellExecuteExW(ref SHELLEXECUTEINFO info);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string a, string b);
    }
}
