using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace AvaGodots.Services;

/// <summary>
/// 日志级别
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// 结构化日志条目
/// </summary>
public record LogEntry(
    LogLevel Level,
    string Category,
    string Message,
    string? Detail = null,
    string? StackTrace = null,
    DateTime? Timestamp = null);

/// <summary>
/// 全局日志服务 — 异步写入 log.db，带内存缓冲
/// </summary>
public sealed class LoggerService : IDisposable
{
    private static LoggerService? _instance;
    public static LoggerService Instance => _instance ??= new LoggerService();

    private SqliteConnection? _connection;
    private readonly ConcurrentQueue<LogEntry> _buffer = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private Timer? _flushTimer;
    private bool _disposed;
    private bool _initialized;

    /// <summary>最小写入级别，低于此级别将被忽略</summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Debug;

    /// <summary>缓冲区刷新间隔（毫秒）</summary>
    public int FlushIntervalMs { get; set; } = 2000;

    /// <summary>缓冲区最大容量，超过后立即刷新</summary>
    public int MaxBufferSize { get; set; } = 50;

    /// <summary>是否在控制台输出日志</summary>
    public bool ConsoleOutput { get; set; }
#if DEBUG
        = true;
#else
        = false;
#endif

    private LoggerService() { }

    /// <summary>
    /// 初始化日志数据库
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AvaGodots");
        Directory.CreateDirectory(appData);
        var dbPath = Path.Combine(appData, "log.db");

        _connection = new SqliteConnection($"Data Source={dbPath}");
        await _connection.OpenAsync();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                level TEXT NOT NULL,
                category TEXT NOT NULL,
                message TEXT NOT NULL,
                detail TEXT,
                stack_trace TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON logs(timestamp DESC);
            CREATE INDEX IF NOT EXISTS idx_logs_level ON logs(level);
            CREATE INDEX IF NOT EXISTS idx_logs_category ON logs(category);
        """;
        await cmd.ExecuteNonQueryAsync();

        // 启动定时刷新
        _flushTimer = new Timer(_ => _ = FlushAsync(), null, FlushIntervalMs, FlushIntervalMs);
        _initialized = true;
    }

    // ===== 便捷日志方法 =====

    public void Debug(string category, string message, string? detail = null)
        => Enqueue(LogLevel.Debug, category, message, detail);

    public void Info(string category, string message, string? detail = null)
        => Enqueue(LogLevel.Info, category, message, detail);

    public void Warning(string category, string message, string? detail = null)
        => Enqueue(LogLevel.Warning, category, message, detail);

    public void Error(string category, string message, string? detail = null, string? stackTrace = null)
        => Enqueue(LogLevel.Error, category, message, detail, stackTrace);

    public void Error(string category, string message, Exception ex)
        => Enqueue(LogLevel.Error, category, message, ex.Message, ex.StackTrace);

    public void Fatal(string category, string message, Exception ex)
        => Enqueue(LogLevel.Fatal, category, message, ex.Message, ex.StackTrace);

    // ===== 内部逻辑 =====

    private void Enqueue(LogLevel level, string category, string message,
        string? detail = null, string? stackTrace = null)
    {
        if (level < MinLevel) return;

        var entry = new LogEntry(level, category, message, detail, stackTrace, DateTime.Now);
        _buffer.Enqueue(entry);

        if (ConsoleOutput)
        {
            var color = level switch
            {
                LogLevel.Debug => "\u001b[90m",   // gray
                LogLevel.Info => "\u001b[36m",     // cyan
                LogLevel.Warning => "\u001b[33m",  // yellow
                LogLevel.Error => "\u001b[31m",    // red
                LogLevel.Fatal => "\u001b[35m",    // magenta
                _ => ""
            };
            Console.Error.WriteLine($"{color}[{level}] [{category}] {message}\u001b[0m");
            if (detail != null) Console.Error.WriteLine($"  Detail: {detail}");
        }

        // 缓冲区满时立即刷新
        if (_buffer.Count >= MaxBufferSize)
            _ = FlushAsync();
    }

    /// <summary>
    /// 刷新缓冲区到数据库
    /// </summary>
    public async Task FlushAsync()
    {
        if (_connection == null || _disposed || _buffer.IsEmpty) return;
        if (!await _flushLock.WaitAsync(0)) return; // 非阻塞，已有在刷新则跳过

        try
        {
            using var transaction = _connection.BeginTransaction();
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO logs (timestamp, level, category, message, detail, stack_trace)
                VALUES ($ts, $level, $cat, $msg, $detail, $trace)
            """;

            var pTs = cmd.Parameters.Add("$ts", SqliteType.Text);
            var pLevel = cmd.Parameters.Add("$level", SqliteType.Text);
            var pCat = cmd.Parameters.Add("$cat", SqliteType.Text);
            var pMsg = cmd.Parameters.Add("$msg", SqliteType.Text);
            var pDetail = cmd.Parameters.Add("$detail", SqliteType.Text);
            var pTrace = cmd.Parameters.Add("$trace", SqliteType.Text);

            var flushed = 0;
            while (_buffer.TryDequeue(out var entry))
            {
                pTs.Value = (entry.Timestamp ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss.fff");
                pLevel.Value = entry.Level.ToString();
                pCat.Value = entry.Category;
                pMsg.Value = entry.Message;
                pDetail.Value = (object?)entry.Detail ?? DBNull.Value;
                pTrace.Value = (object?)entry.StackTrace ?? DBNull.Value;
                await cmd.ExecuteNonQueryAsync();
                flushed++;
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            // 日志写入失败只输出到控制台，避免死循环
            Console.Error.WriteLine($"[LoggerService] Flush failed: {ex.Message}");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    /// <summary>
    /// 清理旧日志
    /// </summary>
    public async Task CleanOldLogsAsync(int daysToKeep = 30)
    {
        if (_connection == null) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM logs WHERE timestamp < datetime('now', '-{daysToKeep} days')";
        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _flushTimer?.Dispose();
        // 同步刷新剩余日志
        FlushAsync().GetAwaiter().GetResult();
        _connection?.Dispose();
        _flushLock.Dispose();
    }
}
