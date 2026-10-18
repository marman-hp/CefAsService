using System;
using System.IO;
using System.Linq;

namespace Xilium.CefGlue.Headless.Service
{
    internal static class RecordingSettings
    {
        public const string ConsentVersion = "2";

        public static bool Enabled => Env("ENABLED") == "1";

        public static int MaxConcurrent => Int("MAX_CONCURRENT", 2, 1, 16);

        public static int MaxMinutes => Int("MAX_MINUTES", 30, 1, 24 * 60);

        public static int MaxMb => Int("MAX_MB", 2048, 16, 64 * 1024);

        public static int LogDays => Int("LOG_DAYS", Xilium.CefGlue.Broker.Storage.RecordingLog.DefaultDays, 1, 3650);

        public static long DownloadBytesPerSecond => Int("DOWNLOAD_KBPS", 2048, 0, 1024 * 1024) * 1024L;

        public static bool Allowlist => Env("ALLOWLIST") == "1";

        public static string[] Domains => (Env("DOMAINS") ?? "")
            .Split(new[] { ',', ';', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(d => d.Trim())
            .ToArray();

        public static string OutputDir(string tenantId)
        {
            var cacheRoot = TenantCachePaths.Root(AppContext.BaseDirectory);
            var root = Path.Combine(Path.GetDirectoryName(cacheRoot.TrimEnd('\\', '/')) ?? cacheRoot, "recordings");
            var dir = string.IsNullOrEmpty(tenantId) ? "standalone" : TenantCachePaths.TenantDirName(tenantId);
            return Path.Combine(root, dir);
        }

        private static string Env(string name) => Environment.GetEnvironmentVariable("CEFGLUE_RECORDING_" + name);

        private static int Int(string name, int fallback, int min, int max)
            => int.TryParse(Env(name), out var v) && v >= min && v <= max ? v : fallback;
    }
}
