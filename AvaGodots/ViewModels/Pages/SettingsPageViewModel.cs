using System;
using System.Threading.Tasks;
using AvaGodots.Interfaces;
using AvaGodots.Models;
using AvaGodots.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaGodots.ViewModels.Pages;

/// <summary>
/// 设置页面视图模型
/// 对应 godots 的 Settings 窗口
/// 左侧树形分类：Application > Config / Theme / Advanced, Network > Http Proxy
/// </summary>
public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly IVsCodeIntegrationService _vsCodeService;

    public event Action? RequestClose;

    // ========== 分类导航 ==========

    /// <summary>
    /// 当前选中的设置分类
    /// 0 = Application > Config
    /// 1 = Application > Theme
    /// 2 = Application > Advanced
    /// 3 = Network > Http Proxy
    /// </summary>
    [ObservableProperty]
    private int _selectedCategoryIndex;

    // 分类可见性（供 View 绑定）
    public bool IsConfigVisible => SelectedCategoryIndex == 0;
    public bool IsThemeVisible => SelectedCategoryIndex == 1;
    public bool IsAdvancedVisible => SelectedCategoryIndex == 2;
    public bool IsProxyVisible => SelectedCategoryIndex == 3;

    partial void OnSelectedCategoryIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsConfigVisible));
        OnPropertyChanged(nameof(IsThemeVisible));
        OnPropertyChanged(nameof(IsAdvancedVisible));
        OnPropertyChanged(nameof(IsProxyVisible));
    }

    /// <summary>
    /// 是否需要重启以应用某些变更
    /// </summary>
    [ObservableProperty]
    private bool _needsRestart;

    // ========== Application > Config ==========

    [ObservableProperty]
    private string _language = "en";

    partial void OnLanguageChanged(string value)
    {
        // 运行时切换语言 (不需要重启)
        LocalizationService.SetLanguage(value);
    }

    [ObservableProperty]
    private bool _autoClose;

    [ObservableProperty]
    private string _scale = "Auto";

    [ObservableProperty]
    private string _defaultProjectsPath = string.Empty;

    [ObservableProperty]
    private string _directoryNamingConvention = "snake_case";

    [ObservableProperty]
    private bool _useSystemTitleBar;

    [ObservableProperty]
    private bool _useNativeFileDialog;

    [ObservableProperty]
    private bool _rememberWindowSize;

    // ========== Application > Theme ==========

    [ObservableProperty]
    private string _themePreset = "Default";

    [ObservableProperty]
    private string _baseColor = "#353D4A";

    [ObservableProperty]
    private string _accentColor = "#70BAFA";

    [ObservableProperty]
    private double _contrast;

    // ========== Application > Advanced ==========

    [ObservableProperty]
    private string _downloadsPath = string.Empty;

    [ObservableProperty]
    private string _versionsPath = string.Empty;

    [ObservableProperty]
    private bool _showOrphanEditorExplorer;

    [ObservableProperty]
    private bool _allowInstallToNotEmptyDir;

    [ObservableProperty]
    private bool _onlyStableUpdates = true;

    [ObservableProperty]
    private bool _enableVsCodeIntegration = true;

    // ========== Network > Http Proxy ==========

    [ObservableProperty]
    private string _proxyHost = string.Empty;

    [ObservableProperty]
    private int _proxyPort = 8080;

    // ========== 选项 ==========

    public string[] LanguageOptions { get; } = ["en", "zh-CN"];

    public string[] ScaleOptions { get; } =
        ["Auto", "100%", "125%", "150%", "175%", "200%"];

    public string[] NamingConventionOptions { get; } =
        ["snake_case", "kebab-case", "camelCase", "PascalCase", "Title Case", "None"];

    public string[] ThemePresetOptions { get; } =
        ["Default", "Dark", "Light", "Custom"];

    public SettingsPageViewModel(IConfigService configService, IVsCodeIntegrationService vsCodeService)
    {
        _configService = configService;
        _vsCodeService = vsCodeService;
    }

    // Design-time
    public SettingsPageViewModel() : this(null!, null!)
    {
    }

    // ========== 分类导航命令 ==========

    [RelayCommand]
    private void SelectCategory(string index)
    {
        if (int.TryParse(index, out var i))
            SelectedCategoryIndex = i;
    }

    // ========== 加载/保存 ==========

    public void LoadSettings()
    {
        var config = _configService?.Config;
        if (config == null) return;

        // Config
        Language = config.Language;
        AutoClose = config.AutoClose;
        Scale = config.Theme.CustomDisplayScale < 0 ? "Auto" : $"{(int)(config.Theme.CustomDisplayScale * 100)}%";
        DefaultProjectsPath = config.ProjectsPath;
        DirectoryNamingConvention = config.NamingConvention switch
        {
            Models.DirectoryNamingConvention.SnakeCase => "snake_case",
            Models.DirectoryNamingConvention.KebabCase => "kebab-case",
            Models.DirectoryNamingConvention.CamelCase => "camelCase",
            Models.DirectoryNamingConvention.PascalCase => "PascalCase",
            Models.DirectoryNamingConvention.TitleCase => "Title Case",
            _ => "snake_case"
        };
        UseSystemTitleBar = config.UseSystemTitleBar;
        UseNativeFileDialog = config.UseNativeFileDialog;
        RememberWindowSize = config.RememberWindowSize;

        // Theme
        ThemePreset = config.Theme.Preset;
        BaseColor = config.Theme.BaseColor;
        AccentColor = config.Theme.AccentColor;
        Contrast = config.Theme.Contrast;

        // Advanced
        DownloadsPath = config.DownloadsPath;
        VersionsPath = config.VersionsPath;
        ShowOrphanEditorExplorer = config.ShowOrphanEditorExplorer;
        AllowInstallToNotEmptyDir = config.AllowInstallToNotEmptyDir;
        OnlyStableUpdates = config.OnlyStableUpdates;
        EnableVsCodeIntegration = config.EnableVsCodeIntegration;

        // Proxy
        ProxyHost = config.Proxy.Host;
        ProxyPort = config.Proxy.Port;
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var config = _configService?.Config;
        if (config == null) return;

        // Config
        config.Language = Language;
        config.AutoClose = AutoClose;
        config.Theme.CustomDisplayScale = Scale == "Auto" ? -1 : double.Parse(Scale.TrimEnd('%')) / 100.0;
        config.ProjectsPath = DefaultProjectsPath;
        config.NamingConvention = DirectoryNamingConvention switch
        {
            "snake_case" => Models.DirectoryNamingConvention.SnakeCase,
            "kebab-case" => Models.DirectoryNamingConvention.KebabCase,
            "camelCase" => Models.DirectoryNamingConvention.CamelCase,
            "PascalCase" => Models.DirectoryNamingConvention.PascalCase,
            "Title Case" => Models.DirectoryNamingConvention.TitleCase,
            _ => Models.DirectoryNamingConvention.SnakeCase
        };
        config.UseSystemTitleBar = UseSystemTitleBar;
        config.UseNativeFileDialog = UseNativeFileDialog;
        config.RememberWindowSize = RememberWindowSize;

        // Theme
        config.Theme.Preset = ThemePreset;
        config.Theme.BaseColor = BaseColor;
        config.Theme.AccentColor = AccentColor;
        config.Theme.Contrast = Contrast;

        // Advanced
        config.DownloadsPath = DownloadsPath;
        config.VersionsPath = VersionsPath;
        config.ShowOrphanEditorExplorer = ShowOrphanEditorExplorer;
        config.AllowInstallToNotEmptyDir = AllowInstallToNotEmptyDir;
        config.OnlyStableUpdates = OnlyStableUpdates;
        config.EnableVsCodeIntegration = EnableVsCodeIntegration;

        // Proxy
        config.Proxy.Host = ProxyHost;
        config.Proxy.Port = ProxyPort;

        await _configService!.SaveAsync();
    }

    // ========== 需要重启标记 ==========

    partial void OnScaleChanged(string value) => NeedsRestart = true;
    partial void OnUseSystemTitleBarChanged(bool value) => NeedsRestart = true;
    partial void OnUseNativeFileDialogChanged(bool value) => NeedsRestart = true;
    partial void OnThemePresetChanged(string value) => NeedsRestart = true;
    partial void OnBaseColorChanged(string value) => NeedsRestart = true;
    partial void OnAccentColorChanged(string value) => NeedsRestart = true;
    partial void OnContrastChanged(double value) => NeedsRestart = true;
    partial void OnDownloadsPathChanged(string value) => NeedsRestart = true;
    partial void OnVersionsPathChanged(string value) => NeedsRestart = true;

    // ========== 路径浏览 ==========

    [RelayCommand]
    private async Task BrowseDefaultProjectsPathAsync()
    {
        var folder = await FileDialogHelper.PickFolderAsync(
            LocalizationService.GetString("Settings.Dialog.SelectDefaultProjectsPath", "Select default projects directory"));
        if (folder != null) DefaultProjectsPath = folder;
    }

    [RelayCommand]
    private async Task BrowseDownloadsPathAsync()
    {
        var folder = await FileDialogHelper.PickFolderAsync(
            LocalizationService.GetString("Settings.Dialog.SelectDownloadsPath", "Select downloads directory"));
        if (folder != null) DownloadsPath = folder;
    }

    [RelayCommand]
    private async Task BrowseVersionsPathAsync()
    {
        var folder = await FileDialogHelper.PickFolderAsync(
            LocalizationService.GetString("Settings.Dialog.SelectVersionsPath", "Select editor versions directory"));
        if (folder != null) VersionsPath = folder;
    }

    // ========== Save & Restart ==========

    [RelayCommand]
    private async Task SaveAndRestartAsync()
    {
        await SaveSettingsAsync();
        // TODO: 实际重启逻辑
        NeedsRestart = false;
    }

    [RelayCommand]
    private async Task SaveAndCloseAsync()
    {
        await SaveSettingsAsync();
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void ResetAllToDefault()
    {
        Language = "en";
        AutoClose = false;
        Scale = "100%";
        DefaultProjectsPath = "";
        DirectoryNamingConvention = "PascalCase";
        UseSystemTitleBar = false;
        UseNativeFileDialog = true;
        RememberWindowSize = false;
        ThemePreset = "Default Dark";
        BaseColor = "#2B2D31";
        AccentColor = "#478CBF";
        Contrast = 0.0;
        DownloadsPath = "";
        VersionsPath = "";
        ShowOrphanEditorExplorer = false;
        AllowInstallToNotEmptyDir = false;
        OnlyStableUpdates = true;
        EnableVsCodeIntegration = false;
        ProxyHost = "";
        ProxyPort = 0;
    }

    // ========== 工具 ==========

    [RelayCommand]
    private void OpenConfigDirectory()
    {
        var configDir = _configService?.GetAppDataPath();
        if (string.IsNullOrEmpty(configDir)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = configDir,
                UseShellExecute = true
            });
        }
        catch { /* ignore */ }
    }
}
