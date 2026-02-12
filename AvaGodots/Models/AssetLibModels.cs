using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AvaGodots.Models;

/// <summary>
/// Godot 资源库素材项
/// 对应 godotengine.org/asset-library/api 返回的数据
/// </summary>
public class AssetLibItem
{
    [JsonPropertyName("asset_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("category_id")]
    public string CategoryId { get; set; } = string.Empty;

    [JsonPropertyName("godot_version")]
    public string GodotVersion { get; set; } = string.Empty;

    [JsonPropertyName("cost")]
    public string Cost { get; set; } = string.Empty; // License: MIT, Apache, etc.

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("version_string")]
    public string VersionString { get; set; } = string.Empty;

    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("download_hash")]
    public string DownloadHash { get; set; } = string.Empty;

    [JsonPropertyName("browse_url")]
    public string BrowseUrl { get; set; } = string.Empty;

    [JsonPropertyName("icon_url")]
    public string IconUrl { get; set; } = string.Empty;

    [JsonPropertyName("support_level")]
    public string SupportLevel { get; set; } = string.Empty;

    [JsonPropertyName("modify_date")]
    public string ModifyDate { get; set; } = string.Empty;

    [JsonPropertyName("previews")]
    public List<AssetPreview> Previews { get; set; } = [];
}

/// <summary>
/// 素材预览图/视频
/// </summary>
public class AssetPreview
{
    [JsonPropertyName("preview_id")]
    public string PreviewId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // "image" or "video"

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;

    [JsonPropertyName("thumbnail")]
    public string Thumbnail { get; set; } = string.Empty;

    public bool IsVideo => Type == "video";
}

/// <summary>
/// 资源库 API 返回的列表包装
/// </summary>
public class AssetLibResult
{
    [JsonPropertyName("result")]
    public List<AssetLibItem> Result { get; set; } = [];

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pages")]
    public int Pages { get; set; }

    [JsonPropertyName("page_length")]
    public int PageLength { get; set; }

    [JsonPropertyName("total_items")]
    public int TotalItems { get; set; }
}

/// <summary>
/// 资源库分类
/// </summary>
public class AssetLibCategory
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// 资源库配置 API 返回
/// </summary>
public class AssetLibConfig
{
    [JsonPropertyName("categories")]
    public List<AssetLibCategory> Categories { get; set; } = [];
}

/// <summary>
/// GitHub release 版本
/// </summary>
public class GithubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("prerelease")]
    public bool IsPrerelease { get; set; }

    [JsonPropertyName("draft")]
    public bool IsDraft { get; set; }

    [JsonPropertyName("assets")]
    public List<GithubReleaseAsset> Assets { get; set; } = [];
}

/// <summary>
/// GitHub release 中的文件
/// </summary>
public class GithubReleaseAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;

    public bool IsZip => Name.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase);
}
