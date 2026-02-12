using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AvaGodots;

/// <summary>
/// 设置分类选中状态转换器
/// 将分类索引转换为背景色
/// </summary>
public class SettingsCategoryConverter : IValueConverter
{
    public static SettingsCategoryConverter Instance { get; } = new();

    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#344966"));
    private static readonly IBrush NormalBrush = Brushes.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int selectedIndex && parameter is string paramStr &&
            int.TryParse(paramStr, out var targetIndex))
        {
            return selectedIndex == targetIndex ? SelectedBrush : NormalBrush;
        }
        return NormalBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
