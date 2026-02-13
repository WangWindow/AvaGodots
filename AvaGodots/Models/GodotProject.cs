using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaGodots.Models;

/// <summary>
/// Godot 项目数据模型
/// 表示一个 Godot 引擎项目，包含路径、名称、绑定的编辑器等信息
/// </summary>
public partial class GodotProject : ObservableObject
{
    /// <summary>
    /// 项目 project.godot 文件的完整路径（唯一标识）
    /// </summary>
    [ObservableProperty]
    private string _path = string.Empty;

    /// <summary>
    /// 项目名称（从 project.godot 读取）
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 项目图标路径（从 project.godot 读取）
    /// </summary>
    [ObservableProperty]
    private string _iconPath = string.Empty;

    /// <summary>
    /// 绑定的编辑器可执行文件路径
    /// </summary>
    [ObservableProperty]
    private string _editorPath = string.Empty;

    /// <summary>
    /// 绑定的编辑器显示名称（派生属性）
    /// </summary>
    [ObservableProperty]
    private string _editorName = string.Empty;

    /// <summary>
    /// 是否收藏
    /// </summary>
    [ObservableProperty]
    private bool _isFavorite;

    /// <summary>
    /// 标签列表
    /// </summary>
    [ObservableProperty]
    private List<string> _tags = [];

    /// <summary>
    /// 特性列表（从 project.godot 读取）
    /// </summary>
    [ObservableProperty]
    private List<string> _features = [];

    /// <summary>
    /// 版本提示（如 "v4.3-stable"）
    /// </summary>
    [ObservableProperty]
    private string _versionHint = string.Empty;

    /// <summary>
    /// 最后修改时间
    /// </summary>
    [ObservableProperty]
    private DateTime _lastModified = DateTime.MinValue;

    /// <summary>
    /// 自定义命令列表
    /// </summary>
    [ObservableProperty]
    private List<CustomCommand> _customCommands = [];

    /// <summary>
    /// 是否显示编辑警告
    /// </summary>
    [ObservableProperty]
    private bool _showEditWarning = true;

    /// <summary>
    /// 项目配置版本（3 或 5，对应 Godot 3.x 或 4.x）
    /// </summary>
    [ObservableProperty]
    private int _configVersion;

    /// <summary>
    /// 是否包含 Mono/C# 支持
    /// </summary>
    [ObservableProperty]
    private bool _hasMono;

    /// <summary>
    /// 项目文件是否存在（缓存值，调用 RefreshFileStatus() 更新）
    /// </summary>
    [ObservableProperty]
    private bool _isMissing;

    public double ListOpacity => IsMissing ? 0.5 : 1.0;

    /// <summary>
    /// 绑定的编辑器是否有效（缓存值，调用 RefreshFileStatus() 更新）
    /// </summary>
    [ObservableProperty]
    private bool _hasInvalidEditor;

    /// <summary>
    /// 项目目录路径
    /// </summary>
    public string DirectoryPath => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;

    /// <summary>
    /// 刷新文件系统相关的缓存状态
    /// </summary>
    public void RefreshFileStatus()
    {
        IsMissing = !System.IO.File.Exists(Path);
        HasInvalidEditor = !string.IsNullOrEmpty(EditorPath) && !System.IO.File.Exists(EditorPath);
        OnPropertyChanged(nameof(ListOpacity));
    }
}
