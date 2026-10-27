using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xilium.CefGlue.Headless.Service;

namespace Xilium.CefGlue.Broker
{
    internal enum DevToolsProfile
    {
        Full,

        Learn,

        Restricted,
    }

    internal sealed record GateVerdict(bool Allow, string Reason);

    internal static class DevToolsGate
    {
        public const int MaxMessageBytes = 4 * 1024 * 1024;

        public static DevToolsProfile ParseProfile(string value) => value?.Trim().ToLowerInvariant() switch
        {
            "full" => DevToolsProfile.Full,
            "learn" => DevToolsProfile.Learn,
            _ => DevToolsProfile.Restricted,
        };

        private static readonly HashSet<string> AllowedDomains = new(StringComparer.Ordinal)
        {
            "DOM", "CSS", "Runtime", "Debugger", "Console", "Log", "Overlay", "Inspector", "Page", "Network", "Emulation", "Input",
            "Performance", "PerformanceTimeline", "Profiler", "HeapProfiler", "Accessibility", "Animation", "DOMDebugger", "DOMSnapshot",
            "Audits", "Media", "Memory", "ServiceWorker", "IndexedDB", "CacheStorage", "Storage", "Tracing", "LayerTree", "EventBreakpoints",
            "Autofill", "Preload", "DOMStorage",
        };

        private static readonly Dictionary<string, string> DenyMethods = new(StringComparer.Ordinal)
        {
            ["DOM.setFileInputFiles"] = "attaches a local file of the server to a file input without any URL (reads files)",
            ["DOM.getFileInfo"] = "reads information about local files",
            ["Page.setInterceptFileChooserDialog"] = "file chooser interception",
            ["Page.setBypassCSP"] = "turns off the page's content security policy",
            ["Page.setDownloadBehavior"] = "chooses where downloads are written on the server",
            ["Network.setBypassServiceWorker"] = "changes how requests are routed",
        };

        private static readonly HashSet<string> AllowedTargetMethods = new(StringComparer.Ordinal)
        {
            "Target.setAutoAttach", "Target.setDiscoverTargets", "Target.getTargets", "Target.getTargetInfo",
            "IO.read", "IO.close",
            "Security.enable", "Security.disable", "WebMCP.enable", "WebMCP.disable",
            "Target.attachToTarget", "Target.detachFromTarget", "Browser.getVersion",
        };

        private static readonly HashSet<string> UrlRuleMethods = new(StringComparer.Ordinal)
        {
            "Network.loadNetworkResource", "Page.navigate",
        };

        public static GateVerdict Evaluate(string method, Func<string> urlParam)
        {
            if (string.IsNullOrEmpty(method))
            {
                return new GateVerdict(false, "no method");
            }

            if (DenyMethods.TryGetValue(method, out var why))
            {
                return new GateVerdict(false, why);
            }

            if (UrlRuleMethods.Contains(method))
            {
                var url = urlParam?.Invoke();
                if (string.IsNullOrEmpty(url))
                {
                    return new GateVerdict(false, "url rule: no url given");
                }

                return AddressPolicy.IsEnforced(AddressPolicy.Check(url))
                    ? new GateVerdict(false, "url rule: the address is refused by the browsing filter")
                    : new GateVerdict(true, null);
            }

            if (AllowedTargetMethods.Contains(method))
            {
                return new GateVerdict(true, null);
            }

            var dot = method.IndexOf('.');
            var domain = dot > 0 ? method[..dot] : method;
            return AllowedDomains.Contains(domain)
                ? new GateVerdict(true, null)
                : new GateVerdict(false, $"domain '{domain}' is not on the allow-list");
        }

        public static void ConfigureAddressPolicy(BrokerSettingsData settings, int workerPort)
        {
            var enforcement = settings.FilterEnforce?.Trim().ToLowerInvariant() switch
            {
                "log" => AddressPolicy.EnforceLevel.LogOnly,
                "full" => AddressPolicy.EnforceLevel.Full,
                _ => AddressPolicy.EnforceLevel.Essential,
            };

            AddressPolicy.Configure(
                string.Equals(settings.FilterMode, "Off", StringComparison.OrdinalIgnoreCase) ? AddressPolicy.ModeOff : AddressPolicy.ModeInternal,
                settings.FilterAllowHosts,
                settings.FilterDenyHosts,
                settings.FilterBlockFileScheme != "0",
                AddressPolicy.ParsePorts(Program.FilterReservedPorts).Append(workerPort),
                enforcement);
        }

        private static readonly ConcurrentDictionary<string, byte> SeenGlobally = new();

