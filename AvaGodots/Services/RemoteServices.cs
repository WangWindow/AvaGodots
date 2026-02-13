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
            var config = await _httpClient.GetFromJsonAsync(url, AppJsonSerializerContext.Default.AssetLibConfig, ct);
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
                    var cachedResult = JsonSerializer.Deserialize(cached.Value.body, AppJsonSerializerContext.Default.AssetLibResult);
                    if (cachedResult != null) return cachedResult;
                }
                catch { /* 缓存损坏,继续网络请求 */ }
            }
        }

        try
        {
            var json = await _httpClient.GetByteArrayAsync(url, ct);
            var result = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.AssetLibResult) ?? new AssetLibResult();

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
            return await _httpClient.GetFromJsonAsync(url, AppJsonSerializerContext.Default.AssetLibItem, ct);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// 远程编辑器版本源 — 从 GitHub 获取 Godot 版本列表和发布资产
/// </summary>
public class RemoteEditorService
{
    private readonly HttpClient _httpClient;
    private DatabaseService? _db;
    private List<RemoteGodotVersion>? _cachedVersions;
    private readonly Dictionary<string, List<GithubReleaseAsset>> _assetsCache = new();
    private static readonly string[] Repos = ["godotengine/godot", "godotengine/godot-builds"];

    public RemoteEditorService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "AvaGodots/1.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>注入数据库服务（用于持久化缓存）</summary>
    public void SetDatabase(DatabaseService db) => _db = db;

    /// <summary>获取所有远程 Godot 版本（优先使用缓存）</summary>
    public async Task<List<RemoteGodotVersion>> GetVersionsAsync(CancellationToken ct = default)
    {
        if (_cachedVersions != null) return _cachedVersions;

        if (_db != null)
        {
            var cached = await _db.GetVersionsCacheAsync("versions_yml", TimeSpan.FromHours(24));
            if (cached != null) { _cachedVersions = ParseVersionsYml(cached); return _cachedVersions; }
        }

        try
        {
            const string url = "https://raw.githubusercontent.com/godotengine/godot-website/master/_data/versions.yml";
            var yml = await _httpClient.GetStringAsync(url, ct);
            if (_db != null) await _db.SetVersionsCacheAsync("versions_yml", yml);
            _cachedVersions = ParseVersionsYml(yml);
            return _cachedVersions;
        }
        catch { return []; }
    }

    /// <summary>获取指定 tag 的发布资产（依次尝试 godotengine/godot 和 godotengine/godot-builds）</summary>
    public async Task<List<GithubReleaseAsset>> GetReleaseAssetsAsync(string tag, CancellationToken ct = default)
    {
        if (_assetsCache.TryGetValue(tag, out var mem)) return mem;

        if (_db != null)
        {
            var hit = await _db.GetHttpCacheAsync($"assets:{tag}", TimeSpan.FromHours(24));
            if (hit?.body != null)
            {
                try
                {
                    var list = JsonSerializer.Deserialize(hit.Value.body, AppJsonSerializerContext.Default.ListGithubReleaseAsset);
                    if (list is { Count: > 0 }) { _assetsCache[tag] = list; return list; }
                }
                catch { /* corrupt cache */ }
            }
        }

        foreach (var repo in Repos)
        {
            try
            {
                var url = $"https://api.github.com/repos/{repo}/releases/tags/{tag}";
                var release = await _httpClient.GetFromJsonAsync(url, AppJsonSerializerContext.Default.GithubRelease, ct);
                if (release?.Assets is { Count: > 0 })
                {
                    _assetsCache[tag] = release.Assets;
                    if (_db != null)
                    {
                        var json = JsonSerializer.SerializeToUtf8Bytes(release.Assets, AppJsonSerializerContext.Default.ListGithubReleaseAsset);
                        await _db.SetHttpCacheAsync($"assets:{tag}", json, "application/json");
                    }
                    return release.Assets;
                }
            }
            catch { /* try next repo */ }
        }

        return [];
    }

    // ========== 平台匹配 ==========

