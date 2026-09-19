using MediaColor = System.Windows.Media.Color;

namespace ScreenCanvas.Core;

/// <summary>Visual properties remembered independently for each annotation tool.</summary>
public readonly record struct ToolStyle(
    MediaColor Color,
    double Thickness,
    byte Opacity,
    bool PressureEnabled,
    TimeSpan? FadeDuration,
    bool FillEnabled,
    byte FillOpacity)
{
    public static ToolStyle Capture(ToolSettings settings) => new(
        settings.Color, settings.Thickness, settings.Opacity,
        settings.PressureEnabled, settings.FadeDuration,
        settings.ShapeFillEnabled, settings.ShapeFillOpacity);

    public void Apply(ToolSettings settings)
    {
        settings.Color = Color;
        settings.Thickness = Math.Clamp(Thickness, 1, 64);
        settings.Opacity = Opacity;
        settings.PressureEnabled = PressureEnabled;
        settings.FadeDuration = FadeDuration;
        settings.ShapeFillEnabled = FillEnabled;
        settings.ShapeFillOpacity = FillOpacity;
    }
}
