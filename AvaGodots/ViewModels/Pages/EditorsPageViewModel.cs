using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaGodots.Interfaces;
using AvaGodots.Models;
using AvaGodots.Services;
using Avalonia.Input.Platform;
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
    private readonly IDownloadManagerService? _downloadManager;

    /// <summary>
    /// 编辑器数据变更事件（增删改后触发，用于通知父级更新状态栏）
    /// </summary>
    public event Action? EditorsChanged;

    private CancellationTokenSource? _searchDebounce;

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

    public EditorsPageViewModel(IEditorService editorService, IConfigService configService, IDownloadManagerService? downloadManager = null)
    {
        _editorService = editorService;
        _configService = configService;
        _downloadManager = downloadManager;
        _remoteEditorService = new RemoteEditorService();

        // 监听编辑器安装完成事件，自动刷新本地列表
        if (_downloadManager != null)
        {
            _downloadManager.EditorInstalled += OnEditorInstalled;
        }
    }

    /// <summary>
    /// 下载安装完成后自动刷新本地编辑器列表
    /// </summary>
    private async void OnEditorInstalled()
    {
        await _editorService.LoadAsync();
        RefreshEditors();
    }

    /// <summary>
    /// 注入项目服务（从 MainViewModel 调用）
    /// </summary>
    public void SetProjectService(IProjectService projectService) => _projectService = projectService;

    /// <summary>
    /// 注入数据库服务（从 MainViewModel 调用）
    /// </summary>
    public void SetDatabase(IDatabaseService db) => _remoteEditorService.SetDatabase(db);

    // ========================= 属性变更回调 =========================

    partial void OnSearchTextChanged(string value) => DebounceRefreshEditors();
    partial void OnSortIndexChanged(int value) => RefreshEditors();

    private async void DebounceRefreshEditors()
    {
        _searchDebounce?.Cancel();
        var cts = _searchDebounce = new CancellationTokenSource();
        try
        {
            await Task.Delay(200, cts.Token);
            RefreshEditors();
        }
        catch (TaskCanceledException) { /* debounced */ }
    }

    partial void OnSelectedEditorChanged(GodotEditor? value)
    {
        HasSelection = value != null;
    }

    // ========================= 数据刷新 =========================

    public void RefreshEditors()
    {
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

        var newList = editors.ToList();
        SyncCollection(FilteredEditors, newList);

        IsEmpty = FilteredEditors.Count == 0;
        EditorsChanged?.Invoke();
    }

    /// <summary>
    /// 同步 ObservableCollection 与目标列表，最小化 UI 变更通知
    /// </summary>
    private static void SyncCollection<T>(ObservableCollection<T> target, System.Collections.Generic.List<T> source)
    {
        // 如果内容相同则跳过
        if (target.Count == source.Count && target.SequenceEqual(source)) return;

        // 小规模列表直接重建更高效
        if (source.Count <= 50 || Math.Abs(target.Count - source.Count) > target.Count / 2)
        {
            target.Clear();
            foreach (var item in source) target.Add(item);
            return;
        }

        // 中等规模：移除多余、插入缺少、更新位置
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!source.Contains(target[i]))
                target.RemoveAt(i);
        }
        for (var i = 0; i < source.Count; i++)
        {
            if (i >= target.Count)
                target.Add(source[i]);
            else if (!Equals(target[i], source[i]))
            {
                var idx = target.IndexOf(source[i]);
                if (idx >= 0) target.Move(idx, i);
                else target.Insert(i, source[i]);
            }
        }
    }

    // ========================= 扫描目录 =========================

    [RelayCommand]
    private async Task ScanDirectoryAsync()
    {
        var folder = await FileDialogHelper.PickFolderAsync(LocalizationService.GetString("Dialog.SelectScanDir", "Select directory to scan"));
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
        if (editor == null) { LoggerService.Instance.Warning("Editors", "ShowRenameDialog: editor is null"); return; }
        LoggerService.Instance.Debug("Editors", $"ShowRenameDialog: {editor.Name}");
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
        if (editor == null) { LoggerService.Instance.Warning("Editors", "ShowExtraArgsDialog: editor is null"); return; }
        LoggerService.Instance.Debug("Editors", $"ShowExtraArgsDialog: {editor.Name}");
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
        if (editor == null) { LoggerService.Instance.Warning("Editors", "ShowDeleteConfirm: editor is null"); return; }
        LoggerService.Instance.Debug("Editors", $"ShowDeleteConfirm: {editor.Name}");
        _pendingDeleteEditor = editor;
        DeleteAlsoFromDisk = false;
        DeleteConfirmMessage = string.Format(
            LocalizationService.GetString("Editors.Dialog.ConfirmRemoveMessage", "Remove editor \"{0}\"?"),
            editor.Name);
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
        LoggerService.Instance.Info("Editors", "Loading remote versions...");
        try
        {
            var versions = await _remoteEditorService.GetVersionsAsync();
            RemoteVersions.Clear();

            foreach (var ver in versions)
            {
                if (!ShouldShowVersion(ver.Name)) continue;

                var node = new RemoteVersionNode { Name = ver.Name, IsFolder = true };

                // 主版本的 flavor（stable / rc1 等）
                if (ver.Flavor == "stable")
                {
                    var stableNode = CreateReleaseNode(ver.Name, "stable", ver.Name);
                    node.Children.Add(stableNode);
                }
                else if (!string.IsNullOrEmpty(ver.Flavor) && FilterUnstable)
                {
                    var flavorNode = CreateReleaseNode(ver.Name, ver.Flavor, ver.Name);
                    node.Children.Add(flavorNode);
                }

                // 历史预发布版本
                if (FilterUnstable)
                {
                    foreach (var rel in ver.Releases)
                    {
                        if (rel.Name == "stable" || rel.Name == ver.Flavor) continue;
                        var tagVer = rel.ReleaseVersion ?? ver.Name;
                        var relNode = CreateReleaseNode(tagVer, rel.Name, ver.Name);
                        node.Children.Add(relNode);
                    }
                }

                if (node.Children.Count > 0)
                    RemoteVersions.Add(node);
            }

            IsRemoteEmpty = RemoteVersions.Count == 0;
            LoggerService.Instance.Info("Editors", $"Loaded {RemoteVersions.Count} remote version groups");
        }
        catch (Exception ex)
        {
            IsRemoteEmpty = true;
            LoggerService.Instance.Error("Editors", "Failed to load remote versions", ex);
        }
        finally { IsRemoteLoading = false; }
    }

    /// <summary>创建发布节点（folder），带占位子节点以显示展开箭头</summary>
    private RemoteVersionNode CreateReleaseNode(string tagVersion, string releaseName, string parentVersion)
    {
        var node = new RemoteVersionNode
        {
            Name = releaseName,
            Tag = $"{tagVersion}-{releaseName}",
            IsFolder = true
        };
        // 添加占位子节点以显示展开箭头
        node.Children.Add(new RemoteVersionNode { Name = "Loading...", IsFolder = false });
        // 监听展开事件以触发懒加载
        node.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(RemoteVersionNode.IsExpanded) && node.IsExpanded)
                await LoadNodeAssetsAsync(node);
        };
        return node;
    }

    partial void OnFilterMonoChanged(bool value) => _ = LoadRemoteVersionsAsync();
    partial void OnFilterUnstableChanged(bool value) => _ = LoadRemoteVersionsAsync();
    partial void OnFilter4XChanged(bool value) => _ = LoadRemoteVersionsAsync();
    partial void OnFilter3XChanged(bool value) => _ = LoadRemoteVersionsAsync();
    partial void OnFilterOtherVersionsChanged(bool value) => _ = LoadRemoteVersionsAsync();

    /// <summary>懒加载指定节点的发布资产</summary>
    private async Task LoadNodeAssetsAsync(RemoteVersionNode node)
    {
        if (string.IsNullOrEmpty(node.Tag) || node.AssetsLoaded || node.IsLoading) return;

        node.IsLoading = true;
        node.Children.Clear();
        LoggerService.Instance.Debug("Editors", $"Lazy-loading assets for tag: {node.Tag}");

        try
        {
            var assets = await _remoteEditorService.GetReleaseAssetsAsync(node.Tag);
            foreach (var asset in assets)
            {
                if (!RemoteEditorService.IsDesktopAsset(asset.Name)) continue;
                if (!FilterAnyPlatform && !RemoteEditorService.IsForCurrentPlatform(asset.Name)) continue;
                if (!FilterMono && RemoteEditorService.IsMono(asset.Name)) continue;

                node.Children.Add(new RemoteVersionNode
                {
                    Name = asset.Name,
                    DownloadUrl = asset.BrowserDownloadUrl,
                    IsFolder = false,
                    FileSize = FormatFileSize(asset.Size)
                });
            }
            node.AssetsLoaded = true;
        }
        finally { node.IsLoading = false; }
    }

    [RelayCommand]
    private async Task DownloadRemoteEditorAsync(RemoteVersionNode? node)
    {
        if (node == null) return;

        // 文件夹节点：切换展开
        if (node.IsFolder)
        {
            node.IsExpanded = !node.IsExpanded;
            return;
        }

        // 文件节点：下载
        if (string.IsNullOrEmpty(node.DownloadUrl)) return;

        // 防止重复下载（相同 URL 已在队列中且仍在下载/等待）
        if (_downloadManager != null &&
            _downloadManager.Downloads.Any(d => d.Url == node.DownloadUrl && (d.IsDownloading || (!d.IsCompleted && !d.IsFailed))))
        {
            LoggerService.Instance.Debug("Editors", $"Download already in progress: {node.Name}");
            return;
        }

        LoggerService.Instance.Info("Editors", $"Starting download: {node.Name}");

        if (_downloadManager != null)
        {
            if (RemoteEditorService.IsExportTemplate(node.Name))
                _downloadManager.DownloadExportTemplate(node.DownloadUrl, node.Name, node.Name);
            else
                _downloadManager.DownloadEditor(node.DownloadUrl, node.Name, node.Name);
        }
        else
        {
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

    /// <summary>为本地编辑器下载导出模板</summary>
    [RelayCommand]
    private async Task DownloadExportTemplateAsync(GodotEditor? editor)
    {
        editor ??= SelectedEditor;
        if (editor == null || _downloadManager == null) return;

        var tag = ExtractTag(editor.VersionHint);
        if (string.IsNullOrEmpty(tag))
        {
            LoggerService.Instance.Warning("Editors", $"Cannot extract tag from VersionHint: '{editor.VersionHint}'");
            return;
        }

        LoggerService.Instance.Info("Editors", $"Fetching export template for tag: {tag}");

        try
        {
            var assets = await _remoteEditorService.GetReleaseAssetsAsync(tag);
            if (assets.Count == 0)
            {
                LoggerService.Instance.Warning("Editors", $"No assets found for tag: {tag}");
                return;
            }

            var isMono = editor.Path.Contains("mono", StringComparison.OrdinalIgnoreCase) ||
                         editor.VersionHint.Contains("mono", StringComparison.OrdinalIgnoreCase);

            var template = assets.FirstOrDefault(a =>
                RemoteEditorService.IsExportTemplate(a.Name) &&
                RemoteEditorService.IsMono(a.Name) == isMono);

            if (template != null)
            {
                LoggerService.Instance.Info("Editors", $"Downloading export template: {template.Name}");
                _downloadManager.DownloadExportTemplate(template.BrowserDownloadUrl, template.Name, $"Export Templates ({tag})");
            }
            else
            {
                LoggerService.Instance.Warning("Editors", $"No matching export template found (mono={isMono}) in {assets.Count} assets for tag: {tag}");
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error("Editors", "Failed to download export template", ex);
        }
    }

    private static string ExtractTag(string versionHint)
    {
        if (string.IsNullOrWhiteSpace(versionHint)) return "";

        var tag = versionHint.TrimStart('v').Trim();

        // 如果 tag 不包含 flavor 后缀 (如 "-stable")，尝试添加 "-stable"
        // GitHub releases 的 tag 格式为 "4.3-stable", "4.2.2-stable", "4.3-rc1" 等
        if (!tag.Contains('-') && !string.IsNullOrEmpty(tag))
            tag += "-stable";

        return tag;
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
