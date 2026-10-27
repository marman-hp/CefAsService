using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Xilium.CefGlue.Broker
{
    internal sealed record SpawnLimitValues(int MaxWorkers, int MaxSleeping, int MaxSpawnsInFlight, int MaxWorkersPerIp, int NewWorkersPerIpPerMinute);

    internal sealed record SpawnDenial(int Status, string Reason, string Message, int RetryAfterSeconds);

    internal static class SpawnLimits
    {
        public const string MaxWorkersKey = "MaxWorkers";
        public const string MaxSleepingKey = "MaxSleeping";
        public const string MaxSpawnsInFlightKey = "MaxSpawnsInFlight";
        public const string MaxWorkersPerIpKey = "MaxWorkersPerIp";
        public const string NewWorkersPerIpPerMinuteKey = "NewWorkersPerIpPerMinute";

        public static int DefaultMaxWorkers => Math.Max(1, Environment.ProcessorCount);
        public const int DefaultMaxSleeping = 100;
        public const int DefaultMaxSpawnsInFlight = 4;
        public const int DefaultMaxWorkersPerIp = 0;
        public const int DefaultNewWorkersPerIpPerMinute = 0;

        public static SpawnLimitValues Current { get; private set; } = FromSettings(new BrokerSettingsData());

        public static void Load(BrokerSettingsData settings)
        {
            Current = FromSettings(settings);
            var l = Current;
            Console.WriteLine($"[Limits] MaxWorkers={Show(l.MaxWorkers)} (this machine: {Environment.ProcessorCount} logical processors), MaxSleeping={Show(l.MaxSleeping)}, " +
                $"MaxSpawnsInFlight={Show(l.MaxSpawnsInFlight)}, MaxWorkersPerIp={Show(l.MaxWorkersPerIp)}, NewWorkersPerIpPerMinute={Show(l.NewWorkersPerIpPerMinute)}  (0 = unlimited; set in Admin > Settings > Limits)");
        }

        private static string Show(int value) => value == 0 ? "unlimited" : value.ToString();

        public static SpawnLimitValues FromSettings(BrokerSettingsData s) => new(
            Parse(s.MaxWorkers, DefaultMaxWorkers),
            Parse(s.MaxSleeping, DefaultMaxSleeping),
            Parse(s.MaxSpawnsInFlight, DefaultMaxSpawnsInFlight),
            Parse(s.MaxWorkersPerIp, DefaultMaxWorkersPerIp),
            Parse(s.NewWorkersPerIpPerMinute, DefaultNewWorkersPerIpPerMinute));

        private static int Parse(string value, int fallback) => int.TryParse(value, out var n) && n >= 0 ? n : fallback;

        public static readonly object Gate = new();

        private static readonly ConcurrentDictionary<string, string> TenantIp = new();
        private static readonly Dictionary<string, Queue<DateTime>> RecentSpawns = new();
        private static readonly ConcurrentDictionary<string, DateTime> LastLogged = new();

        public static SpawnDenial Check(string keyId, bool waking, string clientIp, DateTime nowUtc)
        {
            var limits = Current;

            foreach (var gone in TenantIp.Keys.Where(k => !Program.SpawnedProcesses.ContainsKey(k)).ToList())
            {
                TenantIp.TryRemove(gone, out _);
            }

            var live = Program.SpawnedProcesses.Where(p => !p.Value.HasExited).Select(p => p.Key).ToList();

            if (limits.MaxWorkers > 0 && live.Count >= limits.MaxWorkers)
            {
                return new SpawnDenial(503, "workers", $"The server is full ({live.Count} of {limits.MaxWorkers} sessions are running). Try again in a minute.", 30);
            }

            if (limits.MaxSpawnsInFlight > 0 && live.Count(k => !Program.Registry.TryGet(k, out _)) >= limits.MaxSpawnsInFlight)
            {
                return new SpawnDenial(503, "starting", "Too many sessions are starting right now. Try again in a few seconds.", 5);
            }

            if (!waking && limits.MaxSleeping > 0 && WorkerManifest.GetGone().Count >= limits.MaxSleeping)
            {
                return new SpawnDenial(503, "sleeping", $"The server is holding its maximum of {limits.MaxSleeping} saved sessions. Try again later.", 60);
            }

            if (!string.IsNullOrEmpty(clientIp))
            {
                if (limits.MaxWorkersPerIp > 0 && live.Count(k => TenantIp.TryGetValue(k, out var ip) && ip == clientIp) >= limits.MaxWorkersPerIp)
                {
                    return new SpawnDenial(429, "per-ip", $"This address already has {limits.MaxWorkersPerIp} running sessions.", 60);
                }

                if (limits.NewWorkersPerIpPerMinute > 0)
                {
                    var window = nowUtc.AddMinutes(-1);
                    if (RecentSpawns.TryGetValue(clientIp, out var queue))
                    {
                        while (queue.Count > 0 && queue.Peek() < window)
                        {
                            queue.Dequeue();
                        }

                        if (queue.Count >= limits.NewWorkersPerIpPerMinute)
                        {
                            return new SpawnDenial(429, "rate", "Too many new sessions from this address. Try again in a minute.",
                                Math.Max(1, (int)Math.Ceiling((queue.Peek().AddMinutes(1) - nowUtc).TotalSeconds)));
                        }
                    }
                }
            }

            return null;
        }

        public static void Admitted(string keyId, string clientIp, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(clientIp))
            {
                return;
            }

            TenantIp[keyId] = clientIp;
            if (!RecentSpawns.TryGetValue(clientIp, out var queue))
            {
                queue = new Queue<DateTime>();
                RecentSpawns[clientIp] = queue;
            }

            queue.Enqueue(nowUtc);

            if (RecentSpawns.Count > 512)
            {
                foreach (var stale in RecentSpawns.Where(p => p.Value.Count == 0 || p.Value.Peek() < nowUtc.AddMinutes(-1)).Select(p => p.Key).ToList())
                {
                    RecentSpawns.Remove(stale);
                }
            }
        }

        public static void LogDenied(SpawnDenial denial, string keyId, string clientIp)
        {
            var now = DateTime.UtcNow;
            if (LastLogged.TryGetValue(denial.Reason, out var last) && now - last < TimeSpan.FromSeconds(10))
            {
                return;
            }

            LastLogged[denial.Reason] = now;
            Console.WriteLine($"[Limits] /assign refused ({denial.Reason}) for tenant '{keyId}' from {clientIp}: {denial.Message}");
        }
    }
}
