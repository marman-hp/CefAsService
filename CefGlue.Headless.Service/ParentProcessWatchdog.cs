using System;
using System.IO;
using System.Threading;

namespace Xilium.CefGlue.Headless.Service
{
    internal static class ParentProcessWatchdog
    {
        public static void Start(Action onParentGone)
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            var parent = ReadParentPid();
            if (parent <= 1)
            {
                return;
            }

            var thread = new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(2000);
                    var current = ReadParentPid();
                    if (current != parent)
                    {
                        onParentGone();
                        return;
                    }
                }
            })
            {
                IsBackground = true,
                Name = "parent-process-watchdog",
            };
            thread.Start();
        }

        private static int ReadParentPid()
        {
            try
            {
                var stat = File.ReadAllText("/proc/self/stat");
                var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
                return int.Parse(fields[1]);
            }
            catch
            {
                return -1;
            }
        }
    }
}
