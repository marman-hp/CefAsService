using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xilium.CefGlue.Broker.Storage;

namespace Xilium.CefGlue.Broker.Admin
{
    internal sealed class AdminSettingsData
    {
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        public int PasswordIterations { get; set; }

        public string BrokerExePath { get; set; }

        public string WorkerExePath { get; set; }
    }

    internal sealed class AdminSettingsStore
    {
        private static readonly string LegacyJsonPath = Path.Combine(AppContext.BaseDirectory, "admin-settings.json");

        private readonly object _gate = new();
        private AdminSettingsData _data = new();

        public AdminSettingsStore()
        {
            try
            {
                var db = AdminDb.Db;
                MigrateLegacyJson(db);
                var rows = BrokerDb.ReadJson(db, BrokerDb.AdminSettings);
                _data = new AdminSettingsData
                {
                    PasswordHash = BrokerDb.AsString(rows.GetValueOrDefault(nameof(AdminSettingsData.PasswordHash))),
                    PasswordSalt = BrokerDb.AsString(rows.GetValueOrDefault(nameof(AdminSettingsData.PasswordSalt))),
                    PasswordIterations = int.TryParse(BrokerDb.AsString(rows.GetValueOrDefault(nameof(AdminSettingsData.PasswordIterations))), out var it) ? it : 0,
                    BrokerExePath = BrokerDb.AsString(rows.GetValueOrDefault(nameof(AdminSettingsData.BrokerExePath))),
                    WorkerExePath = BrokerDb.AsString(rows.GetValueOrDefault(nameof(AdminSettingsData.WorkerExePath))),
                };
            }
            catch (Exception ex)
            {
                AdminDb.Fail(AdminDb.Error ?? ex.Message);
            }
        }

        public AdminSettingsData Snapshot()
        {
            lock (_gate)
            {
                return JsonSerializer.Deserialize<AdminSettingsData>(JsonSerializer.Serialize(_data));
            }
        }

        public void Update(Action<AdminSettingsData> change)
        {
            lock (_gate)
            {
                var next = Snapshot();
                change(next);
                var db = AdminDb.Db;
                db.Transaction(() => Write(db, next));
                _data = next;
            }
        }

        private static void Write(SqliteDb db, AdminSettingsData data) =>
            BrokerDb.Write(db, BrokerDb.AdminSettings, new Dictionary<string, object>
            {
                [nameof(AdminSettingsData.PasswordHash)] = data.PasswordHash,
                [nameof(AdminSettingsData.PasswordSalt)] = data.PasswordSalt,
                [nameof(AdminSettingsData.PasswordIterations)] = data.PasswordIterations == 0 ? null : data.PasswordIterations,
                [nameof(AdminSettingsData.BrokerExePath)] = string.IsNullOrEmpty(data.BrokerExePath) ? null : data.BrokerExePath,
                [nameof(AdminSettingsData.WorkerExePath)] = string.IsNullOrEmpty(data.WorkerExePath) ? null : data.WorkerExePath,
            });

        private static void MigrateLegacyJson(SqliteDb db)
        {
            if (!File.Exists(LegacyJsonPath) || db.ScalarInt64($"SELECT COUNT(*) FROM {BrokerDb.AdminSettings}") > 0)
            {
                return;
            }

            AdminSettingsData legacy;
            try
            {
                legacy = JsonSerializer.Deserialize<AdminSettingsData>(File.ReadAllText(LegacyJsonPath)) ?? new AdminSettingsData();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"{LegacyJsonPath} exists but could not be read ({ex.Message}) - fix or delete it.", ex);
            }

            db.Transaction(() => Write(db, legacy));
            File.Move(LegacyJsonPath, LegacyJsonPath + ".migrated", overwrite: true);
            Console.WriteLine($"[Admin] Imported admin-settings.json into broker.db (renamed .migrated).");
        }
    }
}
