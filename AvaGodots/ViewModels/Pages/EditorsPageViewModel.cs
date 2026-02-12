using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AvaGodots.Interfaces;
using AvaGodots.Models;
using AvaGodots.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaGodots.ViewModels.Pages;

/// <summary>
/// 编辑器管理页面视图模型
/// 对应 godots 的 LocalEditorsControl / RemoteEditorsControl
/// </summary>
public partial class EditorsPageViewModel : ViewModelBase
{
    private readonly IEditorService _editorService;
    private readonly IConfigService _configService;
    private readonly RemoteEditorService _remoteEditorService;
    private readonly DownloadManagerService? _downloadManager;

    // ========================= 列表与过滤 =========================

    public ObservableCollection<GodotEditor> FilteredEditors { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _sortIndex;
    [ObservableProperty] private GodotEditor? _selectedEditor;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _isLocalView = true;

    // ========================= 远程编辑器 =========================

    public ObservableCollection<RemoteVersionNode> RemoteVersions { get; } = [];

    [ObservableProperty] private bool _isRemoteLoading;
    [ObservableProperty] private bool _isRemoteEmpty = true;
    [ObservableProperty] private string _remoteMirror = "GitHub";

    // 筛选复选框
    [ObservableProperty] private bool _filterMono = true;
    [ObservableProperty] private bool _filterUnstable;
    [ObservableProperty] private bool _filterAnyPlatform;
    [ObservableProperty] private bool _filter64Bit = true;
    [ObservableProperty] private bool _filter4X = true;
    [ObservableProperty] private bool _filter3X;
    [ObservableProperty] private bool _filterOtherVersions;

    // ========================= 重命名对话框 =========================

    [ObservableProperty] private bool _isRenameDialogVisible;
    [ObservableProperty] private string _renameEditorName = string.Empty;
    [ObservableProperty] private string _renameVersionHint = string.Empty;

    // ========================= 额外参数对话框 =========================

    [ObservableProperty] private bool _isExtraArgsDialogVisible;
    [ObservableProperty] private string _extraArgsText = string.Empty;

    // ========================= 标签管理对话框 =========================

    [ObservableProperty] private bool _isTagDialogVisible;
    [ObservableProperty] private string _newTagText = string.Empty;
    public ObservableCollection<string> AssignedTags { get; } = [];
    public ObservableCollection<string> AllKnownTags { get; } = [];

    // ========================= 删除确认对话框 =========================

    [ObservableProperty] private bool _isDeleteConfirmVisible;
    [ObservableProperty] private string _deleteConfirmMessage = string.Empty;
    [ObservableProperty] private bool _deleteAlsoFromDisk;
    private GodotEditor? _pendingDeleteEditor;

    // ========================= 引用查看对话框 =========================

    [ObservableProperty] private bool _isReferencesDialogVisible;
    public ObservableCollection<GodotProject> ReferencingProjects { get; } = [];

    /// <summary>
    /// 项目服务引用（用于查看引用该编辑器的项目）
    /// </summary>
    private IProjectService? _projectService;

    public EditorsPageViewModel() : this(null!, null!, null!) { }

    public EditorsPageViewModel(IEditorService editorService, IConfigService configService, DownloadManagerService? downloadManager = null)
    {
        _editorService = editorService;
        _configService = configService;
        _downloadManager = downloadManager;
        _remoteEditorService = new RemoteEditorService();
    }

    /// <summary>
    /// 注入项目服务（从 MainViewModel 调用）
    /// </summary>
    public void SetProjectService(IProjectService projectService) => _projectService = projectService;

    // ========================= 属性变更回调 =========================

    partial void OnSearchTextChanged(string value) => RefreshEditors();
    partial void OnSortIndexChanged(int value) => RefreshEditors();

    partial void OnSelectedEditorChanged(GodotEditor? value)
    {
        HasSelection = value != null;
    }

    // ========================= 数据刷新 =========================

    public void RefreshEditors()
    {
        FilteredEditors.Clear();
        var editors = _editorService.Editors.AsEnumerable();

        var search = SearchText?.Trim() ?? string.Empty;
        if (search.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
        {
            var tagFilter = search[4..].Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(tagFilter))
                editors = editors.Where(e =>
                    e.Tags.Any(t => t.Contains(tagFilter, StringComparison.OrdinalIgnoreCase)));
        }
        else if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLowerInvariant();
            editors = editors.Where(e =>
                e.Name.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                e.Path.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                e.VersionHint.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                e.Tags.Any(t => t.Contains(lower, StringComparison.OrdinalIgnoreCase)));
        }

        editors = SortIndex switch
        {
            0 => editors.OrderByDescending(e => e.IsFavorite).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
            1 => editors.OrderByDescending(e => e.IsFavorite).ThenBy(e => e.Path, StringComparer.OrdinalIgnoreCase),
            2 => editors.OrderByDescending(e => e.IsFavorite).ThenBy(e => string.Join(",", e.Tags)),
            _ => editors
        };

        foreach (var editor in editors)
            FilteredEditors.Add(editor);

        IsEmpty = FilteredEditors.Count == 0;
    }

