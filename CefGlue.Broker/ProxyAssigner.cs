using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xilium.CefGlue.Headless.Service;

namespace Xilium.CefGlue.Broker
{
    public sealed record ProxyRequest(string TenantId, ProxySettings Settings, string ChosenProxyId = null);

    public sealed record ProxySettings(string GlobalProxy, string Mode = null, string PoolText = null, bool Enabled = false, string GlobalProxyId = null);

    public sealed record ProxyAssignment(string ProxyId, ProxyEndpoint Endpoint);

    public sealed record ProxyOption(string Id, string Label);

    public enum ProxyDecisionKind
    {
        Direct,

        Assigned,

        Refused,

        Choose,
    }

    public sealed class ProxyDecision
    {
        private ProxyDecision(ProxyDecisionKind kind, ProxyAssignment assignment, string reason, IReadOnlyList<ProxyOption> options)
        {
            Kind = kind;
            Assignment = assignment;
            Reason = reason;
            Options = options;
        }

        public ProxyDecisionKind Kind { get; }
        public ProxyAssignment Assignment { get; }
        public string Reason { get; }
        public IReadOnlyList<ProxyOption> Options { get; }

        public static ProxyDecision Direct { get; } = new(ProxyDecisionKind.Direct, null, null, null);
        public static ProxyDecision Assigned(ProxyAssignment assignment) => new(ProxyDecisionKind.Assigned, assignment ?? throw new ArgumentNullException(nameof(assignment)), null, null);
        public static ProxyDecision Refused(string reason) => new(ProxyDecisionKind.Refused, null, reason, null);
        public static ProxyDecision Choose(IReadOnlyList<ProxyOption> options) => new(ProxyDecisionKind.Choose, null, null, options ?? throw new ArgumentNullException(nameof(options)));
    }

    public interface IProxyAssigner
    {
        ValueTask<ProxyDecision> AssignAsync(ProxyRequest request, CancellationToken cancellationToken);

        void ReportFailure(string tenantId, string proxyId, string reason)
        {
        }
    }

    internal sealed class ConfiguredProxyAssigner : IProxyAssigner
    {
        private readonly GlobalProxyAssigner _global = new();
        private readonly PoolProxyAssigner _pool = new();

        public ValueTask<ProxyDecision> AssignAsync(ProxyRequest request, CancellationToken cancellationToken)
        {
            if (!request.Settings.Enabled)
            {
                return ValueTask.FromResult(ProxyDecision.Direct);
            }

            return string.Equals(request.Settings.Mode, "pool", StringComparison.OrdinalIgnoreCase)
                ? _pool.AssignAsync(request, cancellationToken)
                : _global.AssignAsync(request, cancellationToken);
        }
    }

    internal sealed class GlobalProxyAssigner : IProxyAssigner
    {
        public ValueTask<ProxyDecision> AssignAsync(ProxyRequest request, CancellationToken cancellationToken)
        {
            var settings = request.Settings;
            if (!string.IsNullOrWhiteSpace(settings.GlobalProxyId))
            {
                if (!ProxyUrl.TryParsePool(settings.PoolText, out var pool, out var poolError))
                {
                    return ValueTask.FromResult(ProxyDecision.Refused($"The proxy table saved in the Admin is not valid ({poolError}) - fix it there."));
                }

                var chosen = pool.FirstOrDefault(p => p.Id == settings.GlobalProxyId);
                return chosen != null
                    ? ValueTask.FromResult(ProxyDecision.Assigned(new ProxyAssignment(chosen.Label, chosen.Endpoint)))
                    : ValueTask.FromResult(ProxyDecision.Refused("The global proxy chosen in the Admin is no longer in the table - choose one there."));
            }

            var legacy = settings.GlobalProxy;
            if (string.IsNullOrWhiteSpace(legacy))
            {
                return ValueTask.FromResult(ProxyDecision.Refused("The proxy is switched on but no global proxy is chosen in the Admin."));
            }

            return ProxyUrl.TryParse(legacy, out var endpoint, out var error)
                ? ValueTask.FromResult(ProxyDecision.Assigned(new ProxyAssignment(endpoint.Server, endpoint)))
                : ValueTask.FromResult(ProxyDecision.Refused($"The proxy saved in the Admin is not valid ({error}) - fix it there."));
        }
    }

    internal sealed class PoolProxyAssigner : IProxyAssigner
    {
        private static readonly object Gate = new();

