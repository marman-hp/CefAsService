using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Xilium.CefGlue.Broker
{
    internal static class ControlPlaneServer
    {
        private static readonly string[] KnownEncoderSettingEnvVars =
        {
            "CEFGLUE_X264_PRESET", "CEFGLUE_X264_PROFILE", "CEFGLUE_X264_CRF",
            "CEFGLUE_X264_BITRATE_MBPS", "CEFGLUE_X264_KEYINT_SECONDS", "CEFGLUE_X264_THREADS",
            "CEFGLUE_OPENH264_COMPLEXITY", "CEFGLUE_OPENH264_PROFILE",
            "CEFGLUE_OPENH264_TARGET_BITRATE_MBPS", "CEFGLUE_OPENH264_MAX_BITRATE_MBPS",
            "CEFGLUE_OPENH264_KEYINT_SECONDS", "CEFGLUE_OPENH264_THREADS",
            "CEFGLUE_OPENH264_SLICE_KB", "CEFGLUE_OPENH264_SLICES",
            "CEFGLUE_VP9_CPU_USED", "CEFGLUE_VP9_CQ_LEVEL", "CEFGLUE_VP9_BITRATE_MBPS",
            "CEFGLUE_VP9_KEYINT_SECONDS", "CEFGLUE_VP9_THREADS",
            "CEFGLUE_QSV_TARGET_USAGE", "CEFGLUE_QSV_PROFILE", "CEFGLUE_QSV_QVBR_QUALITY",
            "CEFGLUE_QSV_TARGET_BITRATE_MBPS", "CEFGLUE_QSV_MAX_BITRATE_MBPS", "CEFGLUE_QSV_KEYINT_SECONDS",
        };

        private static readonly object WatchLock = new();
        private static AdminWatch _currentWatch;

        private static readonly TimeSpan RelaunchWindow = TimeSpan.FromSeconds(60);
        private const int MaxRelaunchAttemptsPerWindow = 3;

        private sealed class AdminWatch
        {
            public int Pid;
            public string ExePath;
            public string[] Args;
            public Dictionary<string, string> Env;
            public CancellationTokenSource Cts;
        }

        public static async Task RunAsync(int port)
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            Console.WriteLine($"[Broker] Control channel listening on tcp://127.0.0.1:{port}/ (loopback-only, plain TCP - not HTTP).");

            while (true)
            {
                TcpClient client;

                try
                {
                    client = await listener.AcceptTcpClientAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Broker] Control channel listener failed: {ex.Message}");
                    return;
                }

                _ = HandleConnectionAsync(client);
            }
        }

        private static async Task HandleConnectionAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var noBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                    var reader = new System.IO.StreamReader(stream, noBom);
                    var writer = new System.IO.StreamWriter(stream, noBom) { AutoFlush = true, NewLine = "\n" };

                    string line;

                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        string response;

                        try
                        {
                            response = HandleRequest(line);
                        }
                        catch (Exception ex)
                        {
                            response = JsonSerializer.Serialize(new { ok = false, error = ex.Message });
                        }

                        await writer.WriteLineAsync(response);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static string HandleRequest(string line)
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var op = root.GetProperty("op").GetString();

            switch (op)
            {
                case "hello":
                {
                    var pid = root.GetProperty("pid").GetInt32();
                    var exePath = root.GetProperty("exePath").GetString();
                    var args = root.TryGetProperty("args", out var argsProp)
                        ? argsProp.EnumerateArray().Select(a => a.GetString()).ToArray()
                        : Array.Empty<string>();

                    var env = new Dictionary<string, string>();
                    if (root.TryGetProperty("env", out var envProp))
                    {
                        foreach (var prop in envProp.EnumerateObject())
                        {
                            env[prop.Name] = prop.Value.GetString();
                        }
                    }

                    RegisterAdminWatch(pid, exePath, args, env);
                    return JsonSerializer.Serialize(new { ok = true, brokerPid = Environment.ProcessId });
                }

                case "listWorkers":
                {
                    var manifest = WorkerManifest.GetAllByTenant();
                    var running = Program.Registry.GetAll()
                        .Select(w => new
                        {
                            tenantId = w.TenantId,
                            pageId = w.PageId,
                            address = w.Address,
                            status = w.Status.ToString(),
                            lastHeartbeatUtc = w.LastHeartbeatUtc,
                            cpu = w.Cpu,
                            encoder = w.Encoder,
                            sessions = w.Sessions,
                            createdUtc = manifest.TryGetValue(w.TenantId, out var m) ? m.CreatedUtc : null,
                            clientIp = m?.ClientIp,
                            clientOs = m?.ClientOs,
                        })
                        .ToList();

                    var runningIds = running.Select(w => w.tenantId).ToHashSet();
                    var sleeping = WorkerManifest.GetGone()
                        .Where(e => !runningIds.Contains(e.TenantId))
                        .Select(e => new
                        {
                            tenantId = e.TenantId,
                            pageId = e.PageId,
                            address = (string)null,
                            status = "Sleeping",
                            lastHeartbeatUtc = e.GoneAtUtc.Value,
                            cpu = 0.0,
                            encoder = 0.0,
                            sessions = e.Tabs?.Count ?? 0,
                            createdUtc = e.CreatedUtc,
                            clientIp = e.ClientIp,
                            clientOs = e.ClientOs,
                        });

                    var workers = running.Concat(sleeping).OrderBy(w => w.tenantId);
                    return JsonSerializer.Serialize(new { ok = true, workers });
                }

                case "killWorker":
                {
                    var tenantId = root.GetProperty("tenantId").GetString();

                    if (!Program.SpawnedProcesses.ContainsKey(tenantId) && !Program.Registry.TryGet(tenantId, out _) && !WorkerManifest.Contains(tenantId))
                    {
                        return JsonSerializer.Serialize(new { ok = false, error = $"Unknown tenant '{tenantId}' (already ended, or never seen by this broker)." });
                    }

                    _ = Program.ReleaseTenantAsync(tenantId, "killed from Admin");
                    return JsonSerializer.Serialize(new { ok = true });
                }

                case "getCacheInfo":
                {
                    var (sizeBytes, tenantCount) = MeasureWorkerCache();
                    var (standaloneSizeBytes, _) = MeasureDirectory(GetStandaloneCacheRoot());
                    return JsonSerializer.Serialize(new { ok = true, sizeBytes, tenantCount, standaloneSizeBytes });
                }

                case "clearCache":
                {
                    if (Program.Registry.GetAll().Any())
                    {
                        return JsonSerializer.Serialize(new { ok = false, error = "Refusing to clear cache while worker(s) are still running - stop them first." });
                    }

                    var clearEverything = root.TryGetProperty("clearEverything", out var ceProp) && ceProp.ValueKind == JsonValueKind.True;

                    if (clearEverything && Process.GetProcessesByName("Xilium.CefGlue.Headless.Service").Length > 0)
                    {
                        return JsonSerializer.Serialize(new { ok = false, error = "Refusing to clear everything - a standalone CefGlue.Headless.Service.exe is still running (not broker-managed, so Stop above won't touch it) - close it first." });
                    }

                    var (sizeBytesBefore, _) = MeasureWorkerCache();
                    var (standaloneSizeBytesBefore, _) = clearEverything ? MeasureDirectory(GetStandaloneCacheRoot()) : (0, 0);
                    var cacheRoot = GetWorkerCacheRoot();
                    var deletedOk = true;
                    if (cacheRoot != null && Directory.Exists(cacheRoot))
                    {
                        try
                        {
                            Directory.Delete(cacheRoot, recursive: true);
                        }
                        catch (Exception ex)
                        {
                            deletedOk = false;
                            Console.WriteLine($"[ControlPlaneServer] clearCache: failed to delete '{cacheRoot}': {ex.Message}");
                        }
                    }

                    IsolationModeGuard.WriteMarker();

                    if (clearEverything)
                    {
                        var standaloneCacheRoot = GetStandaloneCacheRoot();
                        if (standaloneCacheRoot != null && Directory.Exists(standaloneCacheRoot))
                        {
                            try
                            {
                                Directory.Delete(standaloneCacheRoot, recursive: true);
                            }
                            catch (Exception ex)
                            {
                                deletedOk = false;
                                Console.WriteLine($"[ControlPlaneServer] clearCache: failed to delete '{standaloneCacheRoot}': {ex.Message}");
                            }
                        }
                    }

                    return deletedOk
                        ? JsonSerializer.Serialize(new { ok = true, freedBytes = sizeBytesBefore + standaloneSizeBytesBefore })
                        : JsonSerializer.Serialize(new { ok = false, error = "Cache folder could not be fully deleted (see Broker's own console) - some files may still be in use." });
                }

                default:
                    return JsonSerializer.Serialize(new { ok = false, error = $"Unknown op '{op}'." });
            }
        }

        internal static Dictionary<string, object> RuntimeSnapshot()
        {
            static string Env(string name) => Environment.GetEnvironmentVariable(name);

            return new Dictionary<string, object>
            {
                [Storage.BrokerDb.RuntimeActive] = new Dictionary<string, object>
                {
                    ["VideoEncoder"] = Env("CEFGLUE_VIDEO_ENCODER"),
                    ["ConnectionMode"] = Program.ActiveConnectionMode,
                    ["AudioBitrateKbps"] = Env("CEFGLUE_OPUS_BITRATE_KBPS"),
                    ["DisableGpu"] = Env("CEFGLUE_DISABLE_GPU"),
                    ["TextureEnable"] = Env("CEFGLUE_TEXTURE_ENABLE"),
                    ["DiskCacheSizeBytes"] = Env("CEFGLUE_DISK_CACHE_SIZE_BYTES"),
                    ["MediaCacheSizeBytes"] = Env("CEFGLUE_MEDIA_CACHE_SIZE_BYTES"),
                    ["DefaultUrl"] = Env("CEFGLUE_DEFAULT_URL"),
                    ["NewPageUseLastUrl"] = Env("CEFGLUE_NEW_PAGE_USE_LAST_URL"),
                    ["WebRtcPacingBps"] = Env("CEFGLUE_WEBRTC_VIDEO_PACING_BPS"),
                    ["WebRtcIceServers"] = Env("CEFGLUE_WEBRTC_ICE_SERVERS"),
                    ["SessionTimeoutSeconds"] = Env("CEFGLUE_ABANDON_GRACE_SECONDS"),
                    ["ManifestTtlSeconds"] = Program.ActiveManifestTtlSeconds,
                    ["EncoderSettings"] = KnownEncoderSettingEnvVars
                        .Where(name => !string.IsNullOrEmpty(Env(name)))
                        .ToDictionary(name => name, Env),
                },
                [Storage.BrokerDb.RuntimeUseWebRtc] = Env("CEFGLUE_USE_WEBRTC"),
                [Storage.BrokerDb.RuntimeIsolationMode] = Program.IsolationMode,
                [Storage.BrokerDb.RuntimeIsolationSwitch] = IsolationModeGuard.LastSwitch is { } sw
                    ? new { from = sw.From, to = sw.To, atUtc = sw.AtUtc, wipeOk = sw.WipeOk }
                    : null,
                [Storage.BrokerDb.RuntimeWorkerExePath] = ProcessSpawner.WorkerExePath,
                [Storage.BrokerDb.RuntimeAvailableEncoders] = Program.AvailableEncoders,
            };
        }

        private static void RegisterAdminWatch(int pid, string exePath, string[] args, Dictionary<string, string> env)
        {
            AdminWatch previous;
            AdminWatch current;

            lock (WatchLock)
            {
                previous = _currentWatch;
                current = new AdminWatch { Pid = pid, ExePath = exePath, Args = args, Env = env, Cts = new CancellationTokenSource() };
                _currentWatch = current;
            }

            previous?.Cts.Cancel();
            Console.WriteLine($"[Broker] Watching Admin pid={pid} ('{exePath}') - will relaunch it if it disappears unexpectedly.");
            _ = WatchAdminAsync(current);
        }

        private static async Task WatchAdminAsync(AdminWatch watch)
        {
            var relaunchTimestamps = new List<DateTime>();

            try
            {
                while (!watch.Cts.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), watch.Cts.Token);

                    if (IsProcessAlive(watch.Pid))
                    {
                        continue;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), watch.Cts.Token);

                    if (IsProcessAlive(watch.Pid))
                    {
                        continue;
                    }

                    if (FindRunningAdmin(watch.ExePath) is { } otherPid)
                    {
                        Console.WriteLine($"[Broker] Admin (pid={watch.Pid}) exited, but Admin pid={otherPid} is already running - watching it instead of relaunching.");
                        watch.Pid = otherPid;
                        continue;
                    }

                    Console.WriteLine($"[Broker] Admin (pid={watch.Pid}) disappeared unexpectedly.");

                    var now = DateTime.UtcNow;
                    relaunchTimestamps.RemoveAll(t => now - t > RelaunchWindow);

                    if (relaunchTimestamps.Count >= MaxRelaunchAttemptsPerWindow)
                    {
                        Console.WriteLine($"[Broker] Admin has crashed {relaunchTimestamps.Count} times in the last {RelaunchWindow.TotalSeconds:0}s - giving up on auto-relaunch. Start it manually.");
                        return;
                    }

                    relaunchTimestamps.Add(now);

                    try
                    {
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = watch.ExePath,
                            UseShellExecute = false,
                            CreateNoWindow = false,
                        };

                        foreach (var arg in watch.Args)
                        {
                            startInfo.ArgumentList.Add(arg);
                        }

                        foreach (var kv in watch.Env)
                        {
                            startInfo.Environment[kv.Key] = kv.Value;
                        }

                        var relaunched = Process.Start(startInfo);
                        Console.WriteLine($"[Broker] Relaunched Admin: pid={relaunched?.Id}.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Broker] Failed to relaunch Admin: {ex.Message}");
                    }

                    return;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static int? FindRunningAdmin(string exePath)
        {
            if (string.IsNullOrEmpty(exePath))
            {
                return null;
            }

            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exePath)))
            {
                using (process)
                {
                    try
                    {
                        if (!process.HasExited && string.Equals(process.MainModule?.FileName, exePath, StringComparison.OrdinalIgnoreCase))
                        {
                            return process.Id;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                return !Process.GetProcessById(pid).HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        internal static string GetWorkerCacheRoot()
        {
            var workerExePath = ProcessSpawner.WorkerExePath;
            if (string.IsNullOrEmpty(workerExePath))
            {
                return null;
            }
            var workerDir = Path.GetDirectoryName(workerExePath);
            return workerDir == null ? null : Xilium.CefGlue.Headless.Service.TenantCachePaths.Root(workerDir);
        }

        private static string GetStandaloneCacheRoot()
        {
            var workerExePath = ProcessSpawner.WorkerExePath;
            if (string.IsNullOrEmpty(workerExePath))
            {
                return null;
            }
            var workerDir = Path.GetDirectoryName(workerExePath);
            return workerDir == null ? null : Path.Combine(workerDir, "cef_temp");
        }

        private static (long SizeBytes, int TenantCount) MeasureWorkerCache() => MeasureDirectory(GetWorkerCacheRoot());

        private static (long SizeBytes, int SubfolderCount) MeasureDirectory(string root)
        {
            if (root == null || !Directory.Exists(root))
            {
                return (0, 0);
            }

            long totalSize = 0;
            var subfolders = Directory.GetDirectories(root);
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    totalSize += new FileInfo(file).Length;
                }
                catch
                {
                }
            }
            return (totalSize, subfolders.Length);
        }
    }
}
