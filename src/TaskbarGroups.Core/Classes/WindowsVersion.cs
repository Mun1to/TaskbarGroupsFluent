using System;

namespace TaskbarGroups.Core
{
    /// <summary>
    /// Which Windows this is, where it changes what the app does. Windows 10 and 11
    /// share most of the shell, but not all of it: the taskbar of Windows 11 is a
    /// new one, its right-click menu hides "Pin to taskbar" one level down, and only
    /// it rounds the corners of a window.
    /// </summary>
    public static class WindowsVersion
    {
        /// <summary>
        /// TBGF_AS_WINDOWS10=1 sends the app down its Windows 10 paths on Windows 11.
        /// It is only for trying those paths out; Windows itself stays what it is.
        /// </summary>
        private static readonly bool ActAsWindows10 =
            Environment.GetEnvironmentVariable("TBGF_AS_WINDOWS10") == "1";

        /// <summary>True on Windows 11, build 22000 and later.</summary>
        public static bool IsWindows11 { get; } =
            !ActAsWindows10 && Environment.OSVersion.Version.Build >= 22000;
    }
}
