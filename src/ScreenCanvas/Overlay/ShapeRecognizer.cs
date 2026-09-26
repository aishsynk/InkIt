using System.Windows;
using System.Windows.Media;
using ScreenCanvas.Core;
using Point = System.Windows.Point;

namespace ScreenCanvas.Overlay;

/// <summary>A clean shape that replaces a rough pen stroke.</summary>
public sealed record RecognizedShape(ShapeKind Kind, Geometry Geometry);

/// <summary>A "V" stroke (arrowhead) with its apex and the ends of its two arms.</summary>
public sealed record Chevron(Point Apex, Point ArmA, Point ArmB);

/// <summary>
/// Smart shapes: turns hand-drawn strokes into clean geometry.
/// Closed loops become circles/ellipses, boxes, triangles or diamonds; straight strokes become lines
/// (snapped to horizontal, vertical or 45 degrees when close); a line with a hook becomes an arrow.
/// A separate "V" stroke is reported as a <see cref="Chevron"/> so the overlay can turn a nearby line into an arrow.
/// Small marks (dots, letters, ticks) stay freehand.
/// </summary>
public static class ShapeRecognizer
{
    private const double MinLineLength = 40;     // shorter strokes stay freehand (dots, dashes in handwriting)
    private const double MinClosedSize = 46;     // smaller loops are probably letters such as "o"

    public static RecognizedShape? Recognize(IReadOnlyList<Point> raw, double thickness)
    {
        var points = Resample(raw, 96);
        if (points.Count < 8) return null;
        var bounds = Bounds(points);
        var diagonal = Math.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height);
        var length = PathLength(points);
        var gap = (points[^1] - points[0]).Length;

        var closed = length > 1.5 * diagonal && gap < Math.Max(22, 0.24 * diagonal);
        if (closed) return Math.Max(bounds.Width, bounds.Height) >= MinClosedSize ? RecognizeClosed(points, bounds, diagonal) : null;

        if (gap >= MinLineLength && IsStraight(points, gap))
        {
            var (start, end) = SnapAngle(points[0], points[^1]);
            return new RecognizedShape(ShapeKind.Line, new LineGeometry(start, end));
        }

        if (RecognizeHookedArrow(points) is { } arrow)
            return new RecognizedShape(ShapeKind.Arrow, ShapeGeometry.Build(ShapeKind.Arrow, arrow.Start, arrow.End, thickness));

