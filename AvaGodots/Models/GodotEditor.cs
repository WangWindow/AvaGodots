using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaGodots.Models;

/// <summary>
/// 本地 Godot 编辑器数据模型
/// 表示一个已安装的 Godot 编辑器实例
/// </summary>
public partial class GodotEditor : ObservableObject
{
    /// <summary>
    /// 编辑器可执行文件的完整路径（唯一标识）
    /// </summary>
    [ObservableProperty]
    private string _path = string.Empty;

    /// <summary>
    /// 编辑器显示名称（如 "Godot v4.3 stable"）
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 是否收藏
    /// </summary>
    [ObservableProperty]
    private bool _isFavorite;

    /// <summary>
    /// 标签列表（如 "stable", "4.x", "mono"）
    /// </summary>
    [ObservableProperty]
    private List<string> _tags = [];

    /// <summary>
    /// 额外启动参数
    /// </summary>
    [ObservableProperty]
    private List<string> _extraArguments = [];

    /// <summary>
    /// 版本提示（如 "v4.3-stable", "v4.3-stable-mono"）
    /// </summary>
    [ObservableProperty]
    private string _versionHint = string.Empty;

    /// <summary>
    /// 自定义命令列表
    /// </summary>
    [ObservableProperty]
    private List<CustomCommand> _customCommands = [];

    /// <summary>
    /// 编辑器文件是否存在
    /// </summary>
    public bool IsValid => System.IO.File.Exists(Path);

    /// <summary>
    /// 编辑器目录路径
    /// </summary>
    public string DirectoryPath => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;

    /// <summary>
    /// 是否为 Self-Contained 模式（编辑器目录包含 ._sc_ 或 _sc_ 文件）
    /// </summary>
    public bool IsSelfContained
    {
        get
        {
            var dir = DirectoryPath;
            if (string.IsNullOrEmpty(dir)) return false;
            return System.IO.File.Exists(System.IO.Path.Combine(dir, "._sc_")) ||
                   System.IO.File.Exists(System.IO.Path.Combine(dir, "_sc_"));
        }
    }

    /// <summary>
    /// 是否为 Mono/C# 版本
    /// </summary>
    public bool IsMono => Name.Contains("mono", System.StringComparison.OrdinalIgnoreCase) ||
                          VersionHint.Contains("mono", System.StringComparison.OrdinalIgnoreCase);
}
