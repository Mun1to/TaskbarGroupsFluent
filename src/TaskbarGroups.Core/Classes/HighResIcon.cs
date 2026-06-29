using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Extracts 256px "jumbo" icons via the Windows system image list, instead of
    /// the 32px <see cref="Icon.ExtractAssociatedIcon"/>. Jumbo icons are also free
    /// of the shortcut-arrow overlay, fixing the blurry, arrowed .lnk icons.
    /// </summary>
    public static class HighResIcon
    {
        private const int SHIL_JUMBO = 0x4;       // 256x256
        private const int SHIL_EXTRALARGE = 0x2;  // 48x48
        private const uint SHGFI_SYSICONINDEX = 0x4000;
        private const int ILD_TRANSPARENT = 0x1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll", EntryPoint = "#727")]
        private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

        [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IImageList
        {
            [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
            [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
            [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
            [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
            [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
            [PreserveSig] int Draw(IntPtr pimldp);
            [PreserveSig] int Remove(int i);
            [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
        }

        /// <summary>
        /// Returns a tightly-cropped, high-resolution bitmap for the file's icon,
        /// or null if it can't be resolved.
        /// </summary>
        public static Bitmap GetIcon(string path)
        {
            foreach (int size in new[] { SHIL_JUMBO, SHIL_EXTRALARGE })
            {
                Bitmap result = TryGetFromImageList(path, size);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static Bitmap TryGetFromImageList(string path, int imageListSize)
        {
            var shfi = new SHFILEINFO();
            IntPtr res = SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_SYSICONINDEX);
            if (res == IntPtr.Zero)
                return null;

            if (SHGetImageList(imageListSize, ref IID_IImageList, out IImageList list) != 0 || list == null)
                return null;

            IntPtr hIcon = IntPtr.Zero;
            try
            {
                if (list.GetIcon(shfi.iIcon, ILD_TRANSPARENT, ref hIcon) != 0 || hIcon == IntPtr.Zero)
                    return null;

                using var icon = Icon.FromHandle(hIcon);
                using var raw = icon.ToBitmap();
                return CropToContent(raw);
            }
            finally
            {
                if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
            }
        }

        /// <summary>
        /// Jumbo icons place small artwork in a 256px transparent canvas. Crop to the
        /// non-transparent bounds so the icon fills its slot instead of looking tiny.
        /// </summary>
        private static Bitmap CropToContent(Bitmap source)
        {
            int minX = source.Width, minY = source.Height, maxX = -1, maxY = -1;

            BitmapData data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] bytes = new byte[stride * source.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

                for (int y = 0; y < source.Height; y++)
                {
                    for (int x = 0; x < source.Width; x++)
                    {
                        byte alpha = bytes[y * stride + x * 4 + 3];
                        if (alpha > 16)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
            }
            finally
            {
                source.UnlockBits(data);
            }

            if (maxX < minX || maxY < minY)
                return new Bitmap(source); // fully transparent — return as-is

            var rect = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return source.Clone(rect, PixelFormat.Format32bppArgb);
        }
    }
}
