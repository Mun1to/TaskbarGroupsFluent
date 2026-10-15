using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// The right-click menu of a pinned group: "Open all" and "Edit group". Windows
    /// keeps one such list per AppUserModelID, and every pinned group already has
    /// its own (<see cref="Category.AppIdPrefix"/> plus its name), so registering a
    /// list under that ID is all it takes for the menu to appear on its button.
    ///
    /// Both tasks start our own programs with arguments: the flyout with
    /// "--open-all", the app with "--edit". Everything here is best effort; a list
    /// that fails to register only means the menu shows Windows' defaults.
    /// </summary>
    public static class GroupJumpList
    {
        public static bool Register(string groupName, string openAllTitle, string editTitle)
        {
            try
            {
                var list = (ICustomDestinationList)new CDestinationList();
                list.SetAppID(Category.AppIdPrefix + groupName);
                Guid iid = typeof(IObjectArray).GUID;
                list.BeginList(out _, ref iid, out _);

                string quoted = "\"" + groupName + "\"";
                string groupIcon = Path.Combine(Paths.ConfigPath, groupName, "GroupIcon.ico");
                string icon = File.Exists(groupIcon) ? groupIcon : Paths.BackgroundApplication;

                var tasks = (IObjectCollection)new CEnumerableObjectCollection();
                tasks.AddObject(Task(Paths.BackgroundApplication, quoted + " --open-all", openAllTitle, icon));
                tasks.AddObject(Task(Paths.MainApplication, "--edit " + quoted, editTitle, Paths.MainApplication));
                list.AddUserTasks((IObjectArray)tasks);
                list.CommitList();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Drops a group's menu, for when the group is deleted or renamed.</summary>
        public static void Remove(string groupName)
        {
            try { ((ICustomDestinationList)new CDestinationList()).DeleteList(Category.AppIdPrefix + groupName); }
            catch { /* nothing registered, or nothing to do */ }
        }

        private static ShellLink.IShellLinkW Task(string exe, string args, string title, string icon)
        {
            var link = (ShellLink.IShellLinkW)new ShellLink.CShellLink();
            link.SetPath(exe);
            link.SetArguments(args);
            link.SetIconLocation(icon, 0);

            // A task's caption is the shortcut's title property, not its description.
            var store = (ShellLink.IPropertyStore)link;
            var value = new ShellLink.PropVariantHelper();
            value.SetValue(title);
            var key = TitleKey;
            var variant = value.Propvariant;
            store.SetValue(ref key, ref variant);
            store.Commit();
            return link;
        }

        private static readonly ShellLink.PROPERTYKEY TitleKey =
            new ShellLink.PROPERTYKEY(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 2);

        #region COM APIs
        [ComImport, Guid("6332DEBF-87B5-4670-90C0-5E57B408A49E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICustomDestinationList
        {
            void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
            void BeginList(out uint pcMinSlots, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
            void AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string pszCategory, [MarshalAs(UnmanagedType.Interface)] IObjectArray poa);
            void AppendKnownCategory(int category);
            void AddUserTasks([MarshalAs(UnmanagedType.Interface)] IObjectArray poa);
            void CommitList();
            void GetRemovedDestinations(ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
            void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
            void AbortList();
        }

        [ComImport, Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IObjectArray
        {
            void GetCount(out uint pcObjects);
            void GetAt(uint uiIndex, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        }

        [ComImport, Guid("5632B1A4-E38A-400A-928A-D4CD63230295"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IObjectCollection
        {
            // IObjectArray's two methods come first in the vtable.
            void GetCount(out uint pcObjects);
            void GetAt(uint uiIndex, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
            void AddObject([MarshalAs(UnmanagedType.Interface)] object pvObject);
            void AddFromArray([MarshalAs(UnmanagedType.Interface)] IObjectArray poaSource);
            void RemoveObjectAt(uint uiIndex);
            void Clear();
        }

        [ComImport, Guid("77F10CF0-3DB5-4966-B520-B7C54FD35ED6"), ClassInterface(ClassInterfaceType.None)]
        private class CDestinationList { }

        [ComImport, Guid("2D3468C1-36A7-43B6-AC24-D3F02FD9607A"), ClassInterface(ClassInterfaceType.None)]
        private class CEnumerableObjectCollection { }
        #endregion
    }
}
