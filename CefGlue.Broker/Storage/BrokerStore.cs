using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xilium.CefGlue.Headless.Service;

namespace Xilium.CefGlue.Broker.Storage
{
    internal static class BrokerStore
    {
        private static readonly string WorkersJson = Path.Combine(AppContext.BaseDirectory, "broker-workers.json");
        private static readonly string RevokedJson = Path.Combine(AppContext.BaseDirectory, "broker-revoked-tenants.json");
        private static readonly string SettingsJson = Path.Combine(AppContext.BaseDirectory, "broker-settings.json");

        private static readonly object InitGate = new();
        private static SqliteDb _db;

        public static string DbPath =>
            BrokerDb.PathFor(Path.GetDirectoryName(ProcessSpawner.WorkerExePath ?? ""));

        private static SqliteDb Db
        {
            get
            {
                lock (InitGate)
                {
                    if (_db == null)
                    {
                        Open();
                    }
                    return _db;
                }
            }
        }

        private static void Open()
        {
            var path = DbPath;
            try
            {
                _db = BrokerDb.OpenReadWrite(path);
                Console.WriteLine($"[BrokerStore] SQLite {Sqlite3Native.LibVersion()} - state in {path}");
                MigrateFromJson();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[BrokerStore] FATAL: cannot open {path} ({ex.GetType().Name}: {ex.Message}). The Broker needs its database - fix the problem (missing sqlite3.dll next to the exe, folder permissions, a damaged file) and start it again.");
                Console.ResetColor();
                ErrorLog.Append(ErrorLog.BrokerFileName, AppContext.BaseDirectory, $"Cannot open {path} - Broker stopped: {ex}");
                Environment.Exit(3);
            }
        }

        public static List<WorkerManifestEntry> LoadManifest()
        {
            var db = Db;
            var entries = db.Query(
                "SELECT tenant_id, page_id, address, pid, spawned_utc, created_utc, gone_at_utc, selected_tab_id, client_ip, client_os FROM tenants",
                r => new WorkerManifestEntry
                {
                    TenantId = r.GetString(0),
                    PageId = r.GetString(1),
                    Address = r.GetString(2),
                    Pid = (int)r.GetInt64(3),
                    SpawnedUtc = FromMs(r.GetNullableInt64(4)) ?? default,
                    CreatedUtc = FromMs(r.GetNullableInt64(5)),
                    GoneAtUtc = FromMs(r.GetNullableInt64(6)),
                    SelectedTabId = r.GetString(7),
                    ClientIp = r.GetString(8),
                    ClientOs = r.GetString(9),
                });

            var tabsByTenant = db.Query(
                    "SELECT tenant_id, tab_id, url, context_id FROM tabs ORDER BY tenant_id, position",
                    r => (TenantId: r.GetString(0), Tab: new TabManifestEntry { Id = r.GetString(1), Url = r.GetString(2), ContextId = r.GetString(3) }))
                .GroupBy(t => t.TenantId)
                .ToDictionary(g => g.Key, g => g.Select(t => t.Tab).ToList());

            foreach (var entry in entries)
            {
                entry.Tabs = tabsByTenant.TryGetValue(entry.TenantId, out var tabs) ? tabs : null;
            }

            return entries;
        }

        public static void SaveTenant(List<WorkerManifestEntry> all, WorkerManifestEntry changed) =>
            Persist("WorkerManifest", db => db.Transaction(() => WriteTenant(db, changed)));

        public static void RemoveTenants(List<WorkerManifestEntry> all, IReadOnlyCollection<string> tenantIds) =>
            Persist("WorkerManifest", db => db.Transaction(() =>
            {
                foreach (var id in tenantIds)
                {
                    db.Execute("DELETE FROM tenants WHERE tenant_id = ?", id);
                }
            }));

        public static void SaveAllTenants(List<WorkerManifestEntry> all) =>
            Persist("WorkerManifest", db => db.Transaction(() =>
            {
                db.Execute("DELETE FROM tenants");
                foreach (var entry in all)
                {
                    WriteTenant(db, entry);
                }
            }));

        private static void WriteTenant(SqliteDb db, WorkerManifestEntry e)
        {
            db.Execute(
                @"INSERT INTO tenants (tenant_id, page_id, address, pid, spawned_utc, created_utc, gone_at_utc, selected_tab_id, client_ip, client_os)
                  VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                  ON CONFLICT(tenant_id) DO UPDATE SET
                    page_id = excluded.page_id, address = excluded.address, pid = excluded.pid,
                    spawned_utc = excluded.spawned_utc, created_utc = excluded.created_utc, gone_at_utc = excluded.gone_at_utc,
                    selected_tab_id = excluded.selected_tab_id, client_ip = excluded.client_ip, client_os = excluded.client_os",
                e.TenantId, e.PageId, e.Address, e.Pid, ToMs(e.SpawnedUtc), ToMs(e.CreatedUtc), ToMs(e.GoneAtUtc),
                e.SelectedTabId, e.ClientIp, e.ClientOs);

            db.Execute("DELETE FROM tabs WHERE tenant_id = ?", e.TenantId);
            if (e.Tabs != null)
            {
                for (var i = 0; i < e.Tabs.Count; i++)
                {
                    db.Execute("INSERT INTO tabs (tenant_id, position, tab_id, url, context_id) VALUES (?, ?, ?, ?, ?)",
                        e.TenantId, i, e.Tabs[i].Id, e.Tabs[i].Url, e.Tabs[i].ContextId);
                }
            }
        }

