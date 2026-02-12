using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AvaGodots.Models;

namespace AvaGodots.Services;

/// <summary>
/// Godot 资源库 API 服务
/// 调用 godotengine.org/asset-library/api
/// </summary>
public partial class AssetLibService
{
    private readonly HttpClient _httpClient;
    private List<AssetLibCategory>? _cachedCategories;

    public string SiteUrl { get; set; } = "https://godotengine.org/asset-library/api";

    public AssetLibService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "AvaGodots/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 获取分类列表（带缓存）
    /// </summary>
    public async Task<List<AssetLibCategory>> GetCategoriesAsync(CancellationToken ct = default)
    {
        if (_cachedCategories != null) return _cachedCategories;

        try
        {
            var url = $"{SiteUrl}/configure?type=project";
            var config = await _httpClient.GetFromJsonAsync<AssetLibConfig>(url, ct);
            _cachedCategories = config?.Categories ?? [];
            return _cachedCategories;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 搜索素材
    /// </summary>
    public async Task<AssetLibResult> SearchAssetsAsync(
        string filter = "",
        string godotVersion = "",
        string sort = "updated",
        int category = 0,
        int page = 0,
        int maxResults = 40,
        string type = "any",
        CancellationToken ct = default)
    {
        try
        {
            var url = $"{SiteUrl}/asset?type={type}&sort={sort}&max_results={maxResults}&page={page}";

            if (!string.IsNullOrWhiteSpace(filter))
                url += $"&filter={Uri.EscapeDataString(filter)}";

            if (!string.IsNullOrWhiteSpace(godotVersion))
                url += $"&godot_version={Uri.EscapeDataString(godotVersion)}";

            if (category > 0)
                url += $"&category={category}";

            var result = await _httpClient.GetFromJsonAsync<AssetLibResult>(url, ct);
            return result ?? new AssetLibResult();
        }
        catch
        {
            return new AssetLibResult();
        }
    }

    /// <summary>
    /// 获取素材详情
    /// </summary>
    public async Task<AssetLibItem?> GetAssetDetailAsync(string assetId, CancellationToken ct = default)
    {
        try
        {
            var url = $"{SiteUrl}/asset/{assetId}";
            return await _httpClient.GetFromJsonAsync<AssetLibItem>(url, ct);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// 远程编辑器版本源（GitHub）
/// </summary>
public class RemoteEditorService
{
    private readonly HttpClient _httpClient;
    private List<RemoteGodotVersion>? _cachedVersions;

    // 平台文件后缀
    private static readonly string[] LinuxSuffixes = ["_linux.x86_64.zip", "_linux_x86_64.zip", "_linux.64.zip", "_x11.64.zip", "_linux.x86_32.zip"];
    private static readonly string[] WindowsSuffixes = ["_win64.exe.zip", "_win32.exe.zip", "_win64.zip", "_win32.zip"];
    private static readonly string[] MacOsSuffixes = ["_osx.universal.zip", "_macos.universal.zip", "_osx.fat.zip"];

    public RemoteEditorService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "AvaGodots/1.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 获取所有远程 Godot 版本列表（从 GitHub YAML 解析）
    /// </summary>
    public async Task<List<RemoteGodotVersion>> GetVersionsAsync(CancellationToken ct = default)
    {
        if (_cachedVersions != null) return _cachedVersions;

        try
        {
            var url = "https://raw.githubusercontent.com/godotengine/godot-website/master/_data/versions.yml";
            var yml = await _httpClient.GetStringAsync(url, ct);
            _cachedVersions = ParseVersionsYml(yml);
            return _cachedVersions;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 获取指定版本-release 的 assets（如 4.3-stable）
    /// </summary>
    public async Task<List<GithubReleaseAsset>> GetReleaseAssetsAsync(string tag, CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/godotengine/godot-builds/releases/tags/{tag}";
            var release = await _httpClient.GetFromJsonAsync<GithubRelease>(url, ct);
            return release?.Assets ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 检测文件是否适用于当前平台
    /// </summary>
    public static bool IsForCurrentPlatform(string fileName)
    {
        var suffixes = GetCurrentPlatformSuffixes();
        foreach (var suffix in suffixes)
        {
            if (fileName.Contains(suffix.Replace(".zip", ""), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 判断文件是否为 Mono 版
    /// </summary>
    public static bool IsMono(string fileName) =>
        fileName.Contains("mono", StringComparison.OrdinalIgnoreCase);

    private static string[] GetCurrentPlatformSuffixes()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return LinuxSuffixes;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return WindowsSuffixes;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return MacOsSuffixes;
        return LinuxSuffixes;
    }

    /// <summary>
    /// 解析 Godot 版本 YAML
    /// </summary>
    private static List<RemoteGodotVersion> ParseVersionsYml(string yml)
    {
        var versions = new List<RemoteGodotVersion>();
        RemoteGodotVersion? current = null;

        foreach (var rawLine in yml.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.StartsWith("- name:"))
            {
                current = new RemoteGodotVersion
                {
                    Name = line.Replace("- name:", "").Trim().Trim('"')
                };
                versions.Add(current);
            }
            else if (line.StartsWith("flavor:") && current != null)
            {
                current.Flavor = line.Replace("flavor:", "").Trim().Trim('"');
            }
            else if (line.StartsWith("- ") && current != null && !line.StartsWith("- name:"))
            {
                var release = line[2..].Trim().Trim('"');
                if (!string.IsNullOrEmpty(release))
                    current.Releases.Add(release);
            }
        }

        return versions;
    }
}

/// <summary>
/// 远程 Godot 版本信息
/// </summary>
public class RemoteGodotVersion
{
    public string Name { get; set; } = string.Empty;
    public string Flavor { get; set; } = "stable";
    public List<string> Releases { get; set; } = [];
}
