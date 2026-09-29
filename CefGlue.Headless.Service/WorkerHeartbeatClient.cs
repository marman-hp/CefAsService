using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xilium.CefGlue.Common.Helpers;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed class WorkerHeartbeatClient : IDisposable
    {
        private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

        private readonly string _brokerUrl;
        private readonly string _tenantId;
        private readonly string _pageId;
        private readonly string _address;
        private readonly Func<int> _getSessionCount;
        private readonly Func<(List<(string Id, string Url, string ContextId)> Tabs, string SelectedId)> _getAllTabUrls;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private ClientWebSocket _socket;
        private Task _runLoop;

        public WorkerHeartbeatClient(string brokerUrl, string tenantId, string pageId, string address, Func<int> getSessionCount,
            Func<(List<(string Id, string Url, string ContextId)> Tabs, string SelectedId)> getAllTabUrls)
        {
            _brokerUrl = brokerUrl;
            _tenantId = tenantId;
            _pageId = pageId;
            _address = address;
            _getSessionCount = getSessionCount;
            _getAllTabUrls = getAllTabUrls;
        }

        public void Start()
        {
            OverlayDiagnostics.EnsureBackgroundSampling();
            _runLoop = Task.Run(RunLoopAsync);
        }

        private async Task RunLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    using var socket = new ClientWebSocket();
                    _socket = socket;

                    await socket.ConnectAsync(new Uri(_brokerUrl), _cts.Token);

                    await SendAsync(socket, JsonSerializer.Serialize(new
                    {
                        type = "register",
                        tenantId = _tenantId,
                        pageId = _pageId,
                        address = _address,
                    }));

                    Console.WriteLine($"[WorkerHeartbeatClient] Registered with broker at {_brokerUrl} as tenant '{_tenantId}'.");

                    while (!_cts.IsCancellationRequested && socket.State == WebSocketState.Open)
                    {
                        var (tabs, selectedTabId) = _getAllTabUrls();

                        await SendAsync(socket, JsonSerializer.Serialize(new
                        {
                            type = "heartbeat",
                            cpu = OverlayDiagnostics.CpuUsagePercent,
                            encoder = OverlayDiagnostics.GpuUsagePercent,
                            sessions = _getSessionCount(),
                            tabs = tabs.Select(t => new { id = t.Id, url = t.Url, contextId = t.ContextId }),
                            selectedTabId = selectedTabId,
                        }));

                        await Task.Delay(HeartbeatInterval, _cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WorkerHeartbeatClient] Broker connection failed, retrying in 5s: {ex.Message}");
                }

                try
                {
                    await Task.Delay(HeartbeatInterval, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private static async Task SendAsync(ClientWebSocket socket, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public void Dispose()
        {
            _cts.Cancel();

            try
            {
                _socket?.Abort();
            }
            catch
            {
            }

            _cts.Dispose();
        }
    }
}
