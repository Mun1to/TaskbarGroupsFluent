using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using TaskbarGroups.Core;

namespace TaskbarGroups.App.Helpers;

/// <summary>
/// One main window per copy of the app. Since issues #10 and #11 the app can be
/// opened from the tray icon's double click and from every pinned group's menu,
/// and each of those used to start another copy with a window of its own, all of
/// them editing the same groups. Now a second start brings the open window forward
/// and leaves.
///
/// The names carry the app's folder, so a copy run from somewhere else (a test
/// build next to the installed one) keeps a window of its own and never raises
/// the other's.
/// </summary>
internal static class MainInstance
{
    private static readonly string Prefix = @"Local\TaskbarGroupsFluent.App." + FolderKey();

    private static Mutex? _owned;
    private static EventWaitHandle? _show;

    /// <summary>
    /// True if a main window of this copy was already open and has been asked to
    /// come forward, in which case this start has nothing left to do. False if this
    /// process should open the main window itself.
    /// </summary>
    public static bool HandOver()
    {
        try
        {
            _owned = new Mutex(initiallyOwned: true, Prefix + ".Main", out bool mine);
            if (mine) return false;

            _owned.Dispose();
            _owned = null;

            // Still starting, or gone between the two checks: open one ourselves
            // rather than leave the user with nothing.
            if (!EventWaitHandle.TryOpenExisting(Prefix + ".Show", out EventWaitHandle? show)) return false;

            using (show)
            {
                // The user started this process, so it may take the foreground; the
                // window lives in the other one, which has to be lent that right or
                // it would only flash on the taskbar.
                int pid = OtherMainWindowProcess();
                AllowSetForegroundWindow(pid != 0 ? pid : ASFW_ANY);
                show.Set();
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Brings <paramref name="window"/> forward whenever another start asks for it,
    /// for as long as this process lives.
    /// </summary>
    public static void Serve(Window window)
    {
        if (_owned is null) return;
        try
        {
            _show = new EventWaitHandle(false, EventResetMode.AutoReset, Prefix + ".Show");
        }
        catch { return; }

        var thread = new Thread(() =>
        {
            while (true)
            {
                try { _show.WaitOne(); }
                catch { return; }

                window.Dispatcher.BeginInvoke(() =>
                {
                    if (!window.IsVisible) window.Show();
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    window.Activate();
                });
            }
        })
        { IsBackground = true, Name = "main-instance" };
        thread.Start();
    }

    /// <summary>The process of this copy's open main window, or 0 if it cannot be told.</summary>
    private static int OtherMainWindowProcess()
    {
        int self = Environment.ProcessId;
        string here = Path.GetFullPath(AppContext.BaseDirectory);
        foreach (var p in Process.GetProcessesByName("TaskbarGroups.App"))
        {
            using (p)
            {
                try
                {
                    if (p.Id == self || p.MainWindowHandle == IntPtr.Zero) continue;
                    string? exe = p.MainModule?.FileName;
                    if (exe is not null && string.Equals(Path.GetDirectoryName(exe) + Path.DirectorySeparatorChar,
                            here, StringComparison.OrdinalIgnoreCase))
                        return p.Id;
                }
                catch { /* gone, or not ours to look at */ }
            }
        }
        return 0;
    }

    private static string FolderKey()
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Paths.exeFolder.ToUpperInvariant()));
        return Convert.ToHexString(hash, 0, 8);
    }

    private const int ASFW_ANY = -1;

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);
}
