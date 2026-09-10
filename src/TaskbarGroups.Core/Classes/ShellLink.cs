using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskbarGroups.Core
{
    static class ShellLink
    {
        /// <summary>
        /// Reads back what <see cref="InstallShortcut"/> wrote: where the shortcut
        /// points, what it passes on the command line, and the AppUserModelID filed
        /// with it. Reading the argument is how a pinned group's real name is
        /// recovered, since the ID can be an older name the group no longer uses.
        /// </summary>
        public static void ReadShortcut(string linkPath, out string target, out string arguments, out string appId)
        {
            target = string.Empty;
            arguments = string.Empty;
            appId = string.Empty;

            IShellLinkW link = (IShellLinkW)new CShellLink();
            ((IPersistFile)link).Load(linkPath, 0 /* STGM_READ */);

            var buffer = new StringBuilder(1024);
            link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
            target = buffer.ToString();

            buffer.Clear();
            link.GetArguments(buffer, buffer.Capacity);
            arguments = buffer.ToString();

            appId = ReadAppUserModelId(linkPath);
        }

        /// <summary>
        /// The AppUserModelID filed with a shortcut, or an empty string if it has
        /// none.
        ///
        /// It is read through the shell rather than off the IShellLinkW just loaded:
        /// that object does expose IPropertyStore, but after IPersistFile::Load every
        /// property comes back VT_EMPTY, because that store is only populated for a
        /// link being written. SHGetPropertyStoreFromParsingName asks the shell for
        /// the file's own store, which is where the saved value actually lives.
        /// </summary>
        private static string ReadAppUserModelId(string linkPath)
        {
            Guid iid = typeof(IPropertyStore).GUID;
            IPropertyStore store;
            try
            {
                SHGetPropertyStoreFromParsingName(linkPath, IntPtr.Zero, GPS_DEFAULT, ref iid, out store);
            }
            catch { return string.Empty; }
            if (store == null) return string.Empty;

            try
            {
                PROPERTYKEY key = PROPERTYKEY.AppUserModel_ID;
                store.GetValue(ref key, out PROPVARIANT value);
                try
                {
                    // Anything but a string means no ID was filed with this shortcut.
                    if (value.vt == (ushort)VarEnum.VT_LPWSTR && value.unionmember != IntPtr.Zero)
                        return Marshal.PtrToStringUni(value.unionmember) ?? string.Empty;
                }
                finally { PropVariantHelper.Clear(ref value); }
            }
            catch { /* a shortcut without the property is not an error */ }
            finally { Marshal.ReleaseComObject(store); }

            return string.Empty;
        }

        private const uint GPS_DEFAULT = 0;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHGetPropertyStoreFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr bindContext, uint flags,
            ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

        public static void InstallShortcut(string exePath, string appId, string desc, string wkDirec, string iconLocation, string saveLocation, string arguments)
        {
            // Use passed parameters as to construct the shortcut
            IShellLinkW newShortcut = (IShellLinkW)new CShellLink();
            newShortcut.SetPath(exePath);
            newShortcut.SetDescription(desc);
            newShortcut.SetWorkingDirectory(wkDirec);
            newShortcut.SetArguments(arguments);
            newShortcut.SetIconLocation(iconLocation, 0);


            // Set the classID of the shortcut that is created
            // Crucial to avoid program stacking
            IPropertyStore newShortcutProperties = (IPropertyStore)newShortcut;

            PropVariantHelper varAppId = new PropVariantHelper();
            varAppId.SetValue(appId);
            newShortcutProperties.SetValue(PROPERTYKEY.AppUserModel_ID, varAppId.Propvariant);

            // Save the shortcut as per passed save location
            IPersistFile newShortcutSave = (IPersistFile)newShortcut;
            newShortcutSave.Save(saveLocation, true);
        }

        #region COM APIs
        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellLinkW
        {
            void GetPath([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotKey(out short wHotKey);
            void SetHotKey(short wHotKey);
            void GetShowCmd(out uint iShowCmd);
            void SetShowCmd(uint iShowCmd);
            void GetIconLocation([Out(), MarshalAs(UnmanagedType.LPWStr)] out StringBuilder pszIconPath, int cchIconPath, out int iIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPersistFile
        {
            void GetCurFile([Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile);
            void IsDirty();
            // dwMode is a DWORD. It was declared as a long marshalled as U4, which
            // .NET rejects outright ("Int64/UInt64 must be paired with I8 or U8"), so
            // any call to Load threw before it reached COM. Nothing called it until
            // now, which is why the mismatch went unnoticed.
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        }

        // A native PROPVARIANT is 24 bytes on x64: an 8-byte header, then a union
        // wide enough for a DECIMAL. This was declared 16 bytes wide, which was
        // harmless while the only use was SetValue (COM reads it and never writes
        // back), but GetValue writes the full 24 into whatever it is handed, and a
        // short struct means it writes past the end and reads back as VT_EMPTY. The
        // trailing padding is never touched by hand; it is here to make the struct
        // the size the callee expects.
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        public struct PROPVARIANT
        {
            [FieldOffset(0)]
            public ushort vt;
            [FieldOffset(8)]
            public IntPtr unionmember;
            [FieldOffset(8)]
            public UInt64 forceStructToLargeEnoughSize;
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            void GetCount([Out] out uint propertyCount);
            void GetAt([In] uint propertyIndex, [Out, MarshalAs(UnmanagedType.Struct)] out PROPERTYKEY key);
            // Neither argument is marshalled as UnmanagedType.Struct, which means
            // "VARIANT" and is a different layout entirely. Declaring the key that
            // way sent the store a mangled key, so every read came back VT_EMPTY
            // even for shortcuts that plainly had the property. Both structs already
            // match their native shape, so they pass straight through.
            void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            void SetValue([In, MarshalAs(UnmanagedType.Struct)] ref PROPERTYKEY key, [In, MarshalAs(UnmanagedType.Struct)] ref PROPVARIANT pv);
            void Commit();
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)]
        internal class CShellLink { }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;

            public PROPERTYKEY(Guid guid, uint id)
            {
                fmtid = guid;
                pid = id;
            }

            public static readonly PROPERTYKEY AppUserModel_ID = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
        }
        #endregion

        internal class PropVariantHelper
        {
            private static class NativeMethods
            {
                [DllImport("Ole32.dll", PreserveSig = false)]
                internal static extern void PropVariantClear(ref PROPVARIANT pvar);
            }

            private PROPVARIANT variant;
            public PROPVARIANT Propvariant => variant;

            /// <summary>Frees a PROPVARIANT handed back by IPropertyStore.GetValue.</summary>
            public static void Clear(ref PROPVARIANT value) => NativeMethods.PropVariantClear(ref value);

            public void SetValue(string val)
            {
                NativeMethods.PropVariantClear(ref variant);
                variant.vt = (ushort)VarEnum.VT_LPWSTR;
                variant.unionmember = Marshal.StringToCoTaskMemUni(val);
            }
        }
    }
}
