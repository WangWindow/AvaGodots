using System.Collections.Generic;
using System.Threading.Tasks;
using AvaGodots.Models;

namespace AvaGodots.Interfaces;

/// <summary>
/// 项目管理服务接口
/// 负责 Godot 项目的 CRUD 操作
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// 获取所有项目列表
    /// </summary>
    IReadOnlyList<GodotProject> Projects { get; }

    /// <summary>
    /// 加载所有项目
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// 保存所有项目
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// 添加项目（通过 project.godot 路径）
    /// </summary>
    /// <param name="projectGodotPath">project.godot 文件路径</param>
    /// <param name="editorPath">绑定的编辑器路径（可选）</param>
    Task<GodotProject?> AddProjectAsync(string projectGodotPath, string? editorPath = null);

    /// <summary>
    /// 移除项目
    /// </summary>
    Task RemoveProjectAsync(string projectPath);

    /// <summary>
    /// 移除所有缺失的项目
    /// </summary>
    Task RemoveMissingProjectsAsync();

    /// <summary>
    /// 扫描目录查找项目
    /// </summary>
    /// <param name="directory">要扫描的目录</param>
    Task<List<string>> ScanDirectoryAsync(string directory);

    /// <summary>
    /// 创建新项目
    /// </summary>
    /// <param name="name">项目名称</param>
    /// <param name="directory">项目目录</param>
    /// <param name="editorPath">编辑器路径</param>
    /// <param name="godotVersion">Godot 版本（3 或 4）</param>
    /// <param name="editorVersionHint">编辑器版本提示（如 "v4.6-stable"），用于生成 project.godot 中的版本号</param>
    Task<GodotProject?> CreateProjectAsync(string name, string directory, string editorPath, int godotVersion = 4, string renderer = "Forward+", string versionControl = "Git", string editorVersionHint = "");

    /// <summary>
    /// 用编辑器打开项目
    /// </summary>
    Task EditProjectAsync(GodotProject project);

    /// <summary>
    /// 运行项目
    /// </summary>
    Task RunProjectAsync(GodotProject project);

    /// <summary>
    /// 读取 project.godot 文件获取项目元信息
    /// </summary>
    Task<GodotProject?> ReadProjectInfoAsync(string projectGodotPath);
}
