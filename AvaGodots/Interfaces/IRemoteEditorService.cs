using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AvaGodots.Models;

namespace AvaGodots.Interfaces;

/// <summary>
/// 远程编辑器版本服务接口 — 从 GitHub 获取 Godot 版本列表和发布资产
/// </summary>
/// <remarks>
/// 静态工具方法 (IsDesktopAsset / IsExportTemplate / IsMono / IsForCurrentPlatform)
/// 保留在 <see cref="Services.RemoteEditorService"/> 上，不纳入接口。
/// </remarks>
public interface IRemoteEditorService
{
    void SetDatabase(IDatabaseService db);
    Task<List<Services.RemoteGodotVersion>> GetVersionsAsync(CancellationToken ct = default);
    Task<List<GithubReleaseAsset>> GetReleaseAssetsAsync(string tag, CancellationToken ct = default);
}