    // ========================= 扫描目录 =========================

    [RelayCommand]
    private async Task ScanDirectoryAsync()
    {
        var folder = await FileDialogHelper.PickFolderAsync("选择扫描目录");
        if (string.IsNullOrWhiteSpace(folder)) return;
        var found = await _editorService.ScanDirectoryAsync(folder);
        foreach (var path in found)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            await _editorService.ImportEditorAsync(name, path);
        }
        RefreshEditors();
    }

    // ========================= 运行编辑器 =========================

    [RelayCommand]
    private async Task RunEditorAsync(GodotEditor? editor)
    {
        editor ??= SelectedEditor;
        if (editor != null) await _editorService.RunEditorAsync(editor);
    }

    // ========================= 重命名编辑器 =========================

    [RelayCommand]
    private void ShowRenameDialog(GodotEditor? editor)
    {
        editor ??= SelectedEditor;
        if (editor == null) return;
        SelectedEditor = editor;
        RenameEditorName = editor.Name;
        RenameVersionHint = editor.VersionHint;
        IsRenameDialogVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmRenameAsync()
    {
        if (SelectedEditor == null || string.IsNullOrWhiteSpace(RenameEditorName)) return;
        SelectedEditor.Name = RenameEditorName;
        SelectedEditor.VersionHint = RenameVersionHint;
        await _editorService.SaveAsync();
        RefreshEditors();
        IsRenameDialogVisible = false;
    }

    [RelayCommand] private void CancelRename() => IsRenameDialogVisible = false;

    // ========================= 额外启动参数 =========================

    [RelayCommand]
    private void ShowExtraArgsDialog(GodotEditor? editor)
    {
        editor ??= SelectedEditor;
        if (editor == null) return;
        SelectedEditor = editor;
        ExtraArgsText = string.Join(" ", editor.ExtraArguments);
        IsExtraArgsDialogVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmExtraArgsAsync()
    {
        if (SelectedEditor == null) return;
        SelectedEditor.ExtraArguments = ExtraArgsText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        await _editorService.SaveAsync();
        IsExtraArgsDialogVisible = false;
    }

    [RelayCommand] private void CancelExtraArgs() => IsExtraArgsDialogVisible = false;

    // ========================= 标签管理 =========================

    [RelayCommand]
    private void ShowTagDialog(GodotEditor? editor)
    {
        editor ??= SelectedEditor;
        if (editor == null) return;
        SelectedEditor = editor;
        AssignedTags.Clear();
        foreach (var t in editor.Tags) AssignedTags.Add(t);

        AllKnownTags.Clear();
        var defaultTags = new[] { "dev", "rc", "alpha", "beta", "stable", "mono", "4.x", "3.x" };
        var allTags = _editorService.Editors.SelectMany(e => e.Tags)
            .Concat(defaultTags)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToList();
        foreach (var t in allTags) AllKnownTags.Add(t);

        NewTagText = string.Empty;
        IsTagDialogVisible = true;
    }

    [RelayCommand]
    private void AddTag()
    {
        var tag = NewTagText?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(tag) || tag.Contains(' ') || tag.Contains('/') || tag.Contains('\\'))
            return;
        if (!AssignedTags.Contains(tag)) AssignedTags.Add(tag);
        if (!AllKnownTags.Contains(tag)) AllKnownTags.Add(tag);
        NewTagText = string.Empty;
    }

    [RelayCommand] private void RemoveTag(string? tag) { if (tag != null) AssignedTags.Remove(tag); }
    [RelayCommand] private void AssignKnownTag(string? tag) { if (tag != null && !AssignedTags.Contains(tag)) AssignedTags.Add(tag); }

    [RelayCommand]
    private async Task ConfirmTagsAsync()
    {
        if (SelectedEditor == null) return;
        SelectedEditor.Tags = [.. AssignedTags];
        await _editorService.SaveAsync();
        RefreshEditors();
        IsTagDialogVisible = false;
    }

    [RelayCommand] private void CancelTags() => IsTagDialogVisible = false;
    [RelayCommand] private void FilterByTag(string? tag) { if (tag != null) SearchText = $"tag:{tag}"; }

    // ========================= 删除确认 =========================

    [RelayCommand]
    private void ShowDeleteConfirm(GodotEditor? editor)
    {
        editor ??= SelectedEditor;
        if (editor == null) return;
        _pendingDeleteEditor = editor;
        DeleteAlsoFromDisk = false;
        DeleteConfirmMessage = $"确定要移除编辑器 \"{editor.Name}\" 吗？";
        IsDeleteConfirmVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (_pendingDeleteEditor != null)
        {
            if (DeleteAlsoFromDisk && System.IO.File.Exists(_pendingDeleteEditor.Path))
            {
                try { System.IO.File.Delete(_pendingDeleteEditor.Path); }
                catch { /* 忽略删除失败 */ }
            }
            await _editorService.RemoveEditorAsync(_pendingDeleteEditor.Path);
            RefreshEditors();
            if (SelectedEditor == _pendingDeleteEditor) SelectedEditor = null;
        }
        IsDeleteConfirmVisible = false;
        _pendingDeleteEditor = null;
    }

    [RelayCommand] private void CancelDelete() { IsDeleteConfirmVisible = false; _pendingDeleteEditor = null; }

    // ========================= 查看引用 =========================

    [RelayCommand]
    private void ShowReferences(GodotEditor? editor)
    {
        editor ??= SelectedEditor;
        if (editor == null || _projectService == null) return;

        ReferencingProjects.Clear();
        foreach (var project in _projectService.Projects.Where(p => p.EditorPath == editor.Path))
            ReferencingProjects.Add(project);

        IsReferencesDialogVisible = true;
    }

    [RelayCommand] private void CloseReferences() => IsReferencesDialogVisible = false;

    // ========================= 其他操作 =========================

    [RelayCommand]
    private async Task RemoveMissingEditorsAsync()
    {
        await _editorService.RemoveMissingEditorsAsync();
        RefreshEditors();
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(GodotEditor? editor)
    {
        if (editor == null) return;
        editor.IsFavorite = !editor.IsFavorite;
        await _editorService.SaveAsync();
        RefreshEditors();
    }

    [RelayCommand]
    private void ShowInFileManager(GodotEditor? editor)
    {
        editor ??= SelectedEditor;
        if (editor == null) return;
        var dir = editor.DirectoryPath;
        if (string.IsNullOrEmpty(dir)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dir, UseShellExecute = true }); }
        catch { }
    }

    [RelayCommand]
    private async Task CopyPathAsync(GodotEditor? editor)
    {
        editor ??= SelectedEditor;
        if (editor == null) return;
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime switch
        {
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d => d.MainWindow as Avalonia.Controls.TopLevel,
            _ => null
        };
        if (topLevel?.Clipboard != null) await topLevel.Clipboard.SetTextAsync(editor.Path);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _editorService.LoadAsync();
        RefreshEditors();
    }

    [RelayCommand]
    private void SwitchView(bool isLocal)
    {
        IsLocalView = isLocal;
        if (!isLocal && RemoteVersions.Count == 0)
            _ = LoadRemoteVersionsAsync();
    }

    // ========================= 远程编辑器功能 =========================

    [RelayCommand]
    private async Task LoadRemoteVersionsAsync()
    {
        IsRemoteLoading = true;
        try
        {
            var versions = await _remoteEditorService.GetVersionsAsync();
            RemoteVersions.Clear();

            foreach (var ver in versions)
            {
                // 版本大类过滤
                if (!ShouldShowVersion(ver.Name)) continue;

                var node = new RemoteVersionNode
                {
                    Name = ver.Name,
                    IsFolder = true
                };

                // 如果 flavor 是 stable，自动添加 stable 作为子节点
                if (ver.Flavor == "stable")
                {
                    node.Children.Add(new RemoteVersionNode
                    {
                        Name = "stable",
                        Tag = $"{ver.Name}-stable",
                        IsFolder = true
                    });
                }
                // 如果 flavor 不是 stable (例如 rc1, beta1)，将其作为当前版本的子节点
                else if (!string.IsNullOrEmpty(ver.Flavor))
                {
                    if (FilterUnstable)
                    {
                        node.Children.Add(new RemoteVersionNode
                        {
                            Name = ver.Flavor,
                            Tag = $"{ver.Name}-{ver.Flavor}",
                            IsFolder = true
                        });
                    }
                }

                // 添加 releases 作为子节点 (历史预发布版本)
                if (FilterUnstable)
                {
                    foreach (var release in ver.Releases)
                    {
                        if (release == "stable" || release == ver.Flavor) continue; // 避免重复
                        node.Children.Add(new RemoteVersionNode
                        {
                            Name = release,
                            Tag = $"{ver.Name}-{release}",
                            IsFolder = true
                        });
                    }
                }

                if (node.Children.Count > 0)
                    RemoteVersions.Add(node);
            }

            IsRemoteEmpty = RemoteVersions.Count == 0;
        }
        catch
        {
            IsRemoteEmpty = true;
        }
        finally
        {
            IsRemoteLoading = false;
        }
    }

    partial void OnFilterMonoChanged(bool value) => _ = LoadRemoteVersionsAsync();
    partial void OnFilterUnstableChanged(bool value) => _ = LoadRemoteVersionsAsync();
    partial void OnFilter4XChanged(bool value) => _ = LoadRemoteVersionsAsync();
    partial void OnFilter3XChanged(bool value) => _ = LoadRemoteVersionsAsync();
    partial void OnFilterOtherVersionsChanged(bool value) => _ = LoadRemoteVersionsAsync();

    [RelayCommand]
    private async Task LoadReleaseAssetsAsync(RemoteVersionNode? node)
    {
        if (node == null || string.IsNullOrEmpty(node.Tag) || node.Children.Count > 0) return;
        node.IsLoading = true;
        try
        {
            var assets = await _remoteEditorService.GetReleaseAssetsAsync(node.Tag);
            foreach (var asset in assets.Where(a => a.IsDownloadable))
            {
                // 平台过滤
                if (!FilterAnyPlatform && !RemoteEditorService.IsForCurrentPlatform(asset.Name))
                    continue;
                // Mono 过滤
                if (!FilterMono && RemoteEditorService.IsMono(asset.Name))
                    continue;
                // 64-bit 过滤
                if (Filter64Bit && asset.Name.Contains("32") && !asset.Name.Contains("64"))
                    continue;

                node.Children.Add(new RemoteVersionNode
                {
                    Name = asset.Name,
                    DownloadUrl = asset.BrowserDownloadUrl,
                    IsFolder = false,
                    FileSize = FormatFileSize(asset.Size)
                });
            }
        }
        finally
        {
            node.IsLoading = false;
        }
    }

    [RelayCommand]
    private void DownloadRemoteEditor(RemoteVersionNode? node)
    {
        if (node == null || string.IsNullOrEmpty(node.DownloadUrl)) return;

        if (_downloadManager != null)
        {
            // 判断是导出模板还是编辑器
            if (node.Name.EndsWith(".tpz", StringComparison.OrdinalIgnoreCase))
                _downloadManager.DownloadExportTemplate(node.DownloadUrl, node.Name, node.Name);
            else
                _downloadManager.DownloadEditor(node.DownloadUrl, node.Name, node.Name);
        }
        else
        {
            // 回退：在浏览器打开
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = node.DownloadUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    private bool ShouldShowVersion(string name)
    {
        if (Filter4X && name.StartsWith('4')) return true;
        if (Filter3X && name.StartsWith('3')) return true;
        if (FilterOtherVersions && !name.StartsWith('4') && !name.StartsWith('3')) return true;
        return false;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
