using System.Windows;
using System.Windows.Media;
using ScreenCanvas.Core;
using Point = System.Windows.Point;

namespace ScreenCanvas.Overlay;

/// <summary>Vector geometry for annotation shapes, following the InkIt design's canvas renderer.</summary>
public static class ShapeGeometry
{
    public static bool HasSolidHead(ShapeKind kind) => kind is ShapeKind.Arrow or ShapeKind.DoubleArrow;

    public static Geometry Build(ShapeKind kind, Point start, Point end, double thickness)
    {
        var rect = new Rect(start, end);
        return kind switch
        {
            ShapeKind.Line => new LineGeometry(start, end),
            ShapeKind.Arrow => Arrow(start, end, thickness, false),
            ShapeKind.DoubleArrow => Arrow(start, end, thickness, true),
            ShapeKind.Rectangle => new RectangleGeometry(rect),
            ShapeKind.RoundedRectangle => RoundedRect(rect),
            ShapeKind.Ellipse => new EllipseGeometry(rect),
            ShapeKind.Diamond => Polygon([
                new Point(rect.Left + rect.Width / 2, rect.Top), new Point(rect.Right, rect.Top + rect.Height / 2),
                new Point(rect.Left + rect.Width / 2, rect.Bottom), new Point(rect.Left, rect.Top + rect.Height / 2)]),
            _ => new RectangleGeometry(rect)
        };
    }

    /// <summary>Corner radius min(16, w/4, h/4), as in the design.</summary>
    private static Geometry RoundedRect(Rect rect)
    {
        var r = Math.Min(16, Math.Min(rect.Width / 4, rect.Height / 4));
        return new RectangleGeometry(rect, r, r);
    }

    /// <summary>Straight shaft with a filled 30° arrowhead of length max(16, thickness * 3.5).</summary>
    private static Geometry Arrow(Point start, Point end, double thickness, bool bothEnds)
    {
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        group.Children.Add(new LineGeometry(start, end));
        if ((end - start).Length < 1) return group;
        group.Children.Add(Head(start, end, thickness));
        if (bothEnds) group.Children.Add(Head(end, start, thickness));
        return group;
    }

    private static Geometry Head(Point from, Point to, double thickness)
    {
        var headLength = Math.Max(16, thickness * 3.5);
        var angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
        return Polygon([
            to,
            new Point(to.X - headLength * Math.Cos(angle - Math.PI / 6), to.Y - headLength * Math.Sin(angle - Math.PI / 6)),
            new Point(to.X - headLength * Math.Cos(angle + Math.PI / 6), to.Y - headLength * Math.Sin(angle + Math.PI / 6))]);
    }

    public static Geometry Polygon(IReadOnlyList<Point> points)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
        return new PathGeometry([figure]);
    }

    /// <summary>
    /// Auto-shape assist: a nearly closed loop becomes an ellipse (roughly square bounds) or rectangle,
    /// a nearly straight stroke becomes a line. Returns null when the stroke should stay freehand.
    /// </summary>
    public static (ShapeKind Kind, Point Start, Point End)? Recognize(IReadOnlyList<Point> points)
    {
        if (points.Count < 6) return null;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue, length = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
            minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
            if (i > 0) length += (p - points[i - 1]).Length;
        }
        var w = maxX - minX;
        var h = maxY - minY;
        var gap = (points[^1] - points[0]).Length;
        if (gap < 40 && length > 80)
        {
            var square = Math.Abs(w - h) < Math.Max(w, h) * 0.45;
            return (square ? ShapeKind.Ellipse : ShapeKind.Rectangle, new Point(minX, minY), new Point(maxX, maxY));
        }
        if (length > 40 && gap / length > 0.88) return (ShapeKind.Line, points[0], points[^1]);
        return null;
    }
}
