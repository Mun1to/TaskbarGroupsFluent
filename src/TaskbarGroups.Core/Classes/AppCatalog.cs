#nullable enable annotations
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// A launchable application from the Windows Shell "AppsFolder" — the exact
    /// list the Start Menu shows. Covers UWP <em>and</em> Win32 apps uniformly,
    /// already curated (no uninstallers/documentation), and launched the same way
    /// via <c>shell:AppsFolder\{LaunchId}</c>.
    /// </summary>
    public class AppCatalogEntry
    {
        public string DisplayName { get; set; } = "";
        public string LaunchId { get; set; } = ""; // AUMID / parsing name under AppsFolder
    }

    /// <summary>
    /// Reads the shell's app catalog and resolves icons through the shell's own
    /// image pipeline (<c>IShellItemImageFactory</c>) — the same icons the Start
    /// Menu shows, correct for every app type, with no shortcut-arrow overlay and
    /// no need to resolve shortcut targets or handle launcher stubs ourselves.
    /// </summary>
    public static class AppCatalog
    {
        // ---- P/Invoke ----
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            string pszPath, IntPtr pbc, in Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out object ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref BITMAP lpvObject);

        // Extracts an icon straight from a PE file's resources at a requested size —
        // bypasses the (sometimes stale/generic) system image list.
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int PrivateExtractIcons(string szFileName, int nIconIndex,
            int cxIcon, int cyIcon, IntPtr[] phicon, int[] piconid, int nIcons, int flags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType, bmWidth, bmHeight, bmWidthBytes;
            public ushort bmPlanes, bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx, cy; public SIZE(int w, int h) { cx = w; cy = h; } }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROPERTYKEY { public Guid fmtid; public uint pid; }

        private static readonly Guid IID_IShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
        private static readonly Guid IID_IShellItem2 = new("7e9fb0d3-919f-4307-ab2e-9b1860310c93");
        // PKEY_Link_TargetParsingPath: the target path an AppsFolder entry launches.
        private static readonly PROPERTYKEY PKEY_Link_TargetParsingPath =
            new() { fmtid = new Guid("B9B4B3FC-2B51-4A42-B5D8-324146AFCF25"), pid = 2 };
        private static readonly Guid IID_IEnumShellItems = new("70629033-e363-4a28-a567-0db78006e6d7");
        private static readonly Guid IID_IShellItemImageFactory = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");
        private static readonly Guid BHID_EnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid,
                [MarshalAs(UnmanagedType.Interface)] out object ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        // IShellItem2 — we only need GetString (property store), so slots 6-14 are
        // vtable placeholders that we never call. Declaration order must match the
        // COM vtable so GetString lands on the right slot.
        [ComImport, Guid("7e9fb0d3-919f-4307-ab2e-9b1860310c93"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem2
        {
            void _BindToHandler(); void _GetParent(); void _GetDisplayName();
            void _GetAttributes(); void _Compare();                 // IShellItem  (1-5)
            void _GetPropertyStore(); void _GetPropertyStoreWithCreateObject();
            void _GetPropertyStoreForKeys(); void _GetPropertyDescriptionList();
            void _Update(); void _GetProperty(); void _GetCLSID();
            void _GetFileTime(); void _GetInt32();                   // IShellItem2 (6-14)
            [PreserveSig] int GetString(in PROPERTYKEY key,
                [MarshalAs(UnmanagedType.LPWStr)] out string ppsz);  // slot 15
        }

        [ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IEnumShellItems
        {
            [PreserveSig] int Next(uint celt, out IShellItem rgelt, out uint pceltFetched);
            [PreserveSig] int Skip(uint celt);
            [PreserveSig] int Reset();
            void Clone(out IEnumShellItems ppenum);
        }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
        }

        private enum SIGDN : uint
        {
            NORMALDISPLAY = 0,
            PARENTRELATIVEPARSING = 0x80018001,
        }

        private const int SIIGBF_BIGGERSIZEOK = 0x1;

        // ---- Public API ----

        /// <summary>Enumerates installed apps from the shell AppsFolder (STA COM).</summary>
        public static List<AppCatalogEntry> Enumerate()
        {
            List<AppCatalogEntry> result = new();
            var sta = new Thread(() => result = EnumerateCore()) { IsBackground = true };
            sta.SetApartmentState(ApartmentState.STA);
            sta.Start();
            sta.Join();
            return result;
        }

        private static List<AppCatalogEntry> EnumerateCore()
        {
            var list = new List<AppCatalogEntry>();
            try
            {
                Guid itemIid = IID_IShellItem;
                SHCreateItemFromParsingName("shell:AppsFolder", IntPtr.Zero, in itemIid, out object appsObj);
                var apps = (IShellItem)appsObj;

                Guid bhid = BHID_EnumItems, enumIid = IID_IEnumShellItems;
                apps.BindToHandler(IntPtr.Zero, in bhid, in enumIid, out object enumObj);
                var e = (IEnumShellItems)enumObj;

                while (e.Next(1, out IShellItem item, out uint fetched) == 0 && fetched == 1)
                {
                    try
                    {
                        item.GetDisplayName(SIGDN.NORMALDISPLAY, out string name);
                        item.GetDisplayName(SIGDN.PARENTRELATIVEPARSING, out string id);
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id) && !IsNoise(id))
                            list.Add(new AppCatalogEntry { DisplayName = name, LaunchId = id });
                    }
                    catch { /* skip items we can't read */ }
                    finally { if (item != null) Marshal.ReleaseComObject(item); }
                }

                Marshal.ReleaseComObject(e);
                Marshal.ReleaseComObject(apps);
            }
            catch { /* return whatever we gathered */ }
            return list;
        }

        // Hide AppsFolder entries that aren't really "apps you'd group": web links,
        // documentation/console files, Control Panel applets (their id is a
        // "{CLSID}\tool.exe" path), Windows system apps, and SDK tools. Real apps
        // use their own AUMID or a plain executable path, so this has no false
        // positives on user apps; anything hidden is reachable via Browse….
        private static bool IsNoise(string id)
        {
            string lid = id.ToLowerInvariant();
            if (lid.StartsWith("http://") || lid.StartsWith("https://")) return true;
            if (lid.EndsWith(".url") || lid.EndsWith(".msc") || lid.EndsWith(".txt")
                || lid.EndsWith(".chm") || lid.EndsWith(".htm") || lid.EndsWith(".html")) return true;
            // Control Panel / system applets live under a CLSID folder: "{GUID}\tool".
            if (id.Length > 38 && id[0] == '{' && id.IndexOf('}') == 37) return true;
            if (lid.StartsWith("microsoft.windows.")) return true;            // Control Panel, Admin Tools, Remote Desktop…
            if (lid.StartsWith("windows.immersivecontrolpanel")) return true; // Settings
            if (lid.Contains(@"\vulkansdk\") || lid.Contains(@"\windows kits\")) return true;
            return false;
        }

        /// <summary>
        /// Resolves an app's icon at the requested size. Prefers the shell image
        /// factory (correct for UWP and most Win32 apps); if the shell renders the
        /// generic blank-document placeholder — some Squirrel/Electron installs
        /// (Obsidian, Brave) register an icon location the shell can't resolve — it
        /// pulls the real icon straight from the target .exe's PE resources instead.
        /// Returns null only if nothing can be resolved.
        /// </summary>
        public static Bitmap? GetIcon(string launchId, int size)
        {
            Bitmap? shell = GetShellImage("shell:AppsFolder\\" + launchId, size);
            if (shell == null || IsGenericPlaceholder(shell))
            {
                Bitmap? fromExe = GetIconFromTargetExe(launchId, size);
                if (fromExe != null) { shell?.Dispose(); return fromExe; }
            }
            return shell;
        }

        // The shell's image pipeline for a single parsing name (AppsFolder id or path).
        private static Bitmap? GetShellImage(string parsingName, int size)
        {
            IntPtr hbitmap = IntPtr.Zero;
            try
            {
                Guid iid = IID_IShellItemImageFactory;
                SHCreateItemFromParsingName(parsingName, IntPtr.Zero, in iid, out object obj);
                var factory = (IShellItemImageFactory)obj;
                int hr = factory.GetImage(new SIZE(size, size), SIIGBF_BIGGERSIZEOK, out hbitmap);
                Marshal.ReleaseComObject(factory);
                if (hr != 0 || hbitmap == IntPtr.Zero) return null;
                return BitmapFromHBitmap(hbitmap);
            }
            catch { return null; }
            finally { if (hbitmap != IntPtr.Zero) DeleteObject(hbitmap); }
        }

        // The shell's generic blank-document placeholder is a small, fully-desaturated
        // glyph on a mostly-transparent canvas: it covers very little of the icon and
        // has no colour at all. Real app icons are either colourful or fill most of the
        // canvas, so this two-signal check cleanly separates them (measured across a
        // range of apps: broken shells ~7% opaque / 0% colour; real ones 50-80% opaque).
        // It's also safe by construction — the fallback only *replaces* the icon when it
        // actually extracts one from the .exe, so a rare misflag can't blank an icon.
        private static bool IsGenericPlaceholder(Bitmap icon)
        {
            try
            {
                using var s = new Bitmap(icon, 48, 48);
                int opaque = 0, colorful = 0;
                for (int y = 0; y < 48; y++)
                    for (int x = 0; x < 48; x++)
                    {
                        Color c = s.GetPixel(x, y);
                        if (c.A <= 32) continue;
                        opaque++;
                        int mx = Math.Max(c.R, Math.Max(c.G, c.B));
                        int mn = Math.Min(c.R, Math.Min(c.G, c.B));
                        if (mx - mn > 24) colorful++;
                    }
                if (opaque == 0) return false; // fully transparent: leave it alone
                double opaqueFrac = opaque / (48.0 * 48.0);
                double colorfulFrac = (double)colorful / opaque;
                return opaqueFrac < 0.22 && colorfulFrac < 0.03;
            }
            catch { return false; }
        }

        // Resolves the app's target .exe (via the AppsFolder property store) and
        // extracts its embedded icon from the PE resources. Returns null for UWP
        // apps (no .exe target) and anything we can't extract.
        private static Bitmap? GetIconFromTargetExe(string launchId, int size)
        {
            string? exe = ResolveTargetPath(launchId);
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return null;
            foreach (int s in new[] { size, 256, 48, 32 })
            {
                Bitmap? bmp = IconFromPE(exe, s);
                if (bmp != null) return bmp;
            }
            return null;
        }

        // The path an AppsFolder entry launches (PKEY_Link_TargetParsingPath), if it
        // is a plain executable. Null for UWP/packaged apps and non-.exe targets.
        private static string? ResolveTargetPath(string launchId)
        {
            try
            {
                Guid iid = IID_IShellItem2;
                SHCreateItemFromParsingName("shell:AppsFolder\\" + launchId, IntPtr.Zero, in iid, out object obj);
                var item = (IShellItem2)obj;
                PROPERTYKEY key = PKEY_Link_TargetParsingPath;
                int hr = item.GetString(in key, out string val);
                Marshal.ReleaseComObject(item);
                if (hr == 0 && !string.IsNullOrWhiteSpace(val)
                    && val.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return val;
                return null;
            }
            catch { return null; }
        }

        private static Bitmap? IconFromPE(string path, int size)
        {
            IntPtr[] h = { IntPtr.Zero };
            try
            {
                int n = PrivateExtractIcons(path, 0, size, size, h, null, 1, 0);
                if (n <= 0 || h[0] == IntPtr.Zero) return null;
                using var icon = Icon.FromHandle(h[0]);
                using var raw = icon.ToBitmap();
                return new Bitmap(raw); // owns its pixels, detached from the HICON
            }
            catch { return null; }
            finally { if (h[0] != IntPtr.Zero) DestroyIcon(h[0]); }
        }

        // The shell returns a top-down 32-bit premultiplied-alpha DIB section.
        // Image.FromHbitmap drops the alpha (black background), so read the DIB
        // pixels directly and clone them into an owned bitmap.
        private static Bitmap? BitmapFromHBitmap(IntPtr hbitmap)
        {
            BITMAP bm = default;
            if (GetObject(hbitmap, Marshal.SizeOf<BITMAP>(), ref bm) == 0) return null;
            if (bm.bmWidth <= 0 || bm.bmHeight <= 0) return null;

            // Non-DIB or non-32bpp: no usable alpha, return an opaque copy.
            if (bm.bmBits == IntPtr.Zero || bm.bmBitsPixel != 32)
            {
                using var opaque = Image.FromHbitmap(hbitmap);
                return new Bitmap(opaque);
            }

            // Wrap the DIB pixels (premultiplied BGRA) and deep-copy into a bitmap
            // that owns its memory; cloning a PArgb bitmap yields correct alpha.
            using var wrapped = new Bitmap(bm.bmWidth, bm.bmHeight, bm.bmWidthBytes,
                PixelFormat.Format32bppPArgb, bm.bmBits);
            return new Bitmap(wrapped);
        }
    }
}
