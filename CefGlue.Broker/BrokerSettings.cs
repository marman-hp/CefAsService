using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Xilium.CefGlue.Broker
{
    internal sealed class BrokerSettingsData
    {
        public string VideoEncoder { get; set; }

        public string DisableGpu { get; set; }

        public string TextureEnable { get; set; }

        public string DiskCacheSizeBytes { get; set; }

        public string MediaCacheSizeBytes { get; set; }

        public string DefaultUrl { get; set; }

        public string NewPageUseLastUrl { get; set; }

        public string AudioBitrateKbps { get; set; }

        public string WebRtcPacingBps { get; set; }

        public string WebRtcIceServers { get; set; }

        public string ConnectionMode { get; set; }

        public string SessionTimeoutSeconds { get; set; }

        public string ManifestTtlSeconds { get; set; }

        public Dictionary<string, string> EncoderSettings { get; set; }
    }

    internal static class BrokerSettings
    {
        private static readonly string Path =
            System.IO.Path.Combine(AppContext.BaseDirectory, "broker-settings.json");

        private static readonly object Gate = new();

        public static BrokerSettingsData Load()
        {
            lock (Gate)
            {
                try
                {
                    if (!File.Exists(Path))
                    {
                        return new BrokerSettingsData();
                    }

                    return JsonSerializer.Deserialize<BrokerSettingsData>(File.ReadAllText(Path))
                           ?? new BrokerSettingsData();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BrokerSettings] Could not read {Path} ({ex.Message}) - using defaults.");
                    return new BrokerSettingsData();
                }
            }
        }

        public static void Save(BrokerSettingsData data)
        {
            lock (Gate)
            {
                try
                {
                    File.WriteAllText(Path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BrokerSettings] Could not write {Path}: {ex.Message}");
                }
            }
        }

        public static void Update(Action<BrokerSettingsData> mutate)
        {
            lock (Gate)
            {
                var data = Load();
                mutate(data);
                Save(data);
            }
        }
    }
}
