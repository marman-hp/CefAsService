using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Xilium.CefGlue.Broker
{
    internal static class ProcessSpawner
    {
        internal static readonly string WorkerExePath = Environment.GetEnvironmentVariable("CEFGLUE_WORKER_EXE_PATH") is { Length: > 0 } fromEnv
            ? fromEnv
            : PackagedWorkerExePath();

        private static string PackagedWorkerExePath()
        {
            var packaged = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "Worker", "Xilium.CefGlue.Headless.Service.exe"));
            return System.IO.File.Exists(packaged) ? packaged : null;
        }

        internal static Process SpawnWorker(string tenantId, string pageId, int port, string brokerWorkerUrl, string httpsCertPath,
            List<TabManifestEntry> initialTabs = null, string initialSelectedTabId = null)
        {
            if (string.IsNullOrEmpty(WorkerExePath))
            {
                throw new InvalidOperationException(
                    "CEFGLUE_WORKER_EXE_PATH is not set - point it at CefGlue.Headless.Service's " +
                    "built .exe (e.g. ...\\CefGlue.Headless.Service\\bin\\x64\\Debug\\net10.0\\Xilium.CefGlue.Headless.Service.exe).");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = WorkerExePath,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            startInfo.Environment["CEFGLUE_TENANT_ID"] = tenantId;
            startInfo.Environment["CEFGLUE_PAGE_ID"] = pageId;
            startInfo.Environment["CEFGLUE_PORT"] = port.ToString();
            startInfo.Environment["CEFGLUE_BROKER_URL"] = brokerWorkerUrl;

            if (!string.IsNullOrEmpty(Program.ActiveManifestTtlSeconds))
            {
                startInfo.Environment["CEFGLUE_CONTEXT_TTL_SECONDS"] = Program.ActiveManifestTtlSeconds;
            }

            if (initialTabs is { Count: > 0 })
            {
                startInfo.Environment["CEFGLUE_INITIAL_TABS"] =
                    JsonSerializer.Serialize(initialTabs.Select(t => new { id = t.Id, url = t.Url, contextId = t.ContextId }));
                if (!string.IsNullOrEmpty(initialSelectedTabId))
                {
                    startInfo.Environment["CEFGLUE_INITIAL_SELECTED_TAB_ID"] = initialSelectedTabId;
                }
            }

            if (!string.IsNullOrEmpty(httpsCertPath))
            {
                startInfo.Environment["CEFGLUE_HTTPS_CERT_PATH"] = httpsCertPath;
            }

            return Process.Start(startInfo);
        }

        internal static int FindFreePort()
        {
            var rangeStart = Environment.GetEnvironmentVariable("CEFGLUE_WORKER_PORT_RANGE_START");
            var rangeEnd = Environment.GetEnvironmentVariable("CEFGLUE_WORKER_PORT_RANGE_END");

            if (int.TryParse(rangeStart, out var start) && int.TryParse(rangeEnd, out var end) && start <= end)
            {
                for (var port = start; port <= end; port++)
                {
                    try
                    {
                        using var listener = new TcpListener(IPAddress.Any, port);
                        listener.Start();
                        listener.Stop();
                        return port;
                    }
                    catch (SocketException)
                    {
                    }
                }

                throw new InvalidOperationException(
                    $"No free port available in the configured range {start}-{end} " +
                    "(CEFGLUE_WORKER_PORT_RANGE_START/_END) - every port in it is already in use.");
            }

            using var freeListener = new TcpListener(IPAddress.Loopback, 0);
            freeListener.Start();
            var freePort = ((IPEndPoint)freeListener.LocalEndpoint).Port;
            freeListener.Stop();
            return freePort;
        }
    }
}
