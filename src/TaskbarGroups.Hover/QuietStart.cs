using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskbarGroups.Hover;

/// <summary>
/// Starts a process without the "app starting" cursor.
///
/// Windows puts the spinning circle next to the arrow whenever a program starts
/// another one, and keeps it there until the new one is ready for input. That is
/// right for something the user launched, and wrong for what the watcher starts
/// on its own: the flyout it loads in the background as the cursor nears the
/// taskbar. Each of those flashed the cursor at the user for no reason they
/// could see, and on a PC where the flyout could not stay up, the watcher
/// started one after another and the cursor never stopped blinking (issue #6).
/// Process.Start has no way to turn it off, so this goes to CreateProcess.
/// </summary>
internal static class QuietStart
{
    public static Process Start(string exe, IEnumerable<string> args)
    {
        var line = new StringBuilder(Quote(exe));
        foreach (string a in args) line.Append(' ').Append(Quote(a));

        var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>(), dwFlags = STARTF_FORCEOFFFEEDBACK };
        if (!CreateProcess(exe, line, 0, 0, false, 0, 0, Path.GetDirectoryName(exe), ref si, out PROCESS_INFORMATION pi))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            // Looked up while our own handle is still open, so the id cannot have
            // been handed to another process in between. One that has already
            // ended throws here, which callers take as a start that failed.
            return Process.GetProcessById(pi.dwProcessId);
        }
        finally
        {
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
        }
    }

    /// <summary>
    /// One argument as the C runtime reads it back: in quotes when it has spaces,
    /// with backslashes doubled only where they come before a quote.
    /// </summary>
    private static string Quote(string arg)
    {
        if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return arg;

        var sb = new StringBuilder("\"");
        int slashes = 0;
        foreach (char c in arg)
        {
            if (c == '\\') { slashes++; continue; }
            sb.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            slashes = 0;
            sb.Append(c);
        }
        sb.Append('\\', slashes * 2).Append('"');
        return sb.ToString();
    }

    private const int STARTF_FORCEOFFFEEDBACK = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public nint lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public nint hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string app, StringBuilder commandLine, nint processAttributes,
        nint threadAttributes, bool inheritHandles, int flags, nint environment, string? currentDirectory,
        ref STARTUPINFO startup, out PROCESS_INFORMATION info);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
}
