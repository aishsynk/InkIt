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
            ShapeKind.Triangle => Polygon([
                new Point(rect.Left + rect.Width / 2, rect.Top), new Point(rect.Right, rect.Bottom), new Point(rect.Left, rect.Bottom)]),
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
}
