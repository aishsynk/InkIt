using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Mouse = System.Windows.Input.Mouse;

namespace ScreenCanvas.Overlay;

/// <summary>Multi-select marquee, group drag and the batch quick-actions bar (design: CanvasOverlay select tool).</summary>
public partial class OverlayWindow
{
    private static readonly Color[] QuickColors =
    [
        Tw.Hex("#2563EB"), Tw.Hex("#DC2626"), Tw.Hex("#16A34A"), Tw.Hex("#F2B705"), Tw.Hex("#9333EA"), Colors.White
    ];

    private readonly HashSet<object> _selection = [];
    private readonly List<UIElement> _selectionVisuals = [];
    private Point? _marqueeStart;
    private Rect? _marquee;
    private Point? _dragLast;
    private Vector _dragTotal;
    private Border? _actionBar;
    private TextBlock? _actionCount;

    // ---------------------------------------------------------------- Pointer flow

    private void BeginSelection(Point point, bool additive)
    {
        SetNoActivate(false);
        Activate();
        var clicked = HitTestItem(point);
        var bounds = SelectionBounds();
        if (clicked is not null)
        {
            if (additive)
            {
                if (!_selection.Remove(clicked)) _selection.Add(clicked);
            }
            else if (!_selection.Contains(clicked))
            {
                _selection.Clear();
                _selection.Add(clicked);
            }
            StartDrag(point);
        }
        else if (bounds is Rect b && b.Contains(point) && _selection.Count > 0)
        {
            StartDrag(point);
        }
        else
        {
            if (!additive) _selection.Clear();
            _marqueeStart = point;
            _marquee = new Rect(point, point);
        }
        InputRoot.CaptureMouse();
        RenderSelection();
    }

    private void StartDrag(Point point)
    {
        _dragLast = point;
        _dragTotal = default;
    }

    private bool ContinueSelection(Point point)
    {
        if (_marqueeStart is Point start)
        {
            _marquee = new Rect(start, point);
            _selection.Clear();
            foreach (var item in AllItems())
                if (ItemBounds(item) is Rect r && r.IntersectsWith(_marquee.Value)) _selection.Add(item);
            RenderSelection();
            return true;
        }
        if (_dragLast is Point last)
        {
            var delta = point - last;
            if (delta.Length > 0)
            {
                foreach (var item in _selection) MoveItem(item, delta);
                _dragTotal += delta;
                _dragLast = point;
                RenderSelection();
            }
            return true;
        }
        return false;
    }

    private void EndSelection()
    {
        if (_dragLast is not null && _dragTotal.Length > 0.5)
        {
            var items = _selection.ToArray();
            var total = _dragTotal;
            PushEdit(() => { foreach (var i in items) MoveItem(i, -total); }, () => { foreach (var i in items) MoveItem(i, total); });
        }
        _dragLast = null;
        _marqueeStart = null;
        _marquee = null;
        if (Mouse.Captured == InputRoot) InputRoot.ReleaseMouseCapture();
        RenderSelection();
    }

    private void ClearSelection()
    {
        if (_selection.Count == 0 && _marquee is null && _selectionVisuals.Count == 0) return;
        _selection.Clear();
        _marquee = null;
        _marqueeStart = null;
        _dragLast = null;
        RenderSelection();
    }

    private bool IsInsideChrome(DependencyObject source) => IsDescendant(ChromeSurface, source) && !ReferenceEquals(source, ChromeSurface);

    // ---------------------------------------------------------------- Item model

    private IEnumerable<object> AllItems() => InkSurface.Strokes.Cast<object>().Concat(ShapeSurface.Children.Cast<object>());

    private object? HitTestItem(Point p)
    {
        for (var i = ShapeSurface.Children.Count - 1; i >= 0; i--)
        {
            if (ShapeSurface.Children[i] is not UIElement element) continue;
            if (ItemBounds(element) is Rect r)
            {
                r.Inflate(10, 10);
                if (r.Contains(p)) return element;
            }
        }
        for (var i = InkSurface.Strokes.Count - 1; i >= 0; i--)
        {
            var stroke = InkSurface.Strokes[i];
            if (stroke.HitTest(p, Math.Max(12, stroke.DrawingAttributes.Width))) return stroke;
        }
        return null;
    }

    private Rect? ItemBounds(object item)
    {
        try
        {
            return item switch
            {
                Stroke s => s.GetBounds(),
                UIElement e => BoundsOf(e, ShapeSurface),
                _ => null
            };
        }
        catch (InvalidOperationException) { return null; }
    }

