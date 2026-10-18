using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Xilium.CefGlue.Headless.Service
{
    internal static class TenantCachePaths
    {
        public const int ChromiumProfileTailLength = 131;

        public static string Root(string workerExeDirectory)
        {
            var configured = Environment.GetEnvironmentVariable("CEFGLUE_CACHE_ROOT");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(configured.Trim());
            }

            if (OperatingSystem.IsWindows())
            {
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (!string.IsNullOrEmpty(programData))
                {
                    return Path.Combine(programData, "CefGlue", "cache");
                }
            }

            return Path.Combine(workerExeDirectory, "cache");
        }

        public static string TenantDirName(string tenantId)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(tenantId ?? ""));
            return Base32(hash).Substring(0, 12);
        }

        private static string Base32(byte[] data)
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyz234567";
            var sb = new StringBuilder((data.Length * 8 + 4) / 5);
            int buffer = 0, bits = 0;
            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    sb.Append(alphabet[(buffer >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }

            if (bits > 0)
            {
                sb.Append(alphabet[(buffer << (5 - bits)) & 31]);
            }

            return sb.ToString();
        }
    }
}
