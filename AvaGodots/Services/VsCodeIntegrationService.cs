using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AvaGodots.Interfaces;

namespace AvaGodots.Services;

/// <summary>
/// VS Code 集成服务实现
/// 在项目目录中创建/更新 .vscode 配置，写入 Godot 编辑器路径
/// </summary>
public class VsCodeIntegrationService : IVsCodeIntegrationService
{
    private const string VsCodeDirName = ".vscode";
    private const string SettingsFileName = "settings.json";
    private const string LaunchFileName = "launch.json";
    private const string TasksFileName = "tasks.json";

    /// <summary>
    /// Godot 路径在 settings.json 中的键名
    /// </summary>
    private const string GodotPathKey = "godotTools.editorPath.godot4";
    private const string GodotPathKey3 = "godotTools.editorPath.godot3";

    /// <summary>
    /// 为项目创建完整的 .vscode 配置
    /// </summary>
    public async Task SetupVsCodeAsync(string projectDir, string godotEditorPath)
    {
        var vscodePath = Path.Combine(projectDir, VsCodeDirName);
        Directory.CreateDirectory(vscodePath);

        // 创建 settings.json
        await CreateSettingsAsync(vscodePath, godotEditorPath);

        // 创建 launch.json（用于 GDScript 调试）
        await CreateLaunchJsonAsync(vscodePath);

        // 创建 tasks.json
        await CreateTasksJsonAsync(vscodePath);
    }

    /// <summary>
    /// 更新 VS Code settings.json 中的 Godot 路径
    /// </summary>
    public async Task UpdateGodotPathAsync(string projectDir, string godotEditorPath)
    {
        var vscodePath = Path.Combine(projectDir, VsCodeDirName);
        var settingsPath = Path.Combine(vscodePath, SettingsFileName);

        if (!Directory.Exists(vscodePath))
        {
            // 如果 .vscode 目录不存在，创建完整配置
            await SetupVsCodeAsync(projectDir, godotEditorPath);
            return;
        }

        if (!File.Exists(settingsPath))
        {
            await CreateSettingsAsync(vscodePath, godotEditorPath);
            return;
        }

        try
        {
            // 读取现有 settings.json 并更新 Godot 路径
            var json = await File.ReadAllTextAsync(settingsPath);
            var settingsNode = JsonNode.Parse(json) as JsonObject ?? new JsonObject();

            // 更新 Godot 4 路径
            settingsNode[GodotPathKey] = godotEditorPath;

            var options = new JsonSerializerOptions { WriteIndented = true };
            var updatedJson = settingsNode.ToJsonString(options);
            await File.WriteAllTextAsync(settingsPath, updatedJson);
        }
        catch (Exception)
        {
            // JSON 解析失败时重新创建
            await CreateSettingsAsync(vscodePath, godotEditorPath);
        }
    }

    /// <summary>
    /// 检查项目是否已有 VS Code 配置
    /// </summary>
    public bool HasVsCodeConfig(string projectDir)
    {
        var vscodePath = Path.Combine(projectDir, VsCodeDirName);
        return Directory.Exists(vscodePath) &&
               File.Exists(Path.Combine(vscodePath, SettingsFileName));
    }

    /// <summary>
    /// 创建 settings.json
    /// </summary>
    private static async Task CreateSettingsAsync(string vscodePath, string godotEditorPath)
    {
        var settings = new JsonObject
        {
            // Godot 编辑器路径配置（godot-tools 扩展使用）
            [GodotPathKey] = godotEditorPath,

            // 文件关联
            ["files.associations"] = new JsonObject
            {
                ["*.gd"] = "gdscript",
                ["*.tscn"] = "godot-resource",
                ["*.tres"] = "godot-resource",
                ["*.godot"] = "ini"
            },

            // 排除 .godot 缓存目录
            ["files.exclude"] = new JsonObject
            {
                ["**/.godot"] = true
            },

            // 搜索排除
            ["search.exclude"] = new JsonObject
            {
                ["**/.godot"] = true,
                ["**/addons/**/build"] = true
            }
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = settings.ToJsonString(options);
        await File.WriteAllTextAsync(Path.Combine(vscodePath, SettingsFileName), json);
    }

    /// <summary>
    /// 创建 launch.json（GDScript 调试配置）
    /// </summary>
    private static async Task CreateLaunchJsonAsync(string vscodePath)
    {
        var launch = new JsonObject
        {
            ["version"] = "0.2.0",
            ["configurations"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "GDScript Godot",
                    ["type"] = "godot",
                    ["request"] = "launch",
                    ["project"] = "${workspaceFolder}",
                    ["port"] = 6007,
                    ["debugServer"] = 6006
                }
            }
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = launch.ToJsonString(options);
        await File.WriteAllTextAsync(Path.Combine(vscodePath, LaunchFileName), json);
    }

    /// <summary>
    /// 创建 tasks.json
    /// </summary>
    private static async Task CreateTasksJsonAsync(string vscodePath)
    {
        var tasks = new JsonObject
        {
            ["version"] = "2.0.0",
            ["tasks"] = new JsonArray
            {
                new JsonObject
                {
                    ["label"] = "godot-run",
                    ["type"] = "shell",
                    ["command"] = "${config:godotTools.editorPath.godot4}",
                    ["args"] = new JsonArray { "--path", "${workspaceFolder}" },
                    ["problemMatcher"] = new JsonArray(),
                    ["group"] = new JsonObject
                    {
                        ["kind"] = "build",
                        ["isDefault"] = true
                    }
                }
            }
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = tasks.ToJsonString(options);
        await File.WriteAllTextAsync(Path.Combine(vscodePath, TasksFileName), json);
    }
}
