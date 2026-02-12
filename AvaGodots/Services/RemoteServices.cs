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
    private DatabaseService? _db;

    public string SiteUrl { get; set; } = "https://godotengine.org/asset-library/api";

    public AssetLibService(DatabaseService? db = null)
    {
        _db = db;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "AvaGodots/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 注入数据库服务（用于缓存）
    /// </summary>
    public void SetDatabase(DatabaseService db) => _db = db;

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
    /// 搜索素材（带 SQLite 缓存）
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
        var url = BuildSearchUrl(filter, godotVersion, sort, category, page, maxResults, type);

        // 尝试从缓存读取
        if (_db != null)
        {
            var cached = await _db.GetHttpCacheAsync(url, TimeSpan.FromMinutes(10));
            if (cached?.body != null)
            {
                try
                {
                    var cachedResult = JsonSerializer.Deserialize<AssetLibResult>(cached.Value.body);
                    if (cachedResult != null) return cachedResult;
                }
                catch { /* 缓存损坏,继续网络请求 */ }
            }
        }

        try
        {
            var json = await _httpClient.GetByteArrayAsync(url, ct);
            var result = JsonSerializer.Deserialize<AssetLibResult>(json) ?? new AssetLibResult();

            // 写入缓存
            if (_db != null)
                await _db.SetHttpCacheAsync(url, json, "application/json");

            return result;
        }
        catch
        {
            return new AssetLibResult();
        }
    }

    /// <summary>
    /// 后台预缓存前几页数据
    /// </summary>
    public async Task PreCacheAsync(int pages = 3, CancellationToken ct = default)
    {
        for (var i = 0; i < pages; i++)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var url = BuildSearchUrl("", "", "updated", 0, i, 40, "any");
                // 检查缓存是否已存在
                if (_db != null)
                {
                    var cached = await _db.GetHttpCacheAsync(url, TimeSpan.FromMinutes(30));
                    if (cached?.body != null) continue; // 已缓存，跳过
                }

                var json = await _httpClient.GetByteArrayAsync(url, ct);
                if (_db != null)
                    await _db.SetHttpCacheAsync(url, json, "application/json");
            }
            catch { break; }
        }
    }

    private string BuildSearchUrl(string filter, string godotVersion, string sort, int category, int page, int maxResults, string type)
    {
        var url = $"{SiteUrl}/asset?type={type}&sort={sort}&max_results={maxResults}&page={page}";

        if (!string.IsNullOrWhiteSpace(filter))
            url += $"&filter={Uri.EscapeDataString(filter)}";

        if (!string.IsNullOrWhiteSpace(godotVersion))
            url += $"&godot_version={Uri.EscapeDataString(godotVersion)}";

        if (category > 0)
            url += $"&category={category}";

        return url;
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
    /// YAML 结构:
    /// - name: "4.6"
    ///   flavor: "stable"
    ///   releases:
    ///     - name: "rc2"    ← 嵌套的 release 子项
    ///       release_date: "..."
    /// </summary>
    private static List<RemoteGodotVersion> ParseVersionsYml(string yml)
    {
        var versions = new List<RemoteGodotVersion>();
        RemoteGodotVersion? current = null;
        var inReleases = false;

        foreach (var rawLine in yml.Split('\n'))
        {
            // 使用缩进区分顶层版本和嵌套 release
            var indent = rawLine.Length - rawLine.TrimStart().Length;
            var line = rawLine.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            // 顶层版本条目 (缩进 0-1)
            if (indent <= 1 && line.StartsWith("- name:"))
            {
                current = new RemoteGodotVersion
                {
                    Name = ExtractYamlValue(line, "- name:")
                };
                versions.Add(current);
                inReleases = false;
            }
            // flavor 字段
            else if (line.StartsWith("flavor:") && current != null && indent < 4)
            {
                current.Flavor = ExtractYamlValue(line, "flavor:");
            }
            // 进入 releases 块
            else if (line == "releases:" && current != null)
            {
                inReleases = true;
            }
            // 其他顶层字段 (release_date, release_notes, featured) → 退出 releases
            else if (indent <= 2 && !line.StartsWith("-") && !inReleases)
            {
                // skip other top-level fields
            }
            // 嵌套在 releases 下的 - name: "rc1" 条目
            else if (inReleases && indent >= 4 && line.StartsWith("- name:"))
            {
                var releaseName = ExtractYamlValue(line, "- name:");
                if (!string.IsNullOrEmpty(releaseName))
                    current?.Releases.Add(releaseName);
            }
            // 嵌套在 releases 下的简单 - "value" 条目 (旧格式)
            else if (inReleases && indent >= 4 && line.StartsWith("- ") && !line.StartsWith("- name:"))
            {
                var release = line[2..].Trim().Trim('"');
                if (!string.IsNullOrEmpty(release))
                    current?.Releases.Add(release);
            }
            // 嵌套属性跳过 (release_date, release_notes等)
            else if (inReleases && indent >= 6)
            {
                // skip nested properties of release entries
            }
        }

        return versions;
    }

    /// <summary>
    /// 从 YAML 行提取值: "key: value" → "value" (去掉引号)
    /// </summary>
    private static string ExtractYamlValue(string line, string key)
    {
        var val = line.Replace(key, "").Trim().Trim('"');
        return val;
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
