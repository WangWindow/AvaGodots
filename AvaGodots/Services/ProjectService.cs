using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AvaGodots.Interfaces;
using AvaGodots.Models;

namespace AvaGodots.Services;

/// <summary>
/// 项目管理服务实现
/// 管理 Godot 项目的 CRUD 操作、扫描、启动等
/// </summary>
public partial class ProjectService : IProjectService
{
    private readonly IConfigService _configService;
    private readonly IEditorService _editorService;
    private readonly IVsCodeIntegrationService _vsCodeService;
    private readonly List<GodotProject> _projects = [];

    /// <summary>
    /// 所有项目列表
    /// </summary>
    public IReadOnlyList<GodotProject> Projects => _projects.AsReadOnly();

    public ProjectService(IConfigService configService, IEditorService editorService, IVsCodeIntegrationService vsCodeService)
    {
        _configService = configService;
        _editorService = editorService;
        _vsCodeService = vsCodeService;
    }

    /// <summary>
    /// 加载项目列表
    /// </summary>
    public async Task LoadAsync()
    {
        _projects.Clear();
        var configPath = _configService.Config.ProjectsConfigPath;

        if (!File.Exists(configPath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            var savedProjects = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.ListSavedProjectData);

            if (savedProjects != null)
            {
                foreach (var saved in savedProjects)
                {
                    var project = new GodotProject
                    {
                        Path = saved.Path,
                        EditorPath = saved.EditorPath ?? string.Empty,
                        IsFavorite = saved.IsFavorite,
                        ShowEditWarning = saved.ShowEditWarning,
                        CustomCommands = saved.CustomCommands ?? []
                    };

                    // 从 project.godot 文件读取项目信息
                    await LoadProjectInfoFromFile(project);

                    // 获取绑定编辑器的名称
                    if (!string.IsNullOrEmpty(project.EditorPath))
                    {
                        var editor = _editorService.GetEditorByPath(project.EditorPath);
                        project.EditorName = editor?.Name ?? Path.GetFileName(project.EditorPath);
                    }

                    _projects.Add(project);
                    project.RefreshFileStatus();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载项目列表失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 保存项目列表
    /// </summary>
    public async Task SaveAsync()
    {
        var configPath = _configService.Config.ProjectsConfigPath;
        var savedProjects = _projects.Select(p => new SavedProjectData
        {
            Path = p.Path,
            EditorPath = p.EditorPath,
            IsFavorite = p.IsFavorite,
            ShowEditWarning = p.ShowEditWarning,
            CustomCommands = p.CustomCommands
        }).ToList();

        var json = JsonSerializer.Serialize(savedProjects, AppJsonSerializerContext.Default.ListSavedProjectData);
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(configPath, json);
    }

    /// <summary>
    /// 添加项目
    /// </summary>
    public async Task<GodotProject?> AddProjectAsync(string projectGodotPath, string? editorPath = null)
    {
        // 检查是否已存在
        if (_projects.Any(p => p.Path.Equals(projectGodotPath, StringComparison.OrdinalIgnoreCase)))
            return null;

        if (!File.Exists(projectGodotPath))
            return null;

        var project = new GodotProject
        {
            Path = projectGodotPath,
            EditorPath = editorPath ?? string.Empty
        };

        await LoadProjectInfoFromFile(project);

        if (!string.IsNullOrEmpty(project.EditorPath))
        {
            var editor = _editorService.GetEditorByPath(project.EditorPath);
            project.EditorName = editor?.Name ?? Path.GetFileName(project.EditorPath);
        }

        _projects.Add(project);
        project.RefreshFileStatus();
        await SaveAsync();
        return project;
    }

    /// <summary>
    /// 移除项目
    /// </summary>
    public async Task RemoveProjectAsync(string projectPath)
    {
        var project = _projects.FirstOrDefault(p => p.Path == projectPath);
        if (project != null)
        {
            _projects.Remove(project);
            await SaveAsync();
        }
    }

    /// <summary>
    /// 移除所有缺失项目
    /// </summary>
    public async Task RemoveMissingProjectsAsync()
    {
        var missing = _projects.Where(p => p.IsMissing).ToList();
        foreach (var project in missing)
        {
            _projects.Remove(project);
        }
        if (missing.Count > 0)
            await SaveAsync();
    }

    /// <summary>
    /// 扫描目录查找 project.godot 文件
    /// </summary>
    public Task<List<string>> ScanDirectoryAsync(string directory)
    {
        var results = new List<string>();
        if (!Directory.Exists(directory)) return Task.FromResult(results);

        try
        {
            var files = Directory.GetFiles(directory, "project.godot", SearchOption.AllDirectories);
            results.AddRange(files);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"扫描目录失败: {ex.Message}");
        }

        return Task.FromResult(results);
    }

    /// <summary>
    /// 创建新项目
    /// </summary>
    public async Task<GodotProject?> CreateProjectAsync(string name, string directory, string editorPath, int godotVersion = 4, string renderer = "Forward+", string versionControl = "Git", string editorVersionHint = "")
    {
        try
        {
            // 创建项目目录
            Directory.CreateDirectory(directory);

            // 从编辑器 VersionHint 提取实际版本号（如 "v4.6-stable" → "4.6"）
            var engineVersion = ExtractEngineVersion(editorVersionHint, godotVersion);

            // 创建 project.godot 文件
            var projectGodotPath = Path.Combine(directory, "project.godot");
            var content = godotVersion >= 4
                ? GenerateGodot4ProjectFile(name, renderer, engineVersion)
                : GenerateGodot3ProjectFile(name);

            await File.WriteAllTextAsync(projectGodotPath, content);

            // 创建 icon.svg（Godot logo）
            if (godotVersion >= 4)
            {
                var iconPath = Path.Combine(directory, "icon.svg");
                await File.WriteAllTextAsync(iconPath, GodotIconSvg);
            }

            // 版本控制初始化
            if (versionControl == "Git")
            {
                var gitignorePath = Path.Combine(directory, ".gitignore");
                var gitattrsPath = Path.Combine(directory, ".gitattributes");
                await File.WriteAllTextAsync(gitignorePath, "# Godot 4+ specific ignores\n.godot/\n");
                await File.WriteAllTextAsync(gitattrsPath, "# Normalize EOL for all files that Git considers text files.\n* text=auto eol=lf\n");
            }

            // VS Code 集成
            if (_configService.Config.EnableVsCodeIntegration && !string.IsNullOrEmpty(editorPath))
            {
                await _vsCodeService.SetupVsCodeAsync(directory, editorPath);
            }

            // 添加到项目列表
            return await AddProjectAsync(projectGodotPath, editorPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"创建项目失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 用编辑器打开项目（编辑模式）
    /// </summary>
    public async Task EditProjectAsync(GodotProject project)
    {
        if (string.IsNullOrEmpty(project.EditorPath) || !File.Exists(project.EditorPath))
            return;

        // 更新 VS Code 配置中的 Godot 路径
        if (_configService.Config.EnableVsCodeIntegration)
        {
            await _vsCodeService.UpdateGodotPathAsync(project.DirectoryPath, project.EditorPath);
        }

        var args = $"--editor --path \"{project.DirectoryPath}\"";
        LaunchProcess(project.EditorPath, args);
    }

    /// <summary>
    /// 运行项目
    /// </summary>
    public Task RunProjectAsync(GodotProject project)
    {
        if (string.IsNullOrEmpty(project.EditorPath) || !File.Exists(project.EditorPath))
            return Task.CompletedTask;

        var args = $"--path \"{project.DirectoryPath}\"";
        LaunchProcess(project.EditorPath, args);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 读取 project.godot 文件中的项目信息
    /// </summary>
    public async Task<GodotProject?> ReadProjectInfoAsync(string projectGodotPath)
    {
        if (!File.Exists(projectGodotPath)) return null;
        var project = new GodotProject { Path = projectGodotPath };
        await LoadProjectInfoFromFile(project);
        return project;
    }

    /// <summary>
    /// 从 project.godot 文件加载项目元数据
    /// </summary>
    private async Task LoadProjectInfoFromFile(GodotProject project)
    {
        if (!File.Exists(project.Path))
        {
            project.Name = Path.GetFileName(Path.GetDirectoryName(project.Path)) ?? "未知项目";
            return;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(project.Path);
            var currentSection = "";

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // 解析节标题
                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    currentSection = trimmed[1..^1];
                    continue;
                }

                // 解析键值对
                if (trimmed.Contains('='))
                {
                    var eqIndex = trimmed.IndexOf('=');
                    var key = trimmed[..eqIndex].Trim();
                    var value = trimmed[(eqIndex + 1)..].Trim();

                    // 移除引号
                    if (value.StartsWith('"') && value.EndsWith('"'))
                        value = value[1..^1];

                    switch (currentSection)
                    {
                        case "application" when key == "config/name":
                            project.Name = value;
                            break;
                        case "application" when key == "config/icon":
                            project.IconPath = value;
                            break;
                        case "application" when key == "config/features":
                            project.Features = ParseGodotArray(value);
                            break;
                        case "application" when key == "config/tags":
                            project.Tags = ParseGodotArray(value);
                            break;
                        case "" when key == "config_version":
                            if (int.TryParse(value, out var version))
                                project.ConfigVersion = version;
                            break;
                        case "godots" when key == "version_hint":
                            project.VersionHint = value;
                            break;
                        case "dotnet":
                            project.HasMono = true;
                            break;
                    }
                }
            }

            // 如果名称为空，使用目录名
            if (string.IsNullOrEmpty(project.Name))
                project.Name = Path.GetFileName(Path.GetDirectoryName(project.Path)) ?? "未知项目";

            // 获取最后修改时间
            project.LastModified = File.GetLastWriteTime(project.Path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"读取项目文件失败: {ex.Message}");
            project.Name = Path.GetFileName(Path.GetDirectoryName(project.Path)) ?? "未知项目";
        }
    }

    /// <summary>
    /// 解析 Godot 数组格式（如 PackedStringArray("4.3", "Mobile")）
    /// </summary>
    private static List<string> ParseGodotArray(string value)
    {
        var result = new List<string>();
        var matches = GodotArrayRegex().Matches(value);
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
                result.Add(match.Groups[1].Value);
        }
        return result;
    }

    [GeneratedRegex("\"([^\"]+)\"")]
    private static partial Regex GodotArrayRegex();

    /// <summary>
    /// 生成 Godot 4 项目文件内容
    /// </summary>
    /// <summary>
    /// 从编辑器 VersionHint 中提取引擎版本号（如 "v4.6-stable" → "4.6", "v4.3-stable-mono" → "4.3"）
    /// </summary>
    private static string ExtractEngineVersion(string versionHint, int godotMajor)
    {
        if (string.IsNullOrWhiteSpace(versionHint))
            return godotMajor >= 4 ? "4.3" : "3.6";

        // 去掉前缀 v，取到第一个 '-' 之前的部分
        var ver = versionHint.TrimStart('v').Split('-')[0].Trim();
        if (!string.IsNullOrEmpty(ver) && char.IsDigit(ver[0]))
            return ver;

        return godotMajor >= 4 ? "4.3" : "3.6";
    }

    private static string GenerateGodot4ProjectFile(string name, string renderer = "Forward+", string engineVersion = "4.3")
    {
        var renderMethod = renderer switch
        {
            "Mobile" => "mobile",
            "Compatibility" => "gl_compatibility",
            _ => "forward_plus"
        };

        var featureLabel = renderer switch
        {
            "Mobile" => "Mobile",
            "Compatibility" => "GL Compatibility",
            _ => "Forward Plus"
        };

        var extra = renderer == "Compatibility"
            ? "\nrenderer/rendering_method.mobile=\"gl_compatibility\""
            : "";

        return $$"""
            ; Engine configuration file.
            ; It's best edited using the editor UI and not directly,
            ; since the parameters that go here are not all obvious.
            ;
            ; Format:
            ;   [section] ; section goes between []
            ;   param=value ; assign values to parameters

            config_version=5

            [application]

            config/name="{{name}}"
            config/features=PackedStringArray("{{engineVersion}}", "{{featureLabel}}")
            config/icon="res://icon.svg"

            [rendering]

            renderer/rendering_method="{{renderMethod}}"{{extra}}
            """;
    }

    private const string GodotIconSvg = """<svg height="128" width="128" xmlns="http://www.w3.org/2000/svg"><rect x="2" y="2" width="124" height="124" rx="14" fill="#478cbf" stroke="#fff" stroke-width="4"/><text x="50%" y="58%" dominant-baseline="middle" text-anchor="middle" font-family="sans-serif" font-size="48" fill="#fff">G</text></svg>""";

    /// <summary>
    /// 生成 Godot 3 项目文件内容
    /// </summary>
    private static string GenerateGodot3ProjectFile(string name)
    {
        return $"""
            ; Engine configuration file.
            ; It's best edited using the editor UI and not directly,
            ; since the parameters that go here are not all obvious.
            ;
            ; Format:
            ;   [section] ; section goes between []
            ;   param=value ; assign values to parameters

            config_version=3

            [application]

            config/name="{name}"
            config/icon="res://icon.png"
            """;
    }

    /// <summary>
    /// 启动外部进程
    /// </summary>
    private static void LaunchProcess(string path, string args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = args,
                UseShellExecute = false,
            };

            // Linux 下确保文件有执行权限
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    var chmodInfo = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{path}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(chmodInfo)?.WaitForExit(3000);
                }
                catch { /* 忽略权限设置失败 */ }
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"启动进程失败: {ex.Message}");
        }
    }

}
