using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xilium.CefGlue.Headless.Service;

namespace Xilium.CefGlue.Broker
{
    internal static class Program
    {
        internal static readonly WorkerRegistry Registry = new();
        internal static readonly TimeSpan AssignmentTimeout = TimeSpan.FromSeconds(20);

        internal static string ActiveConnectionMode { get; private set; }

        internal static string ActiveManifestTtlSeconds { get; private set; }

        internal static string IsolationMode { get; private set; } = IsolationModeGuard.Tenant;

        internal static string[] AvailableEncoders { get; private set; } = Array.Empty<string>();

        internal static bool IsVideoEncoderReady
        {
            get
            {
                var configured = Environment.GetEnvironmentVariable("CEFGLUE_VIDEO_ENCODER");
                return string.IsNullOrEmpty(configured) || AvailableEncoders.Contains(configured, StringComparer.OrdinalIgnoreCase);
            }
        }

        internal static readonly ConcurrentDictionary<string, Process> SpawnedProcesses = new();

        internal static readonly ConcurrentDictionary<string, SemaphoreSlim> SpawnLocks = new();

        internal static int PlainHttpPort;

        private static void ParseCommandLineArgs(string[] args, out bool cliPortMode, out bool disableSelfSignedHttps)
        {
            cliPortMode = false;
            disableSelfSignedHttps = false;

            foreach (var arg in args)
            {
                if (string.Equals(arg, "--port-mode", StringComparison.OrdinalIgnoreCase))
                {
                    cliPortMode = true;
                }
                else if (string.Equals(arg, "--no-selfsigned-https", StringComparison.OrdinalIgnoreCase))
                {
                    disableSelfSignedHttps = true;
                }
                else if (string.Equals(arg, "--use-webrtc", StringComparison.OrdinalIgnoreCase))
                {
                    Environment.SetEnvironmentVariable("CEFGLUE_USE_WEBRTC", "1");
                }
                else if (string.Equals(arg, "--isolation-mode-session", StringComparison.OrdinalIgnoreCase))
                {
                    IsolationMode = IsolationModeGuard.Session;
                }
                else if (arg.StartsWith("--enc-", StringComparison.OrdinalIgnoreCase))
                {
                    var encName = arg.Substring("--enc-".Length).ToLowerInvariant();
                    var mapped = encName switch
                    {
                        "h264" => "x264",
                        "openh264" => "openh264",
                        "vp9" => "vp9",
                        "qsv" => "qsv",
                        "qsv-hevc" => "qsv-hevc",
                        "nvenc" => "nvenc",
                        "x265" => "x265",
                        _ => null,
                    };

                    if (mapped == null)
                    {
                        Console.WriteLine($"[Broker] Unrecognized '{arg}' - ignoring. Known encoders: --enc-h264, --enc-openh264, --enc-vp9, --enc-qsv, --enc-hevc, --enc-nvenc, --enc-x265.");
                    }
                    else
                    {
                        Environment.SetEnvironmentVariable("CEFGLUE_VIDEO_ENCODER", mapped);
                    }
                }
                else
                {
                    Console.WriteLine($"[Broker] Unrecognized command-line argument '{arg}' - ignoring. Known flags: --port-mode, --use-webrtc, --isolation-mode-session, --no-selfsigned-https, --enc-h264/openh264/vp9/hevc.");
                }
            }
        }

        internal static void LogError(string message) =>
            ErrorLog.Append(ErrorLog.BrokerFileName, Path.GetDirectoryName(ProcessSpawner.WorkerExePath ?? "") is { Length: > 0 } workerDir ? workerDir : AppContext.BaseDirectory, message);

        internal static async Task ReportWorkerExitAsync(string tenantId, Process process)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(timeout.Token);

                var code = process.ExitCode;
                if (code == 0 || code == -1)
                {
                    return;
                }

