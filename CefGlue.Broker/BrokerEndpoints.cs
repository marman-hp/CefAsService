using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TicketAuth = Xilium.CefGlue.Headless.Server.TicketAuth;

namespace Xilium.CefGlue.Broker
{
    internal static class BrokerEndpoints
    {
        public static void MapBrokerEndpoints(this WebApplication app, string defaultMode, string defaultFormat)
        {
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value;

                if ((path == "/" || path == "/broker") && MobileDetection.ShouldServeMobile(context.Request))
                {
                    context.Response.Redirect("/broker-mobile" + context.Request.QueryString);
                    return;
                }

                if (path == "/broker" || path == "/broker-mobile")
                {
                    context.Response.Headers["Accept-CH"] = "Sec-CH-UA-Platform-Version";
                }

                await next();
            });

            app.MapGet("/", () => Results.Redirect("/broker"));

            app.MapRazorPages();

            app.Map("/worker", HandleWorkerConnection);
            app.MapPost("/assign", (Delegate)HandleAssign);
            app.MapPost("/tab-proxy", (Delegate)HandleTabProxy);
            app.Map("/relay/frames", HandleRelay);

            app.MapGet("/config", () => Results.Json(new
            {
                defaultMode,
                defaultFormat = Program.IsVideoEncoderReady ? defaultFormat : "jpg",
                videoReady = Program.IsVideoEncoderReady,
            }));

