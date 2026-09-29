using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed class IsolationContextStore
    {
        private static readonly Regex ValidContextId = new("^[0-9a-z]{12}$", RegexOptions.Compiled);

        private const string DirPrefix = "c-";

        private const string LegacyDirPrefix = "ctx-";
        private const string Base36 = "0123456789abcdefghijklmnopqrstuvwxyz";

        public const string ExampleDirName = DirPrefix + "xxxxxxxxxxxx";

        public static string NewContextId()
        {
            var ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var id = new char[12];
            for (var i = 7; i >= 0; i--)
            {
                id[i] = Base36[(int)(ms % 36)];
                ms /= 36;
            }

            for (var i = 8; i < 12; i++)
            {
                id[i] = Base36[RandomNumberGenerator.GetInt32(36)];
            }

            return new string(id);
        }

        private readonly string _root;
        private readonly string _ledgerPath;
        private readonly TimeSpan _ttl;
        private readonly object _gate = new();
        private readonly Dictionary<string, CefRequestContext> _open = new();
        private readonly Dictionary<string, DateTime> _lastUsedUtc = new();

        private readonly Dictionary<string, int> _index = new();

        public IsolationContextStore(string rootCachePath, TimeSpan ttl)
        {
            _root = rootCachePath;
            _ledgerPath = Path.Combine(_root, "isolation-contexts.json");
            _ttl = ttl;
            Directory.CreateDirectory(_root);
            LoadLedger();
        }

        public List<CefRequestContext> OpenContexts()
        {
            lock (_gate)
            {
                return new List<CefRequestContext>(_open.Values);
            }
        }

        public static bool IsValidContextId(string contextId) => contextId != null && ValidContextId.IsMatch(contextId);

        public CefRequestContext GetOrCreate(string contextId)
        {
            if (!IsValidContextId(contextId))
            {
                throw new ArgumentException($"Invalid isolation ContextId '{contextId}'.", nameof(contextId));
            }

            lock (_gate)
            {
                if (_open.TryGetValue(contextId, out var existing))
                {
                    return existing;
                }

                var settings = new CefRequestContextSettings
                {
                    CachePath = ContextDir(contextId),
                };
                var context = CefRequestContext.CreateContext(settings, null);
                _open[contextId] = context;
                _lastUsedUtc[contextId] = DateTime.UtcNow;
                EnsureIndex(contextId);
                SaveLedger();
                Console.WriteLine($"[IsolationContextStore] Opened context {contextId}.");
                return context;
            }
        }

        public void Release(string contextId)
        {
            lock (_gate)
            {
                _open.Remove(contextId);
                _lastUsedUtc[contextId] = DateTime.UtcNow;
                SaveLedger();
            }
        }

        public void Prune(ISet<string> inUse)
        {
            lock (_gate)
            {
                var now = DateTime.UtcNow;
                var changed = false;

                foreach (var contextId in inUse)
                {
                    _lastUsedUtc[contextId] = now;
                    changed = true;
                }

                foreach (var legacyDir in Directory.EnumerateDirectories(_root, LegacyDirPrefix + "*"))
                {
                    try
                    {
                        Directory.Delete(legacyDir, recursive: true);
                        Console.WriteLine($"[IsolationContextStore] Deleted legacy context folder {Path.GetFileName(legacyDir)} (old too-long layout).");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[IsolationContextStore] Could not delete legacy context folder {Path.GetFileName(legacyDir)} yet: {ex.Message}");
                    }
                }

                foreach (var dir in Directory.EnumerateDirectories(_root, DirPrefix + "*"))
                {
                    var contextId = Path.GetFileName(dir).Substring(DirPrefix.Length);
                    if (!IsValidContextId(contextId) || inUse.Contains(contextId) || _open.ContainsKey(contextId))
                    {
                        continue;
                    }

                    if (!_lastUsedUtc.TryGetValue(contextId, out var lastUsed))
                    {
                        _lastUsedUtc[contextId] = now;
                        changed = true;
                        continue;
                    }

                    if (now - lastUsed <= _ttl)
                    {
                        continue;
                    }

                    try
                    {
                        Directory.Delete(dir, recursive: true);
                        _lastUsedUtc.Remove(contextId);
                        _index.Remove(contextId);
                        changed = true;
                        Console.WriteLine($"[IsolationContextStore] Deleted context {contextId} - unused for longer than {_ttl.TotalSeconds:F0}s.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[IsolationContextStore] Could not delete expired context {contextId} yet, retrying next tick: {ex.Message}");
                    }
                }

                foreach (var contextId in _lastUsedUtc.Keys.ToList())
                {
                    if (!inUse.Contains(contextId) && !_open.ContainsKey(contextId) && !Directory.Exists(ContextDir(contextId)))
                    {
                        _lastUsedUtc.Remove(contextId);
                        _index.Remove(contextId);
                        changed = true;
                    }
                }

                if (changed)
                {
                    SaveLedger();
                }
            }
        }

        private string ContextDir(string contextId) => Path.Combine(_root, DirPrefix + contextId);

        public int? GetIndex(string contextId)
        {
            if (contextId == null)
            {
                return null;
            }

            lock (_gate)
            {
                return _index.TryGetValue(contextId, out var index) ? index : null;
            }
        }

        private void EnsureIndex(string contextId)
        {
            if (_index.ContainsKey(contextId))
            {
                return;
            }

            var used = _index.Values.ToHashSet();
            var next = 1;
            while (used.Contains(next))
            {
                next++;
            }

            _index[contextId] = next;
        }

        private sealed class LedgerEntry
        {
            public DateTime LastUsedUtc { get; set; }
            public int Index { get; set; }
        }

        private void LoadLedger()
        {
            try
            {
                if (!File.Exists(_ledgerPath))
                {
                    return;
                }

                using var doc = JsonDocument.Parse(File.ReadAllText(_ledgerPath));
                if (doc.RootElement.TryGetProperty("contexts", out var contexts) && contexts.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in contexts.EnumerateObject())
                    {
                        if (!IsValidContextId(property.Name))
                        {
                            continue;
                        }

                        var entry = property.Value.Deserialize<LedgerEntry>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (entry == null)
                        {
                            continue;
                        }

                        _lastUsedUtc[property.Name] = entry.LastUsedUtc;
                        if (entry.Index > 0 && !_index.ContainsValue(entry.Index))
                        {
                            _index[property.Name] = entry.Index;
                        }
                    }
                }
                else
                {
                    foreach (var property in doc.RootElement.EnumerateObject().OrderBy(p => p.Value.GetDateTime()))
                    {
                        if (IsValidContextId(property.Name))
                        {
                            _lastUsedUtc[property.Name] = property.Value.GetDateTime();
                        }
                    }
                }

                foreach (var contextId in _lastUsedUtc.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key))
                {
                    EnsureIndex(contextId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IsolationContextStore] Could not read {_ledgerPath} ({ex.Message}) - starting from empty.");
                _lastUsedUtc.Clear();
                _index.Clear();
            }
        }

        private void SaveLedger()
        {
            try
            {
                var contexts = _lastUsedUtc.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new LedgerEntry { LastUsedUtc = kvp.Value, Index = _index.TryGetValue(kvp.Key, out var i) ? i : 0 });
                File.WriteAllText(_ledgerPath, JsonSerializer.Serialize(
                    new { contexts },
                    new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IsolationContextStore] Could not write {_ledgerPath}: {ex.Message}");
            }
        }
    }
}
