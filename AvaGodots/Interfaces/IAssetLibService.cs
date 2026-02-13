using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AvaGodots.Models;

namespace AvaGodots.Interfaces;

/// <summary>
/// Godot 资源库 API 服务接口
/// </summary>
public interface IAssetLibService
{
    string SiteUrl { get; set; }

    void SetDatabase(IDatabaseService db);
    Task<List<AssetLibCategory>> GetCategoriesAsync(CancellationToken ct = default);
    Task<AssetLibResult> SearchAssetsAsync(
        string filter = "",
        string godotVersion = "",
        string sort = "updated",
        int category = 0,
        int page = 0,
        int maxResults = 40,
        string type = "any",
        CancellationToken ct = default);
    Task PreCacheAsync(int pages = 3, CancellationToken ct = default);
    Task<AssetLibItem?> GetAssetDetailAsync(string assetId, CancellationToken ct = default);
}