                LogError($"Worker crashed - tenant '{tenantId}', pid {process.Id}, exit code 0x{code:X8} ({DescribeExitCode(code)}).");
            }
            catch
            {
            }
        }

        private static string DescribeExitCode(int code) => unchecked((uint)code) switch
        {
            0xC0000005 => "access violation - native crash",
            0xC00000FD => "stack overflow",
            0xC0000374 => "heap corruption - native crash",
            0xC0000409 => "fail-fast / stack buffer overrun",
            0xE0434352 => "unhandled .NET exception - see service-error.log",
            0x80000003 => "breakpoint",
            _ => "unknown",
        };

        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                LogError($"Unhandled exception - broker pid {Environment.ProcessId}{Environment.NewLine}{e.ExceptionObject}");

            foreach (var k in new[]
{
        "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES",
        "ASPNETCORE_AUTO_RELOAD_WS_ENDPOINT",
        "ASPNETCORE_AUTO_RELOAD_WS_KEY",
        "ASPNETCORE_AUTO_RELOAD_VDIR",
        "DOTNET_STARTUP_HOOKS",
        "DOTNET_MODIFIABLE_ASSEMBLIES",
        "DOTNET_WATCH",
        "DOTNET_WATCH_ITERATION",
    })
                Environment.SetEnvironmentVariable(k, null);

            ConsoleQuickEdit.Disable();

            ParseCommandLineArgs(args, out var cliPortMode, out var disableSelfSignedHttps);

            Environment.SetEnvironmentVariable("CEFGLUE_ISOLATION_MODE", IsolationMode);
            Console.WriteLine(IsolationMode == IsolationModeGuard.Session
                ? "Isolation: per session (--isolation-mode-session) - every tab gets its own persisted browser context."
                : "Isolation: per tenant (default) - every tab of a tenant shares one browser context. Pass --isolation-mode-session to isolate each tab.");

            var persistedSettings = BrokerSettings.Load();

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_VIDEO_ENCODER")) && !string.IsNullOrEmpty(persistedSettings.VideoEncoder))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_VIDEO_ENCODER", persistedSettings.VideoEncoder);
            }

            if (string.Equals(Environment.GetEnvironmentVariable("CEFGLUE_USE_WEBRTC"), "1", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_VIDEO_ENCODER")))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_VIDEO_ENCODER", "openh264");
                Console.WriteLine("WebRTC transport with no video encoder configured - defaulting to openh264.");
            }

            persistedSettings.AudioBitrateKbps ??= "48";
            persistedSettings.DisableGpu ??= "1";
            persistedSettings.DiskCacheSizeBytes ??= "30000000";
            persistedSettings.MediaCacheSizeBytes ??= "30000000";
            persistedSettings.SessionTimeoutSeconds ??= "300";

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_OPUS_BITRATE_KBPS")) && !string.IsNullOrEmpty(persistedSettings.AudioBitrateKbps))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_OPUS_BITRATE_KBPS", persistedSettings.AudioBitrateKbps);
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_DISABLE_GPU")) && !string.IsNullOrEmpty(persistedSettings.DisableGpu))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_DISABLE_GPU", persistedSettings.DisableGpu);
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_TEXTURE_ENABLE")) && !string.IsNullOrEmpty(persistedSettings.TextureEnable))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_TEXTURE_ENABLE", persistedSettings.TextureEnable);
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_DISK_CACHE_SIZE_BYTES")) && !string.IsNullOrEmpty(persistedSettings.DiskCacheSizeBytes))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_DISK_CACHE_SIZE_BYTES", persistedSettings.DiskCacheSizeBytes);
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_MEDIA_CACHE_SIZE_BYTES")) && !string.IsNullOrEmpty(persistedSettings.MediaCacheSizeBytes))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_MEDIA_CACHE_SIZE_BYTES", persistedSettings.MediaCacheSizeBytes);
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_DEFAULT_URL")) && !string.IsNullOrEmpty(persistedSettings.DefaultUrl))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_DEFAULT_URL", persistedSettings.DefaultUrl);
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_NEW_PAGE_USE_LAST_URL")) && !string.IsNullOrEmpty(persistedSettings.NewPageUseLastUrl))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_NEW_PAGE_USE_LAST_URL", persistedSettings.NewPageUseLastUrl);
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_WEBRTC_VIDEO_PACING_BPS")) && !string.IsNullOrEmpty(persistedSettings.WebRtcPacingBps))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_WEBRTC_VIDEO_PACING_BPS", persistedSettings.WebRtcPacingBps);
            }

            if (string.Equals(Environment.GetEnvironmentVariable("CEFGLUE_USE_WEBRTC"), "1", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_WEBRTC_VIDEO_PACING_BPS")))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_WEBRTC_VIDEO_PACING_BPS", "500000000");
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_WEBRTC_ICE_SERVERS")) && !string.IsNullOrEmpty(persistedSettings.WebRtcIceServers))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_WEBRTC_ICE_SERVERS", persistedSettings.WebRtcIceServers);
            }

            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_ABANDON_GRACE_SECONDS")) && !string.IsNullOrEmpty(persistedSettings.SessionTimeoutSeconds))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_ABANDON_GRACE_SECONDS", persistedSettings.SessionTimeoutSeconds);
            }

            if (persistedSettings.EncoderSettings != null)
            {
                foreach (var (envVarName, value) in persistedSettings.EncoderSettings)
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVarName)) && !string.IsNullOrEmpty(value))
                    {
                        Environment.SetEnvironmentVariable(envVarName, value);
                    }
                }
            }

            if (!cliPortMode && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_BROKER_DEFAULT_MODE")) && !string.IsNullOrEmpty(persistedSettings.ConnectionMode))
            {
                Environment.SetEnvironmentVariable("CEFGLUE_BROKER_DEFAULT_MODE", persistedSettings.ConnectionMode);
            }

            var port = int.TryParse(Environment.GetEnvironmentVariable("CEFGLUE_BROKER_PORT"), out var parsedPort) ? parsedPort : 57400;
            PlainHttpPort = port;

            var defaultModeEnv = Environment.GetEnvironmentVariable("CEFGLUE_BROKER_DEFAULT_MODE");
            var defaultMode = cliPortMode || string.Equals(defaultModeEnv, "port", StringComparison.OrdinalIgnoreCase) ? "port" : "relay";
            ActiveConnectionMode = defaultMode;

            var defaultFormat = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_VIDEO_ENCODER")) ? "jpg" : "h264";

            var adminPort = int.TryParse(Environment.GetEnvironmentVariable("CEFGLUE_BROKER_ADMIN_PORT"), out var parsedAdminPort) ? parsedAdminPort : 57401;

            var httpsPort = int.TryParse(Environment.GetEnvironmentVariable("CEFGLUE_BROKER_HTTPS_PORT"), out var parsedHttpsPort) ? parsedHttpsPort : 57443;
            var httpsCert = disableSelfSignedHttps ? null : SelfSignedCertificate.GetOrCreate();

            var localhostHttpsPort = int.TryParse(Environment.GetEnvironmentVariable("CEFGLUE_BROKER_LOCALHOST_HTTPS_PORT"), out var parsedLocalhostHttpsPort) ? parsedLocalhostHttpsPort : 7005;
            var trustedLocalhostCert = SelfSignedCertificate.TryLoadTrustedLocalhostCert();

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory,
            });
            builder.Logging.ClearProviders();

            builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(3));
            builder.Services.AddRazorPages();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Any, port);

                if (httpsCert != null)
                {
                    options.Listen(IPAddress.Any, httpsPort, listenOptions => listenOptions.UseHttps(httpsCert));
                }

                if (trustedLocalhostCert != null)
                {
                    options.Listen(IPAddress.Loopback, localhostHttpsPort, listenOptions => listenOptions.UseHttps(trustedLocalhostCert));
                    Console.WriteLine($"[Broker] https://localhost:{localhostHttpsPort} is warning-free (trusted cert); https://localhost:{httpsPort} still shows the usual self-signed warning.");
                }
            });
            builder.WebHost.UseSetting(WebHostDefaults.PreventHostingStartupKey, "true");
            var app = builder.Build();

            if (httpsCert != null || trustedLocalhostCert != null)
            {
                var pageRoutesToRedirect = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/", "/broker", "/broker-mobile" };
                app.Use(async (context, next) =>
                {
                    if (!context.Request.IsHttps && pageRoutesToRedirect.Contains(context.Request.Path.Value ?? ""))
                    {
                        var targetPort = trustedLocalhostCert != null && string.Equals(context.Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                            ? localhostHttpsPort
                            : httpsPort;

                        if (targetPort == httpsPort && httpsCert == null)
                        {
                            await next();
                            return;
                        }

                        context.Response.Redirect($"https://{context.Request.Host.Host}:{targetPort}{context.Request.Path}{context.Request.QueryString}");
                        return;
                    }

                    await next();
                });
            }

            app.UseWebSockets();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.MapBrokerEndpoints(defaultMode, defaultFormat);

            _ = new Timer(_ =>
            {
                foreach (var tenantId in Registry.RemoveStale())
                {
                    Console.WriteLine($"[Broker] Tenant '{tenantId}' missed its heartbeat window - removed.");
                }
            }, null, WorkerRegistry.MissedHeartbeatTimeout, WorkerRegistry.MissedHeartbeatTimeout);

            var manifestTtlSeconds = int.TryParse(persistedSettings.ManifestTtlSeconds, out var parsedTtl) ? parsedTtl : 900;
            var manifestTtl = TimeSpan.FromSeconds(manifestTtlSeconds);
            ActiveManifestTtlSeconds = manifestTtlSeconds.ToString();
            _ = new Timer(_ =>
            {
                foreach (var tenantId in WorkerManifest.PruneStaleGoneEntries(manifestTtl))
                {
                    Console.WriteLine($"[WorkerManifest] Pruned stale carcass entry for tenant '{tenantId}' - gone longer than {manifestTtl.TotalSeconds:F0}s with no respawn.");
                    DeleteTenantCache(tenantId);
                }
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            Console.WriteLine($"CefGlue.Broker listening on http://0.0.0.0:{port} (worker: ws://.../worker, client: POST /assign)");
            Console.WriteLine(httpsCert != null
                ? $"HTTPS (self-signed, LAN-testing only - browser will show a cert warning once): https://0.0.0.0:{httpsPort}/broker.html"
                : "HTTPS (self-signed): disabled via --no-selfsigned-https - expecting a reverse proxy (e.g. Caddy/Nginx with a real cert) in front of this process.");
            Console.WriteLine($"Control channel: tcp://127.0.0.1:{adminPort}/ (loopback-only, plain TCP/NDJSON, not HTTP) - run CefGlue.Broker.Admin for a live web UI over this.");
            Console.WriteLine($"Default client connection mode: {defaultMode} - set CEFGLUE_BROKER_DEFAULT_MODE=port/relay to switch (restart required); a client's own ?mode= URL override always wins over this.");
            Console.WriteLine(defaultFormat == "jpg"
                ? "Default stream format: jpg - no --enc-h264/openh264/vp9/hevc flag (or CEFGLUE_VIDEO_ENCODER env var) was given, so new sessions start in plain JPG frames instead of assuming a real video encoder is ready."
                : "Default stream format: h264 (a video encoder was explicitly configured).");

            IsolationModeGuard.EnforceOnStartup(IsolationMode);

            WorkerManifest.RecoverOnStartup((entry, proc) =>
            {
                Registry.Register(entry.TenantId, entry.PageId, entry.Address);
                SpawnedProcesses[entry.TenantId] = proc;
            });

            AvailableEncoders = DetectAvailableEncoders();
            Console.WriteLine(AvailableEncoders.Length > 0
                ? $"[Broker] Detected available encoders on this host: {string.Join(", ", AvailableEncoders)}."
                : "[Broker] Detected available encoders on this host: none (only 'unset (jpg fallback)' will work) - CEFGLUE_WORKER_EXE_PATH may be unset/wrong, or no native encoder DLLs/drivers were found.");

            var configuredEncoder = Environment.GetEnvironmentVariable("CEFGLUE_VIDEO_ENCODER");
            if (!string.IsNullOrEmpty(configuredEncoder) && !AvailableEncoders.Contains(configuredEncoder, StringComparer.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Broker] Video encoder '{configuredEncoder}' is not available on this host{(configuredEncoder == "openh264" ? " - download OpenH264 in Admin > Encoder Settings, then Restart" : "")}."
                    + (string.Equals(Environment.GetEnvironmentVariable("CEFGLUE_USE_WEBRTC"), "1") ? " WebRTC viewers get no video until it is." : ""));
                Console.ResetColor();
            }

            _ = ControlPlaneServer.RunAsync(adminPort);

            Storage.BrokerStore.WriteRuntime(ControlPlaneServer.RuntimeSnapshot());
            app.Lifetime.ApplicationStopping.Register(Storage.BrokerStore.MarkStopped);

            app.Run();
        }

        internal static async Task<bool> ReleaseTenantAsync(string tenantId, string reason)
        {
            RevokedTenants.Revoke(tenantId);

            if (SpawnedProcesses.TryGetValue(tenantId, out var process) && TryKillWorker(tenantId, out _))
            {
                try
                {
                    await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
                }
                catch (OperationCanceledException)
                {
                }
            }

            Registry.Remove(tenantId);
            SpawnedProcesses.TryRemove(tenantId, out _);
            WorkerManifest.Remove(tenantId);
            var cacheDeleted = await DeleteTenantCacheWithRetriesAsync(tenantId);
            Console.WriteLine($"[Broker] Tenant '{tenantId}' {reason} - tabs forgotten, cache deleted: {cacheDeleted}.");
            return cacheDeleted;
        }

        internal static async Task<bool> DeleteTenantCacheWithRetriesAsync(string tenantId)
        {
            var cacheRoot = ControlPlaneServer.GetWorkerCacheRoot();
            if (cacheRoot == null)
            {
                return false;
            }

            var tenantDir = Path.Combine(Path.GetFullPath(cacheRoot), Xilium.CefGlue.Headless.Service.TenantCachePaths.TenantDirName(tenantId));
            for (var attempt = 1; attempt <= 10; attempt++)
            {
                if (!Directory.Exists(tenantDir))
                {
                    return true;
                }

                try
                {
                    Directory.Delete(tenantDir, recursive: true);
                    return true;
                }
                catch (Exception) when (attempt < 10)
                {
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Broker] Could not delete cache folder for released tenant '{tenantId}': {ex.Message}");
                }
            }

            return false;
        }

        private static void DeleteTenantCache(string tenantId)
        {
            var cacheRoot = ControlPlaneServer.GetWorkerCacheRoot();
            if (cacheRoot == null || string.IsNullOrEmpty(tenantId) || Registry.TryGet(tenantId, out _))
            {
                return;
            }

            var rootFull = Path.GetFullPath(cacheRoot);
            var tenantDir = Path.GetFullPath(Path.Combine(rootFull, Xilium.CefGlue.Headless.Service.TenantCachePaths.TenantDirName(tenantId)));
            if (!string.Equals(Path.GetDirectoryName(tenantDir), rootFull.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(tenantDir))
            {
                return;
            }

            try
            {
                Directory.Delete(tenantDir, recursive: true);
                Console.WriteLine($"[WorkerManifest] Deleted cache folder for pruned tenant '{tenantId}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WorkerManifest] Could not delete cache folder for pruned tenant '{tenantId}': {ex.Message}");
            }
        }

        private static string[] DetectAvailableEncoders()
        {
            if (string.IsNullOrEmpty(ProcessSpawner.WorkerExePath) || !File.Exists(ProcessSpawner.WorkerExePath))
            {
                Console.WriteLine("[Broker] Skipping encoder availability probe - CEFGLUE_WORKER_EXE_PATH is unset or doesn't point at a real file.");
                return Array.Empty<string>();
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = ProcessSpawner.WorkerExePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                };
                startInfo.ArgumentList.Add("--probe-encoders");

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return Array.Empty<string>();
                }

                var output = process.StandardOutput.ReadToEnd();

                if (!process.WaitForExit(5000))
                {
                    Console.WriteLine("[Broker] Encoder availability probe timed out after 5s - killing it.");
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return Array.Empty<string>();
                }

                using var doc = JsonDocument.Parse(output);
                return doc.RootElement.GetProperty("availableEncoders").EnumerateArray()
                    .Select(e => e.GetString())
                    .ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Broker] Encoder availability probe failed: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        internal static bool TryKillWorker(string tenantId, out string error)
        {
            if (!SpawnedProcesses.TryGetValue(tenantId, out var process))
            {
                error = $"No tracked process for tenant '{tenantId}' (it may have been spawned before this broker instance started, or already exited).";
                return false;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                Console.WriteLine($"[Broker] Admin force-killed worker for tenant '{tenantId}'.");
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Broker] Failed to kill worker for tenant '{tenantId}': {ex}");
                error = $"Failed to kill worker: {ex.Message}";
                return false;
            }
        }

    }
}
