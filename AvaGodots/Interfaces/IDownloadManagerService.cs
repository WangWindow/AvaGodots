using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace AvaGodots.Interfaces;

/// <summary>
/// 下载管理器服务接口 — 管理编辑器和资源的下载、解压、安装
/// </summary>
public interface IDownloadManagerService
{
    ObservableCollection<Services.DownloadItem> Downloads { get; }
    bool HasActiveDownloads { get; }

    /// <summary>编辑器安装完成事件</summary>
    event Action? EditorInstalled;

    Services.DownloadItem DownloadEditor(string url, string fileName, string? displayName = null);
    Services.DownloadItem DownloadExportTemplate(string url, string fileName, string? displayName = null);
    void Retry(Services.DownloadItem item);
    void Cancel(Services.DownloadItem item);
    void Dismiss(Services.DownloadItem item);
}