        public ValueTask<ProxyDecision> AssignAsync(ProxyRequest request, CancellationToken cancellationToken)
        {
            if (!ProxyUrl.TryParsePool(request.Settings.PoolText, out var pool, out var error))
            {
                return ValueTask.FromResult(ProxyDecision.Refused($"The proxy pool saved in the Admin is not valid ({error}) - fix it there."));
            }

            if (pool.Count == 0)
            {
                return ValueTask.FromResult(ProxyDecision.Refused("Proxy pool mode is on but the pool is empty - add proxies in the Admin."));
            }

            lock (Gate)
            {
                var assignments = ProxyAssignments.Load();

                if (assignments.TryGetValue(request.TenantId, out var storedId))
                {
                    var stored = pool.FirstOrDefault(p => p.Id == storedId);
                    if (stored != null)
                    {
                        return ValueTask.FromResult(ProxyDecision.Assigned(new ProxyAssignment(stored.Label, stored.Endpoint)));
                    }

                    ProxyAssignments.Forget(request.TenantId);
                    assignments.Remove(request.TenantId);
                }

                var picked = pool.FirstOrDefault(p => p.Id == request.ChosenProxyId);
                if (picked != null)
                {
                    ProxyAssignments.Set(request.TenantId, picked.Id);
                    return ValueTask.FromResult(ProxyDecision.Assigned(new ProxyAssignment(picked.Label, picked.Endpoint)));
                }

                return ValueTask.FromResult(ProxyDecision.Choose(pool.Select(p => new ProxyOption(p.Id, p.Label)).ToList()));
            }
        }
    }

    internal static class ProxyAssignments
    {
        public static Dictionary<string, string> Load() => Storage.BrokerStore.LoadProxyAssignments();

        public static void Set(string tenantId, string proxyId) => Storage.BrokerStore.SetProxyAssignment(tenantId, proxyId);

        public static void Forget(string tenantId)
        {
            if (!string.IsNullOrEmpty(tenantId))
            {
                Storage.BrokerStore.ForgetProxyAssignment(tenantId);
                Storage.BrokerStore.ForgetContextProxies(tenantId);
            }
        }

        public static bool PerTabChoiceApplies(BrokerSettingsData settings)
            => settings.ProxyEnabled == "1" && string.Equals(settings.ProxyMode, "pool", StringComparison.OrdinalIgnoreCase)
               && Program.IsolationMode == IsolationModeGuard.Session;

        public static Dictionary<string, string> ContextsOf(string tenantId) => Storage.BrokerStore.LoadContextProxies(tenantId);

        public static void SetContext(string contextId, string tenantId, string proxyId) => Storage.BrokerStore.SetContextProxy(contextId, tenantId, proxyId);

        public static string LabelFor(string tenantId, BrokerSettingsData settings)
            => Describe(tenantId, settings) is { } d ? (PerTabChoiceApplies(settings) ? "Manual" : d.Label) : null;

        public static ProxyInfo Describe(string tenantId, BrokerSettingsData settings, IReadOnlyDictionary<string, string> assignments = null,
            IReadOnlyList<ProxyPoolEntry> pool = null)
        {
            if (settings.ProxyEnabled != "1")
            {
                return null;
            }

            if (string.Equals(settings.ProxyMode, "pool", StringComparison.OrdinalIgnoreCase))
            {
                assignments ??= Load();
                if (pool == null)
                {
                    if (!ProxyUrl.TryParsePool(settings.ProxyPool, out var parsed, out _))
                    {
                        return null;
                    }

                    pool = parsed;
                }

                return assignments.TryGetValue(tenantId, out var id) && pool.FirstOrDefault(p => p.Id == id) is { } entry
                    ? new ProxyInfo(entry.Id, entry.Label)
                    : null;
            }

            if (!string.IsNullOrWhiteSpace(settings.ProxyGlobalId))
            {
                if (pool == null)
                {
                    if (!ProxyUrl.TryParsePool(settings.ProxyPool, out var parsedGlobalPool, out _))
                    {
                        return null;
                    }

                    pool = parsedGlobalPool;
                }

                return pool.FirstOrDefault(p => p.Id == settings.ProxyGlobalId) is { } chosen ? new ProxyInfo(chosen.Id, chosen.Label) : null;
            }

            return string.IsNullOrWhiteSpace(settings.ProxyGlobal) ? null : new ProxyInfo("global", "Proxy");
        }
    }

    internal sealed record ProxyInfo(string Id, string Label);

    internal static class ProxyHealth
    {
        internal sealed record Entry(string State, string Detail, DateTime SinceUtc);

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Entry> ByTenant = new();

        public static Entry Get(string tenantId) => ByTenant.TryGetValue(tenantId, out var e) ? e : null;

        public static void Clear(string tenantId) => ByTenant.TryRemove(tenantId, out _);

        public static (bool Changed, string Previous) Update(string tenantId, string state, string detail)
        {
            var previous = Get(tenantId)?.State;
            if (string.IsNullOrEmpty(state))
            {
                Clear(tenantId);
                return (previous != null, previous);
            }

            if (previous == state)
            {
                return (false, previous);
            }

            ByTenant[tenantId] = new Entry(state, detail, DateTime.UtcNow);
            return (true, previous);
        }
    }
}
