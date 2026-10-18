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

        private static readonly object Gate = new();

        public static BrokerSettingsData Load()
        {
            lock (Gate)
            {
                return Storage.BrokerStore.LoadSettings();
            }
        }

        public static void Save(BrokerSettingsData data)
        {
            lock (Gate)
            {
                Storage.BrokerStore.SaveSettings(data);
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
