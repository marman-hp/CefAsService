using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Xilium.CefGlue.Headless.Service
{
    internal static class WorkerControlPlaneClient
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

        public static async Task<JsonElement?> TryGetSettingsAsync(int adminPort)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, adminPort).WaitAsync(RequestTimeout);

                using var stream = client.GetStream();
                var noBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                using var reader = new StreamReader(stream, noBom);
                using var writer = new StreamWriter(stream, noBom) { AutoFlush = true, NewLine = "\n" };

                await writer.WriteLineAsync("{\"op\":\"getSettings\"}").WaitAsync(RequestTimeout);
                var line = await reader.ReadLineAsync().WaitAsync(RequestTimeout);

                if (string.IsNullOrEmpty(line))
                {
                    return null;
                }

                using var doc = JsonDocument.Parse(line);
                return doc.RootElement.Clone();
            }
            catch
            {
                return null;
            }
        }
    }
}
