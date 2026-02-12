using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;

namespace AvaGodots.Services;

/// <summary>
/// i18n 国际化服务
/// 通过切换 Avalonia MergedDictionaries 中的 ResourceInclude 实现运行时语言切换
/// </summary>
public static class LocalizationService
{
    private const string StringsBasePath = "avares://AvaGodots/Assets/Strings";

    /// <summary>
    /// 当前语言代码 (e.g. "en", "zh-CN")
    /// </summary>
    public static string CurrentLanguage { get; private set; } = "en";

    /// <summary>
    /// 支持的语言列表
    /// </summary>
    public static IReadOnlyList<string> SupportedLanguages { get; } = ["en", "zh-CN"];

    /// <summary>
    /// 用于显示的语言名称
    /// </summary>
    public static IReadOnlyDictionary<string, string> LanguageDisplayNames { get; } = new Dictionary<string, string>
    {
        ["en"] = "English",
        ["zh-CN"] = "简体中文",
    };

    /// <summary>
    /// 已挂载的 ResourceInclude (用于卸载旧语言)
    /// </summary>
    private static ResourceInclude? _currentResource;

    /// <summary>
    /// 初始化语言系统，将默认语言资源加载到 Application.Resources
    /// 应在 App.Initialize() 之后调用
    /// </summary>
    public static void Initialize(string language = "en")
    {
        SetLanguage(language);
    }

    /// <summary>
    /// 切换当前语言 (运行时热切换)
    /// </summary>
    public static void SetLanguage(string language)
    {
        if (Application.Current is null) return;

        // 移除旧资源
        if (_currentResource is not null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(_currentResource);
        }

        // 加载新语言资源
        var uri = new Uri($"{StringsBasePath}/{language}.axaml");
        _currentResource = new ResourceInclude(uri) { Source = uri };

        Application.Current.Resources.MergedDictionaries.Add(_currentResource);
        CurrentLanguage = language;
    }

    /// <summary>
    /// 根据 key 从当前语言资源获取字符串
    /// 用于 ViewModel 中需要程序化获取本地化文本的场景
    /// </summary>
    public static string GetString(string key, string fallback = "")
    {
        if (Application.Current is null) return fallback;

        if (Application.Current.TryFindResource(key, out var value) && value is string str)
            return str;

        return fallback;
    }
}
