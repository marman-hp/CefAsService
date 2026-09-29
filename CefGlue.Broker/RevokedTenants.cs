using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Xilium.CefGlue.Broker
{
    internal static class RevokedTenants
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromDays(1);

        private static readonly string FilePath =
            Path.Combine(AppContext.BaseDirectory, "broker-revoked-tenants.json");

        private static readonly object Gate = new();

        private static Dictionary<string, DateTime> _revokedUtc;

        public static void Revoke(string tenantId)
        {
            lock (Gate)
            {
                var revoked = LoadPruned();
                revoked[tenantId] = DateTime.UtcNow;
                Save(revoked);
            }
        }

        public static bool IsRevoked(string tenantId)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return false;
            }

            lock (Gate)
            {
                return LoadPruned().ContainsKey(tenantId);
            }
        }

        private static Dictionary<string, DateTime> LoadPruned()
        {
            if (_revokedUtc == null)
            {
                try
                {
                    _revokedUtc = File.Exists(FilePath)
                        ? JsonSerializer.Deserialize<Dictionary<string, DateTime>>(File.ReadAllText(FilePath)) ?? new()
                        : new();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RevokedTenants] Could not read {FilePath} ({ex.Message}) - starting from empty.");
                    _revokedUtc = new();
                }
            }

            var cutoff = DateTime.UtcNow - Lifetime;
            var expired = _revokedUtc.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
            if (expired.Count > 0)
            {
                expired.ForEach(id => _revokedUtc.Remove(id));
                Save(_revokedUtc);
            }

            return _revokedUtc;
        }

        private static void Save(Dictionary<string, DateTime> revoked)
        {
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(revoked, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RevokedTenants] Could not write {FilePath}: {ex.Message}");
            }
        }
    }
}
