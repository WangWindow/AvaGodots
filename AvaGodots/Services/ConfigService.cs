using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AvaGodots.Interfaces;
using AvaGodots.Models;

namespace AvaGodots.Services;

/// <summary>
/// 配置服务实现
/// 使用 JSON 格式存储在用户数据目录中
/// </summary>
public class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private const string ConfigFileName = "godots.json";

    /// <summary>
    /// 应用配置
    /// </summary>
    public AppConfig Config { get; private set; } = new();

    /// <summary>
    /// 获取应用数据目录路径
    /// </summary>
    public string GetAppDataPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var path = Path.Combine(appData, "AvaGodots");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 加载配置
    /// </summary>
    public async Task LoadAsync()
    {
        var configPath = Path.Combine(GetAppDataPath(), ConfigFileName);

        if (File.Exists(configPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(configPath);
                Config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            }
            catch (Exception)
            {
                Config = new AppConfig();
            }
        }
        else
        {
            Config = new AppConfig();
        }

        // 确保默认路径已设置
        EnsureDefaultPaths();
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    public async Task SaveAsync()
    {
        var configPath = Path.Combine(GetAppDataPath(), ConfigFileName);
        var json = JsonSerializer.Serialize(Config, JsonOptions);
        await File.WriteAllTextAsync(configPath, json);
    }

    /// <summary>
    /// 确保默认路径已初始化
    /// </summary>
    private void EnsureDefaultPaths()
    {
        var appDataPath = GetAppDataPath();

        if (string.IsNullOrEmpty(Config.VersionsPath))
            Config.VersionsPath = Path.Combine(appDataPath, "versions");

        if (string.IsNullOrEmpty(Config.DownloadsPath))
            Config.DownloadsPath = Path.Combine(appDataPath, "downloads");

        if (string.IsNullOrEmpty(Config.ProjectsConfigPath))
            Config.ProjectsConfigPath = Path.Combine(appDataPath, "projects.json");

        if (string.IsNullOrEmpty(Config.EditorsConfigPath))
            Config.EditorsConfigPath = Path.Combine(appDataPath, "editors.json");

        if (string.IsNullOrEmpty(Config.ProjectsPath))
            Config.ProjectsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents");

        // 创建必要的目录
        Directory.CreateDirectory(Config.VersionsPath);
        Directory.CreateDirectory(Config.DownloadsPath);
    }
}
