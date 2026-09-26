using System.Windows.Media;
using Point = System.Windows.Point;
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
    public byte ShapeFillOpacity { get; set; } = 51;
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 22;
    public bool TextBold { get; set; }
    public bool TextItalic { get; set; }
    public bool TextUnderline { get; set; }
    public bool StickyTools { get; set; } = true;

    // Presenter features (defaults follow the InkIt design)
    public double CodeFocusBandHeight { get; set; } = 120;
    public double CodeFocusDimOpacity { get; set; } = 0.85;
    public bool KeyboardVisualizerEnabled { get; set; }
    public bool ClickVisualizerEnabled { get; set; } = true;
    public bool IsHorizontalToolbar { get; set; } = true;
    public double SpotlightRadius { get; set; } = 180;
    public double SpotlightOverlayOpacity { get; set; } = 0.85;
    public double ZoomFactor { get; set; } = 2.0;
    /// <summary>Magnifier mode: false = zoom into a dragged area that stays put (default), true = live zoom that follows the mouse.</summary>
    public bool ZoomFollowsMouse { get; set; }
    /// <summary>Stage curtain drape, 0-100 % of screen height. Runtime only.</summary>
    public double CurtainProgress { get; set; }

    // Canvas alignment
    public bool SnapToGrid { get; set; }
    public int GridSize { get; set; } = 20;
    public bool ShowGridGuides { get; set; } = true;

    // Multi-tool stacking modifiers
    public bool SimultaneousLaser { get; set; }
    public bool SimultaneousSpotlight { get; set; }
    /// <summary>Smart shapes: rough circles, boxes, triangles, lines and arrows drawn with the pen become clean shapes.</summary>
    public bool AutoShapeAssist { get; set; } = true;

    public bool ShowStatusPill { get; set; } = true;

    public int ActiveModifierCount =>
        (SimultaneousLaser ? 1 : 0) + (SimultaneousSpotlight ? 1 : 0) + (SnapToGrid ? 1 : 0);

    public Point Snap(Point point) => !SnapToGrid || GridSize <= 0
        ? point
        : new Point(Math.Round(point.X / GridSize) * GridSize, Math.Round(point.Y / GridSize) * GridSize);
}
