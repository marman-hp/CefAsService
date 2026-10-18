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

        private static readonly object Gate = new();

        private static Dictionary<string, DateTime> _revokedUtc;

        public static void Revoke(string tenantId)
        {
            lock (Gate)
            {
                var revoked = LoadPruned();
                revoked[tenantId] = DateTime.UtcNow;
                Storage.BrokerStore.SaveRevoked(revoked, tenantId, Array.Empty<string>());
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
            _revokedUtc ??= Storage.BrokerStore.LoadRevoked();

            var cutoff = DateTime.UtcNow - Lifetime;
            var expired = _revokedUtc.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
            if (expired.Count > 0)
            {
                expired.ForEach(id => _revokedUtc.Remove(id));
                Storage.BrokerStore.SaveRevoked(_revokedUtc, null, expired);
            }

            return _revokedUtc;
        }
    }
}
