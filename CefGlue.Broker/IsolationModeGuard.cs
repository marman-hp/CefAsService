using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Xilium.CefGlue.Broker
{
    internal static class IsolationModeGuard
    {
        public const string Tenant = "tenant";
        public const string Session = "session";
        private const string MarkerFileName = ".isolation-mode";
        private const string WorkerProcessName = "Xilium.CefGlue.Headless.Service";

        public static (string From, string To, DateTime AtUtc, bool WipeOk)? LastSwitch { get; private set; }

        public static string Epoch { get; private set; }

        public static void EnforceOnStartup(string mode)
        {
            var cacheRoot = ControlPlaneServer.GetWorkerCacheRoot();
            if (cacheRoot == null)
            {
                Console.WriteLine("[Broker] Isolation mode check skipped - CEFGLUE_WORKER_EXE_PATH is unset, so the worker cache location is unknown.");
                return;
            }

            var (previous, previousEpoch) = ReadMarker(cacheRoot);
            if (previous == null || previous == mode)
            {
                Epoch = previousEpoch ?? NewEpoch();
                WriteMarker(mode);
                return;
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Broker] Isolation mode changed {previous} -> {mode}: deleting the worker cache for ALL tenants (logins, cookies, site data) so the two layouts never mix.");
            Console.ResetColor();

            KillManifestWorkers();
            WorkerManifest.ClearAllTabs();
            Epoch = NewEpoch();

            var wipeOk = DeleteWithRetries(cacheRoot);
            if (wipeOk)
            {
                WriteMarker(mode);
                Console.WriteLine($"[Broker] Worker cache deleted - now running per {mode}.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Broker] Could not fully delete '{cacheRoot}' - some files are still in use. Stop every worker and restart the broker to retry the wipe.");
                Console.ResetColor();
            }

            LastSwitch = (previous, mode, DateTime.UtcNow, wipeOk);
        }

        public static void WriteMarker(string mode = null)
        {
            var cacheRoot = ControlPlaneServer.GetWorkerCacheRoot();
            if (cacheRoot == null)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(cacheRoot);
                File.WriteAllText(Path.Combine(cacheRoot, MarkerFileName), $"{mode ?? Program.IsolationMode}|{Epoch ?? NewEpoch()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Broker] Could not write isolation mode marker: {ex.Message}");
            }
        }

        private static (string Mode, string Epoch) ReadMarker(string cacheRoot)
        {
            var markerPath = Path.Combine(cacheRoot, MarkerFileName);
            if (File.Exists(markerPath))
            {
                var parts = File.ReadAllText(markerPath).Trim().Split('|', 2);
                var epoch = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
                return (parts[0] == Session ? Session : Tenant, epoch);
            }

            return (Directory.Exists(cacheRoot) && Directory.EnumerateDirectories(cacheRoot).Any() ? Tenant : null, null);
        }

        private static string NewEpoch() => DateTime.UtcNow.ToString("o");

        private static void KillManifestWorkers()
        {
            foreach (var pid in WorkerManifest.GetLivePids())
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    if (!string.Equals(process.ProcessName, WorkerProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Console.WriteLine($"[Broker] Stopping worker pid={pid} before the isolation mode wipe.");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(10_000);
                }
                catch (ArgumentException)
                {
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Broker] Could not stop worker pid={pid}: {ex.Message}");
                }
            }
        }

        private static bool DeleteWithRetries(string cacheRoot)
        {
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                try
                {
                    if (Directory.Exists(cacheRoot))
                    {
                        Directory.Delete(cacheRoot, recursive: true);
                    }

                    return true;
                }
                catch (Exception ex) when (attempt < 5)
                {
                    Console.WriteLine($"[Broker] Cache wipe attempt {attempt} failed ({ex.Message}) - retrying.");
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Broker] Cache wipe failed: {ex.Message}");
                }
            }

            return false;
        }
    }
}
