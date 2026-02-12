using System.Collections.Generic;

namespace AvaGodots.Models;

/// <summary>
/// 自定义命令模型
/// 支持变量替换：{{EDITOR_PATH}}, {{EDITOR_DIR}}, {{PROJECT_DIR}}
/// </summary>
public class CustomCommand
{
    /// <summary>
    /// 命令名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 图标名称（FluentIcons 图标）
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 可执行文件路径（支持变量替换）
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// 命令参数列表（支持变量替换）
    /// </summary>
    public List<string> Arguments { get; set; } = [];

    /// <summary>
    /// 是否为本地命令（项目/编辑器级别）
    /// </summary>
    public bool IsLocal { get; set; } = true;

    /// <summary>
    /// 替换命令中的变量
    /// </summary>
    /// <param name="editorPath">编辑器路径</param>
    /// <param name="projectDir">项目目录</param>
    /// <returns>替换后的可执行路径和参数</returns>
    public (string Path, List<string> Args) Resolve(string editorPath, string projectDir)
    {
        var editorDir = System.IO.Path.GetDirectoryName(editorPath) ?? string.Empty;

        var resolvedPath = ExecutablePath
            .Replace("{{EDITOR_PATH}}", editorPath)
            .Replace("{{EDITOR_DIR}}", editorDir)
            .Replace("{{PROJECT_DIR}}", projectDir);

        var resolvedArgs = new List<string>();
        foreach (var arg in Arguments)
        {
            resolvedArgs.Add(arg
                .Replace("{{EDITOR_PATH}}", editorPath)
                .Replace("{{EDITOR_DIR}}", editorDir)
                .Replace("{{PROJECT_DIR}}", projectDir));
        }

        return (resolvedPath, resolvedArgs);
    }
}
