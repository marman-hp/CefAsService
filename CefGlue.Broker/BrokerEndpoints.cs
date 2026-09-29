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
            app.Map("/relay/frames", HandleRelay);

            app.MapGet("/config", () => Results.Json(new
            {
                defaultMode,
                defaultFormat = Program.IsVideoEncoderReady ? defaultFormat : "jpg",
                videoReady = Program.IsVideoEncoderReady,
            }));

            app.MapGet("/tenant-status", HandleTenantStatus);
            app.MapPost("/tenant-release", (Delegate)HandleTenantRelease);
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

        private static async Task HandleWorkerConnection(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
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
                        registeredTenantId = root.GetProperty("tenantId").GetString();
                        var pageId = root.GetProperty("pageId").GetString();
                        var address = root.GetProperty("address").GetString();

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

                    Program.SpawnedProcesses.TryRemove(registeredTenantId, out _);
                    WorkerManifest.MarkGone(registeredTenantId, registeredAddress);

                    Program.SpawnLocks.TryRemove(registeredTenantId, out _);
                }
            }
        }

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

            await Task.WhenAny(
                PumpAsync(clientSocket, workerSocket),
                PumpAsync(workerSocket, clientSocket));

            try { await clientSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); } catch { }
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

        private static object IsolationInfo() => new { mode = Program.IsolationMode, epoch = IsolationModeGuard.Epoch };

        private static int KeyIdMaxAgeSeconds() =>
            int.TryParse(Environment.GetEnvironmentVariable("CEFGLUE_ABANDON_GRACE_SECONDS"), out var s) && s > 0 ? s : 900;

        private static void NoteClient(HttpContext context, string keyId)
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

            WorkerManifest.NoteClient(keyId, ip, ClientOsFrom(context.Request.Headers));
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

        private static async Task<IResult> HandleAssign(HttpContext context)
        {
            string requestedKeyId = null;

            try
            {
                using var doc = await JsonDocument.ParseAsync(context.Request.Body);
                if (doc.RootElement.TryGetProperty("keyId", out var keyIdProp) && keyIdProp.ValueKind == JsonValueKind.String)
                {
                    requestedKeyId = keyIdProp.GetString();
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
                return Results.Ok(new { keyId = requestedKeyId, address = reconnectAddress, isolation = IsolationInfo(), keyIdMaxAgeSeconds = KeyIdMaxAgeSeconds() });
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
                    return Results.Ok(new { keyId, address = reconnectAddress, isolation = IsolationInfo(), keyIdMaxAgeSeconds = KeyIdMaxAgeSeconds() });
                }

                if (Program.SpawnedProcesses.TryGetValue(keyId, out var existingProcess) && !existingProcess.HasExited)
                {
                    process = existingProcess;
                }
                else
                {
                    int port = ProcessSpawner.FindFreePort();
                    var (tabs, selectedTabId) = WorkerManifest.GetTabs(keyId);
                    process = ProcessSpawner.SpawnWorker(keyId, pageId, port, $"ws://127.0.0.1:{Program.PlainHttpPort}/worker", httpsCertPath: SelfSignedCertificate.CertPath,
                        initialTabs: tabs, initialSelectedTabId: selectedTabId);
                    Program.SpawnedProcesses[keyId] = process;
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
                    return Results.Ok(new { keyId, address = BuildClientFacingAddress(context, record.Address), isolation = IsolationInfo(), keyIdMaxAgeSeconds = KeyIdMaxAgeSeconds() });
                }

                if (process.HasExited)
                {
                    Console.WriteLine($"[Broker] Worker process for tenant '{keyId}' exited before registering (code {process.ExitCode}).");
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
