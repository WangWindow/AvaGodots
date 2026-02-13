using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AvaGodots.Interfaces;
using AvaGodots.Services;
using AvaGodots.ViewModels.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaGodots.ViewModels;

/// <summary>
/// 主视图模型
/// 管理标签页导航和全局状态
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly IEditorService _editorService;
    private readonly IProjectService _projectService;
    private readonly IVsCodeIntegrationService _vsCodeService;
    private readonly DatabaseService _db;
    private readonly DownloadManagerService _downloadManager;

    /// <summary>
    /// 当前选中的标签页索引
    /// 0=Projects, 1=Editors, 2=Settings
    /// </summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    public bool IsProjectsTabSelected => SelectedTabIndex == 0;
    public bool IsAssetLibTabSelected => SelectedTabIndex == 1;
    public bool IsEditorsTabSelected => SelectedTabIndex == 2;
    public bool IsSettingsTabSelected => SelectedTabIndex == 3;

    /// <summary>
    /// 当前选中的页面内容
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? _currentPage;

    /// <summary>
    /// 应用标题
    /// </summary>
    [ObservableProperty]
    private string _title = "AvaGodots";

    /// <summary>
    /// 应用版本号
    /// </summary>
    [ObservableProperty]
    private string _versionText = "v1.0.0";

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 状态栏文本
    /// </summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>
    /// 当前活跃下载列表（转发自 DownloadManagerService）
    /// </summary>
    public ObservableCollection<DownloadItem> Downloads => _downloadManager.Downloads;

    /// <summary>
    /// 是否有活跃下载
    /// </summary>
    [ObservableProperty]
    private bool _hasActiveDownloads;

    /// <summary>
    /// 页面集合
    /// </summary>
    public ObservableCollection<ViewModelBase> Pages { get; } = [];

    /// <summary>
    /// 项目页面
    /// </summary>
    public ProjectsPageViewModel ProjectsPage { get; }

    /// <summary>
    /// 资源库页面
    /// </summary>
    public AssetLibPageViewModel AssetLibPage { get; }

    /// <summary>
    /// 编辑器页面
    /// </summary>
    public EditorsPageViewModel EditorsPage { get; }

    /// <summary>
    /// 设置页面
    /// </summary>
    public SettingsPageViewModel SettingsPage { get; }

    public MainViewModel()
    {
        // 创建服务实例
        _configService = new ConfigService();
        _vsCodeService = new VsCodeIntegrationService();
        _editorService = new EditorService(_configService);
        _projectService = new ProjectService(_configService, _editorService, _vsCodeService);
        _db = new DatabaseService();
        _downloadManager = new DownloadManagerService(_db, _editorService, _configService);

        // 监听下载列表变更以更新 HasActiveDownloads
        _downloadManager.Downloads.CollectionChanged += (_, _) =>
        {
            HasActiveDownloads = _downloadManager.Downloads.Any(d => d.IsDownloading || d.IsCompleted || d.IsFailed);
        };

        // 创建页面视图模型
        ProjectsPage = new ProjectsPageViewModel(_projectService, _editorService, _configService);
        AssetLibPage = new AssetLibPageViewModel(_db, _editorService);
        EditorsPage = new EditorsPageViewModel(_editorService, _configService, _downloadManager);
        EditorsPage.SetProjectService(_projectService);
        EditorsPage.SetDatabase(_db);
        SettingsPage = new SettingsPageViewModel(_configService, _vsCodeService);
        SettingsPage.RequestClose += OnSettingsPageRequestClose;

        Pages.Add(ProjectsPage);
        Pages.Add(AssetLibPage);
        Pages.Add(EditorsPage);
        Pages.Add(SettingsPage);

        CurrentPage = ProjectsPage;
        VersionText = $"v{ResolveAppVersion()}";
    }

    private static string ResolveAppVersion()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var plusIndex = informationalVersion.IndexOf('+');
            return plusIndex > 0 ? informationalVersion[..plusIndex] : informationalVersion;
        }

        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        return assemblyVersion is null ? "0.0.0" : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
    }

    private void OnSettingsPageRequestClose()
    {
        SelectedTabIndex = 0;
    }

    /// <summary>
    /// 切换标签页时更新当前页面
    /// </summary>
    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsProjectsTabSelected));
        OnPropertyChanged(nameof(IsAssetLibTabSelected));
        OnPropertyChanged(nameof(IsEditorsTabSelected));
        OnPropertyChanged(nameof(IsSettingsTabSelected));

        CurrentPage = value switch
        {
            0 => ProjectsPage,
            1 => AssetLibPage,
            2 => EditorsPage,
            3 => SettingsPage,
            _ => ProjectsPage
        };
    }

    /// <summary>
    /// 初始化应用（加载配置和数据）
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsLoading = true;
        StatusText = LocalizationService.GetString("App.Status.LoadingConfig", "Loading configuration...");

        try
        {
            await LoggerService.Instance.InitializeAsync();
            LoggerService.Instance.Info("App", "AvaGodots starting up");

            await _db.InitializeAsync();
            await _configService.LoadAsync();

            StatusText = LocalizationService.GetString("App.Status.LoadingEditors", "Loading editors...");
            await _editorService.LoadAsync();

            StatusText = LocalizationService.GetString("App.Status.LoadingProjects", "Loading projects...");
            await _projectService.LoadAsync();

            ProjectsPage.RefreshProjects();
            EditorsPage.RefreshEditors();
            SettingsPage.LoadSettings();

            var loadedTemplate = LocalizationService.GetString("App.Status.LoadedSummary", "Loaded {0} projects, {1} editors");
            StatusText = string.Format(loadedTemplate, _projectService.Projects.Count, _editorService.Editors.Count);
            LoggerService.Instance.Info("App", $"Loaded {_projectService.Projects.Count} projects, {_editorService.Editors.Count} editors");
        }
        catch (Exception ex)
        {
            var failedTemplate = LocalizationService.GetString("App.Status.LoadFailed", "Load failed: {0}");
            StatusText = string.Format(failedTemplate, ex.Message);
            LoggerService.Instance.Error("App", "Initialization failed", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 移除下载项
    /// </summary>
    [RelayCommand]
    private void DismissDownload(DownloadItem? item)
    {
        if (item != null)
        {
            _downloadManager.Dismiss(item);
            HasActiveDownloads = _downloadManager.Downloads.Any(d => d.IsDownloading || d.IsCompleted || d.IsFailed);
        }
    }

    /// <summary>
    /// 导航到项目页面
    /// </summary>
    [RelayCommand]
    private void NavigateToProjects() => SelectedTabIndex = 0;

    /// <summary>
    /// 导航到资源库页面
    /// </summary>
    [RelayCommand]
    private void NavigateToAssetLib() => SelectedTabIndex = 1;

    /// <summary>
    /// 导航到编辑器页面
    /// </summary>
    [RelayCommand]
    private void NavigateToEditors() => SelectedTabIndex = 2;

    /// <summary>
    /// 导航到设置页面
    /// </summary>
    [RelayCommand]
    private void NavigateToSettings() => SelectedTabIndex = 3;
}
