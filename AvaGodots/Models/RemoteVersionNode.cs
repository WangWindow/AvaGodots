using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaGodots.Models;

/// <summary>
/// 远程编辑器版本树节点
/// 用于 Remote editors 树形结构展示
/// </summary>
public partial class RemoteVersionNode : ObservableObject
{
    /// <summary>
    /// 显示名称（版本号或文件名）
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// GitHub release tag（如 "4.3-stable"）
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// 下载链接（仅文件节点）
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// 是否为文件夹节点
    /// </summary>
    public bool IsFolder { get; set; } = true;

    /// <summary>
    /// 是否正在加载子节点
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 是否展开
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// 文件大小描述
    /// </summary>
    public string FileSize { get; set; } = string.Empty;

    /// <summary>
    /// 资产是否已加载（防止重复加载）
    /// </summary>
    public bool AssetsLoaded { get; set; }

    /// <summary>
    /// 子节点
    /// </summary>
    public ObservableCollection<RemoteVersionNode> Children { get; } = [];
}
