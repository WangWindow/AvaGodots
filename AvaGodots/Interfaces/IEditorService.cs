using System.Collections.Generic;
using System.Threading.Tasks;
using AvaGodots.Models;

namespace AvaGodots.Interfaces;

/// <summary>
/// 编辑器管理服务接口
/// 负责本地 Godot 编辑器的管理
/// </summary>
public interface IEditorService
{
    /// <summary>
    /// 获取所有本地编辑器
    /// </summary>
    IReadOnlyList<GodotEditor> Editors { get; }

    /// <summary>
    /// 加载所有编辑器
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// 保存所有编辑器
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// 导入编辑器
    /// </summary>
    /// <param name="name">编辑器名称</param>
    /// <param name="executablePath">可执行文件路径</param>
    Task<GodotEditor?> ImportEditorAsync(string name, string executablePath);

    /// <summary>
    /// 移除编辑器
    /// </summary>
    Task RemoveEditorAsync(string editorPath);

    /// <summary>
    /// 移除所有缺失的编辑器
    /// </summary>
    Task RemoveMissingEditorsAsync();

    /// <summary>
    /// 扫描目录查找编辑器
    /// </summary>
    /// <param name="directory">要扫描的目录</param>
    Task<List<string>> ScanDirectoryAsync(string directory);

    /// <summary>
    /// 运行编辑器（不打开项目）
    /// </summary>
    Task RunEditorAsync(GodotEditor editor);

    /// <summary>
    /// 根据路径获取编辑器
    /// </summary>
    GodotEditor? GetEditorByPath(string path);

    /// <summary>
    /// 安装编辑器（从下载的 zip 文件）
    /// </summary>
    /// <param name="zipPath">zip 文件路径</param>
    /// <param name="name">编辑器名称</param>
    /// <param name="targetDirectory">目标安装目录</param>
    Task<GodotEditor?> InstallEditorAsync(string zipPath, string name, string targetDirectory);
}
