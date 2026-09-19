using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace ScreenCanvas.Presentation;

public sealed record PointerEffectsOptions
{
    public bool LaserTrail { get; init; } = true;
    public bool CursorHalo { get; init; }
    public bool ClickPulse { get; init; } = true;
    public bool Spotlight { get; init; }
    public MediaColor AccentColor { get; init; } = MediaColor.FromRgb(255, 64, 96);
    public double PointerRadius { get; init; } = 8;
    public double HaloRadius { get; init; } = 28;
    public double SpotlightRadius { get; init; } = 140;
    public double SpotlightDimOpacity { get; init; } = .72;
    public TimeSpan TrailLifetime { get; init; } = TimeSpan.FromMilliseconds(650);
}

public sealed record CurtainOptions
{
    public MediaColor Color { get; init; } = Colors.Black;
    public double Opacity { get; init; } = 1;
    public string? Message { get; init; }
}
