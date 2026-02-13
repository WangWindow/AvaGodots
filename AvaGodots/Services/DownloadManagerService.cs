using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using AvaGodots.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaGodots.Services;

/// <summary>
/// 下载项 — 表示一个正在进行或已完成的下载任务
/// </summary>
public partial class DownloadItem : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _url = "";
    [ObservableProperty] private string _filePath = "";
    [ObservableProperty] private string _statusText = "Waiting...";
    [ObservableProperty] private double _progress;
    [ObservableProperty] private double _maxProgress = 100;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private bool _canRetry;
    [ObservableProperty] private bool _canInstall;
    [ObservableProperty] private long _totalBytes;
    [ObservableProperty] private long _downloadedBytes;

    /// <summary>下载类型: editor / asset</summary>
    public string DownloadType { get; init; } = "editor";

    /// <summary>下载完成事件</summary>
    public event Action<DownloadItem>? Completed;
    /// <summary>请求安装</summary>
    public event Action<DownloadItem>? InstallRequested;

    internal CancellationTokenSource Cts { get; } = new();

    public void RaiseCompleted() => Completed?.Invoke(this);
    public void RaiseInstallRequested() => InstallRequested?.Invoke(this);
}

/// <summary>
/// 下载管理器 — 管理编辑器和资源的下载、解压、安装
/// </summary>
public sealed class DownloadManagerService : IDownloadManagerService
{
    private readonly HttpClient _httpClient;
    private readonly Interfaces.IDatabaseService _db;
    private readonly Interfaces.IEditorService _editorService;
    private readonly Interfaces.IConfigService _configService;

    public ObservableCollection<DownloadItem> Downloads { get; } = new();

    /// <summary>是否有活跃下载</summary>
    public bool HasActiveDownloads => Downloads.Any(d => d.IsDownloading);

    /// <summary>编辑器安装完成事件（下载并解压成功后触发）</summary>
    public event Action? EditorInstalled;

    public DownloadManagerService(
        Interfaces.IDatabaseService db,
        Interfaces.IEditorService editorService,
        Interfaces.IConfigService configService)
    {
        _db = db;
        _editorService = editorService;
        _configService = configService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AvaGodots/1.0");
    }

    /// <summary>
    /// 下载远程编辑器
    /// </summary>
    public DownloadItem DownloadEditor(string url, string fileName, string? displayName = null)
    {
        var downloadsDir = _configService.Config.DownloadsPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaGodots", "downloads");
        Directory.CreateDirectory(downloadsDir);

        var filePath = Path.Combine(downloadsDir, fileName);

        var item = new DownloadItem
        {
            Title = displayName ?? fileName,
            Url = url,
            FilePath = filePath,
            DownloadType = "editor",
            IsDownloading = true,
            StatusText = "Waiting..."
        };

        item.Completed += OnEditorDownloaded;
        LoggerService.Instance.Info("Download", $"Starting editor download: {fileName}", url);
        Downloads.Add(item);

        _ = ExecuteDownloadAsync(item);
        return item;
    }

    /// <summary>
    /// 并发线程数 (chunk 数量)
    /// </summary>
    private const int DefaultChunkCount = 4;

    /// <summary>
    /// 执行实际下载 — 支持多线程分块下载 (HTTP Range)
    /// </summary>
    private async Task ExecuteDownloadAsync(DownloadItem item)
    {
        item.IsDownloading = true;
        item.StatusText = LocalizationService.GetString("Common.Download", "Connecting...");
        item.CanRetry = false;

        var recordId = await _db.AddDownloadRecordAsync(item.Url, Path.GetFileName(item.FilePath), item.FilePath, item.DownloadType);

        try
        {
            // 1. 发送 HEAD 请求获取文件大小和 Range 支持
            using var headReq = new HttpRequestMessage(HttpMethod.Head, item.Url);
            using var headResp = await _httpClient.SendAsync(headReq, item.Cts.Token);
            headResp.EnsureSuccessStatusCode();

            var totalBytes = headResp.Content.Headers.ContentLength ?? 0;
            var acceptsRanges = headResp.Headers.AcceptRanges.Contains("bytes");

            item.TotalBytes = totalBytes;
            item.MaxProgress = totalBytes > 0 ? totalBytes : 100;

            // 2. 如果支持 Range 且文件足够大 (>2MB)，使用多线程分块下载
            if (acceptsRanges && totalBytes > 2 * 1024 * 1024)
            {
                await ExecuteChunkedDownloadAsync(item, totalBytes, recordId);
            }
            else
            {
                await ExecuteSingleStreamDownloadAsync(item, recordId);
            }
        }
        catch (OperationCanceledException)
        {
            item.IsDownloading = false;
            item.StatusText = "Cancelled";
            item.IsFailed = true;
            await _db.UpdateDownloadStatusAsync(recordId, "cancelled");
            CleanupFile(item.FilePath);
            LoggerService.Instance.Info("Download", $"Download cancelled: {item.Title}");
        }
        catch (Exception ex)
        {
            item.IsDownloading = false;
            item.StatusText = $"Failed: {ex.Message}";
            item.IsFailed = true;
            item.CanRetry = true;
            await _db.UpdateDownloadStatusAsync(recordId, "failed");
            CleanupFile(item.FilePath);
            LoggerService.Instance.Error("Download", $"Download failed: {item.Title}", ex);
        }
    }

