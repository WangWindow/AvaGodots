using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AvaGodots;

/// <summary>
/// 标签页索引转换器
/// 用于 ToggleButton 的 IsChecked 绑定，将整数索引转换为布尔值
/// </summary>
public class TabIndexConverter : IValueConverter
{
    /// <summary>
    /// 对应的标签页索引
    /// </summary>
    public int TargetIndex { get; set; }

    /// <summary>
    /// 项目页面（索引 0）
    /// </summary>
    public static TabIndexConverter Projects { get; } = new() { TargetIndex = 0 };

    /// <summary>
    /// 资源库页面（索引 1）
    /// </summary>
    public static TabIndexConverter AssetLib { get; } = new() { TargetIndex = 1 };

    /// <summary>
    /// 编辑器页面（索引 2）
    /// </summary>
    public static TabIndexConverter Editors { get; } = new() { TargetIndex = 2 };

    /// <summary>
    /// 设置页面（索引 3）
    /// </summary>
    public static TabIndexConverter Settings { get; } = new() { TargetIndex = 3 };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index)
            return index == TargetIndex;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return TargetIndex;
        return -1;
    }
}
