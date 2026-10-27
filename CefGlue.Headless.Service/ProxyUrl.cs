using System;

namespace Xilium.CefGlue.Headless.Service
{
    public sealed record ProxyEndpoint(string Scheme, string Host, int Port, string User, string Password)
    {
        public bool HasCredentials => !string.IsNullOrEmpty(User);

        public string Server => $"{Scheme}://{(Host.Contains(':') ? $"[{Host}]" : Host)}:{Port}";

        public string ToUrl() => HasCredentials
            ? $"{Scheme}://{Uri.EscapeDataString(User)}:{Uri.EscapeDataString(Password ?? "")}@{Server[(Scheme.Length + 3)..]}"
            : Server;
    }

    public sealed record ProxyPoolEntry(string Id, string Label, ProxyEndpoint Endpoint);

    public static class ProxyUrl
    {
        public static string IdOf(ProxyEndpoint endpoint)
            => System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(endpoint.Server)))[..8].ToLowerInvariant();

        public static bool TryParsePool(string text, out System.Collections.Generic.List<ProxyPoolEntry> entries, out string error)
        {
            entries = new System.Collections.Generic.List<ProxyPoolEntry>();
            error = null;
            var firstLineOf = new System.Collections.Generic.Dictionary<string, int>();
            var lineNumber = 0;
            foreach (var rawLine in (text ?? "").Replace("\r", "").Split('\n'))
            {
                lineNumber++;
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                string label = null;
                var hash = line.IndexOf('#');
                if (hash > 0 && char.IsWhiteSpace(line[hash - 1]))
                {
                    label = line[(hash + 1)..].Trim();
                    line = line[..hash].Trim();
                }

                if (!TryParse(line, out var endpoint, out var lineError))
                {
                    entries = null;
                    error = $"Pool line {lineNumber}: {lineError}";
                    return false;
                }

                var id = IdOf(endpoint);
                if (firstLineOf.TryGetValue(id, out var firstLine))
                {
                    entries = null;
                    error = $"Pool line {lineNumber} is the same proxy as line {firstLine}.";
                    return false;
                }

                firstLineOf[id] = lineNumber;
                entries.Add(new ProxyPoolEntry(id, string.IsNullOrEmpty(label) ? $"Proxy {entries.Count + 1}" : label, endpoint));
            }

            return true;
        }

        public const string Format = "scheme://[user:password@]host:port, where scheme is http, https or socks5";

        public const string Socks5AuthMessage =
            "A SOCKS5 proxy with a user name and password is not supported: Chromium cannot log in to SOCKS5. "
            + "Use an HTTP or HTTPS proxy, or a SOCKS5 proxy without a login.";

        public static bool TryParse(string text, out ProxyEndpoint endpoint, out string error)
        {
            endpoint = null;
            error = null;
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                error = "The proxy is empty.";
                return false;
            }

            var schemeEnd = text.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd <= 0)
            {
                error = $"The proxy must look like {Format}.";
                return false;
            }

            var scheme = text[..schemeEnd].ToLowerInvariant();
            if (scheme is not ("http" or "https" or "socks5"))
            {
                error = $"The proxy type '{scheme}' is not supported; use http, https or socks5.";
                return false;
            }

            var rest = text[(schemeEnd + 3)..];
            if (rest.EndsWith('/'))
            {
                rest = rest[..^1];
            }

            if (rest.Length == 0 || rest.IndexOfAny(new[] { '/', '?', '#', ' ' }) >= 0)
            {
                error = $"The proxy must look like {Format}.";
                return false;
            }

            string user = null, password = null;
            var at = rest.LastIndexOf('@');
            if (at >= 0)
            {
                var userInfo = rest[..at];
                rest = rest[(at + 1)..];
                var colon = userInfo.IndexOf(':');
                try
                {
                    user = Uri.UnescapeDataString(colon < 0 ? userInfo : userInfo[..colon]);
                    password = colon < 0 ? "" : Uri.UnescapeDataString(userInfo[(colon + 1)..]);
                }
                catch (UriFormatException)
                {
                    error = "The user name or password in the proxy has a broken %-escape.";
                    return false;
                }

                if (user.Length == 0)
                {
                    error = "The proxy has a login separator but no user name.";
                    return false;
                }
            }

            string host;
            string portText;
            if (rest.StartsWith('['))
            {
                var close = rest.IndexOf(']');
                if (close < 0 || close + 1 >= rest.Length || rest[close + 1] != ':')
                {
                    error = "The proxy's IPv6 address must look like [::1]:port.";
                    return false;
                }

                host = rest[1..close];
                portText = rest[(close + 2)..];
            }
            else
            {
                var colon = rest.LastIndexOf(':');
                if (colon <= 0)
                {
                    error = "The proxy needs a port (host:port).";
                    return false;
                }

                host = rest[..colon];
                portText = rest[(colon + 1)..];
            }

            if (host.Length == 0 || host.IndexOfAny(new[] { '@', '[', ']' }) >= 0)
            {
                error = "The proxy host is not valid.";
                return false;
            }

            if (!int.TryParse(portText, out var port) || port is < 1 or > 65535)
            {
                error = "The proxy port must be a number from 1 to 65535.";
                return false;
            }

            if (scheme == "socks5" && user != null)
            {
                error = Socks5AuthMessage;
                return false;
            }

            endpoint = new ProxyEndpoint(scheme, host, port, user, password);
            return true;
        }
    }
}
