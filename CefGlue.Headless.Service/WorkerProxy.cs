using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xilium.CefGlue.Headless.Service
{
    internal static class WorkerProxy
    {
        private const int MaxAnswersPerTab = 3;
        private static readonly TimeSpan AnswerWindow = TimeSpan.FromSeconds(30);

        private static readonly object Gate = new();
        private static readonly Dictionary<string, (int Count, DateTime Since)> Answers = new();
        private static CefRequestContext _tenantContext;

        public static ProxyEndpoint Endpoint { get; private set; }

        public static string Id { get; private set; }

        public static bool Enabled => Endpoint != null;

        public static bool PerTab { get; private set; }

        public static bool NeedsLoginPromptOff => PerTab || Endpoint is { HasCredentials: true };

        private static readonly Dictionary<string, ProxyEndpoint> Known = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> FailedLogin = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> UsedTickets = new(StringComparer.Ordinal);

        public static bool AuthFailed { get { lock (Gate) { return FailedLogin.Count > 0; } } }

        private static string KeyOf(string host, int port) => $"{host}:{port}";

        private static void Register(ProxyEndpoint endpoint)
        {
            lock (Gate)
            {
                Known[KeyOf(endpoint.Host, endpoint.Port)] = endpoint;
            }
        }

        public static ProxyEndpoint ResolveFromTable(string proxyId)
        {
            try
            {
                var path = Xilium.CefGlue.Broker.Storage.BrokerDb.PathFor(AppContext.BaseDirectory);
                using var db = Xilium.CefGlue.Broker.Storage.BrokerDb.OpenReadOnly(path);
                var saved = Xilium.CefGlue.Broker.Storage.BrokerDb.ReadJson(db, Xilium.CefGlue.Broker.Storage.BrokerDb.Settings);
                var pool = Xilium.CefGlue.Broker.Storage.BrokerDb.AsString(saved.GetValueOrDefault("ProxyPool"));
                return ProxyUrl.TryParsePool(pool, out var entries, out _) ? entries.FirstOrDefault(e => e.Id == proxyId)?.Endpoint : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Proxy] Could not read the proxy table: {ex.Message}");
                return null;
            }
        }

        public static bool TryAuthorizeTab(string ticket, string contextId, string proxyId, out ProxyEndpoint endpoint, out string reason)
        {
            endpoint = null;
            var secret = Environment.GetEnvironmentVariable("CEFGLUE_WORKER_SECRET");
            if (string.IsNullOrEmpty(ticket) || string.IsNullOrEmpty(contextId) || string.IsNullOrEmpty(proxyId) || string.IsNullOrEmpty(secret)
                || !IsolationContextStore.IsValidContextId(contextId))
            {
                reason = "A new tab needs a proxy choice from the server.";
                return false;
            }

            var result = Xilium.CefGlue.Headless.Server.TicketAuth.Verify(secret, ticket, $"tabproxy:{contextId}:{proxyId}", Program.TenantId, out var nonce, out _);
            if (result != Xilium.CefGlue.Headless.Server.TicketResult.Ok)
            {
                reason = $"The proxy choice was refused ({result}).";
                return false;
            }

            lock (Gate)
            {
                if (!UsedTickets.Add(nonce))
                {
                    reason = "The proxy choice was already used.";
                    return false;
                }
            }

            endpoint = ResolveFromTable(proxyId);
            if (endpoint == null)
            {
                reason = "That proxy is no longer in the table.";
                return false;
            }

            Register(endpoint);
            reason = null;
            return true;
        }

        public static ProxyEndpoint ResolveForRestoredTab(string proxyId)
        {
            if (string.IsNullOrEmpty(proxyId))
            {
                return null;
            }

            var endpoint = ResolveFromTable(proxyId);
            if (endpoint != null)
            {
                Register(endpoint);
            }

            return endpoint;
        }

        private static readonly Dictionary<string, ProxyEndpoint> ContextEndpoints = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal);

        private static readonly HashSet<string> Confirmed = new(StringComparer.Ordinal);

        public static void NoteContext(string contextId, ProxyEndpoint own)
        {
            var endpoint = own ?? Endpoint;
            lock (Gate)
            {
                if (endpoint != null)
                {
                    ContextEndpoints[contextId] = endpoint;
                }
                else
                {
                    ContextEndpoints.Remove(contextId);
                }
            }
        }

        public static string LabelOfContext(string contextId)
        {
            ProxyEndpoint endpoint;
            lock (Gate)
            {
                if (!Confirmed.Contains(contextId ?? "tenant"))
                {
                    return null;
                }

                endpoint = contextId != null && ContextEndpoints.TryGetValue(contextId, out var known) ? known : Endpoint;
            }

            if (endpoint == null)
            {
                return "Proxy";
            }

            var id = ProxyUrl.IdOf(endpoint);
            lock (Gate)
            {
                if (Labels.TryGetValue(id, out var cached))
                {
                    return cached;
                }
            }

            string label = null;
            try
            {
                var path = Xilium.CefGlue.Broker.Storage.BrokerDb.PathFor(AppContext.BaseDirectory);
                using var db = Xilium.CefGlue.Broker.Storage.BrokerDb.OpenReadOnly(path);
                var saved = Xilium.CefGlue.Broker.Storage.BrokerDb.ReadJson(db, Xilium.CefGlue.Broker.Storage.BrokerDb.Settings);
                var pool = Xilium.CefGlue.Broker.Storage.BrokerDb.AsString(saved.GetValueOrDefault("ProxyPool"));
                if (ProxyUrl.TryParsePool(pool, out var entries, out _))
                {
                    label = entries.FirstOrDefault(e => e.Id == id)?.Label;
                }
            }
            catch
            {
            }

            label ??= "Proxy";
            lock (Gate)
            {
                Labels[id] = label;
            }

            return label;
        }

        public static void LoadFromEnvironment()
        {
            PerTab = Environment.GetEnvironmentVariable("CEFGLUE_PROXY_PERTAB") == "1";
            var url = Environment.GetEnvironmentVariable("CEFGLUE_PROXY_URL");
            if (string.IsNullOrWhiteSpace(url))
            {
                Console.WriteLine("[Proxy] No proxy: this worker connects directly.");
                return;
            }

            if (!ProxyUrl.TryParse(url, out var parsed, out var error))
            {
                throw new InvalidOperationException($"CEFGLUE_PROXY_URL is not a valid proxy ({error}). Not starting: a worker must not browse without the proxy it was given.");
            }

            var user = Environment.GetEnvironmentVariable("CEFGLUE_PROXY_USER");
            var password = Environment.GetEnvironmentVariable("CEFGLUE_PROXY_PASSWORD") ?? "";
            Endpoint = string.IsNullOrEmpty(user) ? parsed : parsed with { User = user, Password = password };
            Id = Endpoint.Server;
            Register(Endpoint);

            Environment.SetEnvironmentVariable("CEFGLUE_PROXY_USER", null);
            Environment.SetEnvironmentVariable("CEFGLUE_PROXY_PASSWORD", null);

            Console.WriteLine($"[Proxy] This worker browses through {Endpoint.Server} ({(Endpoint.HasCredentials ? "with a login" : "no login")}).");
        }

        public static CefRequestContext GetTenantContext()
        {
            lock (Gate)
            {
                return _tenantContext ??= CefRequestContext.CreateContext(
                    new CefRequestContextSettings { CachePath = Path.Combine(Program.CachePath, "Default") },
                    new ContextHandler("tenant", Endpoint));
            }
        }

        public static CefRequestContextHandler CreateContextHandler(string label, ProxyEndpoint own = null) =>
            new ContextHandler(label, own ?? Endpoint);

        public static bool TryAnswerAuth(string tabId, string host, int port, CefAuthCallback callback)
        {
            ProxyEndpoint endpoint;
            lock (Gate)
            {
                if (!Known.TryGetValue(KeyOf(host, port), out endpoint) || !endpoint.HasCredentials)
                {
                    return false;
                }
            }

            lock (Gate)
            {
                if (FailedLogin.Contains(KeyOf(host, port)))
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                Answers.TryGetValue(tabId, out var entry);
                if (entry.Since == default || now - entry.Since > AnswerWindow)
                {
                    entry = (0, now);
                }

                if (entry.Count >= MaxAnswersPerTab)
                {
                    FailedLogin.Add(KeyOf(host, port));
                    var message = $"[Proxy] {endpoint.Server} keeps refusing the login ({MaxAnswersPerTab} answers within {AnswerWindow.TotalSeconds:0} s on tab '{tabId}'). " +
                                  "Not answering again: fix the proxy login in the Admin, then end this tenant (Kill) so it restarts with the new one.";
                    Console.WriteLine(message);
                    ErrorLog.Append(ErrorLog.ServiceFileName, AppContext.BaseDirectory, message);
                    return false;
                }

                Answers[tabId] = (entry.Count + 1, entry.Since);
            }

            callback.Continue(endpoint.User, endpoint.Password ?? "");
            return true;
        }

        private static DateTime _lastErrorUtc;
        private static bool _lastErrorIsLogin;
        private static string _lastError;
        private static DateTime _lastOkUtc;

        public static void NoteLoadError(CefErrorCode code)
        {
            if (!Enabled)
            {
                return;
            }

            var login = code is CefErrorCode.InvalidAuthCredentials or CefErrorCode.ProxyAuthUnsupported or CefErrorCode.ProxyAuthRequested;
            var connection = code is CefErrorCode.ProxyConnectionFailed or CefErrorCode.TunnelConnectionFailed or CefErrorCode.SocksConnectionFailed
                or CefErrorCode.SocksConnectionHostUnreachable or CefErrorCode.ProxyCertificateInvalid;
            if (!login && !connection)
            {
                return;
            }

            lock (Gate)
            {
                _lastErrorUtc = DateTime.UtcNow;
                _lastErrorIsLogin = login;
                _lastError = code.ToString();
            }
        }

        public static void NoteLoadEnd(int httpStatusCode)
        {
            if (Enabled && httpStatusCode is >= 100 and < 600)
            {
                lock (Gate)
                {
                    _lastOkUtc = DateTime.UtcNow;
                }
            }
        }

        public static object HeartbeatPayload()
        {
            if (!Enabled)
            {
                return null;
            }

            lock (Gate)
            {
                if (AuthFailed)
                {
                    return new { state = "authFailed", detail = "The proxy keeps refusing the login." };
                }

                if (_lastErrorUtc > _lastOkUtc)
                {
                    return new { state = _lastErrorIsLogin ? "authFailed" : "unreachable", detail = _lastError };
                }

                return new { state = "ok", detail = (string)null };
            }
        }

        private sealed class ContextHandler : CefRequestContextHandler
        {
            private readonly string _label;
            private readonly ProxyEndpoint _endpoint;

            public ContextHandler(string label, ProxyEndpoint endpoint)
            {
                _label = label;
                _endpoint = endpoint;
            }

            protected override CefResourceRequestHandler GetResourceRequestHandler(CefBrowser browser, CefFrame frame, CefRequest request,
                bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling) => null;

            protected override void OnRequestContextInitialized(CefRequestContext requestContext)
            {
                if (_endpoint != null)
                {
                    var rules = CefDictionaryValue.Create();
                    rules.SetString("mode", "fixed_servers");
                    rules.SetString("server", _endpoint.Server);
                    var value = CefValue.Create();
                    value.SetDictionary(rules);

                    if (!requestContext.SetPreference("proxy", value, out var error))
                    {
                        var message = $"[Proxy] Could not set the proxy {_endpoint.Server} on context '{_label}' ({error}). Stopping this worker instead of browsing without it.";
                        Console.WriteLine(message);
                        ErrorLog.Append(ErrorLog.ServiceFileName, AppContext.BaseDirectory, message);
                        Environment.Exit(3);
                    }
                }
                else if (requestContext.GetPreference("proxy")?.GetDictionary()?.GetString("mode") is { } stale && stale != "direct" && stale != "system")
                {
                    if (!requestContext.SetPreference("proxy", null, out var error))
                    {
                        Console.WriteLine($"[Proxy] Could not clear the saved proxy ({stale}) on context '{_label}': {error}.");
                    }
                }

                var applied = requestContext.GetPreference("proxy")?.GetDictionary()?.GetString("mode");
                var proxied = applied == "fixed_servers";
                lock (Gate)
                {
                    if (proxied)
                    {
                        Confirmed.Add(_label);
                    }
                    else
                    {
                        Confirmed.Remove(_label);
                    }
                }

                Console.WriteLine(_endpoint != null
                    ? $"[Proxy] Context '{_label}' uses {_endpoint.Server}."
                    : $"[Proxy] Context '{_label}' goes direct (CEF reports proxy mode '{applied ?? "default"}').");
            }
        }
    }
}