        return null;
    }

    /// <summary>A two-armed "V" such as an arrowhead drawn after a line (or a tick mark).</summary>
    public static Chevron? RecognizeChevron(IReadOnlyList<Point> raw)
    {
        var points = Resample(raw, 64);
        if (points.Count < 6) return null;
        var corners = Simplify(points, Math.Max(4, 0.12 * PathLength(points)));
        if (corners.Count != 3) return null;
        var (a, apex, b) = (corners[0], corners[1], corners[2]);
        var armA = (a - apex).Length;
        var armB = (b - apex).Length;
        if (armA < 10 || armB < 10 || Math.Max(armA, armB) > 4 * Math.Min(armA, armB)) return null;
        var angle = Vector.AngleBetween(a - apex, b - apex);
        angle = Math.Abs(angle);
        return angle is >= 20 and <= 140 ? new Chevron(apex, a, b) : null;
    }

    // ------------------------------------------------------------------ Closed shapes

    private static RecognizedShape? RecognizeClosed(List<Point> points, Rect bounds, double diagonal)
    {
        var fill = PolygonArea(points) / Math.Max(1, bounds.Width * bounds.Height);
        var roundness = RadialSpread(points, bounds);
        var corners = ClosedCorners(points, diagonal);

        // Triangle: three real corners, about half the bounding box filled.
        if (corners.Count == 3 && fill is > 0.3 and < 0.7)
            return new RecognizedShape(ShapeKind.Triangle, ShapeGeometry.Polygon(corners));

        if (corners.Count == 4)
        {
            // Diamond: corners near the middles of the bounding box edges.
            if (fill is > 0.35 and < 0.7 && IsDiamond(corners, bounds))
                return new RecognizedShape(ShapeKind.Diamond, ShapeGeometry.Build(ShapeKind.Diamond, bounds.TopLeft, bounds.BottomRight, 0));
            // Box: right-angled corners.
            if (AllRightAngles(corners))
            {
                return IsAxisAligned(corners)
                    ? new RecognizedShape(ShapeKind.Rectangle, new RectangleGeometry(bounds))
                    : new RecognizedShape(ShapeKind.Rectangle, ShapeGeometry.Polygon(corners));
            }
        }

        // Circle / ellipse: evenly round, about pi/4 of the bounding box filled.
        if (fill is > 0.64 and < 0.88 && roundness < 0.15)
        {
            var ellipse = bounds;
            var major = Math.Max(bounds.Width, bounds.Height);
            if (Math.Abs(bounds.Width - bounds.Height) < 0.14 * major)
            {
                // Nearly round: make it a true circle about the same centre.
                var size = (bounds.Width + bounds.Height) / 2;
                var centre = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                ellipse = new Rect(centre.X - size / 2, centre.Y - size / 2, size, size);
            }
            return new RecognizedShape(ShapeKind.Ellipse, new EllipseGeometry(ellipse));
        }

        // Rough box whose corners got rounded while drawing.
        if (fill >= 0.86) return new RecognizedShape(ShapeKind.Rectangle, new RectangleGeometry(bounds));
        return null;
    }

    private static bool IsDiamond(IReadOnlyList<Point> corners, Rect bounds)
    {
        var mids = new[]
        {
            new Point(bounds.Left + bounds.Width / 2, bounds.Top), new Point(bounds.Right, bounds.Top + bounds.Height / 2),
            new Point(bounds.Left + bounds.Width / 2, bounds.Bottom), new Point(bounds.Left, bounds.Top + bounds.Height / 2)
        };
        var tolerance = 0.22 * Math.Max(bounds.Width, bounds.Height);
        return mids.All(m => corners.Any(c => (c - m).Length < tolerance));
    }

    private static bool AllRightAngles(IReadOnlyList<Point> corners)
    {
        for (var i = 0; i < corners.Count; i++)
        {
            var prev = corners[(i + corners.Count - 1) % corners.Count];
            var next = corners[(i + 1) % corners.Count];
            var angle = Math.Abs(Vector.AngleBetween(prev - corners[i], next - corners[i]));
            if (angle is < 62 or > 118) return false;
        }
        return true;
    }

    private static bool IsAxisAligned(IReadOnlyList<Point> corners)
    {
        for (var i = 0; i < corners.Count; i++)
        {
            var edge = corners[(i + 1) % corners.Count] - corners[i];
            var angle = Math.Abs(Math.Atan2(edge.Y, edge.X) * 180 / Math.PI) % 90;
            if (angle is > 16 and < 74) return false;
        }
        return true;
    }

    /// <summary>Corners of a closed loop: simplified outline, nearly straight joints and tiny edges removed.</summary>
    private static List<Point> ClosedCorners(List<Point> points, double diagonal)
    {
        // Split the loop at the point farthest from the start, simplify both halves.
        var far = 0;
        for (var i = 1; i < points.Count; i++)
            if ((points[i] - points[0]).Length > (points[far] - points[0]).Length) far = i;
        var epsilon = 0.075 * diagonal;
        var first = Simplify(points.Take(far + 1).ToList(), epsilon);
        var second = Simplify(points.Skip(far).Append(points[0]).ToList(), epsilon);
        var corners = first.Concat(second.Skip(1).SkipLast(1)).ToList();

        var changed = true;
        while (changed && corners.Count > 3)
        {
            changed = false;
            for (var i = 0; i < corners.Count; i++)
            {
                var prev = corners[(i + corners.Count - 1) % corners.Count];
                var next = corners[(i + 1) % corners.Count];
                var angle = Math.Abs(Vector.AngleBetween(prev - corners[i], next - corners[i]));
                if (angle > 150 || (corners[i] - prev).Length < 0.12 * diagonal)
                {
                    corners.RemoveAt(i);
                    changed = true;
                    break;
                }
            }
        }
        return corners;
    }

    /// <summary>Spread of the loop's distance from the centre, measured on the bounding ellipse (0 = perfectly elliptical).</summary>
    private static double RadialSpread(IReadOnlyList<Point> points, Rect bounds)
    {
        var cx = bounds.X + bounds.Width / 2;
        var cy = bounds.Y + bounds.Height / 2;
        var a = Math.Max(1, bounds.Width / 2);
        var b = Math.Max(1, bounds.Height / 2);
        var radii = points.Select(p => Math.Sqrt(Math.Pow((p.X - cx) / a, 2) + Math.Pow((p.Y - cy) / b, 2))).ToList();
        var mean = radii.Average();
        var variance = radii.Sum(r => (r - mean) * (r - mean)) / radii.Count;
        return Math.Sqrt(variance) / Math.Max(0.001, mean);
    }

    // ------------------------------------------------------------------ Lines and arrows

    private static bool IsStraight(IReadOnlyList<Point> points, double chord)
    {
        var start = points[0];
        var end = points[^1];
        var maxDeviation = points.Max(p => DistanceToLine(p, start, end));
        return PathLength(points) / chord < 1.09 && maxDeviation < Math.Max(7, 0.07 * chord);
    }

    /// <summary>Snaps the direction to horizontal, vertical or 45 degrees when within 5 degrees.</summary>
    private static (Point Start, Point End) SnapAngle(Point start, Point end)
    {
        var v = end - start;
        var angle = Math.Atan2(v.Y, v.X) * 180 / Math.PI;
        var snapped = Math.Round(angle / 45) * 45;
        if (Math.Abs(angle - snapped) > 5) return (start, end);
        var radians = snapped * Math.PI / 180;
        var length = v.Length;
        return (start, new Point(start.X + length * Math.Cos(radians), start.Y + length * Math.Sin(radians)));
    }

    /// <summary>One stroke: a straight shaft ending in a small hook or "V" drawn back from the tip.</summary>
    private static (Point Start, Point End)? RecognizeHookedArrow(List<Point> points)
    {
        var corners = Simplify(points, Math.Max(5, 0.04 * PathLength(points)));
        if (corners.Count < 3) return null;
        var start = corners[0];
        var tip = corners[1];
        var shaft = tip - start;
        if (shaft.Length < 60) return null;
        var direction = shaft / shaft.Length;

        // The shaft itself must be straight.
        var tipIndex = points.IndexOf(points.OrderBy(p => (p - tip).Length).First());
        var shaftPoints = points.Take(tipIndex + 1).ToList();
        if (shaftPoints.Count < 3 || shaftPoints.Max(p => DistanceToLine(p, start, tip)) > Math.Max(8, 0.08 * shaft.Length)) return null;

        // Everything after the tip stays close to it and falls back behind it (the head).
        var head = points.Skip(tipIndex).ToList();
        var reach = head.Max(p => (p - tip).Length);
        if (reach < 0.06 * shaft.Length || reach > 0.5 * shaft.Length) return null;
        if (head.Any(p => Vector.Multiply(p - tip, direction) > 0.15 * shaft.Length)) return null;
        var sideways = head.Max(p => Math.Abs(Vector.CrossProduct(direction, p - tip)));
        if (sideways < 0.03 * shaft.Length) return null;
        return (start, tip);
    }

    // ------------------------------------------------------------------ Geometry helpers

    private static List<Point> Resample(IReadOnlyList<Point> raw, int count)
    {
        var clean = new List<Point>();
        foreach (var p in raw)
            if (clean.Count == 0 || (p - clean[^1]).Length > 0.5) clean.Add(p);
        if (clean.Count < 2) return clean;
        var total = PathLength(clean);
        if (total < 1) return clean;
        var step = total / (count - 1);
        var result = new List<Point> { clean[0] };
        var carried = 0d;
        for (var i = 1; i < clean.Count; i++)
        {
            var a = clean[i - 1];
            var b = clean[i];
            var segment = (b - a).Length;
            while (carried + segment >= step && result.Count < count)
            {
                var t = (step - carried) / segment;
                a = a + (b - a) * t;
                result.Add(a);
                segment = (b - a).Length;
                carried = 0;
            }
            carried += segment;
        }
        if ((result[^1] - clean[^1]).Length > 0.5) result.Add(clean[^1]);
        return result;
    }

    /// <summary>Ramer-Douglas-Peucker simplification of an open polyline.</summary>
    private static List<Point> Simplify(List<Point> points, double epsilon)
    {
        if (points.Count < 3) return [.. points];
        var index = 0;
        var max = 0d;
        for (var i = 1; i < points.Count - 1; i++)
        {
            var d = DistanceToLine(points[i], points[0], points[^1]);
            if (d > max) { max = d; index = i; }
        }
        if (max <= epsilon) return [points[0], points[^1]];
        var left = Simplify(points.Take(index + 1).ToList(), epsilon);
        var right = Simplify(points.Skip(index).ToList(), epsilon);
        return left.Concat(right.Skip(1)).ToList();
    }

    private static double DistanceToLine(Point p, Point a, Point b)
    {
        var ab = b - a;
        var length = ab.Length;
        if (length < 0.001) return (p - a).Length;
        return Math.Abs(Vector.CrossProduct(ab, p - a)) / length;
    }

    private static double PathLength(IReadOnlyList<Point> points)
    {
        var length = 0d;
        for (var i = 1; i < points.Count; i++) length += (points[i] - points[i - 1]).Length;
        return length;
    }

    private static double PolygonArea(IReadOnlyList<Point> points)
    {
        var sum = 0d;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(sum) / 2;
    }

    private static Rect Bounds(IReadOnlyList<Point> points)
    {
        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        return new Rect(minX, minY, points.Max(p => p.X) - minX, points.Max(p => p.Y) - minY);
    }
}
