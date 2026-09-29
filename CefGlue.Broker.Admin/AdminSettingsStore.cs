using System;
using System.IO;
using System.Text.Json;

namespace Xilium.CefGlue.Broker.Admin
{
    internal sealed class AdminSettingsData
    {
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        public int PasswordIterations { get; set; }

        public string BrokerExePath { get; set; }

        public string WorkerExePath { get; set; }
    }

    internal sealed class AdminSettingsStore
    {
        private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "admin-settings.json");
        private readonly object _gate = new();
        private AdminSettingsData _data;

        public AdminSettingsStore()
        {
            _data = Load();
        }

        public AdminSettingsData Snapshot()
        {
            lock (_gate)
            {
                return JsonSerializer.Deserialize<AdminSettingsData>(JsonSerializer.Serialize(_data));
            }
        }

        public void Update(Action<AdminSettingsData> change)
        {
            lock (_gate)
            {
                change(_data);
                try
                {
                    File.WriteAllText(FilePath, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Admin] Could not write {FilePath}: {ex.Message}");
                    throw;
                }
            }
        }

        private static AdminSettingsData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    return JsonSerializer.Deserialize<AdminSettingsData>(File.ReadAllText(FilePath)) ?? new AdminSettingsData();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"{FilePath} exists but could not be read ({ex.Message}) - fix or delete it.", ex);
            }

            return new AdminSettingsData();
        }
    }
}
