using System;
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

        public bool LaunchesWithWebRtc => _childArgs.Any(a => string.Equals(a, "--use-webrtc", StringComparison.OrdinalIgnoreCase));

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
                    return;
                }

                try
                {
                    AttachTo(Process.GetProcessById(pid));
                }
                catch (ArgumentException)
                {
                    return;
                }
            }

            _intentionalStop = true;

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

            StartedUtc = null;
            StatusChanged?.Invoke();
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
            }

            StartedUtc = null;
            StatusChanged?.Invoke();
        }
    }
}
