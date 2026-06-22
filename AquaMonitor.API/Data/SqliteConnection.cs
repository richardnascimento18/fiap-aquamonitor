using System.Runtime.InteropServices;
using System.Text;

namespace AquaMonitor.API.Data;

public sealed class SqliteConnection : IDisposable
{
    private IntPtr _db = IntPtr.Zero;
    private bool _disposed;

    public SqliteConnection(string path)
    {
        var rc = NativeMethods.sqlite3_open(Encoding.UTF8.GetBytes(path + "\0"), out _db);
        if (rc != 0) throw new InvalidOperationException($"Cannot open SQLite DB '{path}': rc={rc}");
    }

    public void Execute(string sql, IReadOnlyDictionary<string, object?>? p = null)
    {
        using var stmt = Prepare(sql);
        Bind(stmt, p);
        var rc = NativeMethods.sqlite3_step(stmt.Handle);
        if (rc != 100 && rc != 101) ThrowError(rc, sql);
    }

    public List<Dictionary<string, object?>> Query(string sql, IReadOnlyDictionary<string, object?>? p = null)
    {
        var results = new List<Dictionary<string, object?>>();
        using var stmt = Prepare(sql);
        Bind(stmt, p);
        int rc;
        while ((rc = NativeMethods.sqlite3_step(stmt.Handle)) == 100)
        {
            var row = new Dictionary<string, object?>();
            int cols = NativeMethods.sqlite3_column_count(stmt.Handle);
            for (int i = 0; i < cols; i++)
            {
                var name = PtrToUtf8(NativeMethods.sqlite3_column_name(stmt.Handle, i)) ?? i.ToString();
                var type = NativeMethods.sqlite3_column_type(stmt.Handle, i);
                object? value = type switch
                {
                    1 => NativeMethods.sqlite3_column_int64(stmt.Handle, i),
                    2 => NativeMethods.sqlite3_column_double(stmt.Handle, i),
                    3 => PtrToUtf8(NativeMethods.sqlite3_column_text(stmt.Handle, i)),
                    5 => null,
                    _ => PtrToUtf8(NativeMethods.sqlite3_column_text(stmt.Handle, i))
                };
                row[name] = value;
            }
            results.Add(row);
        }
        if (rc != 101) ThrowError(rc, sql);
        return results;
    }

    public long LastInsertRowId() => NativeMethods.sqlite3_last_insert_rowid(_db);

    private StmtHandle Prepare(string sql)
    {
        var bytes = Encoding.UTF8.GetBytes(sql + "\0");
        var rc = NativeMethods.sqlite3_prepare_v2(_db, bytes, bytes.Length, out var stmt, IntPtr.Zero);
        if (rc != 0) ThrowError(rc, sql);
        return new StmtHandle(stmt);
    }

    private static void Bind(StmtHandle stmt, IReadOnlyDictionary<string, object?>? p)
    {
        if (p is null) return;
        foreach (var (key, value) in p)
        {
            var nb = Encoding.UTF8.GetBytes(key + "\0");
            int idx = NativeMethods.sqlite3_bind_parameter_index(stmt.Handle, nb);
            if (idx == 0) continue;
            if (value is null)                NativeMethods.sqlite3_bind_null(stmt.Handle, idx);
            else if (value is long l)         NativeMethods.sqlite3_bind_int64(stmt.Handle, idx, l);
            else if (value is int iv)         NativeMethods.sqlite3_bind_int64(stmt.Handle, idx, iv);
            else if (value is double d)       NativeMethods.sqlite3_bind_double(stmt.Handle, idx, d);
            else
            {
                var sb = Encoding.UTF8.GetBytes(value.ToString()! + "\0");
                NativeMethods.sqlite3_bind_text(stmt.Handle, idx, sb, sb.Length - 1, new IntPtr(-1));
            }
        }
    }

    private void ThrowError(int rc, string sql)
    {
        var msg = PtrToUtf8(NativeMethods.sqlite3_errmsg(_db)) ?? "unknown";
        throw new InvalidOperationException($"SQLite {rc} on [{sql}]: {msg}");
    }

    private static string? PtrToUtf8(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        var bytes = new List<byte>();
        int offset = 0;
        byte b;
        while ((b = Marshal.ReadByte(ptr, offset++)) != 0) bytes.Add(b);
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public void Dispose()
    {
        if (!_disposed && _db != IntPtr.Zero)
        { NativeMethods.sqlite3_close(_db); _db = IntPtr.Zero; }
        _disposed = true;
    }

    private sealed class StmtHandle(IntPtr h) : IDisposable
    {
        public IntPtr Handle { get; } = h;
        public void Dispose() { if (Handle != IntPtr.Zero) NativeMethods.sqlite3_finalize(Handle); }
    }

    private static class NativeMethods
    {
        private const string Lib = "libsqlite3.so.0";
        [DllImport(Lib)] public static extern int sqlite3_open(byte[] filename, out IntPtr db);
        [DllImport(Lib)] public static extern int sqlite3_close(IntPtr db);
        [DllImport(Lib)] public static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int nBytes, out IntPtr stmt, IntPtr tail);
        [DllImport(Lib)] public static extern int sqlite3_step(IntPtr stmt);
        [DllImport(Lib)] public static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport(Lib)] public static extern int sqlite3_column_count(IntPtr stmt);
        [DllImport(Lib)] public static extern IntPtr sqlite3_column_name(IntPtr stmt, int col);
        [DllImport(Lib)] public static extern int sqlite3_column_type(IntPtr stmt, int col);
        [DllImport(Lib)] public static extern long sqlite3_column_int64(IntPtr stmt, int col);
        [DllImport(Lib)] public static extern double sqlite3_column_double(IntPtr stmt, int col);
        [DllImport(Lib)] public static extern IntPtr sqlite3_column_text(IntPtr stmt, int col);
        [DllImport(Lib)] public static extern int sqlite3_bind_parameter_index(IntPtr stmt, byte[] name);
        [DllImport(Lib)] public static extern int sqlite3_bind_null(IntPtr stmt, int idx);
        [DllImport(Lib)] public static extern int sqlite3_bind_int64(IntPtr stmt, int idx, long value);
        [DllImport(Lib)] public static extern int sqlite3_bind_double(IntPtr stmt, int idx, double value);
        [DllImport(Lib)] public static extern int sqlite3_bind_text(IntPtr stmt, int idx, byte[] text, int n, IntPtr destructor);
        [DllImport(Lib)] public static extern long sqlite3_last_insert_rowid(IntPtr db);
        [DllImport(Lib)] public static extern IntPtr sqlite3_errmsg(IntPtr db);
    }
}
