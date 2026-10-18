using System;
using System.Runtime.InteropServices;

namespace Xilium.CefGlue.Broker.Admin
{
    internal static class ConsoleQuickEdit
    {
        private const int StdInputHandle = -10;
        private const uint EnableQuickEditMode = 0x0040;
        private const uint EnableExtendedFlags = 0x0080;

        public static void Disable()
        {
            if (!OperatingSystem.IsWindows() || Console.IsInputRedirected)
            {
                return;
            }

            var handle = GetStdHandle(StdInputHandle);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1) || !GetConsoleMode(handle, out var mode))
            {
                return;
            }

            SetConsoleMode(handle, (mode & ~EnableQuickEditMode) | EnableExtendedFlags);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int stdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr consoleHandle, out uint mode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr consoleHandle, uint mode);
    }
}
