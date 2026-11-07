using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Xilium.CefGlue.Broker.Admin
{
    internal sealed class BrokerSupervisor
    {
        private readonly ExePaths _paths;
        private readonly string[] _childArgs;
        private readonly RemoteWorkerSource _controlChannel;

        private Process _process;
        private bool _intentionalStop;

        public BrokerSupervisor(ExePaths paths, string[] childArgs, RemoteWorkerSource controlChannel)
        {
            _paths = paths;
            _childArgs = childArgs;
            _controlChannel = controlChannel;
        }

        public bool IsRunning => _process is { HasExited: false };

        public bool LaunchesWithWebRtc => _childArgs.Any(a => string.Equals(a, "--use-webrtc", StringComparison.OrdinalIgnoreCase))
            || string.Equals(Environment.GetEnvironmentVariable("CEFGLUE_USE_WEBRTC"), "1", StringComparison.OrdinalIgnoreCase);

        public int Pid => IsRunning ? _process.Id : 0;

        public DateTime? StartedUtc { get; private set; }

        public string RunningExePath { get; private set; }

        public string LastStartError { get; private set; }

        public bool Crashed { get; private set; }

        public int LastExitCode { get; private set; }

        public event Action StatusChanged;

        public async Task StartAsync()
        {
            if (IsRunning)
            {
                return;
            }

            var brokerPid = await _controlChannel.HelloAsync(
                Environment.ProcessId,
                Environment.ProcessPath,
                Environment.GetCommandLineArgs()[1..]);

            if (brokerPid is { } pid)
            {
                try
                {
                    AttachTo(Process.GetProcessById(pid));
                    Console.WriteLine($"[Admin] Broker already running (pid={pid}) - attached instead of spawning a new one.");
                    return;
                }
                catch (ArgumentException)
                {
                }
            }

            if (_paths.BrokerStartBlocker is { } blocker)
            {
                LastStartError = blocker;
                Console.WriteLine($"[Admin] Can't start broker - {blocker}");
                StatusChanged?.Invoke();
                return;
            }

            var brokerExePath = _paths.BrokerExePath;
            var startInfo = new ProcessStartInfo
            {
                FileName = brokerExePath,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            foreach (var arg in _childArgs)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var workerExePath = _paths.WorkerExePath;
            if (!string.IsNullOrWhiteSpace(workerExePath))
            {
                startInfo.Environment["CEFGLUE_WORKER_EXE_PATH"] = workerExePath;
            }

            LastStartError = null;
            RunningExePath = brokerExePath;

            _intentionalStop = false;
            Crashed = false;

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.Exited += OnChildExited;
            process.Start();
            _process = process;

            StartedUtc = DateTime.UtcNow;
            StatusChanged?.Invoke();

            _ = AnnounceAfterDelayAsync();
        }

        private async Task AnnounceAfterDelayAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await _controlChannel.HelloAsync(Environment.ProcessId, Environment.ProcessPath, Environment.GetCommandLineArgs()[1..]);
        }

        private void AttachTo(Process process)
        {
            _intentionalStop = false;
            Crashed = false;

            process.EnableRaisingEvents = true;
            process.Exited += OnChildExited;
            _process = process;
            StartedUtc = null;
            try
            {
                RunningExePath = process.MainModule?.FileName;
            }
            catch
            {
                RunningExePath = null;
            }
            StatusChanged?.Invoke();
        }

        public async Task StopAsync()
        {
            if (!IsRunning)
            {
                var brokerPid = await _controlChannel.HelloAsync(
                    Environment.ProcessId,
                    Environment.ProcessPath,
                    Environment.GetCommandLineArgs()[1..]);

                if (brokerPid is not { } pid)
                {
                    await WaitForWorkerTreeExitAsync(KillManifestWorkers());
                    return;
                }

                try
                {
                    AttachTo(Process.GetProcessById(pid));
                }
                catch (ArgumentException)
                {
                    await WaitForWorkerTreeExitAsync(KillManifestWorkers());
                    return;
                }
            }

            _intentionalStop = true;

            var workerTree = SnapshotWorkerProcesses();

            try
            {
                _process.Kill(entireProcessTree: true);

                try
                {
                    await _process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("[Admin] StopAsync: process did not exit within 10s of Kill() - proceeding anyway.");
                }
            }
            catch
            {
            }

            await WaitForWorkerTreeExitAsync(workerTree.Concat(KillManifestWorkers()).ToArray());

            StartedUtc = null;
            StatusChanged?.Invoke();
        }

        private static Process[] SnapshotWorkerProcesses()
        {
            return WorkerProcessNames.SelectMany(Process.GetProcessesByName).ToArray();
        }

        private static readonly string[] WorkerProcessNames = { "Xilium.CefGlue.Headless.Service", "Xilium.CefGlue.BrowserProcess" };

        private static Process[] KillManifestWorkers()
        {
            List<long> pids;
            try
            {
                pids = AdminDb.Db.Query("SELECT pid FROM tenants WHERE pid > 0", r => r.GetInt64(0));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Admin] StopAsync: could not read worker pids from broker.db ({ex.Message}) - adopted workers not stopped.");
                return Array.Empty<Process>();
            }

            var killed = new List<Process>();
            foreach (var pid in pids)
            {
                Process process;
                try
                {
                    process = Process.GetProcessById((int)pid);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                try
                {
                    if (!process.HasExited && string.Equals(process.ProcessName, WorkerProcessNames[0], StringComparison.OrdinalIgnoreCase))
                    {
                        process.Kill(entireProcessTree: true);
                        Console.WriteLine($"[Admin] StopAsync: killed worker pid={pid} recorded in broker.db.");
                        killed.Add(process);
                        continue;
                    }
                }
                catch (Exception)
                {
                }

                process.Dispose();
            }

            return killed.ToArray();
        }

        private static async Task WaitForWorkerTreeExitAsync(Process[] processes)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        await process.WaitForExitAsync(timeout.Token);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }
            catch (OperationCanceledException)
            {
                var alive = processes.Count(p => { try { return !p.HasExited; } catch { return false; } });
                Console.WriteLine($"[Admin] StopAsync: {alive} worker process(es) still alive 15s after Kill() - starting anyway.");
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        public async Task RestartAsync()
        {
            await StopAsync();
            await StartAsync();
        }

        private void OnChildExited(object sender, EventArgs e)
        {
            if (!_intentionalStop)
            {
                Crashed = true;

                try
                {
                    LastExitCode = _process.ExitCode;
                }
                catch
                {
                    LastExitCode = -1;
                }

                if (LastExitCode != 0 && LastExitCode != -1)
                {
                    Program.LogError($"Broker crashed - pid {_process.Id}, exit code 0x{LastExitCode:X8}{(unchecked((uint)LastExitCode) == 0xE0434352 ? " (unhandled .NET exception - see broker-error.log)" : "")}.");
                }
            }

            StartedUtc = null;
            StatusChanged?.Invoke();
        }
    }
}
