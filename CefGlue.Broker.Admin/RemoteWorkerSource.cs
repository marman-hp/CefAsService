using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Xilium.CefGlue.Broker.Admin
{
    internal sealed class RemoteWorkerSource : IDisposable
    {
        private readonly int _brokerControlPort;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;

        public RemoteWorkerSource(int brokerControlPort)
        {
            _brokerControlPort = brokerControlPort;
        }

        public async Task<int?> HelloAsync(int pid, string exePath, string[] args)
        {
            try
            {
                var env = new Dictionary<string, string>();

                foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                {
                    var key = (string)entry.Key;
                    if (key.StartsWith("CEFGLUE_", StringComparison.OrdinalIgnoreCase))
                    {
                        env[key] = (string)entry.Value;
                    }
                }

                var response = await SendAsync(new { op = "hello", pid, exePath, args, env });
                return response.TryGetProperty("brokerPid", out var brokerPidProp) ? brokerPidProp.GetInt32() : null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<IReadOnlyList<WorkerInfo>> GetWorkersAsync()
        {
            try
            {
                var response = await SendAsync(new { op = "listWorkers" });
                var workers = new List<WorkerInfo>();

                foreach (var w in response.GetProperty("workers").EnumerateArray())
                {
                    workers.Add(new WorkerInfo
                    {
                        TenantId = w.GetProperty("tenantId").GetString(),
                        PageId = w.GetProperty("pageId").GetString(),
                        Address = w.GetProperty("address").GetString(),
                        Status = w.GetProperty("status").GetString(),
                        LastHeartbeatUtc = w.GetProperty("lastHeartbeatUtc").GetDateTime(),
                        Cpu = w.GetProperty("cpu").GetDouble(),
                        Encoder = w.GetProperty("encoder").GetDouble(),
                        Sessions = w.GetProperty("sessions").GetInt32(),
                        CreatedUtc = w.TryGetProperty("createdUtc", out var created) && created.ValueKind == JsonValueKind.String ? created.GetDateTime() : null,
                        ClientIp = w.TryGetProperty("clientIp", out var ip) && ip.ValueKind == JsonValueKind.String ? ip.GetString() : null,
                        ClientOs = w.TryGetProperty("clientOs", out var os) && os.ValueKind == JsonValueKind.String ? os.GetString() : null,
                    });
                }

                return workers;
            }
            catch
            {
                return Array.Empty<WorkerInfo>();
            }
        }

        public async Task<(bool Success, string Error)> KillWorkerAsync(string tenantId)
        {
            try
            {
                var response = await SendAsync(new { op = "killWorker", tenantId });
                return response.GetProperty("ok").GetBoolean()
                    ? (true, (string)null)
                    : (false, response.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Unknown error.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(long SizeBytes, int TenantCount, long StandaloneSizeBytes)> GetCacheInfoAsync()
        {
            try
            {
                var response = await SendAsync(new { op = "getCacheInfo" });
                if (!response.GetProperty("ok").GetBoolean())
                {
                    return (0, 0, 0);
                }
                var sizeBytes = response.GetProperty("sizeBytes").GetInt64();
                var tenantCount = response.GetProperty("tenantCount").GetInt32();
                var standaloneSizeBytes = response.TryGetProperty("standaloneSizeBytes", out var s) ? s.GetInt64() : 0;
                return (sizeBytes, tenantCount, standaloneSizeBytes);
            }
            catch
            {
                return (0, 0, 0);
            }
        }

        public async Task<(bool Success, string Error, long FreedBytes)> ClearCacheAsync(bool clearEverything = false)
        {
            try
            {
                var response = await SendAsync(new { op = "clearCache", clearEverything });
                if (response.GetProperty("ok").GetBoolean())
                {
                    var freedBytes = response.TryGetProperty("freedBytes", out var f) ? f.GetInt64() : 0;
                    return (true, null, freedBytes);
                }
                var error = response.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Unknown error.";
                return (false, error, 0);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, 0);
            }
        }

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

        private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(1.5);
        private DateTime _connectRetryAfterUtc;

        private async Task<JsonElement> SendAsync(object request)
        {
            await _gate.WaitAsync();

            try
            {
                await EnsureConnectedAsync().WaitAsync(RequestTimeout);

                await _writer.WriteLineAsync(JsonSerializer.Serialize(request)).WaitAsync(RequestTimeout);
                var line = await _reader.ReadLineAsync().WaitAsync(RequestTimeout)
                           ?? throw new IOException("Broker control connection closed.");

                using var doc = JsonDocument.Parse(line);
                return doc.RootElement.Clone();
            }
            catch
            {
                DisposeConnection();
                throw;
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task EnsureConnectedAsync()
        {
            if (_client is { Connected: true })
            {
                return;
            }

            DisposeConnection();

            if (DateTime.UtcNow < _connectRetryAfterUtc)
            {
                throw new IOException("Broker control channel not reachable (recent connect failure).");
            }

            _client = new TcpClient();
            try
            {
                await _client.ConnectAsync(System.Net.IPAddress.Loopback, _brokerControlPort);
            }
            catch
            {
                _connectRetryAfterUtc = DateTime.UtcNow + ConnectRetryDelay;
                throw;
            }

            var stream = _client.GetStream();
            var noBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            _reader = new StreamReader(stream, noBom);
            _writer = new StreamWriter(stream, noBom) { AutoFlush = true, NewLine = "\n" };
        }

        private void DisposeConnection()
        {
            try { _reader?.Dispose(); } catch { }
            try { _writer?.Dispose(); } catch { }
            try { _client?.Dispose(); } catch { }

            _reader = null;
            _writer = null;
            _client = null;
        }

        public void Dispose()
        {
            _gate.Dispose();
            DisposeConnection();
        }
    }

    internal sealed record IsolationModeSwitchInfo(string From, string To, DateTime AtUtc, bool WipeOk);
}
