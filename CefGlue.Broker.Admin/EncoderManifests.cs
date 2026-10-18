using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Xilium.CefGlue.Broker.Admin
{
    public sealed class EncoderManifest
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }

        public string Library { get; set; }

        public string SettingsPrefix { get; set; }

        public List<EncoderSettingDefinition> Settings { get; set; } = new();
        public List<EncoderFixedValue> Fixed { get; set; } = new();

        public EncoderDownload Download { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string Folder { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string DownloadPath => Download == null || string.IsNullOrEmpty(Folder) ? null : Path.Combine(Folder, Download.File);

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsInstalled => Download == null || File.Exists(DownloadPath);

        public string EnvVarName(EncoderSettingDefinition setting) => $"CEFGLUE_{SettingsPrefix}_{setting.Key}";

        public bool HasTunableSettings => !string.IsNullOrEmpty(SettingsPrefix) && Settings.Count > 0;
    }

    public sealed class EncoderSettingDefinition
    {
        public string Key { get; set; }
        public string Label { get; set; }

        public string Type { get; set; }
        public List<string> Options { get; set; } = new();
        public string Min { get; set; }
        public string Max { get; set; }
        public string Step { get; set; }
        public string Unit { get; set; }
        public string Placeholder { get; set; }
        public string Default { get; set; } = "";
        public string Title { get; set; }
        public string Note { get; set; }
    }

    public sealed class EncoderDownload
    {
        public string Url { get; set; }
        public string File { get; set; }
        public string Sha256 { get; set; }
        public string Provider { get; set; }
        public string Attribution { get; set; }
        public string LicenseUrl { get; set; }
    }

    public sealed class EncoderFixedValue
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Note { get; set; }
        public bool Check { get; set; }
    }

    internal static class EncoderManifestStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public static List<EncoderManifest> Load(string workerExePath)
        {
            var result = new List<EncoderManifest>();
            if (string.IsNullOrEmpty(workerExePath))
            {
                return result;
            }

            var root = Path.Combine(Path.GetDirectoryName(workerExePath) ?? "", "plugins", "encoders");
            if (!Directory.Exists(root))
            {
                return result;
            }

            foreach (var manifestPath in Directory.GetFiles(root, "plugin.json", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var file = JsonSerializer.Deserialize<EncoderPluginFile>(File.ReadAllText(manifestPath), JsonOptions);
                    foreach (var encoder in file?.Encoders ?? new List<EncoderManifest>())
                    {
                        if (!string.IsNullOrEmpty(encoder.Id) && result.All(e => !string.Equals(e.Id, encoder.Id, StringComparison.OrdinalIgnoreCase)))
                        {
                            encoder.DisplayName ??= encoder.Id;
                            encoder.Folder = Path.GetDirectoryName(manifestPath);
                            result.Add(encoder);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Admin] Skipping unreadable encoder manifest {manifestPath}: {ex.Message}");
                }
            }

            return result;
        }
    }

    public sealed class EncoderPluginFile
    {
        public List<EncoderManifest> Encoders { get; set; }
    }
}
