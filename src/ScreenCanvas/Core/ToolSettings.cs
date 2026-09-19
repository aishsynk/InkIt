using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace ScreenCanvas.Core;

public sealed class ToolSettings
{
    public static readonly MediaColor TeachingBlue = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#2563EB");
    public static readonly MediaColor TeachingRed = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#E5484D");
    public static readonly MediaColor TeachingGreen = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#16A34A");
    public static readonly MediaColor TeachingAmber = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#F2B705");
    public static readonly MediaColor TeachingPurple = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#7C3AED");
    public static readonly MediaColor AdaptiveLight = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#0F172A");
    public static readonly MediaColor AdaptiveDark = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF");

    public static IReadOnlyList<MediaColor> PrimaryTeachingColors =>
    [
        TeachingBlue,
        TeachingRed,
        TeachingGreen,
        TeachingAmber,
        TeachingPurple,
        AdaptiveLight
    ];

    public ToolKind Tool { get; set; } = ToolKind.Cursor;
    public PenMode PenMode { get; set; } = PenMode.Ballpoint;
    public ShapeKind Shape { get; set; } = ShapeKind.Arrow;
    public MediaColor Color { get; set; } = TeachingBlue;
    public double Thickness { get; set; } = 4;
    public byte Opacity { get; set; } = 255;
    public bool PressureEnabled { get; set; } = true;
    public int MarkerNumber { get; set; } = 1;
    public bool LetterMarkers { get; set; }
    public bool SquareMarkers { get; set; }
    public double MarkerSize { get; set; } = 34;
    public TimeSpan? FadeDuration { get; set; }
    public bool ShapeFillEnabled { get; set; }
    public byte ShapeFillOpacity { get; set; } = 40;
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 22;
    public bool TextBold { get; set; }
    public bool TextItalic { get; set; }
    public bool TextUnderline { get; set; }
    public bool StickyTools { get; set; } = true;

    // Trainer features
    public double CodeFocusBandHeight { get; set; } = 90;
    public double CodeFocusDimOpacity { get; set; } = 0.65;
    public bool KeyboardVisualizerEnabled { get; set; }
    public bool ClickVisualizerEnabled { get; set; } = true;
    public bool IsHorizontalToolbar { get; set; } = false;

    // Blur/Pixelate
    public double BlurRadius { get; set; } = 10;
    public int BlurPixelationLevel { get; set; } = 10;

    // Spotlight
    public double SpotlightRadius { get; set; } = 110;
    public double SpotlightOverlayOpacity { get; set; } = 0.5;

    // Eyedropper
    public bool EyedropperAutoSwitchBack { get; set; } = true;
}
