using System;
using System.IO;

namespace Xilium.CefGlue.Broker.Admin
{
    internal sealed class ExePaths
    {
        public const string BrokerExeName = "Xilium.CefGlue.Broker.exe";
        public const string WorkerExeName = "Xilium.CefGlue.Headless.Service.exe";

        private readonly AdminSettingsStore _settings;

        public ExePaths(AdminSettingsStore settings)
        {
            _settings = settings;
        }

        public string BrokerExePath => Resolve(_settings.Snapshot().BrokerExePath, "CEFGLUE_BROKER_EXE_PATH", "Broker", BrokerExeName);

        public string WorkerExePath => Resolve(_settings.Snapshot().WorkerExePath, "CEFGLUE_WORKER_EXE_PATH", "Worker", WorkerExeName);

        public string BrokerStartBlocker => Validate(BrokerExePath, BrokerExeName, out _) is { } error
            ? $"Broker exe: {error}"
            : null;

        public static string Validate(string path, string expectedFileName, out string warning)
        {
            warning = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                return "not set.";
            }

            if (!Path.IsPathFullyQualified(path))
            {
                return "use a full path (e.g. D:\\...\\" + expectedFileName + ").";
            }

            if (!File.Exists(path))
            {
                return "file not found.";
            }

            if (!string.Equals(Path.GetFileName(path), expectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                warning = $"expected a file named {expectedFileName}.";
            }

            return null;
        }

        private static string Resolve(string saved, string envVar, string siblingFolder, string exeName)
        {
            if (!string.IsNullOrWhiteSpace(saved)) return saved;

            var fromEnv = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

            var packaged = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", siblingFolder, exeName));
            return File.Exists(packaged) ? packaged : null;
        }
    }
}
