using System;
using System.Threading.Tasks;

namespace AvaGodots.Interfaces;

/// <summary>
/// SQLite 数据库服务接口 — 缓存资源库、远程版本、下载记录等
/// </summary>
public interface IDatabaseService : IDisposable
{
    Task InitializeAsync();

    // HTTP 缓存
    Task<(byte[]? body, string? contentType)?> GetHttpCacheAsync(string url, TimeSpan maxAge);
    Task SetHttpCacheAsync(string url, byte[] body, string? contentType = null);

    // 图片缓存
    Task<byte[]?> GetImageCacheAsync(string url);
    Task SetImageCacheAsync(string url, byte[] data);

    // 远程版本缓存
    Task<string?> GetVersionsCacheAsync(string key, TimeSpan maxAge);
    Task SetVersionsCacheAsync(string key, string jsonData);

    // 下载记录
    Task<long> AddDownloadRecordAsync(string url, string fileName, string filePath, string downloadType);
    Task UpdateDownloadStatusAsync(long id, string status, long? fileSize = null);

    // 清理
    Task CleanExpiredCacheAsync(int daysOld = 7);
}
