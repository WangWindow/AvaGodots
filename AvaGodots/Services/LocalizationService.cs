using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace AvaGodots.Services;

/// <summary>
/// i18n 国际化服务
/// 通过切换 Avalonia MergedDictionaries 中的强类型 ResourceDictionary 实现运行时语言切换
/// </summary>
public static class LocalizationService
{
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
    /// 已挂载的语言资源（用于卸载旧语言）
    /// </summary>
    private static IResourceProvider? _currentResource;

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

        if (!LanguageDisplayNames.ContainsKey(language))
            language = "en";

        // 移除旧资源
        if (_currentResource is not null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(_currentResource);
        }

        var resourceKey = $"L10N.{language}";
        if (!Application.Current.TryFindResource(resourceKey, out var resource) || resource is not IResourceProvider provider)
        {
            if (!Application.Current.TryFindResource("L10N.en", out resource) || resource is not IResourceProvider fallback)
                return;

            provider = fallback;
            language = "en";
        }

        _currentResource = provider;

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