    private Rect? SelectionBounds()
    {
        Rect? union = null;
        foreach (var item in _selection)
            if (ItemBounds(item) is Rect r) union = union is Rect u ? Rect.Union(u, r) : r;
        if (union is Rect b) { b.Inflate(8, 8); return b; }
        return null;
    }

    private static void MoveItem(object item, Vector delta)
    {
        if (item is Stroke stroke) stroke.Transform(new Matrix(1, 0, 0, 1, delta.X, delta.Y), false);
        else if (item is UIElement element) SetOffset(element, GetOffset(element) + delta);
    }

    private void PushEdit(Action undo, Action redo)
    {
        _history.Add(new EditOperation(undo, redo));
        _removed.Clear();
    }

    // ---------------------------------------------------------------- Batch operations

    private void RecolorSelection(Color color)
    {
        if (_selection.Count == 0) return;
        var restores = _selection.Select(CaptureColor).ToArray();
        var items = _selection.ToArray();
        void Apply() { foreach (var item in items) ApplyColor(item, color); }
        Apply();
        PushEdit(() => { foreach (var r in restores) r(); }, Apply);
        RenderSelection();
    }

    private static Action CaptureColor(object item)
    {
        switch (item)
        {
            case Stroke s: { var c = s.DrawingAttributes.Color; return () => s.DrawingAttributes.Color = c; }
            case Path p: { var stroke = p.Stroke; var fill = p.Fill; return () => { p.Stroke = stroke; p.Fill = fill; }; }
            case Border b: { var bg = b.Background; return () => b.Background = bg; }
            case TextBlock t: { var fg = t.Foreground; return () => t.Foreground = fg; }
            default: return () => { };
        }
    }

    private static void ApplyColor(object item, Color color)
    {
        switch (item)
        {
            case Stroke s:
                s.DrawingAttributes.Color = Color.FromArgb(s.DrawingAttributes.Color.A, color.R, color.G, color.B);
                break;
            case Path p:
                var solidHead = p.Tag is Core.ShapeKind kind && ShapeGeometry.HasSolidHead(kind);
                var tint = p.Fill is SolidColorBrush f && f.Color.A is > 0 and < 255 ? f.Color.A : (byte)0;
                var brush = new SolidColorBrush(color);
                p.Stroke = brush;
                p.Fill = solidHead ? brush : tint > 0 ? new SolidColorBrush(Color.FromArgb(tint, color.R, color.G, color.B)) : p.Fill;
                break;
            case Border b:
                b.Background = new SolidColorBrush(color);
                break;
            case TextBlock t:
                t.Foreground = new SolidColorBrush(color);
                break;
        }
    }

    private void ChangeSelectionThickness(double delta)
    {
        var items = _selection.Where(i => i is Stroke or Path).ToArray();
        if (items.Length == 0) return;
        var before = items.Select(ThicknessOf).ToArray();
        void Set(double[] values) { for (var i = 0; i < items.Length; i++) SetThickness(items[i], values[i]); }
        var after = before.Select(t => Math.Clamp(t + delta, 1, 48)).ToArray();
        Set(after);
        PushEdit(() => Set(before), () => Set(after));
        RenderSelection();
    }

    private static double ThicknessOf(object item) => item switch
    {
        Stroke s => s.DrawingAttributes.Width,
        Path p => p.StrokeThickness,
        _ => 0
    };

    private static void SetThickness(object item, double value)
    {
        if (item is Stroke s)
        {
            var ratio = s.DrawingAttributes.Width > 0 ? s.DrawingAttributes.Height / s.DrawingAttributes.Width : 1;
            s.DrawingAttributes.Width = value;
            s.DrawingAttributes.Height = Math.Max(1, value * ratio);
        }
        else if (item is Path p) p.StrokeThickness = value;
    }

    private void SnapSelectionToGrid()
    {
        if (_selection.Count == 0) return;
        var size = Math.Max(1, _settings.GridSize);
        Point SnapTo(Point p) => new(Math.Round(p.X / size) * size, Math.Round(p.Y / size) * size);
        var moves = new List<(object Item, Vector Delta)>();
        foreach (var item in _selection)
        {
            Point origin;
            if (item is Stroke s && s.StylusPoints.Count > 0) origin = new Point(s.StylusPoints[0].X, s.StylusPoints[0].Y);
            else if (ItemBounds(item) is Rect r) origin = r.TopLeft;
            else continue;
            var delta = SnapTo(origin) - origin;
            if (delta.Length > 0.01) moves.Add((item, delta));
        }
        if (moves.Count == 0) return;
        foreach (var (item, delta) in moves) MoveItem(item, delta);
        PushEdit(() => { foreach (var (i, d) in moves) MoveItem(i, -d); }, () => { foreach (var (i, d) in moves) MoveItem(i, d); });
        RenderSelection();
    }

