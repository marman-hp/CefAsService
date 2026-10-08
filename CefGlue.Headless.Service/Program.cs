using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using Xilium.CefGlue.Common;
using Xilium.CefGlue.Headless.Server;
using Xilium.CefGlue.Headless.WebRtc;

namespace Xilium.CefGlue.Headless.Service
{
    internal static class Program
    {
        internal static readonly string DefaultUrl =
            SessionManager.NormalizeStartUrl(Environment.GetEnvironmentVariable("CEFGLUE_DEFAULT_URL")) ?? "https://www.google.com";

        internal static readonly bool NewPageUsesLastUrl =
            Environment.GetEnvironmentVariable("CEFGLUE_NEW_PAGE_USE_LAST_URL") != "0";

        internal static IVideoEncoderPlugin SelectedEncoderPlugin { get; private set; }

        internal static IReadOnlyDictionary<string, string> EncoderPluginSettings { get; private set; } = new Dictionary<string, string>();

        internal static bool TextureEnabled { get; private set; }

        internal static int OpusBitrateBps { get; private set; } = 48_000;

        internal static VideoQuality DefaultH264Quality { get; private set; }

        internal static string TenantId { get; private set; }
        internal static string PageId { get; private set; }

        internal static IsolationContextStore IsolationContexts { get; private set; }

        private static Timer _isolationPruneTimer;

        private static bool _deleteCacheOnExit = true;

