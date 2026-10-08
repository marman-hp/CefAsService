using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Xilium.CefGlue.Broker.Storage
{
    internal static partial class Sqlite3Native
    {
        private const string Lib = "sqlite3";

        public const int SQLITE_OK = 0;
        public const int SQLITE_ROW = 100;
        public const int SQLITE_DONE = 101;
        public const int SQLITE_NULL = 5;

        public const int SQLITE_OPEN_READONLY = 0x00000001;
        public const int SQLITE_OPEN_READWRITE = 0x00000002;
        public const int SQLITE_OPEN_CREATE = 0x00000004;
        public const int SQLITE_OPEN_FULLMUTEX = 0x00010000;

        public static readonly nint SQLITE_TRANSIENT = -1;

        static Sqlite3Native()
        {
            NativeLibrary.SetDllImportResolver(typeof(Sqlite3Native).Assembly, Resolve);
        }

        private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (name != Lib)
            {
                return 0;
            }

            var candidates = OperatingSystem.IsWindows()
                ? new[] { Path.Combine(AppContext.BaseDirectory, "sqlite3.dll"), "sqlite3.dll" }
                : OperatingSystem.IsMacOS()
                    ? new[] { "libsqlite3.dylib", "/usr/lib/libsqlite3.dylib" }
                    : new[] { "libsqlite3.so.0", "libsqlite3.so" };

            foreach (var candidate in candidates)
            {
                if (NativeLibrary.TryLoad(candidate, out var handle))
                {
                    return handle;
                }
            }

            return 0;
        }

        [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
        public static partial int sqlite3_open_v2(string filename, out nint db, int flags, string vfs);

        [LibraryImport(Lib)]
        public static partial int sqlite3_close_v2(nint db);

        [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
        public static partial int sqlite3_exec(nint db, string sql, nint callback, nint arg, nint errmsg);

        [LibraryImport(Lib)]
        public static partial nint sqlite3_errmsg(nint db);

        [LibraryImport(Lib)]
        public static partial nint sqlite3_libversion();

        [LibraryImport(Lib)]
        public static partial int sqlite3_busy_timeout(nint db, int ms);

        [LibraryImport(Lib)]
        public static partial int sqlite3_extended_result_codes(nint db, int onoff);

        [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
        public static partial int sqlite3_prepare_v2(nint db, string sql, int nByte, out nint stmt, nint tail);

        [LibraryImport(Lib)]
        public static partial int sqlite3_step(nint stmt);

        [LibraryImport(Lib)]
        public static partial int sqlite3_finalize(nint stmt);

        [LibraryImport(Lib)]
        public static partial int sqlite3_bind_text(nint stmt, int index, byte[] utf8, int nBytes, nint destructor);

        [LibraryImport(Lib)]
        public static partial int sqlite3_bind_int64(nint stmt, int index, long value);

        [LibraryImport(Lib)]
        public static partial int sqlite3_bind_null(nint stmt, int index);

        [LibraryImport(Lib)]
        public static partial int sqlite3_column_type(nint stmt, int column);

        [LibraryImport(Lib)]
        public static partial long sqlite3_column_int64(nint stmt, int column);

        [LibraryImport(Lib)]
        public static partial nint sqlite3_column_text(nint stmt, int column);

        [LibraryImport(Lib)]
        public static partial int sqlite3_column_bytes(nint stmt, int column);

        public static string ErrorMessage(nint db) =>
            db == 0 ? "out of memory" : Marshal.PtrToStringUTF8(sqlite3_errmsg(db));

        public static string LibVersion() => Marshal.PtrToStringUTF8(sqlite3_libversion());
    }
}
