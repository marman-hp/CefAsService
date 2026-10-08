using System;
using System.IO;
using System.Text.Json;

namespace Xilium.CefGlue.Broker
{
    internal static class DurableJsonFile
    {
        private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

        public static T Read<T>(string path, Func<T> fallback, string logTag) where T : class
        {
            if (TryParse<T>(path, out var value, out var error))
            {
                return value ?? fallback();
            }

            if (error != null)
            {
                var aside = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
                try { File.Move(path, aside); } catch { }
                Console.WriteLine($"[{logTag}] {Path.GetFileName(path)} could not be read ({error}) - moved aside as {Path.GetFileName(aside)}, trying the backup.");
            }

            var backup = path + ".bak";
            if (TryParse<T>(backup, out var restored, out var backupError) && restored != null)
            {
                Console.WriteLine($"[{logTag}] Restored {Path.GetFileName(path)} from {Path.GetFileName(backup)}.");
                try { File.Copy(backup, path, overwrite: true); } catch { }
                return restored;
            }

            if (error != null || backupError != null)
            {
                Console.WriteLine($"[{logTag}] No usable backup either - starting from defaults.");
            }

            return fallback();
        }

        public static void Write<T>(string path, T value, string logTag)
        {
            var temp = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, value, WriteOptions);
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(path))
                {
                    File.Replace(temp, path, path + ".bak", ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{logTag}] Could not write {path}: {ex.Message}");
            }
        }

        private static bool TryParse<T>(string path, out T value, out string error) where T : class
        {
            value = null;
            error = null;
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                value = JsonSerializer.Deserialize<T>(File.ReadAllText(path));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
