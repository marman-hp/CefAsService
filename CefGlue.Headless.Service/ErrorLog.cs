using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Xilium.CefGlue.Headless.Service
{
    internal static class ErrorLog
    {
        public const string ServiceFileName = "service-error.log";
        public const string BrokerFileName = "broker-error.log";
        public const string AdminFileName = "admin-error.log";

        private const long MaxBytes = 5 * 1024 * 1024;

        private static readonly object Gate = new();

        public static string LogDirectory(string workerExeDirectory)
        {
            var root = TenantCachePaths.Root(workerExeDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.Combine(Path.GetDirectoryName(root) ?? root, "logs");
        }

        public static void Append(string fileName, string workerExeDirectory, string message)
        {
            try
            {
                var directory = LogDirectory(workerExeDirectory);
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, fileName);
                var bytes = Encoding.UTF8.GetBytes($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}{Environment.NewLine}{Environment.NewLine}");

                lock (Gate)
                {
                    for (var attempt = 0; attempt < 20; attempt++)
                    {
                        try
                        {
                            var info = new FileInfo(path);
                            if (info.Exists && info.Length > MaxBytes)
                            {
                                File.Move(path, path + ".old", overwrite: true);
                            }

                            using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                            stream.Write(bytes, 0, bytes.Length);
                            return;
                        }
                        catch (IOException)
                        {
                            Thread.Sleep(50);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public static void InstallUnhandledExceptionHandler(string fileName, string workerExeDirectory, Func<string> describeProcess)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                string who;
                try
                {
                    who = describeProcess();
                }
                catch
                {
                    who = $"pid {Environment.ProcessId}";
                }

                Append(fileName, workerExeDirectory, $"Unhandled exception - {who}{Environment.NewLine}{e.ExceptionObject}");
            };
        }
    }
}
