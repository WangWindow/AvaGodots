using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaGodots.Interfaces;
using AvaGodots.Models;
using AvaGodots.Services;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaGodots.ViewModels.Pages;

/// <summary>
/// 项目管理页面视图模型
/// 对应 godots 的 ProjectsControl —— 完整项目 CRUD + 过滤 + 排序 + 标签管理
/// </summary>
public partial class ProjectsPageViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IEditorService _editorService;
    private readonly IConfigService _configService;

    /// <summary>
    /// 项目数据变更事件（增删改后触发，用于通知父级更新状态栏）
    /// </summary>
    public event Action? ProjectsChanged;

    private CancellationTokenSource? _searchDebounce;

    // ========================= 列表与过滤 =========================

    public ObservableCollection<GodotProject> FilteredProjects { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _sortIndex;
    [ObservableProperty] private GodotProject? _selectedProject;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _isEmpty = true;

    public ObservableCollection<GodotEditor> AvailableEditors { get; } = [];

    // ========================= 新建项目对话框 =========================

    [ObservableProperty] private bool _isNewProjectDialogVisible;
    [ObservableProperty] private string _newProjectName = string.Empty;
    [ObservableProperty] private string _newProjectDirectory = string.Empty;
    [ObservableProperty] private GodotEditor? _newProjectEditor;
    [ObservableProperty] private int _newProjectGodotVersion = 4;
    [ObservableProperty] private bool _createFolder = true;
    [ObservableProperty] private string _selectedRenderer = "Forward+";
    [ObservableProperty] private string _selectedVersionControl = "Git";
    [ObservableProperty] private string _newProjectStatus = string.Empty;
    [ObservableProperty] private bool _newProjectStatusIsValid = true;

    private static readonly IBrush ValidStatusBrush = new SolidColorBrush(Color.Parse("#8BC34A"));
    private static readonly IBrush ErrorStatusBrush = new SolidColorBrush(Color.Parse("#FF5252"));

    public IBrush NewProjectStatusBrush => NewProjectStatusIsValid ? ValidStatusBrush : ErrorStatusBrush;

    /// <summary>
    /// 计算完整项目路径（含自动创建的文件夹名）
    /// </summary>
    public string ComputedProjectPath =>
        CreateFolder && !string.IsNullOrWhiteSpace(NewProjectName) && !string.IsNullOrWhiteSpace(NewProjectDirectory)
            ? Path.Combine(NewProjectDirectory, ConvertDirectoryName(NewProjectName, _configService?.Config.NamingConvention ?? DirectoryNamingConvention.SnakeCase))
            : NewProjectDirectory;

    partial void OnNewProjectNameChanged(string value) => UpdateNewProjectValidation();
    partial void OnNewProjectDirectoryChanged(string value) => UpdateNewProjectValidation();
    partial void OnNewProjectStatusIsValidChanged(bool value) => OnPropertyChanged(nameof(NewProjectStatusBrush));
    partial void OnCreateFolderChanged(bool value)
    {
        OnPropertyChanged(nameof(ComputedProjectPath));
        UpdateNewProjectValidation();
    }

    private void UpdateNewProjectValidation()
    {
        OnPropertyChanged(nameof(ComputedProjectPath));
        if (string.IsNullOrWhiteSpace(NewProjectName))
        {
            NewProjectStatus = "Project name cannot be blank.";
            NewProjectStatusIsValid = false;
        }
        else if (string.IsNullOrWhiteSpace(NewProjectDirectory))
        {
            NewProjectStatus = "Please specify a project path.";
            NewProjectStatusIsValid = false;
        }
        else if (CreateFolder)
        {
            NewProjectStatus = "The project folder will be automatically created.";
            NewProjectStatusIsValid = true;
        }
        else if (Directory.Exists(NewProjectDirectory) && Directory.GetFileSystemEntries(NewProjectDirectory).Length == 0)
        {
            NewProjectStatus = "The project folder exists and is empty.";
            NewProjectStatusIsValid = true;
        }
        else if (Directory.Exists(NewProjectDirectory))
        {
            NewProjectStatus = "Warning: the selected path is not empty.";
            NewProjectStatusIsValid = true; // 警告但允许
        }
        else
        {
            NewProjectStatus = "The path specified doesn't exist.";
            NewProjectStatusIsValid = false;
        }
    }

    public string[] RendererOptions { get; } = ["Forward+", "Mobile", "Compatibility"];
    public string[] VersionControlOptions { get; } = ["Git", "None"];

    // 随机项目名生成
    private static readonly string[] NamePrefixes = ["Epic", "Super", "Mega", "Ultra", "Hyper", "Magic", "Cosmic", "Pixel", "Retro", "Turbo"];
    private static readonly string[] NameTopics = ["Adventure", "Quest", "Dungeon", "Dragon", "Knight", "Space", "Robot", "Ninja", "Tower", "Puzzle"];
    private static readonly string[] NameSuffixes = ["Project", "Game", "World", "Land", "Arena", "Saga", "Tales", "Legends", "Craft", "Rush"];
    private static readonly Random _rng = new();

    // ========================= 导入项目对话框 =========================

    [ObservableProperty] private bool _isImportDialogVisible;
    [ObservableProperty] private string _importProjectPath = string.Empty;
    [ObservableProperty] private GodotEditor? _importProjectEditor;

    // ========================= 复制项目对话框 =========================

    [ObservableProperty] private bool _isDuplicateDialogVisible;
    [ObservableProperty] private string _duplicateProjectName = string.Empty;
    [ObservableProperty] private string _duplicateProjectDirectory = string.Empty;

    // ========================= 重命名对话框 =========================

    [ObservableProperty] private bool _isRenameDialogVisible;
    [ObservableProperty] private string _renameProjectName = string.Empty;

    // ========================= 绑定编辑器对话框 =========================

    [ObservableProperty] private bool _isBindEditorDialogVisible;
    [ObservableProperty] private GodotEditor? _bindEditor;

    // ========================= 标签管理对话框 =========================

    [ObservableProperty] private bool _isTagDialogVisible;
    [ObservableProperty] private string _newTagText = string.Empty;
    public ObservableCollection<string> AssignedTags { get; } = [];
    public ObservableCollection<string> AllKnownTags { get; } = [];

    // ========================= 删除确认对话框 =========================

    [ObservableProperty] private bool _isDeleteConfirmVisible;
    [ObservableProperty] private string _deleteConfirmMessage = string.Empty;
    private GodotProject? _pendingDeleteProject;

    // ========================= 编辑警告对话框 =========================

    [ObservableProperty] private bool _isEditWarningVisible;
    [ObservableProperty] private bool _dontShowEditWarning;
    private GodotProject? _pendingEditProject;

    public ProjectsPageViewModel() : this(null!, null!, null!) { }

    public ProjectsPageViewModel(IProjectService projectService, IEditorService editorService, IConfigService configService)
    {
        _projectService = projectService;
        _editorService = editorService;
        _configService = configService;
    }

    // ========================= 属性变更回调 =========================

    partial void OnSearchTextChanged(string value) => DebounceRefreshProjects();
    partial void OnSortIndexChanged(int value) => RefreshProjects();

    private async void DebounceRefreshProjects()
    {
        _searchDebounce?.Cancel();
        var cts = _searchDebounce = new CancellationTokenSource();
        try
        {
            await Task.Delay(200, cts.Token);
            RefreshProjects();
        }
        catch (TaskCanceledException) { /* debounced */ }
    }

    partial void OnSelectedProjectChanged(GodotProject? value)
    {
        HasSelection = value != null;
        SidebarProject = value;
    }

    [ObservableProperty] private GodotProject? _sidebarProject;

    // ========================= 数据刷新 =========================

    /// <summary>
    /// 刷新项目列表（过滤 + 排序 + tag: 语法）
    /// </summary>
    public void RefreshProjects()
    {
        var projects = _projectService.Projects.AsEnumerable();

        // tag: 语法过滤
        var search = SearchText?.Trim() ?? string.Empty;
        if (search.StartsWith("tag:", StringComparison.OrdinalIgnoreCase))
        {
            var tagFilter = search[4..].Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(tagFilter))
                projects = projects.Where(p =>
                    p.Tags.Any(t => t.Contains(tagFilter, StringComparison.OrdinalIgnoreCase)));
        }
        else if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLowerInvariant();
            projects = projects.Where(p =>
                p.Name.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                p.Path.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                p.EditorName.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                p.Tags.Any(t => t.Contains(lower, StringComparison.OrdinalIgnoreCase)));
        }

        projects = SortIndex switch
        {
            0 => projects.OrderByDescending(p => p.IsFavorite).ThenByDescending(p => p.LastModified),
            1 => projects.OrderByDescending(p => p.IsFavorite).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
            2 => projects.OrderByDescending(p => p.IsFavorite).ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase),
            3 => projects.OrderByDescending(p => p.IsFavorite).ThenBy(p => string.Join(",", p.Tags)),
            _ => projects
        };

        var newList = projects.ToList();
        SyncCollection(FilteredProjects, newList);

        IsEmpty = FilteredProjects.Count == 0;

        var newEditors = _editorService.Editors.ToList();
        SyncCollection(AvailableEditors, newEditors);

        ProjectsChanged?.Invoke();
    }

    /// <summary>
    /// 同步 ObservableCollection 与目标列表，最小化 UI 变更通知
    /// </summary>
    private static void SyncCollection<T>(ObservableCollection<T> target, System.Collections.Generic.List<T> source)
    {
        if (target.Count == source.Count && target.SequenceEqual(source)) return;

        if (source.Count <= 50 || Math.Abs(target.Count - source.Count) > target.Count / 2)
        {
            target.Clear();
            foreach (var item in source) target.Add(item);
            return;
        }

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

    // ========================= 新建项目 =========================

    [RelayCommand]
    private void ShowNewProjectDialog()
    {
        // 确保编辑器列表是最新的
        AvailableEditors.Clear();
        foreach (var editor in _editorService.Editors)
            AvailableEditors.Add(editor);

        NewProjectName = "New Game Project";
        NewProjectDirectory = _configService.Config.ProjectsPath;
        NewProjectEditor = AvailableEditors.FirstOrDefault();
        NewProjectGodotVersion = 4;
        CreateFolder = true;
        SelectedRenderer = "Forward+";
        SelectedVersionControl = "Git";
        UpdateNewProjectValidation();
        IsNewProjectDialogVisible = true;
    }

    [RelayCommand]
    private void RandomizeProjectName()
    {
        NewProjectName = $"{NamePrefixes[_rng.Next(NamePrefixes.Length)]} {NameTopics[_rng.Next(NameTopics.Length)]} {NameSuffixes[_rng.Next(NameSuffixes.Length)]}";
    }

    [RelayCommand]
    private async Task BrowseNewProjectDirectoryAsync()
    {
        var folder = await FileDialogHelper.PickFolderAsync(LocalizationService.GetString("Dialog.SelectProjectDir", "Select project directory"));
        if (folder != null) NewProjectDirectory = folder;
    }

    [RelayCommand]
    private async Task ConfirmNewProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName) || string.IsNullOrWhiteSpace(NewProjectDirectory))
            return;

        var projectDir = ComputedProjectPath;

        var result = await _projectService.CreateProjectAsync(
            NewProjectName, projectDir, NewProjectEditor?.Path ?? string.Empty,
            NewProjectGodotVersion, SelectedRenderer, SelectedVersionControl,
            NewProjectEditor?.VersionHint ?? string.Empty);

        if (result != null) { RefreshProjects(); SelectedProject = result; }
        IsNewProjectDialogVisible = false;
    }

    [RelayCommand] private void CancelNewProject() => IsNewProjectDialogVisible = false;

    // ========================= 导入项目 =========================

    [RelayCommand]
    private void ShowImportDialog()
    {
        // 确保编辑器列表是最新的
        AvailableEditors.Clear();
        foreach (var editor in _editorService.Editors)
            AvailableEditors.Add(editor);

        ImportProjectPath = string.Empty;
        ImportProjectEditor = AvailableEditors.FirstOrDefault();
        IsImportDialogVisible = true;
    }

    [RelayCommand]
    private async Task BrowseImportPathAsync()
    {
        var file = await FileDialogHelper.PickFileAsync(LocalizationService.GetString("Dialog.SelectProjectGodot", "Select project.godot"),
            [FileDialogHelper.GodotProjectFilter, FileDialogHelper.AllFilter]);
        if (file != null) ImportProjectPath = file;
    }

    [RelayCommand]
    private async Task ConfirmImportAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportProjectPath)) return;
        var result = await _projectService.AddProjectAsync(ImportProjectPath, ImportProjectEditor?.Path);
        if (result != null) { RefreshProjects(); SelectedProject = result; }
        IsImportDialogVisible = false;
    }

    [RelayCommand]
    private async Task ConfirmImportAndEditAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportProjectPath)) return;
        var result = await _projectService.AddProjectAsync(ImportProjectPath, ImportProjectEditor?.Path);
        if (result != null) { RefreshProjects(); SelectedProject = result; await _projectService.EditProjectAsync(result); }
        IsImportDialogVisible = false;
    }

    [RelayCommand] private void CancelImport() => IsImportDialogVisible = false;

    // ========================= 扫描目录 =========================

    [RelayCommand]
    private async Task ScanDirectoryAsync()
    {
        var folder = await FileDialogHelper.PickFolderAsync(LocalizationService.GetString("Dialog.SelectScanDir", "Select directory to scan"));
        if (string.IsNullOrWhiteSpace(folder)) return;
        var found = await _projectService.ScanDirectoryAsync(folder);
        foreach (var path in found)
            await _projectService.AddProjectAsync(path);
        RefreshProjects();
    }

    // ========================= 编辑 / 运行 =========================

    [RelayCommand]
    private async Task EditProjectAsync(GodotProject? project)
    {
        project ??= SelectedProject;
        if (project == null) return;

        if (string.IsNullOrEmpty(project.EditorPath) || project.HasInvalidEditor)
        { ShowBindEditorDialog(project); return; }

        if (project.ShowEditWarning)
        {
            _pendingEditProject = project;
            DontShowEditWarning = false;
            IsEditWarningVisible = true;
            return;
        }

        await _projectService.EditProjectAsync(project);
    }

    [RelayCommand]
    private async Task ConfirmEditWarningAsync()
    {
        if (_pendingEditProject != null)
        {
            if (DontShowEditWarning)
            { _pendingEditProject.ShowEditWarning = false; await _projectService.SaveAsync(); }
            await _projectService.EditProjectAsync(_pendingEditProject);
        }
        IsEditWarningVisible = false;
        _pendingEditProject = null;
    }

    [RelayCommand] private void CancelEditWarning() { IsEditWarningVisible = false; _pendingEditProject = null; }

    [RelayCommand]
    private async Task RunProjectAsync(GodotProject? project)
    {
        project ??= SelectedProject;
        if (project != null) await _projectService.RunProjectAsync(project);
    }

    // ========================= 复制项目 =========================

    [RelayCommand]
    private void ShowDuplicateDialog(GodotProject? project)
    {
        project ??= SelectedProject;
        if (project == null) return;
        SelectedProject = project;
        DuplicateProjectName = project.Name + " (Copy)";
        DuplicateProjectDirectory = Path.GetDirectoryName(project.DirectoryPath) ?? string.Empty;
        IsDuplicateDialogVisible = true;
    }

    [RelayCommand]
    private async Task BrowseDuplicateDirectoryAsync()
    {
        var folder = await FileDialogHelper.PickFolderAsync(LocalizationService.GetString("Dialog.SelectTargetDir", "Select target directory"));
        if (folder != null) DuplicateProjectDirectory = folder;
    }

    [RelayCommand]
    private async Task ConfirmDuplicateAsync()
    {
        if (SelectedProject == null || string.IsNullOrWhiteSpace(DuplicateProjectName) ||
            string.IsNullOrWhiteSpace(DuplicateProjectDirectory)) return;

        var targetName = ConvertDirectoryName(DuplicateProjectName, _configService.Config.NamingConvention);
        var targetDir = Path.Combine(DuplicateProjectDirectory, targetName);
        try
        {
            CopyDirectory(SelectedProject.DirectoryPath, targetDir);
            var projectFile = Path.Combine(targetDir, "project.godot");
            var result = await _projectService.AddProjectAsync(projectFile, SelectedProject.EditorPath);
            if (result != null) { result.Name = DuplicateProjectName; await _projectService.SaveAsync(); RefreshProjects(); SelectedProject = result; }
        }
        catch { /* 复制失败 */ }
        IsDuplicateDialogVisible = false;
    }

    [RelayCommand] private void CancelDuplicate() => IsDuplicateDialogVisible = false;

    // ========================= 重命名项目 =========================

    [RelayCommand]
    private void ShowRenameDialog(GodotProject? project)
    {
        project ??= SelectedProject;
        if (project == null) return;
        SelectedProject = project;
        RenameProjectName = project.Name;
        IsRenameDialogVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmRenameAsync()
    {
        if (SelectedProject == null || string.IsNullOrWhiteSpace(RenameProjectName)) return;
        SelectedProject.Name = RenameProjectName;
        await _projectService.SaveAsync();
        RefreshProjects();
        IsRenameDialogVisible = false;
    }

    [RelayCommand] private void CancelRename() => IsRenameDialogVisible = false;

    // ========================= 绑定编辑器 =========================

    [RelayCommand]
    private void ShowBindEditorDialog(GodotProject? project)
    {
        project ??= SelectedProject;
        if (project == null) return;
        SelectedProject = project;

        // 确保编辑器列表是最新的
        AvailableEditors.Clear();
        foreach (var editor in _editorService.Editors)
            AvailableEditors.Add(editor);

        BindEditor = AvailableEditors.FirstOrDefault(e => e.Path == project.EditorPath) ?? AvailableEditors.FirstOrDefault();
        IsBindEditorDialogVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmBindEditorAsync()
    {
        if (SelectedProject == null || BindEditor == null) return;
        SelectedProject.EditorPath = BindEditor.Path;
        SelectedProject.EditorName = BindEditor.Name;
        await _projectService.SaveAsync();
        RefreshProjects();
        IsBindEditorDialogVisible = false;
    }

    [RelayCommand] private void CancelBindEditor() => IsBindEditorDialogVisible = false;

    // ========================= 标签管理 =========================

    [RelayCommand]
    private void ShowTagDialog(GodotProject? project)
    {
        project ??= SelectedProject;
        if (project == null) return;
        SelectedProject = project;
        AssignedTags.Clear();
        foreach (var t in project.Tags) AssignedTags.Add(t);

        AllKnownTags.Clear();
        var allTags = _projectService.Projects.SelectMany(p => p.Tags)
            .Concat(_editorService.Editors.SelectMany(e => e.Tags))
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
        if (SelectedProject == null) return;
        SelectedProject.Tags = [.. AssignedTags];
        await _projectService.SaveAsync();
        RefreshProjects();
        IsTagDialogVisible = false;
    }

    [RelayCommand] private void CancelTags() => IsTagDialogVisible = false;
    [RelayCommand] private void FilterByTag(string? tag) { if (tag != null) SearchText = $"tag:{tag}"; }

    // ========================= 删除确认 =========================

    [RelayCommand]
    private void ShowDeleteConfirm(GodotProject? project)
    {
        project ??= SelectedProject;
        if (project == null) return;
        _pendingDeleteProject = project;
        DeleteConfirmMessage = string.Format(
            LocalizationService.GetString("Projects.Dialog.ConfirmRemoveMessage", "Remove project \"{0}\" from the list?\n(Project files will NOT be deleted)"),
            project.Name);
        IsDeleteConfirmVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        if (_pendingDeleteProject != null)
        {
            await _projectService.RemoveProjectAsync(_pendingDeleteProject.Path);
            RefreshProjects();
            if (SelectedProject == _pendingDeleteProject) SelectedProject = null;
        }
        IsDeleteConfirmVisible = false;
        _pendingDeleteProject = null;
    }

    [RelayCommand] private void CancelDelete() { IsDeleteConfirmVisible = false; _pendingDeleteProject = null; }

    // ========================= 其他操作 =========================

    [RelayCommand]
    private async Task RemoveMissingProjectsAsync()
    {
        await _projectService.RemoveMissingProjectsAsync();
        RefreshProjects();
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(GodotProject? project)
    {
        if (project == null) return;
        project.IsFavorite = !project.IsFavorite;
        await _projectService.SaveAsync();
        RefreshProjects();
    }

    [RelayCommand]
    private void ShowInFileManager(GodotProject? project)
    {
        project ??= SelectedProject;
        if (project == null) return;
        var dir = project.DirectoryPath;
        if (string.IsNullOrEmpty(dir)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dir, UseShellExecute = true }); }
        catch { }
    }

    [RelayCommand]
    private async Task CopyPathAsync(GodotProject? project)
    {
        project ??= SelectedProject;
        if (project == null) return;
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime switch
        {
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d => d.MainWindow as Avalonia.Controls.TopLevel,
            _ => null
        };
        if (topLevel?.Clipboard != null) await topLevel.Clipboard.SetTextAsync(project.DirectoryPath);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _projectService.LoadAsync();
        RefreshProjects();
    }

    // ========================= 辅助方法 =========================

    private static string ConvertDirectoryName(string name, DirectoryNamingConvention convention) => convention switch
    {
        DirectoryNamingConvention.SnakeCase => string.Join("_", name.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant(),
        DirectoryNamingConvention.KebabCase => string.Join("-", name.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant(),
        DirectoryNamingConvention.CamelCase => ToCamelCase(name),
        DirectoryNamingConvention.PascalCase => ToPascalCase(name),
        DirectoryNamingConvention.TitleCase => name,
        _ => name
    };

    private static string ToCamelCase(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return name;
        var result = parts[0].ToLowerInvariant();
        for (int i = 1; i < parts.Length; i++)
            if (parts[i].Length > 0)
                result += char.ToUpperInvariant(parts[i][0]) + parts[i][1..].ToLowerInvariant();
        return result;
    }

    private static string ToPascalCase(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = "";
        foreach (var part in parts)
            if (part.Length > 0)
                result += char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant();
        return result;
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName is ".godot" or ".import") continue;
            CopyDirectory(dir, Path.Combine(targetDir, dirName));
        }
    }
}
