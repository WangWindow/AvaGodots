using System;
using System.Collections.ObjectModel;
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

    /// <summary>
    /// 当前选中的标签页索引
    /// 0=Projects, 1=Editors, 2=Settings
    /// </summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>
    /// 当前选中的页面内容
    /// </summary>
    [ObservableProperty]
    private ViewModelBase? _currentPage;

    /// <summary>
    /// 应用标题
    /// </summary>
    [ObservableProperty]
    private string _title = "Godots";

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

        // 创建页面视图模型
        ProjectsPage = new ProjectsPageViewModel(_projectService, _editorService, _configService);
        AssetLibPage = new AssetLibPageViewModel();
        EditorsPage = new EditorsPageViewModel(_editorService, _configService);
        EditorsPage.SetProjectService(_projectService);
        SettingsPage = new SettingsPageViewModel(_configService, _vsCodeService);

        Pages.Add(ProjectsPage);
        Pages.Add(AssetLibPage);
        Pages.Add(EditorsPage);
        Pages.Add(SettingsPage);

        CurrentPage = ProjectsPage;
    }

    /// <summary>
    /// 切换标签页时更新当前页面
    /// </summary>
    partial void OnSelectedTabIndexChanged(int value)
    {
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
        StatusText = "正在加载配置...";

        try
        {
            await _configService.LoadAsync();

            StatusText = "正在加载编辑器...";
            await _editorService.LoadAsync();

            StatusText = "正在加载项目...";
            await _projectService.LoadAsync();

            ProjectsPage.RefreshProjects();
            EditorsPage.RefreshEditors();
            SettingsPage.LoadSettings();

            StatusText = $"已加载 {_projectService.Projects.Count} 个项目, {_editorService.Editors.Count} 个编辑器";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
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
