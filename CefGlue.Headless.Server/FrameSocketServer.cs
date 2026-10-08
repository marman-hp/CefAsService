using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xilium.CefGlue.Headless;

namespace Xilium.CefGlue.Headless.Server
{
    public enum FrameMessageType : byte
    {
        Video = 1,
        Audio = 2,
        AudioOpus = 3,
    }

    public enum FramePixelFormat : byte
    {
        RawBgra = 0,
        Png = 1,
        Webp = 2,
        Jpeg = 3,
        H264 = 4,
    }

    public sealed class FrameSocketServer : IFrameTransport, IDisposable
    {
        private readonly WebApplication _app;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly object _lock = new object();
        private WebSocket _client;
        private bool _disposed;

        public int Port { get; }

        public event Action<string> MessageReceived;

        public event Action Connected;
        public event Action Disconnected;

        public int HttpsPort { get; }

        public FrameSocketServer(int port = 57391, string path = "/frames", string bindAddress = "0.0.0.0", System.Security.Cryptography.X509Certificates.X509Certificate2 httpsCertificate = null, int httpsPort = 0)
        {
            Port = port;

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();

            HttpsPort = httpsCertificate != null ? (httpsPort > 0 ? httpsPort : (port <= 55535 ? port + 10000 : port - 10000)) : 0;

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Parse(bindAddress), port);

