using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskbarGroups.Core
{
    static class ShellLink
    {
        private static readonly Guid ShellLinkClsid = new("00021401-0000-0000-C000-000000000046");
        private const uint EnvVarBlockSignature = 0xA0000001;
        private const uint PropertyStoreBlockSignature = 0xA0000009;
        private const uint SerializedPropertyStorageVersion = 0x53505331; // "1SPS"

        /// <summary>
        /// Reads back what <see cref="InstallShortcut"/> wrote: where the shortcut
        /// points, what it passes on the command line, and the AppUserModelID filed
        /// with it. Reading the argument is how a pinned group's real name is
        /// recovered, since the ID can be an older name the group no longer uses.
        ///
        /// Parses the .lnk binary directly first (sub-millisecond for an entire
        /// taskbar folder, versus tens to hundreds of milliseconds through Shell COM)
        /// and falls back to COM for shortcuts whose target only lives in a PIDL or
        /// uses legacy non-ASCII ANSI strings.
        /// </summary>
        public static void ReadShortcut(string linkPath, out string target, out string arguments, out string appId)
        {
            if (TryReadShortcutFast(linkPath, out target, out arguments, out appId, out bool hasIdList)
                && (target.Length > 0 || !hasIdList))
                return;

            ReadShortcutViaCom(linkPath, out target, out arguments, out appId);
        }

        /// <summary>
        /// Fast path for scanning pinned taskbar shortcuts. Avoids invoking Shell COM
        /// on non-group pins such as File Explorer (no LinkInfo and no arguments) and
        /// only asks the shell for an AppUserModelID when a COM fallback actually
        /// resolves a target path.
        /// </summary>
        internal static bool TryReadPinnedShortcut(
            string linkPath,
            bool requireArguments,
            out string target,
            out string arguments,
            out string appId)
        {
            if (TryReadShortcutFast(linkPath, out target, out arguments, out appId, out bool hasIdList))
            {
                if (requireArguments && arguments.Length == 0) return false;
                if (target.Length > 0 || !hasIdList) return true;
            }

            if (!TryReadTargetViaCom(linkPath, out target, out arguments))
            {
                appId = string.Empty;
                return false;
            }
            if (target.Length == 0 || (requireArguments && arguments.Length == 0))
            {
                appId = string.Empty;
                return false;
            }
            appId = ReadAppUserModelId(linkPath);
            return true;
        }

        private static void ReadShortcutViaCom(string linkPath, out string target, out string arguments, out string appId)
        {
            target = string.Empty;
            arguments = string.Empty;
            appId = string.Empty;

            IShellLinkW link = null;
            try
            {
                link = (IShellLinkW)new CShellLink();
                ((IPersistFile)link).Load(linkPath, 0 /* STGM_READ */);

                var buffer = new StringBuilder(1024);
                link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
                target = buffer.ToString();

                buffer.Clear();
                link.GetArguments(buffer, buffer.Capacity);
                arguments = buffer.ToString();
            }
            finally
            {
                if (link != null) Marshal.ReleaseComObject(link);
            }

            appId = ReadAppUserModelId(linkPath);
        }

        /// <summary>
        /// Where a shortcut points and what it passes, without the AppUserModelID,
        /// which costs a second trip to the shell. False if it does not read.
        /// </summary>
        public static bool TryReadTarget(string linkPath, out string target, out string arguments)
        {
            if (TryReadShortcutFast(linkPath, out target, out arguments, out _, out bool hasIdList)
                && (target.Length > 0 || !hasIdList))
                return true;

            return TryReadTargetViaCom(linkPath, out target, out arguments);
        }

        private static bool TryReadTargetViaCom(string linkPath, out string target, out string arguments)
        {
            target = string.Empty;
            arguments = string.Empty;
            IShellLinkW link = null;
            try
            {
                link = (IShellLinkW)new CShellLink();
                ((IPersistFile)link).Load(linkPath, 0 /* STGM_READ */);

                var buffer = new StringBuilder(1024);
                link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
                target = buffer.ToString();

                // Edge's web apps carry their address too, so leave room for it.
                buffer = new StringBuilder(4096);
                link.GetArguments(buffer, buffer.Capacity);
                arguments = buffer.ToString();
                return true;
            }
            catch { return false; }
            finally { if (link != null) Marshal.ReleaseComObject(link); }
        }

        private static bool TryReadShortcutFast(
            string linkPath,
            out string target,
            out string arguments,
            out string appId,
            out bool hasLinkTargetIdList)
        {
            target = string.Empty;
            arguments = string.Empty;
            appId = string.Empty;
            hasLinkTargetIdList = false;

            try
            {
                byte[] bytes = File.ReadAllBytes(linkPath);
                return TryParseShortcut(bytes, out target, out arguments, out appId, out hasLinkTargetIdList);
            }
            catch { return false; }
        }

        /// <summary>
        /// Parses the target path, command-line arguments and System.AppUserModel.ID
        /// straight from a [MS-SHLLINK] byte stream. Returns false on truncated data
        /// or non-ASCII ANSI strings so the caller falls back to Windows Shell COM.
        /// </summary>
        internal static bool TryParseShortcut(
            ReadOnlySpan<byte> data,
            out string target,
            out string arguments,
            out string appId,
            out bool hasLinkTargetIdList)
        {
            target = string.Empty;
            arguments = string.Empty;
            appId = string.Empty;
            hasLinkTargetIdList = false;

            if (data.Length < 76) return false;
            if (BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0, 4)) != 0x4C) return false;
            if (new Guid(data.Slice(4, 16)) != ShellLinkClsid) return false;

            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(20, 4));
            hasLinkTargetIdList = (flags & 0x0001) != 0;
            bool hasLinkInfo = (flags & 0x0002) != 0;
            bool hasName = (flags & 0x0004) != 0;
            bool hasRelPath = (flags & 0x0008) != 0;
            bool hasWorkDir = (flags & 0x0010) != 0;
            bool hasArgs = (flags & 0x0020) != 0;
            bool hasIcon = (flags & 0x0040) != 0;
            bool isUnicode = (flags & 0x0080) != 0;

            int pos = 76;

            if (hasLinkTargetIdList)
            {
                if (pos + 2 > data.Length) return false;
                ushort idListSize = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(pos, 2));
                pos += 2 + idListSize;
                if (pos > data.Length) return false;
            }

            if (hasLinkInfo)
            {
                if (pos + 4 > data.Length) return false;
                int infoStart = pos;
                uint infoSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos, 4));
                if (infoSize < 0x1C || infoStart + (long)infoSize > data.Length) return false;

                uint infoHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(infoStart + 4, 4));
                uint infoFlags = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(infoStart + 8, 4));
                if ((infoFlags & 0x0001) != 0) // VolumeIDAndLocalBasePath
                {
                    if (infoHeaderSize >= 0x24)
                    {
                        uint localUni = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(infoStart + 0x1C, 4));
                        uint suffixUni = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(infoStart + 0x20, 4));
                        if (!TryReadUtf16Z(data, infoStart, (int)infoSize, (int)localUni, out string localPath)) return false;
                        if (!TryReadUtf16Z(data, infoStart, (int)infoSize, (int)suffixUni, out string suffix)) return false;
                        target = CombinePathSuffix(localPath, suffix);
                    }
                    else
                    {
                        uint localAnsi = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(infoStart + 0x10, 4));
                        uint suffixAnsi = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(infoStart + 0x18, 4));
                        if (!TryReadAsciiZ(data, infoStart, (int)infoSize, (int)localAnsi, out string localPath)) return false;
                        if (!TryReadAsciiZ(data, infoStart, (int)infoSize, (int)suffixAnsi, out string suffix)) return false;
                        target = CombinePathSuffix(localPath, suffix);
                    }
                }
                pos = infoStart + (int)infoSize;
            }

            if (hasName && !TryReadStringData(data, ref pos, isUnicode, out _)) return false;
            if (hasRelPath && !TryReadStringData(data, ref pos, isUnicode, out _)) return false;
            if (hasWorkDir && !TryReadStringData(data, ref pos, isUnicode, out _)) return false;
            if (hasArgs && !TryReadStringData(data, ref pos, isUnicode, out arguments)) return false;
            if (hasIcon && !TryReadStringData(data, ref pos, isUnicode, out _)) return false;

            while (pos + 4 <= data.Length)
            {
                uint blockSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos, 4));
                if (blockSize < 4) break;
                if (pos + (long)blockSize > data.Length) return false;
                if (blockSize >= 8)
                {
                    uint sig = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos + 4, 4));
                    ReadOnlySpan<byte> block = data.Slice(pos, (int)blockSize);
                    if (sig == EnvVarBlockSignature && target.Length == 0 && blockSize >= 788)
                    {
                        if (TryReadUtf16Z(block, 0, block.Length, 268, out string envUni) && envUni.Length > 0)
                            target = Environment.ExpandEnvironmentVariables(envUni);
                        else if (TryReadAsciiZ(block, 0, block.Length, 8, out string envAnsi) && envAnsi.Length > 0)
                            target = Environment.ExpandEnvironmentVariables(envAnsi);
                    }
                    else if (sig == PropertyStoreBlockSignature && appId.Length == 0)
                    {
                        if (TryReadAppIdFromPropertyStore(block.Slice(8), out string foundId))
                            appId = foundId;
                    }
                }
                pos += (int)blockSize;
            }

            return true;
        }

        private static string CombinePathSuffix(string localPath, string suffix)
        {
            if (suffix.Length == 0) return localPath;
            if (localPath.Length == 0) return suffix;
            return Path.Combine(localPath, suffix.TrimStart('\\'));
        }

        private static bool TryReadUtf16Z(ReadOnlySpan<byte> data, int baseOffset, int blockLen, int relOffset, out string value)
        {
            value = string.Empty;
            if (relOffset <= 0 || relOffset >= blockLen) return true;
            int start = baseOffset + relOffset;
            int end = baseOffset + blockLen;
            int p = start;
            while (p + 1 < end && (data[p] != 0 || data[p + 1] != 0)) p += 2;
            value = Encoding.Unicode.GetString(data.Slice(start, p - start));
            return true;
        }

        private static bool TryReadAsciiZ(ReadOnlySpan<byte> data, int baseOffset, int blockLen, int relOffset, out string value)
        {
            value = string.Empty;
            if (relOffset <= 0 || relOffset >= blockLen) return true;
            int start = baseOffset + relOffset;
            int end = baseOffset + blockLen;
            int p = start;
            bool nonAscii = false;
            while (p < end && data[p] != 0)
            {
                if (data[p] >= 0x80) nonAscii = true;
                p++;
            }
            ReadOnlySpan<byte> slice = data.Slice(start, p - start);
            if (!nonAscii)
            {
                value = Encoding.ASCII.GetString(slice);
                return true;
            }
            return TryDecodeAcp(slice, out value);
        }

        private static bool TryReadStringData(ReadOnlySpan<byte> data, ref int pos, bool isUnicode, out string value)
        {
            value = string.Empty;
            if (pos + 2 > data.Length) return false;
            ushort count = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(pos, 2));
            pos += 2;
            int byteLen = isUnicode ? count * 2 : count;
            if (pos + byteLen > data.Length) return false;
            ReadOnlySpan<byte> slice = data.Slice(pos, byteLen);
            pos += byteLen;
            if (isUnicode)
            {
                value = Encoding.Unicode.GetString(slice);
                return true;
            }
            foreach (byte b in slice)
                if (b >= 0x80) return TryDecodeAcp(slice, out value);
            value = Encoding.ASCII.GetString(slice);
            return true;
        }

        private static unsafe bool TryDecodeAcp(ReadOnlySpan<byte> bytes, out string value)
        {
            value = string.Empty;
            if (bytes.IsEmpty) return true;
            fixed (byte* src = bytes)
            {
                int chars = MultiByteToWideChar(0 /* CP_ACP */, 0, src, bytes.Length, null, 0);
                if (chars <= 0) return false;
                char[] buffer = new char[chars];
                fixed (char* dst = buffer)
                {
                    if (MultiByteToWideChar(0 /* CP_ACP */, 0, src, bytes.Length, dst, chars) <= 0)
                        return false;
                }
                value = new string(buffer);
                return true;
            }
        }

        [DllImport("kernel32.dll")]
        private static extern unsafe int MultiByteToWideChar(
            uint codePage, uint flags, byte* multiByteStr, int cbMultiByte, char* wideCharStr, int cchWideChar);

        private static bool TryReadAppIdFromPropertyStore(ReadOnlySpan<byte> store, out string appId)
        {
            appId = string.Empty;
            int pos = 0;
            while (pos + 4 <= store.Length)
            {
                uint storageSize = BinaryPrimitives.ReadUInt32LittleEndian(store.Slice(pos, 4));
                if (storageSize == 0) break;
                if (storageSize < 24 || pos + (long)storageSize > store.Length) return false;

                uint version = BinaryPrimitives.ReadUInt32LittleEndian(store.Slice(pos + 4, 4));
                if (version == SerializedPropertyStorageVersion)
                {
                    Guid fmtId = new(store.Slice(pos + 8, 16));
                    if (fmtId == PROPERTYKEY.AppUserModel_ID.fmtid)
                    {
                        int vpos = pos + 24;
                        int vend = pos + (int)storageSize;
                        while (vpos + 4 <= vend)
                        {
                            uint valSize = BinaryPrimitives.ReadUInt32LittleEndian(store.Slice(vpos, 4));
                            if (valSize == 0) break;
                            if (valSize < 13 || vpos + (long)valSize > vend) break;

                            uint pid = BinaryPrimitives.ReadUInt32LittleEndian(store.Slice(vpos + 4, 4));
                            // vpos + 8 is 1-byte reserved; vpos + 9 is 2-byte VARTYPE; vpos + 11 is 2-byte padding.
                            ushort vt = BinaryPrimitives.ReadUInt16LittleEndian(store.Slice(vpos + 9, 2));
                            if (pid == PROPERTYKEY.AppUserModel_ID.pid
                                && vt == (ushort)VarEnum.VT_LPWSTR
                                && vpos + 17 <= vpos + (long)valSize)
                            {
                                uint chars = BinaryPrimitives.ReadUInt32LittleEndian(store.Slice(vpos + 13, 4));
                                long bytes = (long)chars * 2;
                                if (chars > 0 && vpos + 17 + bytes <= vpos + (long)valSize)
                                {
                                    appId = Encoding.Unicode.GetString(store.Slice(vpos + 17, (int)bytes)).TrimEnd('\0');
                                    return appId.Length > 0;
                                }
                            }
                            vpos += (int)valSize;
                        }
                    }
                }
                pos += (int)storageSize;
            }
            return false;
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

        /// <summary>
        /// The file a shell property holding an item ID list points to, or null:
        /// how the shell files System.AppUserModel.BestShortcut, the Start menu
        /// shortcut an entry of the app list comes from.
        /// </summary>
        internal static string ReadIdListPath(string parsingName, PROPERTYKEY key)
        {
            Guid iid = typeof(IPropertyStore).GUID;
            IPropertyStore store;
            try
            {
                SHGetPropertyStoreFromParsingName(parsingName, IntPtr.Zero, GPS_DEFAULT, ref iid, out store);
            }
            catch { return null; }
            if (store == null) return null;

            try
            {
                store.GetValue(ref key, out PROPVARIANT value);
                try
                {
                    // A byte vector: the count where a pointer would go, the bytes after.
                    if (value.vt != (ushort)(VarEnum.VT_VECTOR | VarEnum.VT_UI1) || value.vectorItems == IntPtr.Zero)
                        return null;
                    var path = new StringBuilder(1024);
                    return SHGetPathFromIDListEx(value.vectorItems, path, (uint)path.Capacity, 0)
                        ? path.ToString() : null;
                }
                finally { PropVariantHelper.Clear(ref value); }
            }
            catch { return null; }
            finally { Marshal.ReleaseComObject(store); }
        }

        private const uint GPS_DEFAULT = 0;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SHGetPathFromIDListEx(IntPtr pidl, StringBuilder path, uint cchPath, uint options);

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
            // A vector keeps its element count at 8 and the pointer to them here.
            [FieldOffset(16)]
            public IntPtr vectorItems;
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPropertyStore
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
            public static readonly PROPERTYKEY AppUserModel_BestShortcut = new PROPERTYKEY(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 10);
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
