using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Xilium.CefGlue.Broker
{
    public static class BrowserIceServers
    {
        public static string Json()
        {
            var raw = Environment.GetEnvironmentVariable("CEFGLUE_WEBRTC_ICE_SERVERS");
            var servers = new List<Dictionary<string, string>>();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (var uri in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (Parse(uri) is { } server)
                    {
                        servers.Add(server);
                    }
                }
            }

            return JsonSerializer.Serialize(servers);
        }

        private static Dictionary<string, string> Parse(string uri)
        {
            var colon = uri.IndexOf(':');
            if (colon <= 0)
            {
                return null;
            }

            var scheme = uri[..colon].ToLowerInvariant();
            if (scheme is not ("stun" or "stuns" or "turn" or "turns"))
            {
                return null;
            }

            var rest = uri[(colon + 1)..];
            var at = rest.LastIndexOf('@');
            if (at < 0)
            {
                return new Dictionary<string, string> { ["urls"] = uri };
            }

            var userInfo = rest[..at];
            var split = userInfo.IndexOf(':');
            var username = split < 0 ? userInfo : userInfo[..split];
            var credential = split < 0 ? "" : userInfo[(split + 1)..];
            return new Dictionary<string, string>
            {
                ["urls"] = scheme + ":" + rest[(at + 1)..],
                ["username"] = Uri.UnescapeDataString(username),
                ["credential"] = Uri.UnescapeDataString(credential),
            };
        }
    }
}
