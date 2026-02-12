using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace AvaGodots.Services;

/// <summary>
/// 文件/文件夹选择对话框辅助类
/// 封装 Avalonia 原生 StorageProvider API
/// </summary>
public static class FileDialogHelper
{
    /// <summary>
    /// 获取当前顶级窗口
    /// </summary>
    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime single)
            return TopLevel.GetTopLevel(single.MainView);
        return null;
    }

    /// <summary>
    /// 选择单个文件
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <param name="filters">文件类型过滤器</param>
    /// <returns>选中文件的路径，取消返回 null</returns>
    public static async Task<string?> PickFileAsync(string title = "选择文件",
        IReadOnlyList<FilePickerFileType>? filters = null)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    /// <summary>
    /// 选择多个文件
    /// </summary>
    public static async Task<IReadOnlyList<string>> PickFilesAsync(string title = "选择文件",
        IReadOnlyList<FilePickerFileType>? filters = null)
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return [];

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = filters
        });

        var paths = new List<string>();
        foreach (var file in result)
        {
            var path = file.TryGetLocalPath();
            if (path != null) paths.Add(path);
        }
        return paths;
    }

    /// <summary>
    /// 选择文件夹
    /// </summary>
    /// <param name="title">对话框标题</param>
    /// <returns>选中文件夹的路径，取消返回 null</returns>
    public static async Task<string?> PickFolderAsync(string title = "选择文件夹")
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return null;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    /// <summary>
    /// Godot 项目文件过滤器
    /// </summary>
    public static FilePickerFileType GodotProjectFilter { get; } = new("Godot Project")
    {
        Patterns = ["project.godot"]
    };

    /// <summary>
    /// Godot 编辑器可执行文件过滤器
    /// </summary>
    public static FilePickerFileType GodotEditorFilter { get; } = new("Godot Editor")
    {
        Patterns = ["Godot*", "godot*", "*.exe", "*.x86_64", "*.x86_32",
                    "*.arm64", "*.arm32", "*.universal"]
    };

    /// <summary>
    /// ZIP 文件过滤器
    /// </summary>
    public static FilePickerFileType ZipFilter { get; } = new("ZIP Archive")
    {
        Patterns = ["*.zip"]
    };

    /// <summary>
    /// 所有文件过滤器
    /// </summary>
    public static FilePickerFileType AllFilter { get; } = new("All Files")
    {
        Patterns = ["*"]
    };
}
