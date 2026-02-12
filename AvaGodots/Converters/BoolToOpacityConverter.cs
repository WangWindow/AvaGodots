using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AvaGodots.Converters;

/// <summary>
/// 布尔值转不透明度转换器
/// true → 正常显示 (1.0), false → 半透明 (0.5)
/// 用于缺失项目/无效编辑器的视觉反馈
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    /// <summary>
    /// 默认实例：true=1.0, false=0.5
    /// </summary>
    public static readonly BoolToOpacityConverter Instance = new();

    /// <summary>
    /// 反转实例：true=0.5, false=1.0（用于 IsMissing 等反向逻辑）
    /// </summary>
    public static readonly BoolToOpacityConverter Inverted = new() { Invert = true };

    /// <summary>
    /// 是否反转逻辑
    /// </summary>
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            if (Invert) b = !b;
            return b ? 1.0 : 0.5;
        }
        return 1.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
