using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ScreenCanvas.UI.Theme;

/// <summary>Turns a height into a fully rounded corner radius (pill shape).</summary>
public sealed class HalfCornerConverter : IValueConverter
{
    public static readonly HalfCornerConverter Instance = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new CornerRadius(value is double d ? d / 2 : 0);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Subtracts a fixed amount from a length, never going below zero.</summary>
public sealed class MinusConverter(double amount) : IValueConverter
{
    public static readonly MinusConverter Four = new(4);
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double d ? Math.Max(0, d - amount) : 0d;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