    private void DuplicateSelection()
    {
        if (_selection.Count == 0) return;
        var offset = new Vector(24, 24);
        var clones = new List<object>();
        foreach (var item in _selection)
        {
            switch (item)
            {
                case Stroke s:
                    var copy = s.Clone();
                    copy.Transform(new Matrix(1, 0, 0, 1, offset.X, offset.Y), false);
                    clones.Add(copy);
                    break;
                case UIElement e:
                    if (CloneElement(e) is UIElement clone)
                    {
                        SetOffset(clone, GetOffset(e) + offset);
                        clones.Add(clone);
                    }
                    break;
            }
        }
        void Add() { foreach (var c in clones) RestoreAnnotation(c); }
        void Remove() { foreach (var c in clones) RemoveAnnotation(c); }
        Add();
        PushEdit(Remove, Add);
        _selection.Clear();
        foreach (var c in clones) _selection.Add(c);
        RenderSelection();
    }

    private static UIElement? CloneElement(UIElement element)
    {
        try
        {
            var clone = (UIElement)XamlReader.Parse(XamlWriter.Save(element));
            if (element is Path source && clone is Path path) path.Tag = source.Tag;
            return clone;
        }
        catch (Exception) { return null; }
    }

    private void DeleteSelection()
    {
        if (_selection.Count == 0) return;
        var items = _selection.ToArray();
        foreach (var item in items)
        {
            if (_expirations.TryGetValue(item, out var expiration) && item is UIElement e) e.Opacity = expiration.OriginalOpacity;
            _expirations.Remove(item);
        }
        void Remove() { foreach (var i in items) RemoveAnnotation(i); }
        void Restore() { foreach (var i in items) RestoreAnnotation(i); }
        Remove();
        PushEdit(Restore, Remove);
        _selection.Clear();
        RenderSelection();
    }

    // ---------------------------------------------------------------- Visuals

    private void RenderSelection()
    {
        foreach (var v in _selectionVisuals) GuideSurface.Children.Remove(v);
        _selectionVisuals.Clear();
        _selection.RemoveWhere(item => item is Stroke s ? !InkSurface.Strokes.Contains(s) : item is UIElement e && !ShapeSurface.Children.Contains(e));

        foreach (var item in _selection)
        {
            if (ItemBounds(item) is not Rect r) continue;
            r.Inflate(4, 4);
            Shape frame = item is Border { CornerRadius.TopLeft: > 8 } ? new Ellipse() : new Rectangle();
            if (frame is Ellipse) r.Inflate(2, 2);
            frame.Width = r.Width; frame.Height = r.Height;
            frame.Stroke = Tw.B(Tw.Indigo500);
            frame.StrokeThickness = frame is Ellipse ? 2 : 1.5;
            var dash = 4 / frame.StrokeThickness;
            frame.StrokeDashArray = [dash, dash];
            AddVisual(frame, r.Left, r.Top);
        }

        if (SelectionBounds() is Rect bounds)
        {
            var box = new Rectangle
            {
                Width = bounds.Width, Height = bounds.Height,
                Stroke = Tw.B(Tw.Indigo400), StrokeThickness = 1.5, StrokeDashArray = [4, 2.67],
                Effect = new DropShadowEffect { Color = Tw.Indigo500, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.4 }
            };
            AddVisual(box, bounds.Left, bounds.Top);
            var cx = bounds.Left + bounds.Width / 2;
            var cy = bounds.Top + bounds.Height / 2;
            foreach (var (x, y) in new[]
                     {
                         (bounds.Left, bounds.Top), (bounds.Right, bounds.Top), (bounds.Left, bounds.Bottom), (bounds.Right, bounds.Bottom),
                         (cx, bounds.Top), (cx, bounds.Bottom), (bounds.Left, cy), (bounds.Right, cy)
                     })
            {
                AddVisual(new Rectangle { Width = 6, Height = 6, Fill = Brushes.White, Stroke = Tw.B(Tw.Indigo600), StrokeThickness = 1.5 }, x - 3, y - 3);
            }
        }

        if (_marquee is Rect m)
        {
            AddVisual(new Rectangle
            {
                Width = m.Width, Height = m.Height,
                Fill = Tw.B(Tw.Indigo500, 0.14), Stroke = Tw.B(Tw.Indigo400), StrokeThickness = 1.5, StrokeDashArray = [3.33, 3.33]
            }, m.Left, m.Top);
        }

        UpdateActionBar();
        UpdateStatusPill();
    }

