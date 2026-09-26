using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenCanvas.Core;
using Brush = System.Windows.Media.Brush;
using Point = System.Windows.Point;

namespace ScreenCanvas.Overlay;

/// <summary>
/// Smart shapes: a rough pen stroke is replaced by the clean shape it resembles (see <see cref="ShapeRecognizer"/>),
/// and a "V" drawn at the end of a line turns that line into an arrow. Every conversion is one undo step:
/// Undo once brings the freehand drawing back, Undo again removes it.
/// </summary>
public partial class OverlayWindow
{
    private bool TrySmartShape(Stroke stroke)
    {
        var points = stroke.StylusPoints.Select(p => new Point(p.X, p.Y)).ToList();
        if (ShapeRecognizer.RecognizeChevron(points) is { } chevron && TryAddArrowHead(stroke, chevron)) return true;
        if (ShapeRecognizer.Recognize(points, _settings.Thickness) is not { } shape) return false;

        var path = NewShapePath(shape.Kind);
        path.Data = shape.Geometry;
        SnapConnector(path);
        InkSurface.Strokes.Remove(stroke);
        ShapeSurface.Children.Add(path);
        RecordConversion(stroke,
            undo: () => { RemoveAnnotation(path); RestoreAnnotation(stroke); },
            redo: () => { RemoveAnnotation(stroke); RestoreAnnotation(path); });
        ScheduleFade(path);
        return true;
    }

    /// <summary>Turns the line (or single arrow) whose end the "V" was drawn at into an arrow (or double arrow).</summary>
    private bool TryAddArrowHead(Stroke stroke, Chevron chevron)
    {
        var armA = chevron.ArmA - chevron.Apex;
        var armB = chevron.ArmB - chevron.Apex;
        var opening = Normalized(Normalized(armA) + Normalized(armB));
        var tolerance = Math.Max(30, 0.8 * (armA.Length + armB.Length) / 2);

        Path? target = null;
        (Point Start, Point End) local = default;
        var headAtEnd = true;
        var best = double.MaxValue;
        foreach (var path in ShapeSurface.Children.OfType<Path>())
        {
            if (path.Tag is not ShapeKind kind || kind is not (ShapeKind.Line or ShapeKind.Arrow)) continue;
            if (Shaft(path) is not { } shaft) continue;
            var offset = GetOffset(path);
            var start = shaft.Start + offset;
            var end = shaft.End + offset;
            foreach (var (tip, tail, atEnd) in new[] { (end, start, true), (start, end, false) })
            {
                if (kind == ShapeKind.Arrow && atEnd) continue; // that end already has a head
                var distance = (tip - chevron.Apex).Length;
                if (distance > tolerance || distance >= best) continue;
                // The V must open back along the shaft, like a real arrowhead.
                if (Vector.Multiply(opening, Normalized(tail - tip)) < 0.6) continue;
                (target, local, headAtEnd, best) = (path, shaft, atEnd, distance);
            }
        }
        if (target is null) return false;

        var oldKind = (ShapeKind)target.Tag;
        var oldData = target.Data;
        var oldFill = target.Fill;
        var newKind = oldKind == ShapeKind.Arrow ? ShapeKind.DoubleArrow : ShapeKind.Arrow;
        var (from, to) = headAtEnd ? (local.Start, local.End) : (local.End, local.Start);
        var newData = ShapeGeometry.Build(newKind, from, to, target.StrokeThickness);

        void Apply(ShapeKind kind, Geometry data, Brush fill) { target.Tag = kind; target.Data = data; target.Fill = fill; }
        InkSurface.Strokes.Remove(stroke);
        Apply(newKind, newData, target.Stroke);
        RecordConversion(stroke,
            undo: () => { Apply(oldKind, oldData, oldFill); RestoreAnnotation(stroke); },
            redo: () => { RemoveAnnotation(stroke); Apply(newKind, newData, target.Stroke); });
        return true;
    }

    // ------------------------------------------------------------------ Connectors

    private const double ConnectorReach = 26;

    /// <summary>
    /// Connectors: a line or arrow whose end lands near a box, circle, diamond or triangle is snapped onto that
    /// shape's edge, so flowcharts come out tidy.
    /// </summary>
    private void SnapConnector(Path path)
    {
        if (path.Tag is not ShapeKind kind || kind is not (ShapeKind.Line or ShapeKind.Arrow or ShapeKind.DoubleArrow)) return;
        if (Shaft(path) is not { } shaft) return;
        var offset = GetOffset(path);
        var start = SnapToOutline(shaft.Start + offset, path) - offset;
        var end = SnapToOutline(shaft.End + offset, path) - offset;
        if (start == shaft.Start && end == shaft.End) return;
        path.Data = kind == ShapeKind.Line ? new LineGeometry(start, end) : ShapeGeometry.Build(kind, start, end, path.StrokeThickness);
    }

    private Point SnapToOutline(Point point, Path self)
    {
        var best = point;
        var bestDistance = ConnectorReach;
        foreach (var shape in ShapeSurface.Children.OfType<Path>())
        {
            if (ReferenceEquals(shape, self) || shape.Tag is not ShapeKind kind) continue;
            if (kind is not (ShapeKind.Rectangle or ShapeKind.RoundedRectangle or ShapeKind.Ellipse or ShapeKind.Diamond or ShapeKind.Triangle)) continue;
            var offset = GetOffset(shape);
            var bounds = shape.Data.Bounds;
            bounds.Offset(offset);
            bounds.Inflate(ConnectorReach, ConnectorReach);
            if (!bounds.Contains(point)) continue;
            var outline = shape.Data.GetFlattenedPathGeometry(0.5, ToleranceType.Absolute);
            foreach (var figure in outline.Figures)
            {
                var previous = figure.StartPoint + offset;
                foreach (var segment in figure.Segments.OfType<PolyLineSegment>())
                    foreach (var raw in segment.Points.Append(figure.StartPoint))
                    {
                        var current = raw + offset;
                        var candidate = NearestOnSegment(point, previous, current);
                        var distance = (candidate - point).Length;
                        if (distance < bestDistance) { bestDistance = distance; best = candidate; }
                        previous = current;
                    }
            }
        }
        return best;
    }

    private static Point NearestOnSegment(Point p, Point a, Point b)
    {
        var ab = b - a;
        var lengthSquared = ab.LengthSquared;
        if (lengthSquared < 0.0001) return a;
        var t = Math.Clamp(Vector.Multiply(p - a, ab) / lengthSquared, 0, 1);
        return a + ab * t;
    }

    /// <summary>History: the freehand stroke, then the conversion, so Undo peels them off one at a time.</summary>
    private void RecordConversion(Stroke original, Action undo, Action redo)
    {
        _history.Add(original);
        _history.Add(new EditOperation(undo, redo));
        _removed.Clear();
    }

    /// <summary>The straight shaft of a line or arrow shape, in its own (untranslated) coordinates.</summary>
    private static (Point Start, Point End)? Shaft(Path path) => path.Data switch
    {
        LineGeometry line => (line.StartPoint, line.EndPoint),
        GeometryGroup { Children.Count: > 0 } group when group.Children[0] is LineGeometry line => (line.StartPoint, line.EndPoint),
        _ => null
    };

    private static Vector Normalized(Vector v) => v.Length < 0.0001 ? v : v / v.Length;
}