                if (httpsCertificate != null)
                {
                    options.Listen(IPAddress.Parse(bindAddress), HttpsPort, listenOptions => listenOptions.UseHttps(httpsCertificate));
                }
            });

            _app = builder.Build();
            _app.UseWebSockets();
            _app.Map(path, HandleConnection);

            _ = _app.RunAsync();
        }

        private async Task HandleConnection(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();

            WebSocket previous;
            lock (_lock)
            {
                previous = _client;
                _client = socket;
            }

            if (previous != null && previous != socket)
            {
                _ = NotifyReplacedAndCloseAsync(previous);
            }

            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            Console.WriteLine($"[FrameSocketServer] Client connected from {remoteIp}");
            BroadcastText(System.Text.Json.JsonSerializer.Serialize(new { type = "clientInfo", remoteIp }));

            Connected?.Invoke();

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

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text && messageStream.Length > 0)
                    {
                        var text = Encoding.UTF8.GetString(messageStream.GetBuffer(), 0, (int)messageStream.Length);

                        try
                        {
                            MessageReceived?.Invoke(text);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                var wasCurrent = false;

                lock (_lock)
                {
                    if (ReferenceEquals(_client, socket))
                    {
                        _client = null;
                        wasCurrent = true;
                    }
                }

                if (wasCurrent)
                {
                    Disconnected?.Invoke();
                }
            }
        }

        public void Broadcast(HeadlessFrame frame)
        {
            if (frame?.Buffer == null)
            {
                return;
            }

            BroadcastVideo(frame.Buffer, frame.Width, frame.Height, FramePixelFormat.RawBgra, isKeyFrame: false, generation: 0);
        }

        public void BroadcastCompressed(byte[] encodedBytes, int width, int height, FramePixelFormat format, bool isKeyFrame = false, int generation = 0)
        {
            if (encodedBytes == null)
            {
                return;
            }

            BroadcastVideo(encodedBytes, width, height, format, isKeyFrame, generation);
        }

        private void BroadcastVideo(byte[] payload, int width, int height, FramePixelFormat format, bool isKeyFrame, int generation)
        {
            var headerSize = format == FramePixelFormat.H264 ? 15 : 11;
            var framed = new byte[headerSize + payload.Length];
            framed[0] = (byte)FrameMessageType.Video;
            framed[1] = (byte)format;
            BitConverter.TryWriteBytes(framed.AsSpan(2, 4), width);
            BitConverter.TryWriteBytes(framed.AsSpan(6, 4), height);
            framed[10] = (byte)(isKeyFrame ? 1 : 0);

            if (format == FramePixelFormat.H264)
            {
                BitConverter.TryWriteBytes(framed.AsSpan(11, 4), generation);
            }

            Buffer.BlockCopy(payload, 0, framed, headerSize, payload.Length);

            var socket = GetClient();
            if (socket == null)
            {
                return;
            }

            if (format == FramePixelFormat.H264)
            {
                _ = SendQueued(socket, framed, WebSocketMessageType.Binary);
                return;
            }

            if (!_sendLock.Wait(0))
            {
                return;
            }

            _ = SendAndRelease(socket, framed, WebSocketMessageType.Binary);
        }

        public void BroadcastAudio(int sampleRate, int channels, float[][] channelData, int frames)
        {
            if (channels <= 0 || frames <= 0 || channelData == null || channelData.Length < channels)
            {
                return;
            }

            const int headerBytes = 13;
            var floatBytes = frames * sizeof(float);
            var framed = new byte[headerBytes + channels * floatBytes];

            framed[0] = (byte)FrameMessageType.Audio;
            BitConverter.TryWriteBytes(framed.AsSpan(1, 4), sampleRate);
            BitConverter.TryWriteBytes(framed.AsSpan(5, 4), channels);
            BitConverter.TryWriteBytes(framed.AsSpan(9, 4), frames);

            for (var c = 0; c < channels; c++)
            {
                System.Buffer.BlockCopy(channelData[c], 0, framed, headerBytes + c * floatBytes, floatBytes);
            }

            var socket = GetClient();
            if (socket == null)
            {
                return;
            }

            _ = SendQueued(socket, framed, WebSocketMessageType.Binary);
        }

        public void BroadcastAudioOpus(int sampleRate, int channels, int frames, byte[] opusPacket)
        {
            if (channels <= 0 || frames <= 0 || opusPacket == null || opusPacket.Length == 0)
            {
                return;
            }

            const int headerBytes = 13;
            var framed = new byte[headerBytes + opusPacket.Length];

            framed[0] = (byte)FrameMessageType.AudioOpus;
            BitConverter.TryWriteBytes(framed.AsSpan(1, 4), sampleRate);
            BitConverter.TryWriteBytes(framed.AsSpan(5, 4), channels);
            BitConverter.TryWriteBytes(framed.AsSpan(9, 4), frames);
            System.Buffer.BlockCopy(opusPacket, 0, framed, headerBytes, opusPacket.Length);

            var socket = GetClient();
            if (socket == null)
            {
                return;
            }

            _ = SendQueued(socket, framed, WebSocketMessageType.Binary);
        }

        public void BroadcastText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var socket = GetClient();
            if (socket == null)
            {
                return;
            }

            _ = SendQueued(socket, Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text);
        }

        private WebSocket GetClient()
        {
            lock (_lock)
            {
                return _client != null && _client.State == WebSocketState.Open ? _client : null;
            }
        }

        private async Task SendAndRelease(WebSocket socket, byte[] data, WebSocketMessageType type)
        {
            try
            {
                await socket.SendAsync(data, type, true, CancellationToken.None);
            }
            catch
            {
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private static readonly byte[] ReplacedMessage = Encoding.UTF8.GetBytes("{\"type\":\"replaced\"}");

        private async Task NotifyReplacedAndCloseAsync(WebSocket previous)
        {
            await SendQueued(previous, ReplacedMessage, WebSocketMessageType.Text);

            try { await previous.CloseAsync(WebSocketCloseStatus.NormalClosure, "replaced by a newer connection", CancellationToken.None); }
            catch { }
        }

        private async Task SendQueued(WebSocket socket, byte[] data, WebSocketMessageType type)
        {
            await _sendLock.WaitAsync();

            try
            {
                await socket.SendAsync(data, type, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FrameSocketServer] SendQueued failed: {ex}");
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            lock (_lock)
            {
                try
                {
                    _client?.Abort();
                }
                catch
                {
                }

                _client = null;
            }

            try
            {
                _ = _app.StopAsync(new CancellationTokenSource(TimeSpan.FromMilliseconds(500)).Token);
            }
            catch
            {
            }
        }
    }
}
