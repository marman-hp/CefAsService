using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Xilium.CefGlue.Broker.Admin
{
    internal static class EncoderDownloader
    {
        private const long MaxDownloadBytes = 64L * 1024 * 1024;

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };

        public static async Task<(bool Ok, string Message)> InstallAsync(EncoderManifest manifest)
        {
            var download = manifest.Download;
            if (download == null || string.IsNullOrEmpty(manifest.DownloadPath))
            {
                return (false, "This encoder has nothing to download.");
            }

            var temp = manifest.DownloadPath + ".download";
            try
            {
                using var response = await Http.GetAsync(download.Url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"Download failed: {(int)response.StatusCode} {response.ReasonPhrase} from {download.Url}");
                }

                if (response.Content.Headers.ContentLength > MaxDownloadBytes)
                {
                    return (false, "Download failed: the file is unexpectedly large.");
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (download.Url.EndsWith(".bz2", StringComparison.OrdinalIgnoreCase))
                {
                    bytes = Bzip2Decoder.Decompress(bytes);
                }

                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                if (!string.Equals(hash, download.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Downloaded file failed verification (SHA-256 {hash.ToLowerInvariant()}, expected {download.Sha256}) - not installed.");
                }

                await File.WriteAllBytesAsync(temp, bytes);
                File.Move(temp, manifest.DownloadPath, overwrite: true);
                return (true, $"{download.File} downloaded from {download.Provider} and verified - used from the next broker Start/Restart.");
            }
            catch (UnauthorizedAccessException)
            {
                return (false, $"No write access to {manifest.Folder} - run Admin from a folder your account can write to.");
            }
            catch (IOException ex) when (File.Exists(manifest.DownloadPath))
            {
                return (false, $"{download.File} is in use by running workers - stop the broker first. ({ex.Message})");
            }
            catch (Exception ex)
            {
                return (false, $"Download failed: {ex.Message}");
            }
            finally
            {
                try { File.Delete(temp); } catch { }
            }
        }

        public static bool IsInstalledFileVerified(EncoderManifest manifest)
        {
            var path = manifest.DownloadPath;
            if (manifest.Download == null || string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                var info = new FileInfo(path);
                var key = (info.Length, info.LastWriteTimeUtc);
                lock (VerifiedCache)
                {
                    if (VerifiedCache.TryGetValue(path, out var cached) && cached.Key == key)
                    {
                        return cached.Verified;
                    }
                }

                using var stream = File.OpenRead(path);
                var verified = string.Equals(Convert.ToHexString(SHA256.HashData(stream)), manifest.Download.Sha256, StringComparison.OrdinalIgnoreCase);
                lock (VerifiedCache)
                {
                    VerifiedCache[path] = (key, verified);
                }

                return verified;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, ((long, DateTime) Key, bool Verified)> VerifiedCache = new(StringComparer.OrdinalIgnoreCase);

        public static (bool Ok, string Message) Remove(EncoderManifest manifest)
        {
            if (string.IsNullOrEmpty(manifest.DownloadPath) || !File.Exists(manifest.DownloadPath))
            {
                return (true, "Already removed.");
            }

            try
            {
                File.Delete(manifest.DownloadPath);
                return (true, $"{manifest.Download.File} removed - applies from the next broker Start/Restart.");
            }
            catch (UnauthorizedAccessException)
            {
                return (false, $"{manifest.Download.File} is in use by running workers (or not writable) - stop the broker, then Remove again.");
            }
            catch (IOException)
            {
                return (false, $"{manifest.Download.File} is in use by running workers - stop the broker, then Remove again.");
            }
        }
    }
}
