using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Xilium.CefGlue.Broker
{
    internal sealed class TabManifestEntry
    {
        public string Id { get; set; }
        public string Url { get; set; }

        public string ContextId { get; set; }
    }

    internal sealed class WorkerManifestEntry
    {
        public string TenantId { get; set; }
        public string PageId { get; set; }
        public string Address { get; set; }
        public int Pid { get; set; }
        public DateTime SpawnedUtc { get; set; }

        public List<TabManifestEntry> Tabs { get; set; }

        public string SelectedTabId { get; set; }

        public DateTime? GoneAtUtc { get; set; }

        public DateTime? CreatedUtc { get; set; }

        public string ClientIp { get; set; }

        public string ClientOs { get; set; }
    }

    internal static class WorkerManifest
    {
        private const string WorkerProcessName = "Xilium.CefGlue.Headless.Service";

        private static readonly object Gate = new();

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Ip, string Os)> LastClient = new();

        private static List<WorkerManifestEntry> _entries;

        private static List<WorkerManifestEntry> Load() => _entries ??= Storage.BrokerStore.LoadManifest();

        public static void Add(string tenantId, string pageId, string address, int pid)
        {
            lock (Gate)
            {
                var entries = Load();
                var previous = entries.FirstOrDefault(e => e.TenantId == tenantId);
                var client = LastClient.TryGetValue(tenantId, out var c) ? c : (previous?.ClientIp, previous?.ClientOs);
                entries.RemoveAll(e => e.TenantId == tenantId);
                var added = new WorkerManifestEntry
                {
                    TenantId = tenantId,
                    PageId = pageId,
                    Address = address,
                    Pid = pid,
                    SpawnedUtc = DateTime.UtcNow,
                    CreatedUtc = previous?.CreatedUtc ?? DateTime.UtcNow,
                    ClientIp = client.Item1,
                    ClientOs = client.Item2,
                };
                entries.Add(added);
                Storage.BrokerStore.SaveTenant(entries, added);
            }
        }

        public static void Remove(string tenantId)
        {
            lock (Gate)
            {
                var entries = Load();
                if (entries.RemoveAll(e => e.TenantId == tenantId) > 0)
                {
                    Storage.BrokerStore.RemoveTenants(entries, new[] { tenantId });
                }
            }
        }

        public static void MarkGone(string tenantId, string address)
        {
            lock (Gate)
            {
                var entries = Load();
                var entry = entries.FirstOrDefault(e => e.TenantId == tenantId);
                if (entry == null || entry.Pid == 0 && entry.GoneAtUtc.HasValue || entry.Address != address)
                {
                    return;
                }

                entry.Pid = 0;
                entry.Address = null;
                entry.SpawnedUtc = default;
                entry.GoneAtUtc = DateTime.UtcNow;
                Storage.BrokerStore.SaveTenant(entries, entry);
            }
        }

        public static bool TryGetGone(string tenantId, out int tabCount)
        {
            lock (Gate)
            {
                var entry = Load().FirstOrDefault(e => e.TenantId == tenantId && e.Pid == 0 && e.GoneAtUtc.HasValue);
                tabCount = entry?.Tabs?.Count ?? 0;
                return entry != null;
            }
        }

        public static List<WorkerManifestEntry> GetGone()
        {
            lock (Gate)
            {
                return Load().Where(e => e.Pid == 0 && e.GoneAtUtc.HasValue).ToList();
            }
        }

        public static void NoteClient(string tenantId, string ip, string os)
        {
            LastClient[tenantId] = (ip, os);

            lock (Gate)
            {
                var entries = Load();
                var entry = entries.FirstOrDefault(e => e.TenantId == tenantId);
                if (entry == null || entry.ClientIp == ip && entry.ClientOs == os)
                {
                    return;
                }

                entry.ClientIp = ip;
                entry.ClientOs = os;
                Storage.BrokerStore.SaveTenant(entries, entry);
            }
        }

        public static Dictionary<string, WorkerManifestEntry> GetAllByTenant()
        {
            lock (Gate)
            {
                return Load().GroupBy(e => e.TenantId).ToDictionary(g => g.Key, g => g.First());
            }
        }

        public static bool Contains(string tenantId)
        {
            lock (Gate)
            {
                return Load().Any(e => e.TenantId == tenantId);
            }
        }

        public static (List<TabManifestEntry> Tabs, string SelectedTabId) GetTabs(string tenantId)
        {
            lock (Gate)
            {
                var entry = Load().FirstOrDefault(e => e.TenantId == tenantId);
                return (entry?.Tabs ?? new List<TabManifestEntry>(), entry?.SelectedTabId);
            }
        }

        public static void UpdateTabs(string tenantId, List<TabManifestEntry> tabs, string selectedTabId)
        {
            if (tabs == null || tabs.Count == 0)
            {
                return;
            }

            lock (Gate)
            {
                var entries = Load();
                var entry = entries.FirstOrDefault(e => e.TenantId == tenantId);

                if (entry == null || (entry.Tabs != null
                        && entry.Tabs.Select(t => (t.Id, t.Url, t.ContextId)).SequenceEqual(tabs.Select(t => (t.Id, t.Url, t.ContextId)))
                        && entry.SelectedTabId == selectedTabId))
                {
                    return;
                }

                entry.Tabs = tabs;
                entry.SelectedTabId = selectedTabId;
                Storage.BrokerStore.SaveTenant(entries, entry);
            }
        }

        public static List<int> GetLivePids()
        {
            lock (Gate)
            {
                return Load().Where(e => e.Pid > 0 && IsWorkerAlive(e.Pid)).Select(e => e.Pid).ToList();
            }
        }

        public static void ClearAllTabs()
        {
            lock (Gate)
            {
                var entries = Load();
                var changed = false;
                foreach (var entry in entries.Where(e => e.Tabs is { Count: > 0 } || e.SelectedTabId != null))
                {
                    entry.Tabs = null;
                    entry.SelectedTabId = null;
                    changed = true;
                }

                if (changed)
                {
                    Storage.BrokerStore.SaveAllTenants(entries);
                }
            }
        }

        private static bool IsWorkerAlive(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                return !p.HasExited && string.Equals(p.ProcessName, WorkerProcessName, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void RecoverOnStartup(Action<WorkerManifestEntry, Process> readopt)
        {
            var noCleanup = string.Equals(Environment.GetEnvironmentVariable("CEFGLUE_BROKER_NO_ORPHAN_CLEANUP"), "1");

            lock (Gate)
            {
                var entries = Load();
                var kept = new List<WorkerManifestEntry>();
                var adoptedPids = new HashSet<int>();

                foreach (var entry in entries)
                {
                    if (IsWorkerAlive(entry.Pid))
                    {
                        try
                        {
                            var proc = Process.GetProcessById(entry.Pid);
                            readopt(entry, proc);
                            kept.Add(entry);
                            adoptedPids.Add(entry.Pid);
                            Console.WriteLine($"[WorkerManifest] Re-adopted running worker: tenant='{entry.TenantId}' pid={entry.Pid} address={entry.Address}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WorkerManifest] Could not re-adopt tenant '{entry.TenantId}' (pid {entry.Pid}): {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[WorkerManifest] Worker gone: tenant='{entry.TenantId}' pid={entry.Pid} - keeping Tabs for its next respawn, clearing process-specific fields.");
                        kept.Add(new WorkerManifestEntry
                        {
                            TenantId = entry.TenantId,
                            PageId = entry.PageId,
                            Tabs = entry.Tabs,
                            SelectedTabId = entry.SelectedTabId,
                            GoneAtUtc = entry.GoneAtUtc ?? DateTime.UtcNow,
                            CreatedUtc = entry.CreatedUtc,
                            ClientIp = entry.ClientIp,
                            ClientOs = entry.ClientOs,
                        });
                    }
                }

                _entries = kept;
                Storage.BrokerStore.SaveAllTenants(kept);

                if (noCleanup)
                {
                    return;
                }

                foreach (var proc in Process.GetProcessesByName(WorkerProcessName))
                {
                    if (adoptedPids.Contains(proc.Id))
                    {
                        continue;
                    }

                    try
                    {
                        Console.WriteLine($"[WorkerManifest] Killing orphan worker not in manifest: pid={proc.Id} (unrecoverable - no tenant/address, would lock the CEF cache dir).");
                        proc.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WorkerManifest] Could not kill orphan pid {proc.Id}: {ex.Message}");
                    }
                }
            }
        }

        public static List<string> PruneStaleGoneEntries(TimeSpan maxAge)
        {
            lock (Gate)
            {
                var entries = Load();
                var now = DateTime.UtcNow;
                var removedTenantIds = new List<string>();
                var kept = new List<WorkerManifestEntry>();

                foreach (var entry in entries)
                {
                    if (entry.Pid == 0 && entry.GoneAtUtc.HasValue && (now - entry.GoneAtUtc.Value) > maxAge)
                    {
                        removedTenantIds.Add(entry.TenantId);
                        continue;
                    }

                    kept.Add(entry);
                }

                if (removedTenantIds.Count > 0)
                {
                    _entries = kept;
                    Storage.BrokerStore.RemoveTenants(kept, removedTenantIds);
                }

                return removedTenantIds;
            }
        }
    }
}
