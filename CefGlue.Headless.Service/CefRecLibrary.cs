using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Xilium.CefGlue.Headless.Service
{
    internal sealed unsafe class CefRecLibrary
    {
        public const int ExpectedApiVersion = 2;
        public const string ExpectedApiHash = "fb71fc7f2f8220f132910892f7c97d48973f41ac";

        public const int Ok = 0, BadHandle = -2, BufferTooSmall = -7, LogUnavailable = -8;

        private static readonly object Gate = new();
        private static readonly Dictionary<string, CefRecLibrary> Cache = new(StringComparer.OrdinalIgnoreCase);

        private readonly delegate* unmanaged[Cdecl]<byte*, int> _verifyId;
        private readonly delegate* unmanaged[Cdecl]<byte*, byte*, int, int> _verifyLog;
        private readonly delegate* unmanaged[Cdecl]<byte*, byte*, int> _erase;

        public string Path { get; }

        public string Error { get; }

        public bool IsUsable => Error == null;

        private CefRecLibrary(string path, string error)
        {
            Path = path;
            Error = error;
        }

        private CefRecLibrary(string path, IntPtr lib)
        {
            Path = path;
            _verifyId = (delegate* unmanaged[Cdecl]<byte*, int>)NativeLibrary.GetExport(lib, "rec_verify_id");
            _verifyLog = (delegate* unmanaged[Cdecl]<byte*, byte*, int, int>)NativeLibrary.GetExport(lib, "rec_verify_log");
            _erase = (delegate* unmanaged[Cdecl]<byte*, byte*, int>)NativeLibrary.GetExport(lib, "rec_log_erase");
        }

        public static CefRecLibrary ForWorker() => Open(System.IO.Path.Combine(AppContext.BaseDirectory, "cefrec.dll"));

        public static CefRecLibrary Open(string dllPath)
        {
            var full = System.IO.Path.GetFullPath(dllPath);
            lock (Gate)
            {
                if (!Cache.TryGetValue(full, out var lib))
                {
                    lib = Load(full);
                    Cache[full] = lib;
                }
                return lib;
            }
        }

        private static CefRecLibrary Load(string path)
        {
            if (!File.Exists(path))
            {
                return new CefRecLibrary(path, "Recording component (cefrec.dll) is not installed");
            }
            if (!NativeLibrary.TryLoad(path, out var lib))
            {
                return new CefRecLibrary(path, "Recording component (cefrec.dll) could not be loaded");
            }
            if (!NativeLibrary.TryGetExport(lib, "rec_api_version", out var versionFn)
                || !NativeLibrary.TryGetExport(lib, "rec_api_hash", out var hashFn))
            {
                return new CefRecLibrary(path, "Recording component (cefrec.dll) is too old for this CAS version - update cefrec.dll");
            }

            var version = ((delegate* unmanaged[Cdecl]<int>)versionFn)();
            var hashPtr = ((delegate* unmanaged[Cdecl]<int, byte*>)hashFn)(ExpectedApiVersion);
            var hash = hashPtr == null ? null : Marshal.PtrToStringUTF8((IntPtr)hashPtr);
            if (!string.Equals(hash, ExpectedApiHash, StringComparison.OrdinalIgnoreCase))
            {
                return new CefRecLibrary(path,
                    $"Recording component version mismatch: cefrec.dll has API {version} ({hash ?? "no hash for API " + ExpectedApiVersion}), " +
                    $"this CAS needs API {ExpectedApiVersion} ({ExpectedApiHash}) - install the matching cefrec.dll");
            }

            foreach (var name in new[] { "rec_begin", "rec_status", "rec_end", "rec_verify_id", "rec_verify_log", "rec_log_erase" })
            {
                if (!NativeLibrary.TryGetExport(lib, name, out _))
                {
                    return new CefRecLibrary(path, $"Recording component (cefrec.dll) is incomplete: {name} is missing");
                }
            }
            return new CefRecLibrary(path, lib);
        }

        public bool? VerifyId(string id)
        {
            if (!IsUsable) return null;
            var u = Utf8Z(id ?? "");
            fixed (byte* p = u) return _verifyId(p) == 1;
        }

        public string VerifyLog(string dbPath, out string error)
        {
            error = Error;
            if (!IsUsable) return null;
            var d = Utf8Z(dbPath);
            for (var cap = 64 * 1024; cap <= 16 * 1024 * 1024; cap *= 4)
            {
                var buf = new byte[cap];
                int n;
                fixed (byte* dp = d)
                fixed (byte* b = buf)
                {
                    n = _verifyLog(dp, b, cap);
                }
                if (n == BufferTooSmall) continue;
                if (n < 0)
                {
                    error = $"Recording log check failed (code {n}): {FromUtf8Z(buf)}";
                    return null;
                }
                return Encoding.UTF8.GetString(buf, 0, n);
            }
            error = "Recording log check result too large";
            return null;
        }

        public int? Erase(string dbPath, string id)
        {
            if (!IsUsable) return null;
            var d = Utf8Z(dbPath);
            var i = Utf8Z(id ?? "");
            fixed (byte* dp = d)
            fixed (byte* ip = i)
            {
                return _erase(dp, ip);
            }
        }

        private static byte[] Utf8Z(string s)
        {
            var bytes = new byte[Encoding.UTF8.GetByteCount(s) + 1];
            Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
            return bytes;
        }

        private static string FromUtf8Z(byte[] buf)
        {
            var n = Array.IndexOf(buf, (byte)0);
            return Encoding.UTF8.GetString(buf, 0, n < 0 ? buf.Length : n);
        }
    }
}
