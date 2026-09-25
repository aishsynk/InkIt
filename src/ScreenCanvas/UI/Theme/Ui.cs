using System.Windows;
using Brush = System.Windows.Media.Brush;

namespace ScreenCanvas.UI.Theme;

/// <summary>Attached properties consumed by the "Ink.Button" template for per-element hover states.</summary>
public static class Ui
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius", typeof(CornerRadius), typeof(Ui), new FrameworkPropertyMetadata(new CornerRadius(8)));

    public static readonly DependencyProperty HoverBackgroundProperty = DependencyProperty.RegisterAttached(
        "HoverBackground", typeof(Brush), typeof(Ui), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty HoverForegroundProperty = DependencyProperty.RegisterAttached(
        "HoverForeground", typeof(Brush), typeof(Ui), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty HoverBorderBrushProperty = DependencyProperty.RegisterAttached(
        "HoverBorderBrush", typeof(Brush), typeof(Ui), new FrameworkPropertyMetadata(null));

    public static CornerRadius GetCornerRadius(DependencyObject d) => (CornerRadius)d.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius(DependencyObject d, CornerRadius value) => d.SetValue(CornerRadiusProperty, value);
    public static Brush? GetHoverBackground(DependencyObject d) => (Brush?)d.GetValue(HoverBackgroundProperty);
    public static void SetHoverBackground(DependencyObject d, Brush? value) => d.SetValue(HoverBackgroundProperty, value);
    public static Brush? GetHoverForeground(DependencyObject d) => (Brush?)d.GetValue(HoverForegroundProperty);
    public static void SetHoverForeground(DependencyObject d, Brush? value) => d.SetValue(HoverForegroundProperty, value);
    public static Brush? GetHoverBorderBrush(DependencyObject d) => (Brush?)d.GetValue(HoverBorderBrushProperty);
    public static void SetHoverBorderBrush(DependencyObject d, Brush? value) => d.SetValue(HoverBorderBrushProperty, value);
}
