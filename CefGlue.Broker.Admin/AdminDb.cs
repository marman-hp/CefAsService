using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xilium.CefGlue.Broker.Storage;

namespace Xilium.CefGlue.Broker.Admin
{
    internal static class AdminDb
    {
        private static readonly object Gate = new();
        private static SqliteDb _db;

        public static string Path =>
            BrokerDb.PathFor(System.IO.Path.GetDirectoryName(Environment.GetEnvironmentVariable("CEFGLUE_WORKER_EXE_PATH") ?? ""));

        public static string Error { get; private set; }

        public static SqliteDb Db
        {
            get
            {
                lock (Gate)
                {
                    if (_db != null)
                    {
                        return _db;
                    }

                    try
                    {
                        _db = BrokerDb.OpenReadWrite(Path);
                        Error = null;
                        return _db;
                    }
                    catch (Exception ex)
                    {
                        Error = $"Cannot open {Path}: {ex.Message}";
                        throw new InvalidOperationException(Error, ex);
                    }
                }
            }
        }

        public static void Fail(string reason)
        {
            lock (Gate)
            {
                Error = reason;
            }
        }

        public static bool TryOpen()
        {
            try
            {
                _ = Db;
                Console.WriteLine($"[Admin] Settings database: {Path}");
                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Admin] FATAL: {Error}. Admin shows only this error until it's fixed and Admin is restarted.");
                Console.ResetColor();
                Program.LogError($"[Admin] {Error}: {ex.InnerException ?? ex}");
                return false;
            }
        }
    }

    internal sealed class BrokerDbView
    {
        public Dictionary<string, string> Saved { get; private init; }
        public Dictionary<string, string> SavedEncoderSettings { get; private init; }

        public Dictionary<string, string> Active { get; private init; }
        public Dictionary<string, string> ActiveEncoderSettings { get; private init; }

        public bool BrokerRunning => Active != null;
        public string UseWebRtc { get; private init; }
        public string IsolationMode { get; private init; }
        public IsolationModeSwitchInfo IsolationSwitch { get; private init; }
        public string WorkerExePath { get; private init; }
        public string[] AvailableEncoders { get; private init; }

        public DateTime? RuntimeUpdatedUtc { get; private init; }

        public static BrokerDbView Read()
        {
            var db = AdminDb.Db;
            var saved = BrokerDb.ReadJson(db, BrokerDb.Settings);
            var runtime = BrokerDb.ReadJson(db, BrokerDb.Runtime);

            string R(string key) => BrokerDb.AsString(runtime.GetValueOrDefault(key));

            var running = !runtime.ContainsKey(BrokerDb.RuntimeStoppedUtc)
                && int.TryParse(R(BrokerDb.RuntimePid), out var pid) && IsBrokerProcess(pid);

            Dictionary<string, string> active = null, activeEncoder = null;
            if (running && runtime.TryGetValue(BrokerDb.RuntimeActive, out var activeJson))
            {
                using var doc = JsonDocument.Parse(activeJson);
                active = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name == BrokerSettingsWriter.EncoderSettingsKey)
                    {
                        activeEncoder = BrokerDb.AsStringMap(prop.Value.GetRawText());
                    }
                    else
                    {
                        active[prop.Name] = BrokerDb.AsString(prop.Value.GetRawText());
                    }
                }
            }

            IsolationModeSwitchInfo isolationSwitch = null;
            if (runtime.TryGetValue(BrokerDb.RuntimeIsolationSwitch, out var switchJson))
            {
                using var doc = JsonDocument.Parse(switchJson);
                var s = doc.RootElement;
                isolationSwitch = new IsolationModeSwitchInfo(s.GetProperty("from").GetString(), s.GetProperty("to").GetString(),
                    s.GetProperty("atUtc").GetDateTime(), s.GetProperty("wipeOk").GetBoolean());
            }

            return new BrokerDbView
            {
                Saved = saved.Where(kv => kv.Key != BrokerSettingsWriter.EncoderSettingsKey)
                    .ToDictionary(kv => kv.Key, kv => BrokerDb.AsString(kv.Value), StringComparer.OrdinalIgnoreCase),
                SavedEncoderSettings = BrokerDb.AsStringMap(saved.GetValueOrDefault(BrokerSettingsWriter.EncoderSettingsKey)),
                Active = active,
                ActiveEncoderSettings = activeEncoder ?? (active != null ? new Dictionary<string, string>() : null),
                UseWebRtc = running ? R(BrokerDb.RuntimeUseWebRtc) : null,
                IsolationMode = running ? R(BrokerDb.RuntimeIsolationMode) : null,
                IsolationSwitch = running ? isolationSwitch : null,
                WorkerExePath = running ? R(BrokerDb.RuntimeWorkerExePath) : null,
                AvailableEncoders = runtime.TryGetValue(BrokerDb.RuntimeAvailableEncoders, out var enc)
                    ? JsonSerializer.Deserialize<string[]>(enc) ?? Array.Empty<string>()
                    : Array.Empty<string>(),
                RuntimeUpdatedUtc = running && long.TryParse(R(BrokerDb.RuntimeUpdatedUtc), out var ms)
                    ? DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
                    : null,
            };
        }

        private static bool IsBrokerProcess(int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                return !p.HasExited && string.Equals(p.ProcessName, "Xilium.CefGlue.Broker", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