        public static async Task PumpFromViewerAsync(WebSocket viewer, WebSocket worker, DevToolsProfile profile, string keyId)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var wouldRefuse = new Dictionary<string, string>(StringComparer.Ordinal);
            var buffer = new byte[65536];
            var restricted = profile == DevToolsProfile.Restricted;
            var refusedLogged = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                while (viewer.State == WebSocketState.Open && worker.State == WebSocketState.Open)
                {
                    using var message = new MemoryStream();
                    WebSocketReceiveResult result;
                    var tooBig = false;

                    do
                    {
                        result = await viewer.ReceiveAsync(buffer, CancellationToken.None);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }

                        if (message.Length + result.Count > MaxMessageBytes)
                        {
                            tooBig = true;
                            if (restricted)
                            {
                                Console.WriteLine($"[DevTools] tenant '{Short(keyId)}': a message over {MaxMessageBytes / (1024 * 1024)} MB - connection closed.");
                                await viewer.CloseAsync(WebSocketCloseStatus.MessageTooBig, "message too large", CancellationToken.None);
                                return;
                            }
                        }

                        if (!tooBig || !restricted)
                        {
                            message.Write(buffer, 0, result.Count);
                        }
                    }
                    while (!result.EndOfMessage);

                    if (restricted && result.MessageType != WebSocketMessageType.Text)
                    {
                        Console.WriteLine($"[DevTools] tenant '{Short(keyId)}': a binary frame from the viewer - connection closed.");
                        await viewer.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "text only", CancellationToken.None);
                        return;
                    }

                    string method = null;
                    string id = null;
                    string sessionId = null;
                    Func<string> urlParam = () => null;
                    JsonDocument doc = null;
                    try
                    {
                        doc = JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
                        var root = doc.RootElement;
                        if (root.ValueKind == JsonValueKind.Object)
                        {
                            method = root.TryGetProperty("method", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                            id = root.TryGetProperty("id", out var i) ? i.GetRawText() : null;
                            sessionId = root.TryGetProperty("sessionId", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                            var rootCopy = root;
                            urlParam = () => rootCopy.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object
                                && p.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
                        }
                    }
                    catch (JsonException)
                    {
                    }

                    if (method == null)
                    {
                        if (restricted)
                        {
                            Console.WriteLine($"[DevTools] tenant '{Short(keyId)}': a message that is not a CDP command - connection closed.");
                            doc?.Dispose();
                            await viewer.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "not a CDP command", CancellationToken.None);
                            return;
                        }

                        doc?.Dispose();
                        await worker.SendAsync(message.ToArray(), result.MessageType, true, CancellationToken.None);
                        continue;
                    }

                    counts[method] = counts.GetValueOrDefault(method) + 1;
                    var verdict = Evaluate(method, urlParam);
                    doc?.Dispose();

                    if (!verdict.Allow)
                    {
                        wouldRefuse[method] = verdict.Reason;
                        if (!restricted && SeenGlobally.TryAdd("learn|deny|" + method, 0))
                        {
                            Log($"learn: restricted would REFUSE {method} - {verdict.Reason}");
                        }
                    }
                    else if (!restricted && SeenGlobally.TryAdd("learn|allow|" + method, 0))
                    {
                        Log($"learn: new method seen {method} (restricted would allow)");
                    }

                    if (restricted && !verdict.Allow)
                    {
                        if (refusedLogged.Add(method))
                        {
                            Console.WriteLine($"[DevTools] tenant '{Short(keyId)}': refused {method} - {verdict.Reason}");
                        }

                        if (id != null)
                        {
                            var error = $"{{\"id\":{id},{(sessionId != null ? $"\"sessionId\":{JsonSerializer.Serialize(sessionId)}," : "")}\"error\":{{\"code\":-32601,\"message\":\"Not allowed by the server administrator\"}}}}";
                            await viewer.SendAsync(Encoding.UTF8.GetBytes(error), WebSocketMessageType.Text, true, CancellationToken.None);
                        }

                        continue;
                    }

                    await worker.SendAsync(message.ToArray(), result.MessageType, true, CancellationToken.None);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (profile == DevToolsProfile.Learn && counts.Count > 0)
                {
                    var used = string.Join(", ", counts.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} x{p.Value}"));
                    var refused = wouldRefuse.Count == 0 ? "none" : string.Join("; ", wouldRefuse.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key} ({p.Value})"));
                    Log($"learn summary for tenant '{Short(keyId)}' - {counts.Count} distinct methods used: {used}. Restricted would have refused: {refused}");
                }
            }
        }

        private static string Short(string keyId) => keyId == null ? "-" : keyId.Length > 8 ? keyId[..8] : keyId;

        private static void Log(string message)
        {
            Console.WriteLine("[DevTools] " + message);
            ErrorLog.Append("devtools-learn.log", AppContext.BaseDirectory, message);
        }
    }
}