            app.MapGet("/tenant-status", HandleTenantStatus);
            app.MapGet("/recording/{keyId}/{id}", HandleRecordingDownload);
            app.Map("/devtools/{keyId}/{**path}", (Func<HttpContext, string, string, Task>)HandleDevTools);
            app.MapPost("/tenant-release", (Delegate)HandleTenantRelease);
        }

        private static readonly System.Net.Http.HttpClient RecordingHttp = new System.Net.Http.HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

        private static readonly System.Net.Http.HttpClient DevToolsHttp = new();

        private static async Task HandleDevTools(HttpContext context, string keyId, string path)
        {
            if (!IsKeyIdShaped(keyId) || !Program.Registry.TryGet(keyId, out var record))
            {
                context.Response.StatusCode = 404;
                return;
            }

            var target = $"{record.Address}/devtools/{path}{context.Request.QueryString}";
            if (context.WebSockets.IsWebSocketRequest)
            {
                var settings = BrokerSettings.Load();
                var profile = DevToolsGate.ParseProfile(settings.DevToolsProfile);
                if (profile != DevToolsProfile.Full)
                {
                    DevToolsGate.ConfigureAddressPolicy(settings, int.TryParse(record.Address.Split(':')[^1], out var workerPort) ? workerPort : 0);

                    if (!path.StartsWith("page/", StringComparison.Ordinal))
                    {
                        Console.WriteLine($"[DevTools] tenant '{keyId[..8]}': WebSocket to '/devtools/{path.Split('/')[0]}/...' " +
                            (profile == DevToolsProfile.Restricted ? "refused (only page targets are allowed)." : "allowed (learn profile)."));
                        if (profile == DevToolsProfile.Restricted)
                        {
                            context.Response.StatusCode = 403;
                            return;
                        }
                    }
                }

                using var workerSocket = new ClientWebSocket();
                workerSocket.Options.SetRequestHeader(TicketAuth.HeaderName, MintTicket(keyId, TicketAuth.InternalScope));
                try
                {
                    await workerSocket.ConnectAsync(new Uri("ws://" + target), context.RequestAborted);
                }
                catch
                {
                    context.Response.StatusCode = 502;
                    return;
                }
                using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();

                using var profileMonitor = new CancellationTokenSource();
                if (profile != DevToolsProfile.Full)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            while (!profileMonitor.IsCancellationRequested)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(3), profileMonitor.Token);
                                if (DevToolsGate.ParseProfile(BrokerSettings.Load().DevToolsProfile) != profile)
                                {
                                    Console.WriteLine($"[DevTools] tenant '{keyId[..8]}': the DevTools profile was changed - closing this connection.");
                                    try { await clientSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "DevTools settings changed", CancellationToken.None); } catch { }
                                    try { await workerSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); } catch { }
                                    return;
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    });
                }

                var viewerToWorker = profile == DevToolsProfile.Full
                    ? PumpAsync(clientSocket, workerSocket)
                    : DevToolsGate.PumpFromViewerAsync(clientSocket, workerSocket, profile, keyId);
                await Task.WhenAny(viewerToWorker, PumpAsync(workerSocket, clientSocket));
                profileMonitor.Cancel();
                try { await clientSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); } catch { }
                try { await workerSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); } catch { }
                return;
            }

            if (!HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.StatusCode = 405;
                return;
            }
            try
            {
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "http://" + target);
                request.Headers.Add(TicketAuth.HeaderName, MintTicket(keyId, TicketAuth.InternalScope));
                using var response = await DevToolsHttp.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType = response.Content.Headers.ContentType?.ToString();
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
            catch
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 502;
                }
            }
        }

        private static async Task HandleRecordingDownload(HttpContext context, string keyId, string id)
        {
            if (!IsKeyIdShaped(keyId) || string.IsNullOrEmpty(id) || id.Length > 64 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
                || !Program.Registry.TryGet(keyId, out var record))
            {
                context.Response.StatusCode = 404;
                return;
            }

            try
            {
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, $"http://{record.Address}/recordings/{id}");
                request.Headers.Add(TicketAuth.HeaderName, MintTicket(keyId, TicketAuth.InternalScope));
                using var response = await RecordingHttp.SendAsync(request,
                    System.Net.Http.HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                if (!response.IsSuccessStatusCode)
                {
                    context.Response.StatusCode = (int)response.StatusCode;
                    return;
                }

                context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                if (response.Content.Headers.ContentLength is { } length)
                {
                    context.Response.ContentLength = length;
                }
                if (response.Content.Headers.ContentDisposition is { } disposition)
                {
                    context.Response.Headers["Content-Disposition"] = disposition.ToString();
                }
                context.Response.Headers["Cache-Control"] = "no-store";

                await using var body = await response.Content.ReadAsStreamAsync(context.RequestAborted);
                await body.CopyToAsync(context.Response.Body, 1 << 16, context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                Console.WriteLine($"[Broker] Recording download for tenant '{keyId}' failed: {ex.Message}");
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 502;
                }
            }
        }

        private static bool IsKeyIdShaped(string keyId) =>
            keyId is { Length: 32 } && keyId.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

        private static IResult HandleTenantStatus(HttpContext context)
        {
            var keyId = context.Request.Query["keyId"].ToString();
            if (!IsKeyIdShaped(keyId))
            {
                return Results.BadRequest();
            }

            if (Program.Registry.TryGet(keyId, out var record))
            {
                return Results.Json(new { alive = true, sleeping = false, sessions = record.Sessions });
            }

            return WorkerManifest.TryGetGone(keyId, out var tabCount)
                ? Results.Json(new { alive = true, sleeping = true, sessions = tabCount })
                : Results.Json(new { alive = false, sleeping = false, sessions = 0 });
        }

        private static async Task<IResult> HandleTenantRelease(HttpContext context)
        {
            string keyId = null;
            try
            {
                using var doc = await JsonDocument.ParseAsync(context.Request.Body);
                if (doc.RootElement.TryGetProperty("keyId", out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    keyId = prop.GetString();
                }
            }
            catch (JsonException)
            {
            }

            if (!IsKeyIdShaped(keyId))
            {
                return Results.BadRequest();
            }

            var cacheDeleted = await Program.ReleaseTenantAsync(keyId, "released by its own client (restore prompt: start new session)");
            return Results.Json(new { ok = true, cacheDeleted });
        }

        private static string MintTicket(string keyId, string scope) => TicketAuth.Mint(Program.WorkerSecret, keyId, scope);

        private static bool IsLoopbackWorkerAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || !address.StartsWith("127.0.0.1:", StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(address.AsSpan("127.0.0.1:".Length), System.Globalization.NumberStyles.None, null, out var port) && port is > 0 and <= 65535;
        }

        private static void ReportProxyHealthChange(HttpContext context, string tenantId, string previous, string state, string detail)
        {
            var label = ProxyAssignments.Describe(tenantId, BrokerSettings.Load())?.Label ?? "(unknown)";
            var shortId = tenantId.Length > 8 ? tenantId[..8] : tenantId;
            if (state is "authFailed" or "unreachable")
            {
                var what = state == "authFailed" ? "refuses the login" : "is unreachable";
                Console.WriteLine($"[Broker] Tenant '{shortId}': proxy {label} {what}{(string.IsNullOrEmpty(detail) ? "" : $" ({detail})")}.");
                try
                {
                    context.RequestServices.GetService<IProxyAssigner>()?.ReportFailure(tenantId, label, $"{state}: {detail}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Broker] IProxyAssigner.ReportFailure threw: {ex.Message}");
                }
            }
            else if (state == "ok" && previous is "authFailed" or "unreachable")
            {
                Console.WriteLine($"[Broker] Tenant '{shortId}': proxy {label} works again.");
            }
        }

        private static async Task HandleWorkerConnection(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var presentedSecret = context.Request.Headers[TicketAuth.WorkerSecretHeaderName].ToString();
            if (!TicketAuth.SecretsEqual(presentedSecret, Program.WorkerSecret))
            {
                Console.WriteLine($"[Security] /worker: connection from {context.Connection.RemoteIpAddress} without a valid worker secret - " +
                    (Program.RequireTickets ? "REJECTED" : "allowed (log-only: Admin > Transport > Require signed tickets is off)"));
                if (Program.RequireTickets)
                {
                    context.Response.StatusCode = 401;
                    return;
                }
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();
            string registeredTenantId = null;
            string registeredAddress = null;
            var buffer = new byte[4096];

            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    using var messageStream = new MemoryStream();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }

                        messageStream.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close || messageStream.Length == 0)
                    {
                        break;
                    }

                    using var doc = JsonDocument.Parse(messageStream.GetBuffer().AsMemory(0, (int)messageStream.Length));
                    var root = doc.RootElement;
                    var type = root.GetProperty("type").GetString();

                    if (type == "register")
                    {
                        var claimedTenantId = root.GetProperty("tenantId").GetString();
                        var pageId = root.GetProperty("pageId").GetString();
                        var address = root.GetProperty("address").GetString();

                        if (!IsLoopbackWorkerAddress(address) || !Program.SpawnedProcesses.ContainsKey(claimedTenantId ?? ""))
                        {
                            var addressOk = IsLoopbackWorkerAddress(address);
                            Console.WriteLine($"[Security] /worker register: tenant '{claimedTenantId}' address '{address}' - " +
                                (addressOk ? "" : "address is not 127.0.0.1:PORT; ") +
                                (Program.SpawnedProcesses.ContainsKey(claimedTenantId ?? "") ? "" : "tenant was not spawned by this Broker; ") +
                                (Program.RequireTickets ? "REJECTED" : "allowed (log-only: Admin > Transport > Require signed tickets is off)"));
                            if (Program.RequireTickets)
                            {
                                try { await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "rejected", CancellationToken.None); } catch { }
                                return;
                            }
                        }

                        registeredTenantId = claimedTenantId;
                        registeredAddress = address;
                        Program.Registry.Register(registeredTenantId, pageId, address);
                        WorkerManifest.Add(registeredTenantId, pageId, address,
                            Program.SpawnedProcesses.TryGetValue(registeredTenantId, out var regProc) ? regProc.Id : 0);
                        Console.WriteLine($"[Broker] Worker registered: tenant='{registeredTenantId}' page='{pageId}' address={address}");
                    }
                    else if (type == "heartbeat" && registeredTenantId != null)
                    {
                        var cpu = root.GetProperty("cpu").GetDouble();
                        var encoder = root.GetProperty("encoder").GetDouble();
                        var sessions = root.GetProperty("sessions").GetInt32();

                        Program.Registry.Heartbeat(registeredTenantId, cpu, encoder, sessions);

                        string proxyState = null, proxyDetail = null;
                        if (root.TryGetProperty("proxy", out var proxyProp) && proxyProp.ValueKind == JsonValueKind.Object)
                        {
                            proxyState = proxyProp.TryGetProperty("state", out var stateProp) && stateProp.ValueKind == JsonValueKind.String ? stateProp.GetString() : null;
                            proxyDetail = proxyProp.TryGetProperty("detail", out var detailProp) && detailProp.ValueKind == JsonValueKind.String ? detailProp.GetString() : null;
                        }

                        var (proxyChanged, proxyPrevious) = ProxyHealth.Update(registeredTenantId, proxyState, proxyDetail);
                        if (proxyChanged)
                        {
                            ReportProxyHealthChange(context, registeredTenantId, proxyPrevious, proxyState, proxyDetail);
                        }

                        var tabs = root.TryGetProperty("tabs", out var tabsProp) && tabsProp.ValueKind == JsonValueKind.Array
                            ? tabsProp.EnumerateArray()
                                .Select(t => new TabManifestEntry
                                {
                                    Id = t.GetProperty("id").GetString(),
                                    Url = t.GetProperty("url").GetString(),
                                    ContextId = t.TryGetProperty("contextId", out var ctxProp) && ctxProp.ValueKind == JsonValueKind.String
                                        ? ctxProp.GetString()
                                        : null,
                                })
                                .ToList()
                            : null;
                        var selectedTabId = root.TryGetProperty("selectedTabId", out var selProp) && selProp.ValueKind == JsonValueKind.String
                            ? selProp.GetString()
                            : null;
                        WorkerManifest.UpdateTabs(registeredTenantId, tabs, selectedTabId);
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (registeredTenantId != null)
                {
                    if (Program.Registry.Remove(registeredTenantId))
                    {
                        Console.WriteLine($"[Broker] Worker for tenant '{registeredTenantId}' disconnected - removed from registry.");
                    }

                    ProxyHealth.Clear(registeredTenantId);

                    if (Program.SpawnedProcesses.TryRemove(registeredTenantId, out var exitedProcess))
                    {
                        _ = Program.ReportWorkerExitAsync(registeredTenantId, exitedProcess);
                    }
                    WorkerManifest.MarkGone(registeredTenantId, registeredAddress);

                    Program.SpawnLocks.TryRemove(registeredTenantId, out _);
                }
            }
        }

        internal const int WorkerGoneCloseCode = 4001;

        private static async Task HandleRelay(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var tenantId = context.Request.Query["tenant"].ToString();

            if (string.IsNullOrEmpty(tenantId) || !Program.Registry.TryGet(tenantId, out var record))
            {
                context.Response.StatusCode = 404;
                return;
            }

            var clientSocket = await context.WebSockets.AcceptWebSocketAsync();

            using var workerSocket = new ClientWebSocket();
            workerSocket.Options.SetRequestHeader(TicketAuth.HeaderName, MintTicket(tenantId, TicketAuth.ViewScope));
            var viewerIp = ClientIpFrom(context);
            if (!string.IsNullOrEmpty(viewerIp))
            {
                workerSocket.Options.SetRequestHeader("X-CefGlue-Viewer-Ip", viewerIp);
            }

            try
            {
                await workerSocket.ConnectAsync(new Uri($"ws://{record.Address}/frames"), context.RequestAborted);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Broker] Relay: could not reach worker for tenant '{tenantId}': {ex.Message}");

                try
                {
                    await clientSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "worker unreachable", CancellationToken.None);
                }
                catch { }

                return;
            }

            var fromWorker = PumpAsync(workerSocket, clientSocket);
            var finished = await Task.WhenAny(PumpAsync(clientSocket, workerSocket), fromWorker);

            var workerGone = finished == fromWorker && workerSocket.CloseStatus == null;

            try
            {
                await clientSocket.CloseAsync(
                    workerGone ? (WebSocketCloseStatus)WorkerGoneCloseCode : WebSocketCloseStatus.NormalClosure,
                    workerGone ? "worker-gone" : null,
                    CancellationToken.None);
            }
            catch { }
            try { await workerSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); } catch { }
        }

        private static async Task PumpAsync(WebSocket from, WebSocket to)
        {
            var buffer = new byte[65536];

            try
            {
                while (from.State == WebSocketState.Open && to.State == WebSocketState.Open)
                {
                    using var messageStream = new MemoryStream();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await from.ReceiveAsync(buffer, CancellationToken.None);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }

                        messageStream.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    await to.SendAsync(messageStream.ToArray(), result.MessageType, true, CancellationToken.None);
                }
            }
            catch (Exception)
            {
            }
        }

        private static string Transport() =>
            string.Equals(Environment.GetEnvironmentVariable("CEFGLUE_USE_WEBRTC"), "1", StringComparison.OrdinalIgnoreCase) ? "webrtc" : "websocket";

        private static object IsolationInfo() => new { mode = Program.IsolationMode, epoch = IsolationModeGuard.Epoch };

        private static int KeyIdMaxAgeSeconds() =>
            int.TryParse(Environment.GetEnvironmentVariable("CEFGLUE_ABANDON_GRACE_SECONDS"), out var s) && s > 0 ? s : 900;

        private static void NoteClient(HttpContext context, string keyId)
            => WorkerManifest.NoteClient(keyId, ClientIpFrom(context), ClientOsFrom(context.Request.Headers));

        private static string ClientIpFrom(HttpContext context)
        {
            var remote = context.Connection.RemoteIpAddress;
            if (remote != null && remote.IsIPv4MappedToIPv6)
            {
                remote = remote.MapToIPv4();
            }

            var ip = remote?.ToString();
            if (remote != null && System.Net.IPAddress.IsLoopback(remote)
                && context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded)
                && !string.IsNullOrWhiteSpace(forwarded.ToString()))
            {
                ip = forwarded.ToString().Split(',')[0].Trim();
            }

            return ip;
        }

        internal static string ClientOsFrom(IHeaderDictionary headers)
        {
            var platform = headers["Sec-CH-UA-Platform"].ToString().Trim('"', ' ');
            if (!string.IsNullOrEmpty(platform))
            {
                var version = headers["Sec-CH-UA-Platform-Version"].ToString().Trim('"', ' ');
                if (platform == "Windows" && Version.TryParse(version, out var winVersion))
                {
                    return winVersion.Major >= 13 ? "Windows 11" : winVersion.Major > 0 ? "Windows 10" : "Windows";
                }

                return string.IsNullOrEmpty(version) ? platform : $"{platform} {version}";
            }

            var ua = headers.UserAgent.ToString();
            string Match(string pattern) => System.Text.RegularExpressions.Regex.Match(ua, pattern).Groups[1].Value.Replace('_', '.');

            if (ua.Contains("Android")) { var v = Match(@"Android ([\d.]+)"); return v.Length > 0 ? $"Android {v}" : "Android"; }
            if (ua.Contains("iPhone")) { var v = Match(@"OS ([\d_]+)"); return v.Length > 0 ? $"iOS {v}" : "iOS"; }
            if (ua.Contains("iPad")) { var v = Match(@"OS ([\d_]+)"); return v.Length > 0 ? $"iPadOS {v}" : "iPadOS"; }
            if (ua.Contains("Windows NT 10.0")) return "Windows 10/11";
            if (ua.Contains("Windows NT 6.3")) return "Windows 8.1";
            if (ua.Contains("Windows NT 6.1")) return "Windows 7";
            if (ua.Contains("Windows")) return "Windows";
            if (ua.Contains("CrOS")) return "ChromeOS";
            if (ua.Contains("Mac OS X")) { var v = Match(@"Mac OS X ([\d_]+)"); return v.Length > 0 ? $"macOS {v}" : "macOS"; }
            if (ua.Contains("Linux")) return "Linux";
            return null;
        }

        private static async Task<IResult> HandleTabProxy(HttpContext context)
        {
            string keyId = null, proxyId = null;
            try
            {
                using var doc = await JsonDocument.ParseAsync(context.Request.Body);
                keyId = doc.RootElement.TryGetProperty("keyId", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
                proxyId = doc.RootElement.TryGetProperty("proxyId", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }

            if (!IsKeyIdShaped(keyId) || !Program.Registry.TryGet(keyId, out _))
            {
                return Results.Json(new { error = "no-session" }, statusCode: StatusCodes.Status404NotFound);
            }

            var settings = BrokerSettings.Load();
            if (!ProxyAssignments.PerTabChoiceApplies(settings))
            {
                return Results.Json(new { choose = false });
            }

            if (!Xilium.CefGlue.Headless.Service.ProxyUrl.TryParsePool(settings.ProxyPool, out var pool, out _) || pool.Count == 0)
            {
                return Results.Json(new { error = "proxy", message = "The proxy table saved in the Admin has no usable proxy." }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var options = pool.Select(o => new { id = o.Id, label = o.Label }).ToList();
            if (string.IsNullOrEmpty(proxyId))
            {
                return Results.Json(new { choose = true, options });
            }

            var picked = pool.FirstOrDefault(o => o.Id == proxyId);
            if (picked == null)
            {
                return Results.Json(new { choose = true, options, error = "choose-proxy" }, statusCode: StatusCodes.Status409Conflict);
            }

            const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
            var contextId = new string(System.Security.Cryptography.RandomNumberGenerator.GetItems<char>(alphabet.AsSpan(), 12));
            ProxyAssignments.SetContext(contextId, keyId, picked.Id);
            Console.WriteLine($"[Broker] Tenant '{keyId[..Math.Min(8, keyId.Length)]}': a new tab will use proxy {picked.Label}.");
            return Results.Json(new
            {
                choose = true,
                contextId,
                proxyId = picked.Id,
                label = picked.Label,
                ticket = MintTicket(keyId, $"tabproxy:{contextId}:{picked.Id}"),
            });
        }

        private static async Task<IResult> HandleAssign(HttpContext context)
        {
            string requestedKeyId = null;
            string chosenProxyId = null;

            try
            {
                using var doc = await JsonDocument.ParseAsync(context.Request.Body);
                if (doc.RootElement.TryGetProperty("keyId", out var keyIdProp) && keyIdProp.ValueKind == JsonValueKind.String)
                {
                    requestedKeyId = keyIdProp.GetString();
                }

                if (doc.RootElement.TryGetProperty("proxyId", out var proxyIdProp) && proxyIdProp.ValueKind == JsonValueKind.String)
                {
                    chosenProxyId = proxyIdProp.GetString();
                }
            }
            catch (JsonException)
            {
            }

            if (RevokedTenants.IsRevoked(requestedKeyId))
            {
                return Results.Json(new { ended = true }, statusCode: StatusCodes.Status410Gone);
            }

            if (!string.IsNullOrEmpty(requestedKeyId) && Program.Registry.TryGet(requestedKeyId, out var existing))
            {
                NoteClient(context, requestedKeyId);
                var reconnectAddress = BuildClientFacingAddress(context, existing.Address);
                Console.WriteLine($"[Broker] Reconnect: tenant '{requestedKeyId}' -> {reconnectAddress}");
                return Results.Ok(new { keyId = requestedKeyId, address = reconnectAddress, ticket = MintTicket(requestedKeyId, TicketAuth.ViewScope), isolation = IsolationInfo(), keyIdMaxAgeSeconds = KeyIdMaxAgeSeconds(), transport = Transport(), proxy = ProxyAssignments.LabelFor(requestedKeyId, BrokerSettings.Load()) });
            }

            var keyId = requestedKeyId ?? Guid.NewGuid().ToString("n");
            NoteClient(context, keyId);
            const string pageId = "default";

            Process process;
            var spawnLock = Program.SpawnLocks.GetOrAdd(keyId, _ => new SemaphoreSlim(1, 1));
            await spawnLock.WaitAsync();

            try
            {
                if (Program.Registry.TryGet(keyId, out var registered))
                {
                    var reconnectAddress = BuildClientFacingAddress(context, registered.Address);
                    Console.WriteLine($"[Broker] Reconnect (post-lock): tenant '{keyId}' -> {reconnectAddress}");
                    return Results.Ok(new { keyId, address = reconnectAddress, ticket = MintTicket(keyId, TicketAuth.ViewScope), isolation = IsolationInfo(), keyIdMaxAgeSeconds = KeyIdMaxAgeSeconds(), transport = Transport(), proxy = ProxyAssignments.LabelFor(keyId, BrokerSettings.Load()) });
                }

                if (Program.SpawnedProcesses.TryGetValue(keyId, out var existingProcess) && !existingProcess.HasExited)
                {
                    process = existingProcess;
                }
                else
                {
                    int port = ProcessSpawner.FindFreePort();
                    var (tabs, selectedTabId) = WorkerManifest.GetTabs(keyId);
                    var clientIp = ClientIpFrom(context);

                    var proxySaved = BrokerSettings.Load();
                    var proxyDecision = await context.RequestServices.GetRequiredService<IProxyAssigner>()
                        .AssignAsync(new ProxyRequest(keyId,
                            new ProxySettings(proxySaved.ProxyGlobal, proxySaved.ProxyMode, proxySaved.ProxyPool,
                                Enabled: proxySaved.ProxyEnabled == "1", GlobalProxyId: proxySaved.ProxyGlobalId),
                            chosenProxyId), context.RequestAborted);
                    if (proxyDecision.Kind == ProxyDecisionKind.Refused)
                    {
                        Console.WriteLine($"[Broker] Worker for tenant '{keyId[..Math.Min(8, keyId.Length)]}' not started: {proxyDecision.Reason}");
                        Program.SpawnLocks.TryRemove(keyId, out _);
                        return Results.Json(new { error = "proxy", message = proxyDecision.Reason }, statusCode: 503);
                    }

                    if (proxyDecision.Kind == ProxyDecisionKind.Choose)
                    {
                        Program.SpawnLocks.TryRemove(keyId, out _);
                        return Results.Json(new
                        {
                            error = "choose-proxy",
                            keyId,
                            options = proxyDecision.Options.Select(o => new { id = o.Id, label = o.Label }),
                        }, statusCode: StatusCodes.Status409Conflict);
                    }

                    SpawnDenial denial;
                    process = null;
                    lock (SpawnLimits.Gate)
                    {
                        denial = SpawnLimits.Check(keyId, WorkerManifest.TryGetGone(keyId, out _), clientIp, DateTime.UtcNow);
                        if (denial == null)
                        {
                            process = ProcessSpawner.SpawnWorker(keyId, pageId, port, $"ws://127.0.0.1:{Program.PlainHttpPort}/worker", httpsCertPath: SelfSignedCertificate.CertPath,
                                initialTabs: tabs, initialSelectedTabId: selectedTabId, proxy: proxyDecision.Assignment);
                            if (proxyDecision.Assignment != null)
                            {
                                Console.WriteLine($"[Broker] Tenant '{keyId[..Math.Min(8, keyId.Length)]}' started through proxy {proxyDecision.Assignment.ProxyId}.");
                            }
                            Program.SpawnedProcesses[keyId] = process;
                            SpawnLimits.Admitted(keyId, clientIp, DateTime.UtcNow);
                        }
                    }

                    if (denial != null)
                    {
                        SpawnLimits.LogDenied(denial, keyId, clientIp);
                        Program.SpawnLocks.TryRemove(keyId, out _);
                        context.Response.Headers["Retry-After"] = denial.RetryAfterSeconds.ToString();
                        return Results.Json(new { error = "busy", reason = denial.Reason, message = denial.Message, retryAfter = denial.RetryAfterSeconds }, statusCode: denial.Status);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Broker] Failed to spawn worker for new tenant '{keyId}': {ex}");
                return Results.Problem($"Failed to start a worker: {ex.Message}", statusCode: 500);
            }
            finally
            {
                spawnLock.Release();
            }

            var deadline = DateTime.UtcNow + Program.AssignmentTimeout;

            while (DateTime.UtcNow < deadline)
            {
                if (Program.Registry.TryGet(keyId, out var record))
                {
                    return Results.Ok(new { keyId, address = BuildClientFacingAddress(context, record.Address), ticket = MintTicket(keyId, TicketAuth.ViewScope), isolation = IsolationInfo(), keyIdMaxAgeSeconds = KeyIdMaxAgeSeconds(), transport = Transport(), proxy = ProxyAssignments.LabelFor(keyId, BrokerSettings.Load()) });
                }

                if (process.HasExited)
                {
                    Console.WriteLine($"[Broker] Worker process for tenant '{keyId}' exited before registering (code {process.ExitCode}).");
                    _ = Program.ReportWorkerExitAsync(keyId, process);
                    return Results.Problem("Worker process exited before it finished starting.", statusCode: 500);
                }

                await Task.Delay(250);
            }

            Console.WriteLine($"[Broker] Timed out waiting for worker (tenant '{keyId}', pid {process.Id}) to register.");
            return Results.Problem("Timed out waiting for the new worker to come up.", statusCode: 504);
        }

        private static string BuildClientFacingAddress(HttpContext context, string workerSelfReportedAddress)
        {
            var port = int.Parse(workerSelfReportedAddress.Split(':')[^1]);
            var effectivePort = context.Request.IsHttps ? (port <= 55535 ? port + 10000 : port - 10000) : port;
            return $"{context.Request.Host.Host}:{effectivePort}";
        }
    }
}
