using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// The icon a user picks for one app in a group (issue #15), from an image
    /// (.png, .jpg, .bmp), an .ico, or one of the icons inside an .exe or .dll.
    /// It is kept as the picture itself, never as the path it came from: see
    /// <see cref="ProgramShortcut.CustomIcon"/>.
    /// </summary>
    public static class PickedIcon
    {
        /// <summary>
        /// The largest side a picked icon is kept at. The panel never draws an app
        /// bigger than about 75 px (its largest size on a screen at 200 %), and the
        /// group's own icon made from its apps uses a quarter of 256 for each, so
        /// this is plenty and keeps ObjectData.xml small.
        /// </summary>
        public const int StoredSize = 128;

        /// <summary>The files an icon can be picked from, for the open dialog.</summary>
        public const string FileFilter = "*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.dll";

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int PrivateExtractIcons(string szFileName, int nIconIndex,
            int cxIcon, int cyIcon, IntPtr[] phicon, int[] piconid, int nIcons, int flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int ExtractIconEx(string lpszFile, int nIconIndex,
            IntPtr[] phiconLarge, IntPtr[] phiconSmall, int nIcons);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        /// <summary>A program or library, which can hold many icons.</summary>
        public static bool HoldsIcons(string path)
        {
            string ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            return ext == ".exe" || ext == ".dll";
        }

        /// <summary>How many icons a program or library holds; 1 for anything else.</summary>
        public static int CountIcons(string path)
        {
            if (!HoldsIcons(path)) return 1;
            try { return Math.Max(0, ExtractIconEx(path, -1, null, null, 0)); }
            catch { return 0; }
        }

        /// <summary>
        /// One icon out of an .ico, .exe or .dll at <paramref name="size"/> pixels,
        /// or null. Windows picks the closest size the file has and scales it.
        /// </summary>
        public static Bitmap Extract(string path, int index, int size)
        {
            IntPtr[] handles = { IntPtr.Zero };
            try
            {
                int n = PrivateExtractIcons(path, index, size, size, handles, null, 1, 0);
                if (n <= 0 || handles[0] == IntPtr.Zero) return null;
                using var icon = Icon.FromHandle(handles[0]);
                using var raw = icon.ToBitmap();
                return new Bitmap(raw); // owns its pixels, detached from the HICON
            }
            catch { return null; }
            finally { if (handles[0] != IntPtr.Zero) DestroyIcon(handles[0]); }
        }

        /// <summary>
        /// The picture at <paramref name="path"/> (the icon number
        /// <paramref name="index"/> for a program or library), at most
        /// <see cref="StoredSize"/> on its longer side, or null if it cannot be read.
        /// </summary>
        public static Bitmap Load(string path, int index = 0)
        {
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                Bitmap picture;
                if (HoldsIcons(path))
                    picture = Extract(path, index, StoredSize);
                else if (ext == ".ico")
                    picture = Extract(path, 0, Math.Min(StoredSize, LargestIconFrame(path)));
                else
                {
                    using var stream = new MemoryStream(File.ReadAllBytes(path));
                    using var loaded = Image.FromStream(stream);
                    picture = new Bitmap(loaded);
                }
                if (picture is null) return null;
                return FitStoredSize(picture);
            }
            catch { return null; }
        }

        /// <summary>The picture as it is kept in <see cref="ProgramShortcut.CustomIcon"/>.</summary>
        public static string Encode(Image picture)
        {
            using Bitmap fitted = FitStoredSize(new Bitmap(picture));
            using var stream = new MemoryStream();
            fitted.Save(stream, ImageFormat.Png);
            return Convert.ToBase64String(stream.ToArray());
        }

        /// <summary>The picture kept in <see cref="ProgramShortcut.CustomIcon"/>, or null if it does not read.</summary>
        public static Bitmap Decode(string data)
        {
            if (string.IsNullOrEmpty(data)) return null;
            try
            {
                using var stream = new MemoryStream(Convert.FromBase64String(data));
                using var loaded = Image.FromStream(stream);
                return new Bitmap(loaded);
            }
            catch { return null; }
        }

        // Shrinks a picture to fit StoredSize, keeping its shape; a smaller one is
        // left as it is. Takes ownership of the bitmap it is given.
        private static Bitmap FitStoredSize(Bitmap picture)
        {
            int longer = Math.Max(picture.Width, picture.Height);
            if (longer <= StoredSize) return picture;

            double scale = (double)StoredSize / longer;
            int width = Math.Max(1, (int)Math.Round(picture.Width * scale));
            int height = Math.Max(1, (int)Math.Round(picture.Height * scale));
            Bitmap fitted = ImageFunctions.ResizeImage(picture, width, height);
            picture.Dispose();
            return fitted;
        }

        // The widest picture in an .ico, from its directory (a width of 0 means 256).
        // Asking for it, rather than for StoredSize, keeps a 48 px icon from being
        // blown up and blurred on its way in.
        private static int LargestIconFrame(string path)
        {
            try
            {
                using var reader = new BinaryReader(File.OpenRead(path));
                reader.ReadUInt16();                // reserved
                if (reader.ReadUInt16() != 1) return StoredSize; // not an icon
                int count = reader.ReadUInt16();
                int largest = 0;
                for (int i = 0; i < count; i++)
                {
                    int width = reader.ReadByte();
                    reader.ReadBytes(15);
                    largest = Math.Max(largest, width == 0 ? 256 : width);
                }
                return largest > 0 ? largest : StoredSize;
            }
            catch { return StoredSize; }
        }
    }
}
