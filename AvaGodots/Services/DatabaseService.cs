using System;
using System.IO;
using System.Threading.Tasks;
using AvaGodots.Interfaces;
using Microsoft.Data.Sqlite;

namespace AvaGodots.Services;

/// <summary>
/// SQLite 数据库服务 — 缓存资源库、远程版本、下载记录等对用户不可见的数据
/// </summary>
public sealed class DatabaseService : IDatabaseService
{
    private SqliteConnection? _connection;
    private readonly string _dbPath;

    public DatabaseService()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AvaGodots");
        Directory.CreateDirectory(appData);
        _dbPath = Path.Combine(appData, "cache.db");
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        await _connection.OpenAsync();

        // 创建缓存表
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS http_cache (
                url TEXT PRIMARY KEY,
                response_body BLOB NOT NULL,
                content_type TEXT,
                etag TEXT,
                last_modified TEXT,
                cached_at TEXT NOT NULL DEFAULT (datetime('now')),
                expires_at TEXT
            );

            CREATE TABLE IF NOT EXISTS image_cache (
                url TEXT PRIMARY KEY,
                image_data BLOB NOT NULL,
                cached_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS download_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                url TEXT NOT NULL,
                file_name TEXT NOT NULL,
                file_path TEXT NOT NULL,
                file_size INTEGER,
                download_type TEXT NOT NULL,
                started_at TEXT NOT NULL DEFAULT (datetime('now')),
                completed_at TEXT,
                status TEXT NOT NULL DEFAULT 'pending'
            );

            CREATE TABLE IF NOT EXISTS remote_versions_cache (
                key TEXT PRIMARY KEY,
                json_data TEXT NOT NULL,
                cached_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
        """;
        await cmd.ExecuteNonQueryAsync();
    }

    // ===== HTTP 缓存 =====

    public async Task<(byte[]? body, string? contentType)?> GetHttpCacheAsync(string url, TimeSpan maxAge)
    {
        if (_connection == null) return null;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT response_body, content_type, cached_at FROM http_cache WHERE url = $url";
        cmd.Parameters.AddWithValue("$url", url);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var cachedAt = DateTime.Parse(reader.GetString(2));
        if (DateTime.UtcNow - cachedAt > maxAge) return null;

        var body = (byte[])reader[0];
        var contentType = reader.IsDBNull(1) ? null : reader.GetString(1);
        return (body, contentType);
    }

    public async Task SetHttpCacheAsync(string url, byte[] body, string? contentType = null)
    {
        if (_connection == null) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO http_cache (url, response_body, content_type, cached_at)
            VALUES ($url, $body, $type, datetime('now'))
        """;
        cmd.Parameters.AddWithValue("$url", url);
        cmd.Parameters.AddWithValue("$body", body);
        cmd.Parameters.AddWithValue("$type", (object?)contentType ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    // ===== 图片缓存 =====

    public async Task<byte[]?> GetImageCacheAsync(string url)
    {
        if (_connection == null) return null;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT image_data FROM image_cache WHERE url = $url";
        cmd.Parameters.AddWithValue("$url", url);
        var result = await cmd.ExecuteScalarAsync();
        return result as byte[];
    }

    public async Task SetImageCacheAsync(string url, byte[] data)
    {
        if (_connection == null) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO image_cache (url, image_data, cached_at)
            VALUES ($url, $data, datetime('now'))
        """;
        cmd.Parameters.AddWithValue("$url", url);
        cmd.Parameters.AddWithValue("$data", data);
        await cmd.ExecuteNonQueryAsync();
    }

    // ===== 远程版本缓存 =====

    public async Task<string?> GetVersionsCacheAsync(string key, TimeSpan maxAge)
    {
        if (_connection == null) return null;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT json_data, cached_at FROM remote_versions_cache WHERE key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var cachedAt = DateTime.Parse(reader.GetString(1));
        if (DateTime.UtcNow - cachedAt > maxAge) return null;
        return reader.GetString(0);
    }

    public async Task SetVersionsCacheAsync(string key, string jsonData)
    {
        if (_connection == null) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO remote_versions_cache (key, json_data, cached_at)
            VALUES ($key, $data, datetime('now'))
        """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$data", jsonData);
        await cmd.ExecuteNonQueryAsync();
    }

    // ===== 下载记录 =====

    public async Task<long> AddDownloadRecordAsync(string url, string fileName, string filePath, string downloadType)
    {
        if (_connection == null) return -1;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO download_history (url, file_name, file_path, download_type, status)
            VALUES ($url, $name, $path, $type, 'downloading');
            SELECT last_insert_rowid();
        """;
        cmd.Parameters.AddWithValue("$url", url);
        cmd.Parameters.AddWithValue("$name", fileName);
        cmd.Parameters.AddWithValue("$path", filePath);
        cmd.Parameters.AddWithValue("$type", downloadType);
        var result = await cmd.ExecuteScalarAsync();
        return result is long id ? id : -1;
    }

    public async Task UpdateDownloadStatusAsync(long id, string status, long? fileSize = null)
    {
        if (_connection == null) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE download_history
            SET status = $status, completed_at = datetime('now'),
                file_size = COALESCE($size, file_size)
            WHERE id = $id
        """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$size", (object?)fileSize ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    // ===== 清理 =====

    public async Task CleanExpiredCacheAsync(int daysOld = 7)
    {
        if (_connection == null) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"""
            DELETE FROM http_cache WHERE cached_at < datetime('now', '-{daysOld} days');
            DELETE FROM image_cache WHERE cached_at < datetime('now', '-{daysOld} days');
            DELETE FROM remote_versions_cache WHERE cached_at < datetime('now', '-{daysOld} days');
        """;
        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
