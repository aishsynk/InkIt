using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Size = System.Windows.Size;

namespace ScreenCanvas.UI.Controls;

/// <summary>
/// Renders a Lucide icon (24x24 stroke geometry from UI/Icons/LucideIcons.xaml) the way lucide-react does:
/// 2px round-capped strokes scaled to <see cref="Size"/>. The stroke follows the inherited Foreground.
/// </summary>
public sealed class LucideIcon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(LucideIcon),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((LucideIcon)d)._geometry = null));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(LucideIcon),
        new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(LucideIcon),
        new FrameworkPropertyMetadata(System.Windows.Media.Brushes.White, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(LucideIcon),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeWidthProperty = DependencyProperty.Register(
        nameof(StrokeWidth), typeof(double), typeof(LucideIcon),
        new FrameworkPropertyMetadata(2d, FrameworkPropertyMetadataOptions.AffectsRender));

    private Geometry? _geometry;

    public LucideIcon()
    {
        SnapsToDevicePixels = true;
        IsHitTestVisible = false;
    }

    public LucideIcon(string kind, double size = 16) : this()
    {
        Kind = kind;
        Size = size;
    }

    public string? Kind { get => (string?)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    /// <summary>Optional fill (e.g. a filled favourite star). Lucide icons are stroke-only by default.</summary>
    public Brush? Fill { get => (Brush?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public double StrokeWidth { get => (double)GetValue(StrokeWidthProperty); set => SetValue(StrokeWidthProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override void OnRender(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Kind)) return;
        _geometry ??= TryFindResource("Lucide." + Kind) as Geometry;
        if (_geometry is null) return;
        var scale = Size / 24d;
        var pen = new System.Windows.Media.Pen(Foreground, StrokeWidth)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawGeometry(Fill, pen, _geometry);
        dc.Pop();
    }
}