    /// <summary>
    /// 多线程分块下载 — 将文件分成多个 chunk 并发下载
    /// </summary>
    private async Task ExecuteChunkedDownloadAsync(DownloadItem item, long totalBytes, long recordId)
    {
        var chunkCount = DefaultChunkCount;
        var chunkSize = totalBytes / chunkCount;
        var chunkPaths = new string[chunkCount];
        var chunkProgress = new long[chunkCount];
        var downloadStart = DateTime.UtcNow;

        // 创建 chunk 任务
        var tasks = new Task[chunkCount];
        for (var i = 0; i < chunkCount; i++)
        {
            var index = i;
            var rangeFrom = i * chunkSize;
            var rangeTo = (i == chunkCount - 1) ? totalBytes - 1 : (i + 1) * chunkSize - 1;
            var chunkPath = item.FilePath + $".part{i}";
            chunkPaths[i] = chunkPath;

            tasks[i] = Task.Run(async () =>
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, item.Url);
                req.Headers.Range = new RangeHeaderValue(rangeFrom, rangeTo);

                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, item.Cts.Token);
                resp.EnsureSuccessStatusCode();

                await using var contentStream = await resp.Content.ReadAsStreamAsync(item.Cts.Token);
                await using var fileStream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                var buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, item.Cts.Token)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), item.Cts.Token);
                    Interlocked.Add(ref chunkProgress[index], bytesRead);

                    // 汇总所有 chunk 的进度
                    var totalDownloaded = chunkProgress.Sum();
                    item.DownloadedBytes = totalDownloaded;
                    item.Progress = totalDownloaded;

                    var elapsed = (DateTime.UtcNow - downloadStart).TotalSeconds;
                    var speed = elapsed > 0 ? totalDownloaded / elapsed : 0;
                    item.StatusText = $"Downloading ({chunkCount} threads)... {FormatSize(totalDownloaded)} / {FormatSize(totalBytes)} @ {FormatSize((long)speed)}/s";
                }
            }, item.Cts.Token);
        }

        // 等待所有 chunk 完成
        await Task.WhenAll(tasks);

        // 合并 chunk 文件
        item.StatusText = "Merging chunks...";
        await using (var outputStream = new FileStream(item.FilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            for (var i = 0; i < chunkCount; i++)
            {
                await using var chunkStream = new FileStream(chunkPaths[i], FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                await chunkStream.CopyToAsync(outputStream, item.Cts.Token);
            }
        }

        // 清理 chunk 文件
        foreach (var p in chunkPaths) CleanupFile(p);

        // 完成
        item.IsDownloading = false;
        item.IsCompleted = true;
        item.StatusText = "Download complete";
        item.CanInstall = true;
        await _db.UpdateDownloadStatusAsync(recordId, "completed", totalBytes);

        if (item.DownloadType == "editor")
            await InstallEditorAsync(item);
    }

    /// <summary>
    /// 单线程下载 (不支持 Range 或文件较小时使用)
    /// </summary>
    private async Task ExecuteSingleStreamDownloadAsync(DownloadItem item, long recordId)
    {
        using var response = await _httpClient.GetAsync(item.Url, HttpCompletionOption.ResponseHeadersRead, item.Cts.Token);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        item.TotalBytes = totalBytes;
        item.MaxProgress = totalBytes > 0 ? totalBytes : 100;

        await using var contentStream = await response.Content.ReadAsStreamAsync(item.Cts.Token);
        await using var fileStream = new FileStream(item.FilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long downloaded = 0;
        int bytesRead;
        var downloadStart = DateTime.UtcNow;

        while ((bytesRead = await contentStream.ReadAsync(buffer, item.Cts.Token)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), item.Cts.Token);
            downloaded += bytesRead;
            item.DownloadedBytes = downloaded;
            item.Progress = downloaded;

            var elapsed = (DateTime.UtcNow - downloadStart).TotalSeconds;
            var speed = elapsed > 0 ? downloaded / elapsed : 0;

            if (totalBytes > 0)
                item.StatusText = $"Downloading... {FormatSize(downloaded)} / {FormatSize(totalBytes)} @ {FormatSize((long)speed)}/s";
            else
                item.StatusText = $"Downloading... {FormatSize(downloaded)} @ {FormatSize((long)speed)}/s";
        }

        item.IsDownloading = false;
        item.IsCompleted = true;
        item.StatusText = "Download complete";
        item.CanInstall = true;
        await _db.UpdateDownloadStatusAsync(recordId, "completed", downloaded);

        if (item.DownloadType == "editor")
            await InstallEditorAsync(item);
    }

    /// <summary>
    /// 下载导出模板 (.tpz)
    /// </summary>
    public DownloadItem DownloadExportTemplate(string url, string fileName, string? displayName = null)
    {
        var downloadsDir = _configService.Config.DownloadsPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaGodots", "downloads");
        Directory.CreateDirectory(downloadsDir);

        var filePath = Path.Combine(downloadsDir, fileName);

        var item = new DownloadItem
        {
            Title = displayName ?? fileName,
            Url = url,
            FilePath = filePath,
            DownloadType = "export_template",
            IsDownloading = true,
            StatusText = "Waiting..."
        };

        Downloads.Add(item);
        _ = ExecuteDownloadAsync(item);
        return item;
    }

    /// <summary>
    /// 安装已下载的编辑器 (解压 + 导入)
    /// </summary>
    private async Task InstallEditorAsync(DownloadItem item)
    {
        try
        {
            item.StatusText = "Installing...";

            var versionsDir = _configService.Config.VersionsPath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvaGodots", "versions");

            var extractDir = Path.Combine(versionsDir, Path.GetFileNameWithoutExtension(item.FilePath));
            Directory.CreateDirectory(extractDir);

            // 解压
            await Task.Run(() => ZipFile.ExtractToDirectory(item.FilePath, extractDir, true));

            // 导入编辑器
            var editorName = Path.GetFileNameWithoutExtension(item.FilePath);
            await _editorService.InstallEditorAsync(item.FilePath, editorName, extractDir);

            item.StatusText = "Installed successfully";
            item.RaiseCompleted();
            EditorInstalled?.Invoke();
            LoggerService.Instance.Info("Download", $"Editor installed: {editorName}", extractDir);

            // 清理 zip 文件
            CleanupFile(item.FilePath);
        }
        catch (Exception ex)
        {
            item.StatusText = $"Install failed: {ex.Message}";
            item.IsFailed = true;
            item.CanRetry = true;
            LoggerService.Instance.Error("Download", $"Install failed: {item.Title}", ex);
        }
    }

    /// <summary>
    /// 重试下载
    /// </summary>
    public void Retry(DownloadItem item)
    {
        item.IsFailed = false;
        item.CanRetry = false;
        item.IsCompleted = false;
        item.Progress = 0;
        item.DownloadedBytes = 0;
        _ = ExecuteDownloadAsync(item);
    }

    /// <summary>
    /// 取消下载
    /// </summary>
    public void Cancel(DownloadItem item)
    {
        item.Cts.Cancel();
    }

    /// <summary>
    /// 移除下载项
    /// </summary>
    public void Dismiss(DownloadItem item)
    {
        if (item.IsDownloading) Cancel(item);
        Downloads.Remove(item);
    }

    private static void CleanupFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }

    private void OnEditorDownloaded(DownloadItem item)
    {
        // 安装完成后无需额外操作，日志已在 InstallEditorAsync 中处理
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
}
