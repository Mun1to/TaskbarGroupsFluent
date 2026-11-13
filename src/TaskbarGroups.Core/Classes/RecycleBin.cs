#nullable enable annotations
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// What a group showing the Recycle Bin can do with it besides listing it
    /// (issue #20): empty it, and put everything in it back where it came from.
    /// Both go through Windows, so Windows asks and warns as it does in File
    /// Explorer: emptying asks first, and a file whose place is taken asks what to
    /// do with it.
    /// </summary>
    public static class RecycleBin
    {
        private const string Clsid = "::{645FF040-5081-101B-9F08-00AA002F954E}";

        /// <summary>Whether a folder group's <paramref name="source"/> is the Recycle Bin.</summary>
        public static bool Is(string? source)
        {
            if (string.IsNullOrWhiteSpace(source)) return false;
            string s = source.Trim();
            return s.Equals("shell:RecycleBinFolder", StringComparison.OrdinalIgnoreCase)
                || s.Equals(Clsid, StringComparison.OrdinalIgnoreCase)
                || s.Equals("shell:" + Clsid, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>How many items the bin holds, all drives together; -1 if it can't be told.</summary>
        public static long Count()
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            return SHQueryRecycleBin(null, ref info) == 0 ? info.i64NumItems : -1;
        }

        /// <summary>
        /// Empties the bin. Windows asks first, in its own words, and shows its
        /// progress; nothing happens if the bin is already empty.
        /// </summary>
        public static void Empty(IntPtr owner)
        {
            if (Count() == 0) return;
            OnItsOwn(() => SHEmptyRecycleBin(owner, null, 0));
        }

        /// <summary>
        /// Puts every item in the bin back where it was deleted from, after asking.
        /// Each one goes through its own "Restore" command, the one File Explorer
        /// runs, by the name Windows gives it in every language ("undelete").
        /// </summary>
        public static void RestoreAll(IntPtr owner, string question, string title)
        {
            long count = Count();
            if (count == 0) return;
            // In front of everything: it is asked from a menu, whose window may be gone by now.
            if (MessageBoxW(owner, string.Format(question, count), title,
                    MB_YESNO | MB_ICONQUESTION | MB_SETFOREGROUND | MB_TOPMOST) != IDYES)
                return;
            OnItsOwn(() => Restore(_ => true));
        }

        /// <summary>
        /// Runs a bin command away from the caller's thread, which draws a panel or
        /// watches the taskbar and must not stop for it: a bin of several hundred
        /// items takes a while either way. Not a background thread, so a panel that
        /// closes meanwhile does not end its process halfway through.
        /// </summary>
        private static void OnItsOwn(Action work)
        {
            var thread = new System.Threading.Thread(() =>
            {
                try { work(); }
                catch { /* Windows says what went wrong in its own dialogs, if anything */ }
            })
            { IsBackground = false, Name = "recycle-bin" };
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
        }

        /// <summary>
        /// Restores the items in the bin whose original name passes
        /// <paramref name="which"/>, without asking. For <see cref="RestoreAll"/>,
        /// and for tests that restore only what they put there themselves.
        /// </summary>
        public static int Restore(Func<string, bool> which)
        {
            int restored = 0;
            object? shell = null;
            try
            {
                Type? type = Type.GetTypeFromProgID("Shell.Application");
                if (type is null) return 0;
                shell = Activator.CreateInstance(type);
                if (shell is null) return 0;

                // 10 is the Recycle Bin among the shell's special folders (ssfBITBUCKET).
                object? bin = Call(shell, "NameSpace", 10);
                object? items = bin is null ? null : Call(bin, "Items");
                if (items is null) return 0;

                // Taken as a list first: restoring an item changes the collection.
                int n = Convert.ToInt32(Get(items, "Count"));
                var list = new System.Collections.Generic.List<object>();
                for (int i = 0; i < n; i++)
                    if (Call(items, "Item", i) is { } item) list.Add(item);

                foreach (object item in list)
                {
                    try
                    {
                        string name = Get(item, "Name") as string ?? "";
                        if (!which(name)) continue;
                        Call(item, "InvokeVerb", "undelete");
                        restored++;
                    }
                    catch { /* one that will not come back must not stop the rest */ }
                    finally { Marshal.ReleaseComObject(item); }
                }
            }
            catch { }
            finally
            {
                if (shell is not null) Marshal.ReleaseComObject(shell);
            }
            return restored;
        }

        private static object? Call(object target, string name, params object[] args)
            => target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);

        private static object? Get(object target, string name)
            => target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);

        // 64-bit layout, the only one this app ships: the counts sit on 8-byte boundaries.
        [StructLayout(LayoutKind.Sequential)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        private const uint MB_YESNO = 0x4, MB_ICONQUESTION = 0x20, MB_SETFOREGROUND = 0x10000, MB_TOPMOST = 0x40000;
        private const int IDYES = 6;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string? rootPath, ref SHQUERYRBINFO info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr owner, string? rootPath, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
    }

    /// <summary>
    /// What the bin's commands are called, in the words File Explorer uses for
    /// them, shared by the menu by the clock and the panel's.
    /// </summary>
    public static class RecycleBinText
    {
        public static string Title => UiPreferences.Say("Recycle Bin", "Papelera de reciclaje", "Корзина");

        public static string Empty => UiPreferences.Say("Empty Recycle Bin", "Vaciar Papelera de reciclaje", "Очистить корзину");

        public static string RestoreAll => UiPreferences.Say("Restore all items", "Restaurar todos los elementos", "Восстановить все объекты");

        /// <summary>Asked before restoring; {0} is how many items there are.</summary>
        public static string RestoreAllQuestion => UiPreferences.Say(
            "Put the {0} items in the Recycle Bin back where they were?",
            "¿Devolver los {0} elementos de la Papelera a donde estaban?",
            "Вернуть все объекты из корзины ({0}) на прежнее место?");
    }
}