    private void AddVisual(UIElement element, double left, double top)
    {
        element.IsHitTestVisible = false;
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        GuideSurface.Children.Add(element);
        _selectionVisuals.Add(element);
    }

    private void UpdateActionBar()
    {
        if (_selection.Count == 0 || SelectionBounds() is not Rect bounds)
        {
            if (_actionBar is not null) _actionBar.Visibility = Visibility.Collapsed;
            return;
        }
        _actionBar ??= BuildActionBar();
        _actionCount!.Text = $"{_selection.Count} Selected";
        _actionBar.Visibility = Visibility.Visible;
        Canvas.SetLeft(_actionBar, Math.Max(16, bounds.Left));
        Canvas.SetTop(_actionBar, Math.Max(70, bounds.Top - 52));
    }

    private Border BuildActionBar()
    {
        Border Group(UIElement content) => new()
        {
            Child = content,
            BorderBrush = Tw.B(Tw.Slate700, 0.8),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(0, 0, 8, 0),
            Margin = new Thickness(0, 0, 6, 0)
        };
        System.Windows.Controls.Button Action(UIElement content, Color fg, Color hoverFg, Color hoverBg, string tip, Action click, Thickness? padding = null)
        {
            var b = DK.Button(content, Tw.B(Colors.Transparent), Tw.B(fg), Tw.B(hoverBg), Tw.B(hoverFg), 4, padding ?? new Thickness(4));
            b.ToolTip = tip;
            b.Click += (_, _) => click();
            return b;
        }

        _actionCount = DK.Text("0 Selected", 12, Tw.B(Tw.Indigo300), FontWeights.SemiBold);
        var header = Group(DK.H(4, new LucideIcon("BoxSelect", 14) { Foreground = Tw.B(Tw.Indigo400) }, _actionCount));

        var swatches = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        foreach (var color in QuickColors)
        {
            var swatch = new Border
            {
                Width = 16, Height = 16, CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(color), BorderBrush = Tw.B(Colors.White, 0.3), BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 4, 0), Cursor = System.Windows.Input.Cursors.Hand,
                RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1),
                ToolTip = "Recolor " + color
            };
            swatch.MouseEnter += (_, _) => swatch.RenderTransform = new ScaleTransform(1.25, 1.25);
            swatch.MouseLeave += (_, _) => swatch.RenderTransform = new ScaleTransform(1, 1);
            swatch.MouseLeftButtonUp += (_, e) => { RecolorSelection(color); e.Handled = true; };
            swatches.Children.Add(swatch);
        }

        var thickness = Group(DK.H(2,
            Action(new LucideIcon("Minus", 14), Tw.Slate300, Colors.White, Tw.Slate800, "Decrease Thickness", () => ChangeSelectionThickness(-2)),
            Action(new LucideIcon("Plus", 14), Tw.Slate300, Colors.White, Tw.Slate800, "Increase Thickness", () => ChangeSelectionThickness(2))));

        UIElement Labeled(string icon, string label) => DK.H(4, new LucideIcon(icon, 14), new TextBlock { Text = label, FontSize = 10, VerticalAlignment = VerticalAlignment.Center });

        var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Children =
        {
            header,
            Group(swatches),
            thickness,
            Action(Labeled("Magnet", "Snap"), Tw.Amber300, Tw.Hex("#FDE68A"), Tw.Slate800, "Snap All Selected to Grid", SnapSelectionToGrid, new Thickness(6, 2, 6, 2)),
            Action(Labeled("Copy", "Duplicate"), Tw.Sky300, Tw.Sky200, Tw.Slate800, "Duplicate Selected (Ctrl+D)", DuplicateSelection, new Thickness(6, 2, 6, 2)),
            Action(new LucideIcon("Trash2", 14), Tw.Rose400, Tw.Rose300, Tw.WithAlpha(Tw.Rose950, 0.6), "Delete Selected (Del)", DeleteSelection),
            Action(new LucideIcon("X", 14), Tw.Slate400, Colors.White, Tw.Slate800, "Clear Selection (Esc)", ClearSelection)
        }};
        foreach (FrameworkElement child in row.Children) child.VerticalAlignment = VerticalAlignment.Center;

        var bar = new Border
        {
            Child = row,
            Background = Tw.B(Tw.Slate900, 0.95),
            BorderBrush = Tw.B(Tw.Indigo500, 0.5),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 6, 10, 6),
            Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 10, Opacity = 0.55, Color = Tw.Indigo950 },
            Visibility = Visibility.Collapsed
        };
        TextElement.SetFontSize(bar, 12);
        ChromeSurface.Children.Add(bar);
        return bar;
    }
}
