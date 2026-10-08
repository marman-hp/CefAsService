using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xilium.CefGlue.Headless.Service;

namespace Xilium.CefGlue.Broker.Storage
{
    internal static class BrokerDb
    {
        public const int SchemaVersion = 2;

        public const string Settings = "settings";
        public const string AdminSettings = "admin_settings";
        public const string Runtime = "runtime";

        public const string RuntimePid = "pid";
        public const string RuntimeUpdatedUtc = "updated_utc";
        public const string RuntimeStoppedUtc = "stopped_utc";
        public const string RuntimeActive = "active";
        public const string RuntimeUseWebRtc = "use_webrtc";
        public const string RuntimeIsolationMode = "isolation_mode";
        public const string RuntimeIsolationSwitch = "isolation_switch";
        public const string RuntimeWorkerExePath = "worker_exe_path";
        public const string RuntimeAvailableEncoders = "available_encoders";

        private const string SchemaSql = @"
CREATE TABLE IF NOT EXISTS tenants (
  tenant_id       TEXT PRIMARY KEY,
  page_id         TEXT,
  address         TEXT,
  pid             INTEGER NOT NULL DEFAULT 0,
  spawned_utc     INTEGER,
  created_utc     INTEGER,
  gone_at_utc     INTEGER,
  selected_tab_id TEXT,
  client_ip       TEXT,
  client_os       TEXT
);
CREATE TABLE IF NOT EXISTS tabs (
  tenant_id  TEXT NOT NULL REFERENCES tenants(tenant_id) ON DELETE CASCADE,
  position   INTEGER NOT NULL,
  tab_id     TEXT,
  url        TEXT,
  context_id TEXT,
  PRIMARY KEY (tenant_id, position)
);
CREATE TABLE IF NOT EXISTS revoked_tenants (
  tenant_id   TEXT PRIMARY KEY,
  revoked_utc INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS admin_settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS runtime (key TEXT PRIMARY KEY, value TEXT NOT NULL);";

        public static string PathFor(string workerExeDirectory)
        {
            var dir = string.IsNullOrEmpty(workerExeDirectory) ? AppContext.BaseDirectory : workerExeDirectory;
            var root = TenantCachePaths.Root(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.Combine(Path.GetDirectoryName(root) ?? root, "broker.db");
        }

        public static SqliteDb OpenReadWrite(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var db = SqliteDb.Open(path);
            try
            {
                db.Exec("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA foreign_keys = ON;");
                if (db.ScalarInt64("PRAGMA user_version") < SchemaVersion)
                {
                    db.Exec(SchemaSql + $"PRAGMA user_version = {SchemaVersion};");
                }
                return db;
            }
            catch
            {
                db.Dispose();
                throw;
            }
        }

        public static SqliteDb OpenReadOnly(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"{path} does not exist - start the Broker (or Admin) once to create it.", path);
            }
            return SqliteDb.Open(path, readOnly: true);
        }

        public static Dictionary<string, string> ReadJson(SqliteDb db, string table) =>
            db.Query($"SELECT key, value FROM {table}", r => (Key: r.GetString(0), Value: r.GetString(1)))
                .ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);

        public static string AsString(string json)
        {
            if (json == null)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.String => doc.RootElement.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => doc.RootElement.GetRawText(),
                _ => null,
            };
        }

        public static Dictionary<string, string> AsStringMap(string json)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (json != null && JsonNode.Parse(json) is JsonObject obj)
            {
                foreach (var (key, value) in obj)
                {
                    if (value is JsonValue v && v.TryGetValue<string>(out var s))
                    {
                        map[key] = s;
                    }
                }
            }
            return map;
        }

        public static void Write(SqliteDb db, string table, IEnumerable<KeyValuePair<string, object>> rows)
        {
            foreach (var (key, value) in rows)
            {
                if (value == null)
                {
                    db.Execute($"DELETE FROM {table} WHERE key = ?", key);
                }
                else
                {
                    db.Execute($"INSERT OR REPLACE INTO {table} (key, value) VALUES (?, ?)", key, JsonSerializer.Serialize(value));
                }
            }
        }
    }

    internal static class BrokerSettingsWriter
    {
        public static readonly string[] ScalarKeys =
        {
            "VideoEncoder", "ConnectionMode", "AudioBitrateKbps", "DisableGpu", "TextureEnable",
            "DiskCacheSizeBytes", "MediaCacheSizeBytes", "DefaultUrl", "NewPageUseLastUrl",
            "WebRtcPacingBps", "WebRtcIceServers", "SessionTimeoutSeconds", "ManifestTtlSeconds",
        };

        public const string EncoderSettingsKey = "EncoderSettings";

        public static string Validate(IReadOnlyDictionary<string, string> fields, IReadOnlyDictionary<string, string> saved)
        {
            int Seconds(string key, int fallback) =>
                int.TryParse(fields.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : saved.GetValueOrDefault(key), out var s) ? s : fallback;

            var timeout = Seconds("SessionTimeoutSeconds", 300);
            var ttl = Seconds("ManifestTtlSeconds", 900);
            return ttl < timeout ? $"Manifest TTL ({ttl}s) can't be shorter than Session Timeout ({timeout}s)." : null;
        }

        public static void Save(SqliteDb db, IReadOnlyDictionary<string, string> fields, IReadOnlyDictionary<string, string> encoderSettings)
        {
            db.Transaction(() =>
            {
                var current = BrokerDb.ReadJson(db, BrokerDb.Settings);
                var saved = current.ToDictionary(kv => kv.Key, kv => BrokerDb.AsString(kv.Value), StringComparer.OrdinalIgnoreCase);
                if (Validate(fields, saved) is { } error)
                {
                    throw new InvalidOperationException(error);
                }

                var rows = new List<KeyValuePair<string, object>>();
                foreach (var key in ScalarKeys)
                {
                    if (fields.TryGetValue(key, out var value))
                    {
                        value = key == "DefaultUrl" ? value?.Trim() : value;
                        rows.Add(new(key, string.IsNullOrEmpty(value) ? null : value));
                    }
                }

                if (encoderSettings != null)
                {
                    var merged = BrokerDb.AsStringMap(current.GetValueOrDefault(EncoderSettingsKey));
                    foreach (var (key, value) in encoderSettings)
                    {
                        if (string.IsNullOrEmpty(value))
                        {
                            merged.Remove(key);
                        }
                        else
                        {
                            merged[key] = value;
                        }
                    }
                    rows.Add(new(EncoderSettingsKey, merged.Count == 0 ? null : merged));
                }

                BrokerDb.Write(db, BrokerDb.Settings, rows);
            });
        }
    }
}