        public static Dictionary<string, DateTime> LoadRevoked() =>
            Db.Query("SELECT tenant_id, revoked_utc FROM revoked_tenants",
                    r => (Id: r.GetString(0), Utc: FromMs(r.GetInt64(1)).Value))
                .ToDictionary(t => t.Id, t => t.Utc);

        public static void SaveRevoked(Dictionary<string, DateTime> all, string added, IReadOnlyCollection<string> removed) =>
            Persist("RevokedTenants", db => db.Transaction(() =>
            {
                foreach (var id in removed)
                {
                    db.Execute("DELETE FROM revoked_tenants WHERE tenant_id = ?", id);
                }
                if (added != null && all.TryGetValue(added, out var utc))
                {
                    db.Execute("INSERT OR REPLACE INTO revoked_tenants (tenant_id, revoked_utc) VALUES (?, ?)", added, ToMs(utc));
                }
            }));

        public static BrokerSettingsData LoadSettings()
        {
            var json = new JsonObject();
            foreach (var (key, value) in BrokerDb.ReadJson(Db, BrokerDb.Settings))
            {
                json[key] = JsonNode.Parse(value);
            }

            return json.Deserialize<BrokerSettingsData>() ?? new BrokerSettingsData();
        }

        public static void SaveSettings(BrokerSettingsData data) =>
            Persist("BrokerSettings", db => db.Transaction(() => WriteSettings(db, data)));

        private static void WriteSettings(SqliteDb db, BrokerSettingsData data)
        {
            db.Execute("DELETE FROM settings");
            foreach (var (key, value) in JsonSerializer.SerializeToNode(data).AsObject())
            {
                if (value != null)
                {
                    db.Execute("INSERT INTO settings (key, value) VALUES (?, ?)", key, value.ToJsonString());
                }
            }
        }

        public static void WriteRuntime(IReadOnlyDictionary<string, object> values)
        {
            var rows = new List<KeyValuePair<string, object>>(values)
            {
                new(BrokerDb.RuntimePid, Environment.ProcessId),
                new(BrokerDb.RuntimeUpdatedUtc, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                new(BrokerDb.RuntimeStoppedUtc, null),
            };
            Persist("Runtime", db => db.Transaction(() =>
            {
                db.Execute($"DELETE FROM {BrokerDb.Runtime}");
                BrokerDb.Write(db, BrokerDb.Runtime, rows);
            }));
        }

        public static void MarkStopped() =>
            Persist("Runtime", db => BrokerDb.Write(db, BrokerDb.Runtime, new[]
            {
                new KeyValuePair<string, object>(BrokerDb.RuntimeStoppedUtc, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
            }));

        private static void MigrateFromJson()
        {
            MigrateFile(WorkersJson, "SELECT COUNT(*) FROM tenants", () =>
            {
                var entries = DurableJsonFile.Read(WorkersJson, () => new List<WorkerManifestEntry>(), "WorkerManifest");
                _db.Transaction(() => entries.ForEach(e => WriteTenant(_db, e)));
                return $"{entries.Count} tenant(s), {entries.Sum(e => e.Tabs?.Count ?? 0)} tab(s)";
            });

            MigrateFile(RevokedJson, "SELECT COUNT(*) FROM revoked_tenants", () =>
            {
                var revoked = DurableJsonFile.Read(RevokedJson, () => new Dictionary<string, DateTime>(), "RevokedTenants");
                _db.Transaction(() =>
                {
                    foreach (var (id, utc) in revoked)
                    {
                        _db.Execute("INSERT OR REPLACE INTO revoked_tenants (tenant_id, revoked_utc) VALUES (?, ?)", id, ToMs(utc));
                    }
                });
                return $"{revoked.Count} revoked tenant(s)";
            });

            MigrateFile(SettingsJson, "SELECT COUNT(*) FROM settings", () =>
            {
                var settings = DurableJsonFile.Read(SettingsJson, () => new BrokerSettingsData(), "BrokerSettings");
                _db.Transaction(() => WriteSettings(_db, settings));
                return "settings";
            });
        }

        private static void MigrateFile(string jsonPath, string countSql, Func<string> import)
        {
            if (!File.Exists(jsonPath) && !File.Exists(jsonPath + ".bak"))
            {
                return;
            }

            if (_db.ScalarInt64(countSql) > 0)
            {
                Console.WriteLine($"[BrokerStore] {Path.GetFileName(jsonPath)} left as is - broker.db already holds this data.");
                return;
            }

            try
            {
                var what = import();
                foreach (var file in new[] { jsonPath, jsonPath + ".bak" })
                {
                    if (File.Exists(file))
                    {
                        File.Move(file, file + ".migrated", overwrite: true);
                    }
                }
                Console.WriteLine($"[BrokerStore] Imported {what} from {Path.GetFileName(jsonPath)} (renamed .migrated).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BrokerStore] Import of {Path.GetFileName(jsonPath)} failed ({ex.Message}) - left untouched, will retry on next start.");
            }
        }

        private static void Persist(string logTag, Action<SqliteDb> write)
        {
            var db = Db;
            try
            {
                write(db);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{logTag}] Could not write to broker.db: {ex.Message}");
                ErrorLog.Append(ErrorLog.BrokerFileName, AppContext.BaseDirectory, $"[{logTag}] broker.db write failed: {ex}");
            }
        }

        private static object ToMs(DateTime? value) =>
            value is { } v && v != default
                ? new DateTimeOffset(v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
                : null;

        private static DateTime? FromMs(long? ms) =>
            ms is { } v ? DateTimeOffset.FromUnixTimeMilliseconds(v).UtcDateTime : null;
    }
}
