using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AvaGodots.Converters;

/// <summary>
/// bool → Brush: true=Green(valid), false=Red(error)
/// </summary>
public class StatusColorConverter : IValueConverter
{
    public static readonly StatusColorConverter Instance = new();

    private static readonly IBrush ValidBrush = new SolidColorBrush(Color.Parse("#8BC34A"));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#FF5252"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? ValidBrush : ErrorBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