    /// <summary>是否为桌面编辑器或导出模板（排除 Android / 调试符号 / 源码包等）</summary>
    public static bool IsDesktopAsset(string fileName)
    {
        var n = fileName.ToLowerInvariant();
        if (!n.EndsWith(".zip") && !n.EndsWith(".tpz")) return false;
        if (n.Contains("android")) return false;
        if (n.Contains("debug_symbol") || n.Contains("native_debug")) return false;
        if (n.StartsWith("godot-lib")) return false;
        return true;
    }

    /// <summary>是否为导出模板</summary>
    public static bool IsExportTemplate(string fileName) =>
        fileName.Contains("export_templates", StringComparison.OrdinalIgnoreCase);

    /// <summary>是否为 Mono/.NET 版</summary>
    public static bool IsMono(string fileName) =>
        fileName.Contains("mono", StringComparison.OrdinalIgnoreCase);

    /// <summary>是否匹配当前操作系统和 CPU 架构</summary>
    public static bool IsForCurrentPlatform(string fileName)
    {
        var n = fileName.ToLowerInvariant();
        if (IsExportTemplate(n)) return true;

        bool osOk;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            osOk = n.Contains("linux") || n.Contains("x11");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            osOk = n.Contains("win");
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            osOk = n.Contains("macos") || n.Contains("osx");
        else return false;

        if (!osOk) return false;
        if (n.Contains("universal") || n.Contains(".fat")) return true;

        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => MatchesX64(n),
            Architecture.Arm64 => n.Contains("arm64") || n.Contains("aarch64"),
            Architecture.X86 => MatchesX86(n),
            Architecture.Arm => n.Contains("arm32"),
            _ => true
        };
    }

    private static bool MatchesX64(string n) =>
        !n.Contains("arm") && (n.Contains("x86_64") || n.Contains("win64") || Regex.IsMatch(n, @"[._]64[._]"));

    private static bool MatchesX86(string n) =>
        !n.Contains("arm") && (n.Contains("x86_32") || n.Contains("win32") || Regex.IsMatch(n, @"[._]32[._]"));

    // ========== YAML 解析 ==========

    private static List<RemoteGodotVersion> ParseVersionsYml(string yml)
    {
        var versions = new List<RemoteGodotVersion>();
        RemoteGodotVersion? cur = null;
        RemoteGodotRelease? curRel = null;
        var inReleases = false;

        foreach (var raw in yml.Split('\n'))
        {
            var indent = raw.Length - raw.TrimStart().Length;
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

            if (indent <= 1 && line.StartsWith("- name:"))
            {
                cur = new RemoteGodotVersion { Name = YamlVal(line) };
                versions.Add(cur);
                inReleases = false;
                curRel = null;
            }
            else if (line.StartsWith("flavor:") && cur != null && indent < 4)
                cur.Flavor = YamlVal(line);
            else if (line == "releases:" && cur != null)
                inReleases = true;
            else if (inReleases && indent >= 4 && line.StartsWith("- name:"))
            {
                curRel = new RemoteGodotRelease { Name = YamlVal(line) };
                cur?.Releases.Add(curRel);
            }
            else if (inReleases && indent >= 6 && line.StartsWith("release_version:") && curRel != null)
                curRel.ReleaseVersion = YamlVal(line);
        }

        return versions;
    }

    private static string YamlVal(string line)
    {
        var idx = line.IndexOf(':');
        return idx >= 0 ? line[(idx + 1)..].Trim().Trim('"') : line.Trim().Trim('"');
    }
}

/// <summary>版本信息</summary>
public class RemoteGodotVersion
{
    public string Name { get; set; } = "";
    public string Flavor { get; set; } = "stable";
    public List<RemoteGodotRelease> Releases { get; set; } = [];
}

/// <summary>版本内的子发布</summary>
public class RemoteGodotRelease
{
    public string Name { get; set; } = "";
    /// <summary>可选的版本号覆盖（如 3.3 的 rc 实际对应 3.2.4）</summary>
    public string? ReleaseVersion { get; set; }
}
