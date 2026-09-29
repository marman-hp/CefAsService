using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Xilium.CefGlue.Headless
{
    public enum VideoCodecFamily
    {
        H264,
        H265,
        Vp9,
        Av1,
    }

    public interface IVideoEncoderPlugin
    {
        string Id { get; }

        string HudName { get; }

        VideoCodecFamily Codec { get; }

        string SettingsPrefix { get; }

        bool IsAvailable(out string reason);

        string DescribeSettings(IReadOnlyDictionary<string, string> settings);

        IVideoFrameEncoder Create(EncoderCreateArgs args);

        string H264ProfileLevelId(IReadOnlyDictionary<string, string> settings) => null;
    }

    public sealed class EncoderCreateArgs
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public VideoQuality Quality { get; init; }

        public int EncodeWidth { get; init; }
        public int EncodeHeight { get; init; }

        public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>();
    }

    public static class EncoderSettings
    {
        public static string H264ProfileLevelId(string profile) => profile?.ToLowerInvariant() switch
        {
            "baseline" => "42e028",
            "main" => "4d0028",
            "high" => "640028",
            _ => null,
        };

        public static string GetString(IReadOnlyDictionary<string, string> settings, string key, string defaultValue)
            => settings.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : defaultValue;

        public static long GetMbpsAsBps(IReadOnlyDictionary<string, string> settings, string key, long defaultBps)
            => settings.TryGetValue(key, out var value) && long.TryParse(value, out var mbps) && mbps > 0
                ? mbps * 1_000_000L
                : defaultBps;

        public static double GetPositiveDouble(IReadOnlyDictionary<string, string> settings, string key, double defaultValue)
            => settings.TryGetValue(key, out var value) && double.TryParse(value, out var parsed) && parsed > 0
                ? parsed
                : defaultValue;

        public static int GetIntInRange(IReadOnlyDictionary<string, string> settings, string key, int min, int max, int defaultValue)
            => settings.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) && parsed >= min && parsed <= max
                ? parsed
                : defaultValue;

        public static int? GetPositiveIntOrNull(IReadOnlyDictionary<string, string> settings, string key)
            => settings.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) && parsed > 0
                ? parsed
                : null;

        public static IReadOnlyDictionary<string, string> FromEnvironment(string prefix)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(prefix))
            {
                return result;
            }

            var fullPrefix = "CEFGLUE_" + prefix.ToUpperInvariant() + "_";
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                var name = (string)entry.Key;
                if (name.StartsWith(fullPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    result[name.Substring(fullPrefix.Length)] = (string)entry.Value;
                }
            }

            return result;
        }

        public static string FormatMbps(long bps) => (bps / 1_000_000).ToString(CultureInfo.InvariantCulture);
    }

    public static class EncoderPluginNative
    {
        public static bool TryLoad(Type pluginType, string libraryName, out string reason)
        {
            var pluginDir = Path.GetDirectoryName(pluginType.Assembly.Location);
            if (!string.IsNullOrEmpty(pluginDir))
            {
                var localPath = Path.Combine(pluginDir, libraryName + ".dll");
                if (File.Exists(localPath) && NativeLibrary.TryLoad(localPath, out var localHandle))
                {
                    NativeLibrary.Free(localHandle);
                    reason = null;
                    return true;
                }
            }

            if (NativeLibrary.TryLoad(libraryName, out var handle))
            {
                NativeLibrary.Free(handle);
                reason = null;
                return true;
            }

            reason = $"{libraryName}.dll could not be loaded";
            return false;
        }
    }
}
