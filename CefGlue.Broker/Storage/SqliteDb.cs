using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using static Xilium.CefGlue.Broker.Storage.Sqlite3Native;

namespace Xilium.CefGlue.Broker.Storage
{
    internal sealed class SqliteException : Exception
    {
        public SqliteException(int code, string message) : base($"SQLite error {code}: {message}")
        {
            Code = code;
        }

        public int Code { get; }
    }

    internal readonly struct SqliteRow
    {
        private readonly nint _stmt;

        public SqliteRow(nint stmt)
        {
            _stmt = stmt;
        }

        public bool IsNull(int column) => sqlite3_column_type(_stmt, column) == SQLITE_NULL;

        public string GetString(int column)
        {
            if (IsNull(column))
            {
                return null;
            }

            var text = sqlite3_column_text(_stmt, column);
            return Marshal.PtrToStringUTF8(text, sqlite3_column_bytes(_stmt, column));
        }

        public long GetInt64(int column) => sqlite3_column_int64(_stmt, column);

        public long? GetNullableInt64(int column) => IsNull(column) ? null : sqlite3_column_int64(_stmt, column);
    }

    internal sealed class SqliteDb : IDisposable
    {
        private readonly object _gate = new();
        private nint _db;

        private SqliteDb(nint db)
        {
            _db = db;
        }

        public static SqliteDb Open(string path, bool readOnly = false)
        {
            var flags = (readOnly ? SQLITE_OPEN_READONLY : SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE) | SQLITE_OPEN_FULLMUTEX;
            var rc = sqlite3_open_v2(path, out var db, flags, null);
            if (rc != SQLITE_OK)
            {
                var message = ErrorMessage(db);
                if (db != 0)
                {
                    sqlite3_close_v2(db);
                }
                throw new SqliteException(rc, message);
            }

            sqlite3_extended_result_codes(db, 1);
            sqlite3_busy_timeout(db, 5000);
            return new SqliteDb(db);
        }

        public void Exec(string sql)
        {
            lock (_gate)
            {
                Check(sqlite3_exec(_db, sql, 0, 0, 0));
            }
        }

        public void Execute(string sql, params object[] args)
        {
            lock (_gate)
            {
                Run(sql, args, null);
            }
        }

        public List<T> Query<T>(string sql, Func<SqliteRow, T> map, params object[] args)
        {
            var results = new List<T>();
            lock (_gate)
            {
                Run(sql, args, row => results.Add(map(row)));
            }
            return results;
        }

        public long ScalarInt64(string sql, params object[] args)
        {
            long value = 0;
            lock (_gate)
            {
                Run(sql, args, row => value = row.GetInt64(0));
            }
            return value;
        }

        public void Transaction(Action body)
        {
            lock (_gate)
            {
                Exec("BEGIN IMMEDIATE");
                try
                {
                    body();
                    Exec("COMMIT");
                }
                catch
                {
                    try { Exec("ROLLBACK"); } catch { }
                    throw;
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_db != 0)
                {
                    sqlite3_close_v2(_db);
                    _db = 0;
                }
            }
        }

        private void Run(string sql, object[] args, Action<SqliteRow> onRow)
        {
            Check(sqlite3_prepare_v2(_db, sql, -1, out var stmt, 0));
            try
            {
                for (var i = 0; i < args.Length; i++)
                {
                    Check(Bind(stmt, i + 1, args[i]));
                }

                while (true)
                {
                    var rc = sqlite3_step(stmt);
                    if (rc == SQLITE_ROW)
                    {
                        onRow?.Invoke(new SqliteRow(stmt));
                        continue;
                    }

                    if (rc != SQLITE_DONE)
                    {
                        Check(rc);
                    }
                    break;
                }
            }
            finally
            {
                sqlite3_finalize(stmt);
            }
        }

        private static int Bind(nint stmt, int index, object value)
        {
            switch (value)
            {
                case null:
                    return sqlite3_bind_null(stmt, index);
                case string s:
                    var utf8 = Encoding.UTF8.GetBytes(s);
                    return sqlite3_bind_text(stmt, index, utf8.Length == 0 ? new byte[1] : utf8, utf8.Length, SQLITE_TRANSIENT);
                case long l:
                    return sqlite3_bind_int64(stmt, index, l);
                case int n:
                    return sqlite3_bind_int64(stmt, index, n);
                case bool b:
                    return sqlite3_bind_int64(stmt, index, b ? 1 : 0);
                default:
                    throw new ArgumentException($"Unsupported SQLite parameter type {value.GetType().Name}");
            }
        }

        private void Check(int rc)
        {
            if (rc != SQLITE_OK)
            {
                throw new SqliteException(rc, ErrorMessage(_db));
            }
        }
    }
}
