using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Xilium.CefGlue.Broker
{
    internal enum WorkerStatus
    {
        Healthy,
        Degraded,
    }

    internal sealed class WorkerRecord
    {
        public string WorkerId { get; init; }
        public string TenantId { get; init; }
        public string PageId { get; init; }
        public string Address { get; init; }
        public WorkerStatus Status { get; set; } = WorkerStatus.Healthy;
        public DateTime LastHeartbeatUtc { get; set; }
        public double Cpu { get; set; }
        public double Encoder { get; set; }
        public int Sessions { get; set; }
    }

    internal sealed class WorkerRegistry
    {
        private readonly ConcurrentDictionary<string, WorkerRecord> _byTenant = new();

        public static readonly TimeSpan MissedHeartbeatTimeout = TimeSpan.FromSeconds(15);

        public WorkerRecord Register(string tenantId, string pageId, string address)
        {
            var record = new WorkerRecord
            {
                WorkerId = Guid.NewGuid().ToString("n"),
                TenantId = tenantId,
                PageId = pageId,
                Address = address,
                LastHeartbeatUtc = DateTime.UtcNow,
            };

            _byTenant[tenantId] = record;
            return record;
        }

        public void Heartbeat(string tenantId, double cpu, double encoder, int sessions)
        {
            if (!_byTenant.TryGetValue(tenantId, out var record))
            {
                return;
            }

            record.Cpu = cpu;
            record.Encoder = encoder;
            record.Sessions = sessions;
            record.LastHeartbeatUtc = DateTime.UtcNow;
        }

        public bool Remove(string tenantId) => _byTenant.TryRemove(tenantId, out _);

        public bool TryGet(string tenantId, out WorkerRecord record) => _byTenant.TryGetValue(tenantId, out record);

        public IReadOnlyCollection<WorkerRecord> GetAll() => _byTenant.Values.ToArray();

        public IReadOnlyList<string> RemoveStale()
        {
            var cutoff = DateTime.UtcNow - MissedHeartbeatTimeout;
            var stale = _byTenant.Where(kv => kv.Value.LastHeartbeatUtc < cutoff).Select(kv => kv.Key).ToArray();

            foreach (var tenantId in stale)
            {
                _byTenant.TryRemove(tenantId, out _);
            }

            return stale;
        }
    }
}
