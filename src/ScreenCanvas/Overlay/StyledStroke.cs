using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using ScreenCanvas.Core;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace ScreenCanvas.Overlay;

/// <summary>
/// Ink stroke that renders the design's effect pens (neon glow, airbrush, chalk, crayon, rainbow)
/// while keeping normal InkCanvas hit-testing, erasing and undo behaviour.
/// </summary>
public sealed class StyledStroke : Stroke
{
    public PenMode Mode { get; }

    public StyledStroke(StylusPointCollection points, DrawingAttributes attributes, PenMode mode) : base(points, attributes)
    {
        Mode = mode;
    }

    public static bool HasCustomRendering(PenMode mode) =>
        mode is PenMode.Glow or PenMode.Airbrush or PenMode.Chalk or PenMode.Crayon or PenMode.Rainbow;

    public override Stroke Clone() => new StyledStroke(StylusPoints.Clone(), DrawingAttributes.Clone(), Mode);

    protected override void DrawCore(DrawingContext dc, DrawingAttributes attributes)
    {
        switch (Mode)
        {
            case PenMode.Glow: DrawLayers(dc, attributes, [(2.6, 0.12), (1.9, 0.2), (1.35, 0.35), (1, 1)]); break;
            case PenMode.Airbrush: DrawLayers(dc, attributes, [(2.2, 0.08), (1.7, 0.14), (1.3, 0.22), (0.9, 0.45)]); break;
            case PenMode.Chalk: DrawTextured(dc, attributes, 5, 0.34, 0.55); break;
            case PenMode.Crayon: DrawTextured(dc, attributes, 4, 0.22, 0.8); break;
            case PenMode.Rainbow: DrawRainbow(dc, attributes); break;
            default: base.DrawCore(dc, attributes); break;
        }
    }

    /// <summary>Concentric widened passes of the same colour produce glow / soft spray edges.</summary>
    private void DrawLayers(DrawingContext dc, DrawingAttributes attributes, (double Scale, double Alpha)[] layers)
    {
        var baseAlpha = attributes.Color.A / 255d;
        foreach (var (scale, alpha) in layers)
        {
            var da = attributes.Clone();
            da.Width = Math.Max(1, attributes.Width * scale);
            da.Height = Math.Max(1, attributes.Height * scale);
            da.Color = WithAlpha(attributes.Color, baseAlpha * alpha);
            da.IsHighlighter = false;
            dc.DrawGeometry(new SolidColorBrush(da.Color), null, GetGeometry(da));
        }
    }

    /// <summary>Several thin jittered passes give a porous chalk / waxy crayon texture.</summary>
    private void DrawTextured(DrawingContext dc, DrawingAttributes attributes, int passes, double spread, double alpha)
    {
        var random = new Random(StylusPoints.Count * 7919 + (int)(StylusPoints[0].X * 31 + StylusPoints[0].Y));
        var width = Math.Max(1.5, attributes.Width);
        var baseAlpha = attributes.Color.A / 255d;
        for (var pass = 0; pass < passes; pass++)
        {
            var offset = new Vector((random.NextDouble() - 0.5) * width * spread * 2, (random.NextDouble() - 0.5) * width * spread * 2);
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(ToPoint(StylusPoints[0]) + offset, false, false);
                for (var i = 1; i < StylusPoints.Count; i++)
                {
                    var jitter = new Vector((random.NextDouble() - 0.5) * width * 0.25, (random.NextDouble() - 0.5) * width * 0.25);
                    ctx.LineTo(ToPoint(StylusPoints[i]) + offset + jitter, true, true);
                }
            }
            geometry.Freeze();
            var pen = new Pen(new SolidColorBrush(WithAlpha(attributes.Color, baseAlpha * alpha)), width * (0.45 + random.NextDouble() * 0.3))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            dc.DrawGeometry(null, pen, geometry);
        }
    }

    /// <summary>Hue shifts continuously along the stroke length.</summary>
    private void DrawRainbow(DrawingContext dc, DrawingAttributes attributes)
    {
        var width = Math.Max(1, attributes.Width);
        var alpha = attributes.Color.A;
        var hue = 0d;
        if (StylusPoints.Count == 1)
        {
            dc.DrawEllipse(new SolidColorBrush(FromHue(0, alpha)), null, ToPoint(StylusPoints[0]), width / 2, width / 2);
            return;
        }
        for (var i = 1; i < StylusPoints.Count; i++)
        {
            var a = ToPoint(StylusPoints[i - 1]);
            var b = ToPoint(StylusPoints[i]);
            hue = (hue + (b - a).Length * 1.5) % 360;
            var pen = new Pen(new SolidColorBrush(FromHue(hue, alpha)), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(pen, a, b);
        }
    }

    private static Point ToPoint(StylusPoint p) => new(p.X, p.Y);

    private static Color WithAlpha(Color c, double alpha) => Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), c.R, c.G, c.B);

    private static Color FromHue(double hue, byte alpha)
    {
        var h = hue / 60d;
        var x = 1 - Math.Abs(h % 2 - 1);
        var (r, g, b) = (int)h switch
        {
            0 => (1d, x, 0d),
            1 => (x, 1d, 0d),
            2 => (0d, 1d, x),
            3 => (0d, x, 1d),
            4 => (x, 0d, 1d),
            _ => (1d, 0d, x)
        };
        return Color.FromArgb(alpha, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
