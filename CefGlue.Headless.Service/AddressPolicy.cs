using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed record FilterResult(bool Block, string Reason, string Host, int Port)
    {
        public static readonly FilterResult Allowed = new(false, null, null, 0);
    }

    internal static class AddressPolicy
    {
        public const string ModeOff = "Off";
        public const string ModeInternal = "Internal";

        public static string Mode { get; private set; } = ModeInternal;
        public static bool BlockFileScheme { get; private set; } = true;

        private static readonly object ConfigLock = new();
        private static List<HostPattern> _allow = new();
        private static List<HostPattern> _deny = new();
        private static HashSet<int> _reservedPorts = new();
        private static HashSet<IPAddress> _localAddresses = new();
        private static HashSet<string> _ownNames = new(StringComparer.OrdinalIgnoreCase);

        public static void ConfigureFromEnvironment()
        {
            string Env(string name) => Environment.GetEnvironmentVariable(name);
            var mode = Env("CEFGLUE_FILTER_MODE");
            Configure(
                string.Equals(mode, ModeOff, StringComparison.OrdinalIgnoreCase) ? ModeOff : ModeInternal,
                Env("CEFGLUE_FILTER_ALLOW_HOSTS"),
                Env("CEFGLUE_FILTER_DENY_HOSTS"),
                Env("CEFGLUE_FILTER_BLOCK_FILE") != "0",
                ParsePorts(Env("CEFGLUE_FILTER_RESERVED_PORTS")),
                Env("CEFGLUE_FILTER_ENFORCE")?.Trim().ToLowerInvariant() switch
                {
                    "0" or "log" or "logonly" => EnforceLevel.LogOnly,
                    "2" or "full" => EnforceLevel.Full,
                    _ => EnforceLevel.Essential,
                });
        }

        public enum EnforceLevel
        {
            LogOnly,

            Essential,

            Full,
        }

        public static EnforceLevel Enforcement { get; private set; } = EnforceLevel.Essential;

        public static bool IsEnforced(FilterResult result)
        {
            if (result == null || !result.Block || Mode == ModeOff)
            {
                return false;
            }

            return Enforcement switch
            {
                EnforceLevel.LogOnly => false,
                EnforceLevel.Essential => result.Reason is "reserved-port" or "file-scheme" or "deny-list",
                _ => true,
            };
        }

        public static void Configure(string mode, string allowHosts, string denyHosts, bool blockFileScheme, IEnumerable<int> reservedPorts,
            EnforceLevel enforcement = EnforceLevel.Essential)
        {
            lock (ConfigLock)
            {
                Mode = mode;
                Enforcement = enforcement;
                BlockFileScheme = blockFileScheme;
                _allow = ParsePatterns(allowHosts);
                _deny = ParsePatterns(denyHosts);
                _reservedPorts = new HashSet<int>(reservedPorts ?? Array.Empty<int>());
                RefreshLocalIdentity();
            }
        }

        public static void AddReservedPorts(IEnumerable<int> ports)
        {
            lock (ConfigLock)
            {
                var merged = new HashSet<int>(_reservedPorts);
                foreach (var port in ports.Where(p => p > 0))
                {
                    merged.Add(port);
                }

                _reservedPorts = merged;
            }
        }

        public static IReadOnlyCollection<int> ReservedPorts => _reservedPorts;

        public static IEnumerable<int> ParsePorts(string csv) =>
            (csv ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => int.TryParse(p, out var n) ? n : 0).Where(n => n is > 0 and <= 65535);

        private static List<HostPattern> ParsePatterns(string text) =>
            (text ?? "").Split(new[] { '\r', '\n', ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(HostPattern.TryParse).Where(p => p != null).ToList();

        private static void RefreshLocalIdentity()
        {
            var addresses = new HashSet<IPAddress>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "localhost" };
            try
            {
                names.Add(Environment.MachineName);
                var host = Dns.GetHostName();
                names.Add(host);
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                    {
                        addresses.Add(unicast.Address);
                    }
                }
            }
            catch
            {
            }

            _localAddresses = addresses;
            _ownNames = names;
        }

        public static FilterResult Check(string url)
        {
            try
            {
                return CheckCore(url);
            }
            catch
            {
                return FilterResult.Allowed;
            }
        }

        private static FilterResult CheckCore(string url)
        {
            if (Mode == ModeOff || string.IsNullOrEmpty(url))
            {
                return FilterResult.Allowed;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return FilterResult.Allowed;
            }

            var scheme = uri.Scheme.ToLowerInvariant();
            if (scheme is "about" or "data" or "blob" or "chrome-error")
            {
                return FilterResult.Allowed;
            }

            var host = uri.DnsSafeHost.TrimEnd('.').ToLowerInvariant();
            var port = uri.Port;

            if (scheme == "file")
            {
                return BlockFileScheme ? Block("file-scheme", "(local file)", 0) : FilterResult.Allowed;
            }

            if (scheme is not ("http" or "https" or "ws" or "wss"))
            {
                return scheme is "ftp" ? Block("scheme-" + scheme, host, port) : scheme is "chrome" or "devtools" ? Block("browser-internal-scheme", host, port) : FilterResult.Allowed;
            }

            if (string.IsNullOrEmpty(host))
            {
                return FilterResult.Allowed;
            }

            var isIp = IPAddress.TryParse(host, out var ip);
            var thisMachine = IsThisMachine(host, isIp ? ip : null);

            if (thisMachine && _reservedPorts.Contains(port))
            {
                return Block("reserved-port", host, port);
            }

            foreach (var pattern in _deny)
            {
                if (pattern.Matches(host, port))
                {
                    return Block("deny-list", host, port);
                }
            }

            foreach (var pattern in _allow)
            {
                if (pattern.Matches(host, port))
                {
                    return FilterResult.Allowed;
                }
            }

            if (isIp)
            {
                var kind = Classify(ip);
                return kind != null ? Block(kind, host, port) : FilterResult.Allowed;
            }

            if (thisMachine)
            {
                return Block("this-machine-name", host, port);
            }

            var resolved = ResolvedKind(host);
            return resolved != null ? Block("resolves-to-" + resolved, host, port) : FilterResult.Allowed;
        }

        private static FilterResult Block(string reason, string host, int port) => new(true, reason, host, port);

        private static bool IsThisMachine(string host, IPAddress ip)
        {
            if (ip != null)
            {
                if (ip.IsIPv4MappedToIPv6)
                {
                    ip = ip.MapToIPv4();
                }

                return IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || _localAddresses.Contains(ip);
            }

            return host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal) || _ownNames.Contains(host);
        }

        public static string Classify(IPAddress ip)
        {
            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (IPAddress.IsLoopback(ip)) return "loopback";
                if (ip.Equals(IPAddress.IPv6Any)) return "unspecified";
                if (ip.IsIPv6LinkLocal) return "link-local";
                var bytes6 = ip.GetAddressBytes();
                if ((bytes6[0] & 0xFE) == 0xFC) return "unique-local";
                if (bytes6[0] == 0xFF) return "multicast";
                return null;
            }

            var b = ip.GetAddressBytes();
            if (b[0] == 127) return "loopback";
            if (b[0] == 0) return "unspecified";
            if (b[0] == 169 && b[1] == 254) return "link-local";
            if (b[0] == 10) return "private";
            if (b[0] == 172 && b[1] is >= 16 and <= 31) return "private";
            if (b[0] == 192 && b[1] == 168) return "private";
            if (b[0] == 100 && b[1] is >= 64 and <= 127) return "cgnat";
            if (b[0] >= 224) return "multicast";
            return null;
        }

        private sealed record Resolved(DateTime ValidUntilUtc, string Kind);

        private static readonly ConcurrentDictionary<string, Resolved> ResolveCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> InFlight = new(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan ResolveTtl = TimeSpan.FromSeconds(60);

        private static string ResolvedKind(string host)
        {
            if (ResolveCache.TryGetValue(host, out var cached) && cached.ValidUntilUtc > DateTime.UtcNow)
            {
                return cached.Kind;
            }

            if (ResolveCache.Count > 4096)
            {
                ResolveCache.Clear();
            }

            if (InFlight.TryAdd(host, 0))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var addresses = await Dns.GetHostAddressesAsync(host).WaitAsync(TimeSpan.FromSeconds(3));
                        var kind = addresses.Select(Classify).FirstOrDefault(k => k != null);
                        ResolveCache[host] = new Resolved(DateTime.UtcNow + ResolveTtl, kind);
                    }
                    catch
                    {
                        ResolveCache[host] = new Resolved(DateTime.UtcNow + TimeSpan.FromSeconds(10), null);
                    }
                    finally
                    {
                        InFlight.TryRemove(host, out _);
                    }
                });
            }

            return cached?.Kind;
        }

        private sealed class HostPattern
        {
            private readonly string _host;
            private readonly bool _suffix;
            private readonly int _port;

            private HostPattern(string host, bool suffix, int port)
            {
                _host = host;
                _suffix = suffix;
                _port = port;
            }

            public static HostPattern TryParse(string text)
            {
                text = text.Trim().ToLowerInvariant();
                if (text.Length == 0 || text.StartsWith('#'))
                {
                    return null;
                }

                var port = 0;
                var colon = text.LastIndexOf(':');
                if (colon > 0 && !text.EndsWith(']') && text.IndexOf(':') == colon && int.TryParse(text[(colon + 1)..], out var parsedPort))
                {
                    port = parsedPort;
                    text = text[..colon];
                }

                var suffix = text.StartsWith("*.");
                if (suffix)
                {
                    text = text[2..];
                }

                text = text.Trim('[', ']').TrimEnd('.');
                return text.Length == 0 ? null : new HostPattern(text, suffix, port);
            }

            public bool Matches(string host, int port) =>
                (_port == 0 || _port == port) && (_suffix ? host.EndsWith("." + _host, StringComparison.Ordinal) || host == _host : host == _host);
        }
    }
}
