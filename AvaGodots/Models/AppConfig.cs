using System.Collections.Generic;
using Avalonia.Media;

namespace AvaGodots.Models;

/// <summary>
/// 应用配置模型
/// 对应 godots 的 godots.cfg 配置文件
/// </summary>
public class AppConfig
{
    /// <summary>
    /// 编辑器版本文件存储路径
    /// </summary>
    public string VersionsPath { get; set; } = string.Empty;

    /// <summary>
    /// 下载文件存储路径
    /// </summary>
    public string DownloadsPath { get; set; } = string.Empty;

    /// <summary>
    /// 项目配置文件路径
    /// </summary>
    public string ProjectsConfigPath { get; set; } = string.Empty;

    /// <summary>
    /// 编辑器配置文件路径
    /// </summary>
    public string EditorsConfigPath { get; set; } = string.Empty;

    /// <summary>
    /// 默认项目存储目录
    /// </summary>
    public string ProjectsPath { get; set; } = string.Empty;

    /// <summary>
    /// 界面语言
    /// </summary>
    public string Language { get; set; } = "zh_CN";

    /// <summary>
    /// 启动项目/编辑器后是否自动关闭
    /// </summary>
    public bool AutoClose { get; set; }

    /// <summary>
    /// 是否记住窗口大小和位置
    /// </summary>
    public bool RememberWindowSize { get; set; }

    /// <summary>
    /// 是否使用系统标题栏
    /// </summary>
    public bool UseSystemTitleBar { get; set; }

    /// <summary>
    /// 是否只显示稳定版更新
    /// </summary>
    public bool OnlyStableUpdates { get; set; } = true;

    /// <summary>
    /// 默认项目标签
    /// </summary>
    public List<string> DefaultProjectTags { get; set; } = [];

    /// <summary>
    /// 默认编辑器标签
    /// </summary>
    public List<string> DefaultEditorTags { get; set; } =
        ["dev", "rc", "alpha", "4.x", "3.x", "stable", "mono"];

    /// <summary>
    /// 目录命名规范
    /// </summary>
    public DirectoryNamingConvention NamingConvention { get; set; } = DirectoryNamingConvention.SnakeCase;

    /// <summary>
    /// 使用系统原生文件对话框
    /// </summary>
    public bool UseNativeFileDialog { get; set; }

    /// <summary>
    /// VS Code 集成：新建项目时自动创建 .vscode 目录
    /// </summary>
    public bool EnableVsCodeIntegration { get; set; } = true;

    /// <summary>
    /// 显示孤儿编辑器浏览器
    /// </summary>
    public bool ShowOrphanEditorExplorer { get; set; }

    /// <summary>
    /// 允许安装到非空目录
    /// </summary>
    public bool AllowInstallToNotEmptyDir { get; set; }

    /// <summary>
    /// 主题设置
    /// </summary>
    public ThemeConfig Theme { get; set; } = new();

    /// <summary>
    /// 网络代理设置
    /// </summary>
    public ProxyConfig Proxy { get; set; } = new();
}

/// <summary>
/// 主题配置
/// </summary>
public class ThemeConfig
{
    /// <summary>
    /// 主题预设名称
    /// </summary>
    public string Preset { get; set; } = "Default";

    /// <summary>
    /// 强调色
    /// </summary>
    public string AccentColor { get; set; } = "#70BAFA";

    /// <summary>
    /// 基础背景色
    /// </summary>
    public string BaseColor { get; set; } = "#353D4A";

    /// <summary>
    /// 对比度 (-1.0 ~ 1.0)
    /// </summary>
    public double Contrast { get; set; }

    /// <summary>
    /// 主字体大小
    /// </summary>
    public int MainFontSize { get; set; } = 14;

    /// <summary>
    /// 自定义缩放比例（-1 为自动）
    /// </summary>
    public double CustomDisplayScale { get; set; } = -1;
}

/// <summary>
/// 网络代理配置
/// </summary>
public class ProxyConfig
{
    /// <summary>
    /// 代理主机
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// 代理端口
    /// </summary>
    public int Port { get; set; } = 8080;
}

/// <summary>
/// 目录命名规范枚举
/// </summary>
public enum DirectoryNamingConvention
{
    SnakeCase,
    KebabCase,
    CamelCase,
    PascalCase,
    TitleCase
}