        private sealed class InitialTab
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("url")]
            public string Url { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("contextId")]
            public string ContextId { get; set; }
        }

        private static void ApplyBrokerSettingsFromDatabase()
        {
            var path = Xilium.CefGlue.Broker.Storage.BrokerDb.PathFor(AppContext.BaseDirectory);
            try
            {
                using var db = Xilium.CefGlue.Broker.Storage.BrokerDb.OpenReadOnly(path);
                var saved = Xilium.CefGlue.Broker.Storage.BrokerDb.ReadJson(db, Xilium.CefGlue.Broker.Storage.BrokerDb.Settings);
                string Saved(string key) => Xilium.CefGlue.Broker.Storage.BrokerDb.AsString(saved.GetValueOrDefault(key));

                void Apply(string envVarName, string value)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        Environment.SetEnvironmentVariable(envVarName, value);
                    }
                }

                Apply("CEFGLUE_VIDEO_ENCODER", Saved("VideoEncoder"));
                Apply("CEFGLUE_OPUS_BITRATE_KBPS", Saved("AudioBitrateKbps"));
                Apply("CEFGLUE_DISABLE_GPU", Saved("DisableGpu"));
                Apply("CEFGLUE_TEXTURE_ENABLE", Saved("TextureEnable"));
                Apply("CEFGLUE_DISK_CACHE_SIZE_BYTES", Saved("DiskCacheSizeBytes"));
                Apply("CEFGLUE_MEDIA_CACHE_SIZE_BYTES", Saved("MediaCacheSizeBytes"));
                Apply("CEFGLUE_WEBRTC_VIDEO_PACING_BPS", Saved("WebRtcPacingBps"));
                Apply("CEFGLUE_WEBRTC_ICE_SERVERS", Saved("WebRtcIceServers"));

                foreach (var (envVarName, value) in Xilium.CefGlue.Broker.Storage.BrokerDb.AsStringMap(saved.GetValueOrDefault("EncoderSettings")))
                {
                    Apply(envVarName, value);
                }

                Console.WriteLine($"[Worker] Settings read from {path}.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Worker] FATAL: cannot read settings from {path} ({ex.GetType().Name}: {ex.Message}) - worker stopped.");
                Console.ResetColor();
                ErrorLog.Append(ErrorLog.ServiceFileName, AppContext.BaseDirectory, $"Cannot read settings from {path} - worker stopped: {ex}");
                Environment.Exit(4);
            }
        }

        internal static string CefLogFileName { get; private set; } = "cef.log";

        private static string PrepareCefLogPath(bool isTenantScoped)
        {
            if (!isTenantScoped)
            {
                return Path.Combine(AppContext.BaseDirectory, CefLogFileName);
            }

            var directory = ErrorLog.LogDirectory(AppContext.BaseDirectory);
            CefLogFileName = $"cef-{TenantCachePaths.TenantDirName(TenantId)}.log";
            var path = Path.Combine(directory, CefLogFileName);

            try
            {
                Directory.CreateDirectory(directory);

                var prevPath = Path.ChangeExtension(path, ".prev.log");
                for (var attempt = 0; File.Exists(path) && attempt < 8; attempt++)
                {
                    try
                    {
                        File.Move(path, prevPath, overwrite: true);
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(250);
                    }
                }

                if (File.Exists(path))
                {
                    using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var target = new FileStream(prevPath, FileMode.Create, FileAccess.Write);
                    source.CopyTo(target);
                }

                foreach (var old in Directory.EnumerateFiles(directory, "cef-*.log"))
                {
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(old) > TimeSpan.FromDays(14))
                    {
                        try { File.Delete(old); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Program] Could not prepare CEF log folder '{directory}': {ex.Message}");
            }

            return path;
        }

        private static void UseHighResolutionTimer()
        {
            if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("CEFGLUE_WEBRTC_HIGHRES_TIMER") == "0")
            {
                Console.WriteLine("Timer: Windows default resolution (CEFGLUE_WEBRTC_HIGHRES_TIMER=0, or not Windows).");
                return;
            }

            var ok = timeBeginPeriod(1) == 0;
            Console.WriteLine(ok
                ? "Timer: 1 ms resolution for the WebRTC pacer (set CEFGLUE_WEBRTC_HIGHRES_TIMER=0 to turn off)."
                : "Timer: timeBeginPeriod(1) failed - the WebRTC pacer runs on the default ~15.6 ms timer.");
        }

        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);

        private static void Main(string[] args)
        {
            ErrorLog.InstallUnhandledExceptionHandler(ErrorLog.ServiceFileName, AppContext.BaseDirectory, () =>
                $"worker pid {Environment.ProcessId}, tenant '{Environment.GetEnvironmentVariable("CEFGLUE_TENANT_ID") ?? "(standalone)"}', page '{Environment.GetEnvironmentVariable("CEFGLUE_PAGE_ID")}'");

            if (args.Length > 0 && string.Equals(args[0], "--probe-encoders", StringComparison.OrdinalIgnoreCase))
            {
                var stdout = Console.Out;
                Console.SetOut(TextWriter.Null);
                var available = EncoderPluginHost.DetectAvailable();
                Console.SetOut(stdout);
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { availableEncoders = available }));
                Environment.Exit(0);
                return;
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_TENANT_ID"))
                && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CEFGLUE_BROKER_URL")))
            {
                ApplyBrokerSettingsFromDatabase();
            }

            var useWebRtc = (Array.Exists(args, a => string.Equals(a, "--use-webrtc", StringComparison.OrdinalIgnoreCase))
                    || string.Equals(Environment.GetEnvironmentVariable("CEFGLUE_USE_WEBRTC"), "1"))
                && !Array.Exists(args, a => string.Equals(a, "--use-websocket", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine(useWebRtc
                ? "Transport: WebRTC (CefGlue.WebRTC.Transport) - pass --use-websocket to fall back."
                : "Transport: WebSocket (FrameSocketServer, default) - pass --use-webrtc, or set CEFGLUE_USE_WEBRTC=1 (broker-spawned workers), to try the WebRTC transport instead.");
            if (useWebRtc)
            {
                UseHighResolutionTimer();
            }

            SelectedEncoderPlugin = EncoderPluginHost.Select(Environment.GetEnvironmentVariable("CEFGLUE_VIDEO_ENCODER"));
            EncoderPluginSettings = EncoderSettings.FromEnvironment(SelectedEncoderPlugin?.SettingsPrefix);
            Console.WriteLine(SelectedEncoderPlugin == null
                ? "Video encoder: none installed (plugins/encoders is empty) - only the image formats (png/jpg/webp/raw) work."
                : $"Video encoder: {SelectedEncoderPlugin.Id} - set CEFGLUE_VIDEO_ENCODER to one of [{string.Join(", ", EncoderPluginHost.Plugins.Select(p => p.Id))}] to switch (restart required).");
            if (SelectedEncoderPlugin?.DescribeSettings(EncoderPluginSettings) is { } tuningLine)
            {
                Console.WriteLine($"{tuningLine} - set CEFGLUE_{SelectedEncoderPlugin.SettingsPrefix}_* to override (restart required).");
            }

            DefaultH264Quality = VideoQuality.Speed;

            TextureEnabled = string.Equals(Environment.GetEnvironmentVariable("CEFGLUE_TEXTURE_ENABLE"), "1");
            Console.WriteLine(TextureEnabled
                ? "Render path: accelerated D3D11 shared-texture (CEFGLUE_TEXTURE_ENABLE=1) - see TextureEnabled's own remarks for the 2026-09-08 bandwidth/stall investigation this is implicated in."
                : "Render path: software OnPaint (default) - set CEFGLUE_TEXTURE_ENABLE=1 for the accelerated shared-texture path instead.");

            if (int.TryParse(Environment.GetEnvironmentVariable("CEFGLUE_OPUS_BITRATE_KBPS"), out var opusKbps) && opusKbps >= 6 && opusKbps <= 510)
            {
                OpusBitrateBps = opusKbps * 1000;
            }
            Console.WriteLine($"Audio: Opus @ {OpusBitrateBps / 1000}kbps (set CEFGLUE_OPUS_BITRATE_KBPS to change, 6-510).");

            TenantId = Environment.GetEnvironmentVariable("CEFGLUE_TENANT_ID");
            PageId = Environment.GetEnvironmentVariable("CEFGLUE_PAGE_ID");
            if (string.IsNullOrEmpty(PageId))
            {
                PageId = "default";
            }

            var isTenantScoped = !string.IsNullOrEmpty(TenantId);

            var cachePath = isTenantScoped
                ? Path.Combine(TenantCachePaths.Root(AppContext.BaseDirectory), TenantCachePaths.TenantDirName(TenantId), PageId)
                : Path.Combine(AppContext.BaseDirectory, "cef_temp");

            _deleteCacheOnExit = !isTenantScoped;

            AppDomain.CurrentDomain.ProcessExit += delegate { Cleanup(cachePath, _deleteCacheOnExit); };

            if (string.Equals(Environment.GetEnvironmentVariable("CEFGLUE_ISOLATION_MODE"), "session", StringComparison.OrdinalIgnoreCase))
            {
                var contextTtlSeconds = int.TryParse(Environment.GetEnvironmentVariable("CEFGLUE_CONTEXT_TTL_SECONDS"), out var parsedContextTtl) ? parsedContextTtl : 900;
                IsolationContexts = new IsolationContextStore(cachePath, TimeSpan.FromSeconds(contextTtlSeconds));
                Console.WriteLine($"Isolation: per session - every tab gets its own persisted context at {Path.Combine(cachePath, IsolationContextStore.ExampleDirName)} (unused contexts deleted after {contextTtlSeconds}s).");
            }
            else
            {
                Console.WriteLine("Isolation: per tenant (default) - every tab shares one context.");
            }

            var profileDir = Path.Combine(cachePath, IsolationContexts != null ? IsolationContextStore.ExampleDirName : "Default");
            var worstCasePath = profileDir.Length + TenantCachePaths.ChromiumProfileTailLength;
            if (worstCasePath > 259)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Program] WARNING: browser storage paths can reach ~{worstCasePath} chars (Windows limit 260) under '{profileDir}'. " +
                    "Sites using CacheStorage/IndexedDB (e.g. WhatsApp Web) may fail. Set CEFGLUE_CACHE_ROOT to a shorter folder (the default on Windows is C:\\ProgramData\\CefGlue\\cache).");
                Console.ResetColor();
            }

            var cefLogPath = PrepareCefLogPath(isTenantScoped);
            Console.WriteLine($"[Program] CEF log: {cefLogPath}");

            var settings = new CefSettings
            {
                RootCachePath = cachePath,
                WindowlessRenderingEnabled = true,
                LogFile = cefLogPath,
                CommandLineArgsDisabled = false,
            };

            var gpuForcingEnabled = string.Equals(Environment.GetEnvironmentVariable("CEFGLUE_GPU_FORCING"), "1");
            var gpuFlags = gpuForcingEnabled ? GpuAdapterSelection.TryGetIntelAdapterLuidFlags() : null;
            if (gpuForcingEnabled)
            {
                Console.WriteLine("GPU: forcing enabled via CEFGLUE_GPU_FORCING=1.");
            }

            var flags = new List<KeyValuePair<string, string>>
            {
                KeyValuePair.Create("touch-events", "enabled"),
                KeyValuePair.Create("blink-settings", "pictureInPictureEnabled=false"),
                KeyValuePair.Create("disable-blink-features", "DocumentPictureInPictureAPI"),
            };
            if (gpuFlags != null)
            {
                flags.AddRange(gpuFlags);
            }
            CefRuntimeLoader.Initialize(settings, flags: flags.ToArray());

            var portEnv = Environment.GetEnvironmentVariable("CEFGLUE_PORT");
            var port = int.TryParse(portEnv, out var parsedPort) ? parsedPort : 57391;

            X509Certificate2 httpsCertificate = null;
            var httpsCertPath = Environment.GetEnvironmentVariable("CEFGLUE_HTTPS_CERT_PATH");
            if (!string.IsNullOrEmpty(httpsCertPath) && File.Exists(httpsCertPath))
            {
                httpsCertificate = X509CertificateLoader.LoadPkcs12FromFile(httpsCertPath, "cefglue-dev-only", X509KeyStorageFlags.Exportable);
            }

            var frameSocketServer = new FrameSocketServer(port: port, httpsCertificate: httpsCertificate);

            var webRtcVideoCodec = SelectedEncoderPlugin?.Codec switch
            {
                VideoCodecFamily.Av1 => WebRtcVideoCodec.Av1,
                VideoCodecFamily.Vp9 => WebRtcVideoCodec.Vp9,
                VideoCodecFamily.H265 => WebRtcVideoCodec.H265,
                _ => WebRtcVideoCodec.H264,
            };
            var h264ProfileLevelId = webRtcVideoCodec == WebRtcVideoCodec.H264 ? SelectedEncoderPlugin?.H264ProfileLevelId(EncoderPluginSettings) : null;
            IFrameTransport transport = useWebRtc ? new WebRtcTransport(frameSocketServer, webRtcVideoCodec, h264ProfileLevelId) : frameSocketServer;

            var sessions = new SessionManager(transport);
            transport.MessageReceived += sessions.HandleMessage;

            transport.Connected += sessions.OnFrameClientConnected;

            var initialTabsJson = Environment.GetEnvironmentVariable("CEFGLUE_INITIAL_TABS");
            var initialTabs = string.IsNullOrEmpty(initialTabsJson)
                ? null
                : JsonSerializer.Deserialize<InitialTab[]>(initialTabsJson);

            if (initialTabs is { Length: > 0 })
            {
                var selectedTabId = Environment.GetEnvironmentVariable("CEFGLUE_INITIAL_SELECTED_TAB_ID");
                string firstSessionId = null;
                string selectedSessionId = null;

                foreach (var tab in initialTabs)
                {
                    var url = string.IsNullOrEmpty(tab.Url) || SessionManager.IsScriptUrl(tab.Url) ? sessions.AddressBarValue : tab.Url;
                    var sessionId = sessions.AddBrowser(url, existingId: tab.Id, existingContextId: tab.ContextId);
                    firstSessionId ??= sessionId;

                    if (!string.IsNullOrEmpty(selectedTabId) && sessionId == selectedTabId)
                    {
                        selectedSessionId = sessionId;
                    }
                }

                sessions.Select(selectedSessionId ?? firstSessionId);
            }
            else
            {
                var initialUrl = Environment.GetEnvironmentVariable("CEFGLUE_INITIAL_URL");
                sessions.AddBrowser(string.IsNullOrEmpty(initialUrl) ? sessions.AddressBarValue : initialUrl);
            }

            if (IsolationContexts != null)
            {
                _isolationPruneTimer = new Timer(_ =>
                {
                    try
                    {
                        sessions.PruneIsolationContexts();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Program] Isolation context prune failed, retrying next tick: {ex.Message}");
                    }
                }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            }

            Console.WriteLine($"CefGlue.Headless.Service listening on {(httpsCertificate != null ? "wss" : "ws")}://127.0.0.1:{frameSocketServer.Port}/frames");

            WorkerHeartbeatClient heartbeatClient = null;
            var brokerUrl = Environment.GetEnvironmentVariable("CEFGLUE_BROKER_URL");

            if (isTenantScoped && !string.IsNullOrEmpty(brokerUrl))
            {
                heartbeatClient = new WorkerHeartbeatClient(
                    brokerUrl, TenantId, PageId, $"127.0.0.1:{frameSocketServer.Port}",
                    () => sessions.SessionCount, () => sessions.AllTabUrls);
                heartbeatClient.Start();

                var graceSeconds = int.TryParse(Environment.GetEnvironmentVariable("CEFGLUE_ABANDON_GRACE_SECONDS"), out var parsedGrace) ? parsedGrace : 900;
                var watchdog = new AbandonmentWatchdog(TimeSpan.FromSeconds(graceSeconds), () =>
                {
                    Console.WriteLine("[Program] No viewer connected within the grace window - exiting to free resources (cache kept; the Broker deletes it after its Manifest TTL).");
                    Environment.Exit(0);
                });
                transport.Connected += watchdog.Cancel;
                transport.Disconnected += watchdog.Start;
                watchdog.Start();
            }

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("[Program] Ctrl+C - saving cookies, then exiting.");
                FlushCookies(TimeSpan.FromSeconds(1));
                CefRuntimeLoader.SkipShutdownOnExit = true;
                Environment.Exit(0);
            };

            new ManualResetEvent(false).WaitOne();
        }

        private static void FlushCookies(TimeSpan timeout)
        {
            try
            {
                var managers = new List<CefCookieManager>();
                if (CefCookieManager.GetGlobal(null) is { } global)
                {
                    managers.Add(global);
                }

                foreach (var context in IsolationContexts?.OpenContexts() ?? new List<CefRequestContext>())
                {
                    if (context.GetCookieManager(null) is { } manager)
                    {
                        managers.Add(manager);
                    }
                }

                var pending = new CountdownEvent(managers.Count);
                foreach (var manager in managers)
                {
                    if (!manager.FlushStore(new FlushCompleted(pending)))
                    {
                        FlushCompleted.SignalQuietly(pending);
                    }
                }

                pending.Wait(timeout);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Program] Cookie flush before exit failed: {ex.Message}");
            }
        }

        private sealed class FlushCompleted : CefCompletionCallback
        {
            private readonly CountdownEvent _pending;

            public FlushCompleted(CountdownEvent pending) => _pending = pending;

            protected override void OnComplete() => SignalQuietly(_pending);

            public static void SignalQuietly(CountdownEvent pending)
            {
                try
                {
                    pending.Signal();
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private static void Cleanup(string cachePath, bool deleteCache)
        {
            if (!CefRuntimeLoader.SkipShutdownOnExit)
            {
                CefRuntimeLoader.Shutdown();
            }

            if (!deleteCache)
            {
                return;
            }

            const int maxAttempts = 20;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(cachePath);
                    if (dirInfo.Exists)
                    {
                        dirInfo.Delete(true);
                    }

                    return;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    if (attempt == maxAttempts)
                    {
                        Console.WriteLine($"[Program] Failed to delete abandoned CachePath '{cachePath}' after {maxAttempts} attempts: {ex.Message}");
                        return;
                    }

                    Thread.Sleep(500);
                }
            }
        }
    }
}
