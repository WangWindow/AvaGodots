using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AvaGodots.Interfaces;
using AvaGodots.Models;

namespace AvaGodots.Services;

/// <summary>
/// 编辑器管理服务实现
/// 管理本地 Godot 编辑器的导入、扫描、启动等
/// </summary>
public partial class EditorService : IEditorService
{
    private readonly IConfigService _configService;
    private readonly List<GodotEditor> _editors = [];

    /// <summary>
    /// 所有本地编辑器
    /// </summary>
    public IReadOnlyList<GodotEditor> Editors => _editors.AsReadOnly();

    public EditorService(IConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// 加载编辑器列表
    /// </summary>
    public async Task LoadAsync()
    {
        _editors.Clear();
        var configPath = _configService.Config.EditorsConfigPath;

        if (!File.Exists(configPath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(configPath);
            var savedEditors = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.ListSavedEditorData);
            if (savedEditors != null)
            {
                foreach (var saved in savedEditors)
                {
                    var editor = new GodotEditor
                    {
                        Path = saved.Path,
                        Name = saved.Name,
                        IsFavorite = saved.IsFavorite,
                        Tags = saved.Tags ?? [],
                        ExtraArguments = saved.ExtraArguments ?? [],
                        VersionHint = saved.VersionHint,
                        CustomCommands = saved.CustomCommands ?? []
                    };
                    editor.RefreshFileStatus();
                    _editors.Add(editor);
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error("EditorService", $"加载编辑器列表失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 保存编辑器列表
    /// </summary>
    public async Task SaveAsync()
    {
        var configPath = _configService.Config.EditorsConfigPath;
        var savedEditors = _editors.Select(e => new SavedEditorData
        {
            Path = e.Path,
            Name = e.Name,
            IsFavorite = e.IsFavorite,
            Tags = e.Tags,
            ExtraArguments = e.ExtraArguments,
            VersionHint = e.VersionHint,
            CustomCommands = e.CustomCommands
        }).ToList();
        var json = JsonSerializer.Serialize(savedEditors, AppJsonSerializerContext.Default.ListSavedEditorData);
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(configPath, json);
    }

    /// <summary>
    /// 导入编辑器
    /// </summary>
    public async Task<GodotEditor?> ImportEditorAsync(string name, string executablePath)
    {
        if (!File.Exists(executablePath)) return null;

        // 检查是否已存在
        if (_editors.Any(e => e.Path.Equals(executablePath, StringComparison.OrdinalIgnoreCase)))
            return null;

        var editor = new GodotEditor
        {
            Path = executablePath,
            Name = name,
            VersionHint = ExtractVersionHint(name)
        };

        // Linux 下设置执行权限
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            SetExecutePermission(executablePath);
        }

        _editors.Add(editor);
        editor.RefreshFileStatus();
        await SaveAsync();
        return editor;
    }

    /// <summary>
    /// 移除编辑器
    /// </summary>
    public async Task RemoveEditorAsync(string editorPath)
    {
        var editor = _editors.FirstOrDefault(e => e.Path == editorPath);
        if (editor != null)
        {
            _editors.Remove(editor);
            await SaveAsync();
        }
    }

    /// <summary>
    /// 移除所有缺失的编辑器
    /// </summary>
    public async Task RemoveMissingEditorsAsync()
    {
        var missing = _editors.Where(e => !e.IsValid).ToList();
        foreach (var editor in missing)
        {
            _editors.Remove(editor);
        }
        if (missing.Count > 0)
            await SaveAsync();
    }

    /// <summary>
    /// 扫描目录查找 Godot 编辑器可执行文件
    /// </summary>
    public Task<List<string>> ScanDirectoryAsync(string directory)
    {
        var results = new List<string>();
        if (!Directory.Exists(directory)) return Task.FromResult(results);

        try
        {
            // 查找符合 Godot 编辑器命名模式的文件
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file).ToLowerInvariant();
                if (IsGodotExecutable(fileName))
                {
                    results.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"扫描编辑器目录失败: {ex.Message}");
        }

        return Task.FromResult(results);
    }

    /// <summary>
    /// 运行编辑器（无项目）
    /// </summary>
    public Task RunEditorAsync(GodotEditor editor)
    {
        if (!editor.IsValid) return Task.CompletedTask;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = editor.Path,
                Arguments = string.Join(" ", editor.ExtraArguments),
                UseShellExecute = false,
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"启动编辑器失败: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 根据路径获取编辑器
    /// </summary>
    public GodotEditor? GetEditorByPath(string path)
    {
        return _editors.FirstOrDefault(e => e.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 从 zip 安装编辑器
    /// </summary>
    public async Task<GodotEditor?> InstallEditorAsync(string zipPath, string name, string targetDirectory)
    {
        try
        {
            Directory.CreateDirectory(targetDirectory);
            ZipFile.ExtractToDirectory(zipPath, targetDirectory, overwriteFiles: true);

            // 查找解压后的可执行文件
            var executables = await ScanDirectoryAsync(targetDirectory);
            var execPath = executables.FirstOrDefault();

            if (execPath == null) return null;

            return await ImportEditorAsync(name, execPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"安装编辑器失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 判断文件名是否为 Godot 可执行文件
    /// </summary>
    private static bool IsGodotExecutable(string fileName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return fileName.Contains("godot") && fileName.EndsWith(".exe") &&
                   !fileName.Contains("console");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return fileName.Contains("godot") &&
                   (fileName.EndsWith(".x86_64") || fileName.EndsWith(".64") ||
                    (!fileName.Contains('.') && fileName.StartsWith("godot")));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return fileName.Contains("godot") &&
                   (fileName.EndsWith(".app") || fileName.EndsWith(".universal"));
        }

        return false;
    }

    /// <summary>
    /// 从名称提取版本提示
    /// 格式: vX.Y.Z-stage[-mono]
    /// </summary>
    private static string ExtractVersionHint(string name)
    {
        var match = VersionHintRegex().Match(name);
        return match.Success ? match.Value : string.Empty;
    }

    [GeneratedRegex(@"v?\d+\.\d+(?:\.\d+)?[-.](?:stable|beta|rc|alpha|dev)\d*(?:[-.]mono)?", RegexOptions.IgnoreCase)]
    private static partial Regex VersionHintRegex();

    /// <summary>
    /// 设置 Linux 文件执行权限
    /// </summary>
    private static void SetExecutePermission(string path)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(startInfo)?.WaitForExit(3000);
        }
        catch { /* 忽略权限设置失败 */ }
    }
}
