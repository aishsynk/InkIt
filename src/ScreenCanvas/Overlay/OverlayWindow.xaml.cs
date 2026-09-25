using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenCanvas.Core;
using ScreenCanvas.Displays;
using ScreenCanvas.Interop;
using Point = System.Windows.Point;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MediaPen = System.Windows.Media.Pen;
using System.Windows.Threading;
using InputCursors = System.Windows.Input.Cursors;
using Size = System.Windows.Size;

namespace ScreenCanvas.Overlay;

/// <summary>Routes in-app keyboard shortcuts (palette, capability centre, orientation, ...) to the toolbar.</summary>
public static class OverlayKeyRouter
{
    public static Func<Key, ModifierKeys, bool>? Handler { get; set; }
    public static bool TryHandle(Key key, ModifierKeys modifiers) => Handler?.Invoke(key, modifiers) == true;
}

public partial class OverlayWindow : Window
{
    private readonly ToolSettings _settings;
    private readonly DisplayInfo _display;
    private readonly Stack<object> _removed = new();
    private readonly List<object> _history = [];
    private BoardKind _board = BoardKind.Transparent;
    private Point? _dragAnchor;
    private Path? _shapePreview;
    private readonly DispatcherTimer _fadeTimer;
    private readonly Dictionary<object, (DateTimeOffset Start, TimeSpan Duration, double OriginalOpacity, byte OriginalAlpha)> _expirations = [];
    private readonly HashSet<UIElement> _erasedThisDrag = [];
    private Stroke? _mouseStroke;
    private TextBox? _textEditor;
    private FrameworkElement? _textEditorHost;
    private readonly IOverlayManager? _manager;

    public OverlayWindow(DisplayInfo display, ToolSettings settings, IOverlayManager? manager = null)
    {
        InitializeComponent();
        _settings = settings;
        _display = display;
        _manager = manager;

        _fadeTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        _fadeTimer.Tick += FadeTimer_OnTick;
        Left = display.Left;
        Top = display.Top;
        Width = display.Width;
        Height = display.Height;
        InkSurface.StrokeCollected += (_, e) => OnStylusStrokeCollected(e.Stroke);
        InkSurface.StrokeErasing += (_, e) =>
        {
            if (_expirations.TryGetValue(e.Stroke, out var expiration))
            {
                var color = e.Stroke.DrawingAttributes.Color;
                e.Stroke.DrawingAttributes.Color = Color.FromArgb(expiration.OriginalAlpha, color.R, color.G, color.B);
                _expirations.Remove(e.Stroke);
            }
            _selection.Remove(e.Stroke);
            _history.Add(new Removal(e.Stroke));
            _removed.Clear();
        };
        MouseMove += OnMouseMove;
        MouseLeave += (_, _) => HidePointerEffects();
        PreviewKeyDown += OverlayWindow_OnPreviewKeyDown;
        SizeChanged += (_, _) => { RefreshOptions(); PlaceStatusPill(); };
        InitializeInputDevices();
        InitializeEffects();
        Loaded += (_, _) => { RefreshTool(); RefreshOptions(); PlaceStatusPill(); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLong(handle, NativeMethods.GwlExStyle, style | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate);
        NativeMethods.SetWindowPos(handle, nint.Zero, (int)_display.Left, (int)_display.Top, (int)_display.Width, (int)_display.Height, NativeMethods.SwpNoActivate);

        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
    }

    private const int WmNcHitTest = 0x0084;
    private const nint HtTransparent = -1;

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmNcHitTest)
        {
            if (Mouse.Captured == InputRoot || _mouseStroke is not null || _dragAnchor is not null || _marquee is not null)
            {
                return nint.Zero;
            }

            var x = unchecked((short)(lParam.ToInt64() & 0xFFFF));
            var y = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF));
            var screenPoint = new Point(x, y);

            if (_manager?.IsPointOverUi(screenPoint) == true)
            {
                handled = true;
                return HtTransparent;
            }
        }
        return nint.Zero;
    }

    public void InitializeHidden()
    {
        _ = new WindowInteropHelper(this).EnsureHandle();
        RefreshTool();
        Hide();
    }

    public void SetClickThrough(bool enabled)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == nint.Zero) return;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
        var next = enabled ? style | NativeMethods.WsExTransparent : style & ~NativeMethods.WsExTransparent;
        NativeMethods.SetWindowLong(handle, NativeMethods.GwlExStyle, next);
        NativeMethods.SetWindowPos(handle, nint.Zero, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder |
            NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
        InkSurface.IsHitTestVisible = !enabled;
        InputRoot.IsHitTestVisible = !enabled;
        if (enabled && !HasVisibleContent) Hide(); else if (!IsVisible) Show();
    }

    private bool HasVisibleContent =>
        _board != BoardKind.Transparent || IsZoomViewActive || _settings.CurtainProgress > 0 || InkSurface.Strokes.Count > 0 ||
        ShapeSurface.Children.Count > 0;

    public void RefreshTool()
    {
        CancelMouseStroke();
        CancelShapePreview();
        if (_settings.Tool != ToolKind.Text) CommitTextEditor();
        if (_settings.Tool != ToolKind.Select) ClearSelection();
        var highlighter = _settings.Tool == ToolKind.Highlighter;
        var attributes = new DrawingAttributes
        {
            Color = Color.FromArgb(_settings.Opacity, _settings.Color.R, _settings.Color.G, _settings.Color.B),
            Width = _settings.Thickness,
            Height = _settings.Thickness,
            IsHighlighter = highlighter,
            FitToCurve = true,
            IgnorePressure = !_settings.PressureEnabled
        };
        if (!highlighter)
        {
            if (_settings.PenMode == PenMode.Calligraphy)
            {
                attributes.StylusTip = StylusTip.Rectangle;
                attributes.Width = _settings.Thickness * 2.2;
                attributes.Height = Math.Max(1.5, _settings.Thickness * .48);
                attributes.StylusTipTransform = new Matrix(.82, .57, -.57, .82, 0, 0);
            }
            else if (_settings.PenMode == PenMode.Brush)
            {
                attributes.StylusTip = StylusTip.Ellipse;
                attributes.Width = _settings.Thickness * 1.35;
                attributes.Height = _settings.Thickness * .72;
                attributes.IgnorePressure = false;
            }
            else if (_settings.PenMode == PenMode.FeltTip)
            {
                attributes.StylusTip = StylusTip.Ellipse;
                attributes.Width = attributes.Height = _settings.Thickness * 1.25;
            }
            else if (_settings.PenMode == PenMode.Pressure) attributes.IgnorePressure = false;
        }
        InkSurface.DefaultDrawingAttributes = attributes;

        InkSurface.EditingMode = _settings.Tool switch
        {
            ToolKind.Pen or ToolKind.Highlighter => InkCanvasEditingMode.Ink,
            ToolKind.Eraser => InkCanvasEditingMode.EraseByStroke,
            _ => InkCanvasEditingMode.None
        };
        var cursor = _settings.Tool switch
        {
            ToolKind.Pen or ToolKind.Highlighter or ToolKind.Shape or ToolKind.NumberMarker or ToolKind.Spotlight => InputCursors.Cross,
            ToolKind.Text => InputCursors.IBeam,
            ToolKind.Select => InputCursors.Arrow,
            ToolKind.Eraser => InputCursors.Cross,
            ToolKind.Laser => InputCursors.None,
            _ => InputCursors.Arrow
        };
        InputRoot.Cursor = cursor;
        InkSurface.Cursor = cursor;
        if (_settings.Tool is not (ToolKind.Laser)) ClearLaserTrail();
        SetNoActivate(_settings.Tool != ToolKind.Select && _textEditor is null);
        RefreshOptions();
    }

    private bool IsOverToolbar(Point screenPoint) => _manager?.IsPointOverUi(screenPoint) ?? false;
    private Point SnapPoint(Point point) => _settings.Snap(point);

    // ------------------------------------------------------------------ Freehand ink

    private void InputRoot_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var screenPoint = PointToScreen(e.GetPosition(this));
        if (IsOverToolbar(screenPoint)) return;
        if (e.OriginalSource is DependencyObject source && IsInsideChrome(source)) return;
        _manager?.NotifyInteractionStarted();
        if (_settings.Tool is not (ToolKind.Pen or ToolKind.Highlighter) || e.StylusDevice is not null) return;
        var point = SnapPoint(e.GetPosition(InkSurface));
        var points = new StylusPointCollection([new StylusPoint(point.X, point.Y)]);
        var attributes = InkSurface.DefaultDrawingAttributes.Clone();
        _mouseStroke = _settings.Tool == ToolKind.Pen && StyledStroke.HasCustomRendering(_settings.PenMode)
            ? new StyledStroke(points, attributes, _settings.PenMode)
            : new Stroke(points, attributes);
        InkSurface.Strokes.Add(_mouseStroke);
        InputRoot.CaptureMouse();
        e.Handled = true;
    }

    private void InputRoot_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_mouseStroke is null || e.LeftButton != MouseButtonState.Pressed) return;
        var point = SnapPoint(e.GetPosition(InkSurface));
        var last = _mouseStroke.StylusPoints[^1];
        if (_settings.SnapToGrid && Math.Abs(last.X - point.X) < 0.5 && Math.Abs(last.Y - point.Y) < 0.5) { e.Handled = true; return; }

        if (_settings.Tool == ToolKind.Highlighter && _settings.PenMode == PenMode.StraightHighlighter)
        {
            while (_mouseStroke.StylusPoints.Count > 1) _mouseStroke.StylusPoints.RemoveAt(1);
            _mouseStroke.StylusPoints.Add(new StylusPoint(point.X, point.Y));
        }
        else
        {
            var pressure = _settings.PenMode is PenMode.Brush or PenMode.Pressure
                ? (float)Math.Clamp(.3 + .7 / (1 + (point - new Point(last.X, last.Y)).Length / 5), .3, 1)
                : .5f;
            _mouseStroke.StylusPoints.Add(new StylusPoint(point.X, point.Y) { PressureFactor = pressure });
        }
        e.Handled = true;
    }

    private void InputRoot_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_mouseStroke is null) return;
        var stroke = _mouseStroke;
        _mouseStroke = null;
        InputRoot.ReleaseMouseCapture();
        FinishStroke(stroke);
        e.Handled = true;
    }

    private void OnStylusStrokeCollected(Stroke collected)
    {
        if (_pinchActive || _touchContacts.Count > 1)
        {
            InkSurface.Strokes.Remove(collected);
            return;
        }
        var stroke = collected;
        if (_settings.SnapToGrid)
        {
            var snapped = new StylusPointCollection(collected.StylusPoints.Description);
            foreach (var p in collected.StylusPoints)
            {
                var s = SnapPoint(new Point(p.X, p.Y));
                snapped.Add(new StylusPoint(s.X, s.Y, p.PressureFactor));
            }
            collected.StylusPoints = snapped;
        }
        if (_settings.Tool == ToolKind.Pen && StyledStroke.HasCustomRendering(_settings.PenMode))
        {
            stroke = new StyledStroke(collected.StylusPoints.Clone(), collected.DrawingAttributes.Clone(), _settings.PenMode);
            var index = InkSurface.Strokes.IndexOf(collected);
            InkSurface.Strokes.Remove(collected);
            if (index >= 0 && index <= InkSurface.Strokes.Count) InkSurface.Strokes.Insert(index, stroke);
            else InkSurface.Strokes.Add(stroke);
        }
        FinishStroke(stroke);
    }

    /// <summary>Commits a finished stroke: auto-shape conversion, dashed/dotted patterning, history and fade.</summary>
    private void FinishStroke(Stroke stroke)
    {
        if (_settings.AutoShapeAssist && _settings.Tool is ToolKind.Pen or ToolKind.Highlighter)
        {
            var points = stroke.StylusPoints.Select(p => new Point(p.X, p.Y)).ToList();
            if (ShapeGeometry.Recognize(points) is { } recognized)
            {
                InkSurface.Strokes.Remove(stroke);
                var path = NewShapePath(recognized.Kind);
                path.Data = ShapeGeometry.Build(recognized.Kind, recognized.Start, recognized.End, _settings.Thickness);
                ShapeSurface.Children.Add(path);
                CommitAnnotation(path);
                return;
            }
        }
        if (_settings.Tool == ToolKind.Pen && _settings.PenMode is PenMode.Dashed or PenMode.Dotted)
        {
            InkSurface.Strokes.Remove(stroke);
            var pieces = PatternStroke(stroke, _settings.PenMode == PenMode.Dotted);
            foreach (var piece in pieces) InkSurface.Strokes.Add(piece);
            _history.Add(new StrokeGroup(pieces));
            _removed.Clear();
            return;
        }
        CommitAnnotation(stroke);
    }

    private void CommitAnnotation(object item)
    {
        _history.Add(item);
        _removed.Clear();
        ScheduleFade(item);
    }

    private void CancelMouseStroke()
    {
        if (_mouseStroke is null) return;
        InkSurface.Strokes.Remove(_mouseStroke);
        _mouseStroke = null;
        if (Mouse.Captured == InputRoot) InputRoot.ReleaseMouseCapture();
    }

    private static StrokeCollection PatternStroke(Stroke source, bool dotted)
    {
        var result = new StrokeCollection();
        var points = source.StylusPoints;
        if (points.Count == 0) return result;
        var onLength = dotted ? 1.5 : 12d;
        var offLength = dotted ? Math.Max(4, source.DrawingAttributes.Width * 1.25) : 7d;
        var on = true; var remaining = onLength; var segment = new StylusPointCollection();
        segment.Add(points[0]);
        for (var i = 1; i < points.Count; i++)
        {
            var previous = points[i - 1]; var current = points[i];
            var distance = Math.Sqrt(Math.Pow(current.X - previous.X, 2) + Math.Pow(current.Y - previous.Y, 2));
            remaining -= distance;
            if (on) segment.Add(current);
            if (remaining <= 0)
            {
                if (on && segment.Count > 0)
                {
                    if (segment.Count == 1) segment.Add(new StylusPoint(segment[0].X + .2, segment[0].Y + .2));
                    result.Add(new Stroke(segment, source.DrawingAttributes.Clone()));
                }
                on = !on; remaining = on ? onLength : offLength; segment = new StylusPointCollection();
                if (on) segment.Add(current);
            }
        }
        if (on && segment.Count > 1) result.Add(new Stroke(segment, source.DrawingAttributes.Clone()));
        return result;
    }

    // ------------------------------------------------------------------ History

    public void Undo()
    {
        if (_history.Count == 0) return;
        ClearSelection();
        var item = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        UndoItem(item);
        _removed.Push(item);
    }

    public void Redo()
    {
        if (_removed.Count == 0) return;
        ClearSelection();
        var item = _removed.Pop();
        RedoItem(item);
        _history.Add(item);
    }

    public void ClearInk()
    {
        CancelShapePreview();
        CancelTextEditor();
        ClearSelection();
        var snapshot = InkSurface.Strokes.Cast<object>()
            .Concat(ShapeSurface.Children.Cast<UIElement>())
            .ToArray();
        if (snapshot.Length == 0) return;
        foreach (var element in snapshot.OfType<UIElement>())
            if (_expirations.TryGetValue(element, out var expiration)) element.Opacity = expiration.OriginalOpacity;
        foreach (var stroke in snapshot.OfType<Stroke>())
            if (_expirations.TryGetValue(stroke, out var expiration))
            {
                var color = stroke.DrawingAttributes.Color;
                stroke.DrawingAttributes.Color = Color.FromArgb(expiration.OriginalAlpha, color.R, color.G, color.B);
            }
        _history.Add(new ClearOperation(snapshot));
        _removed.Clear();
        InkSurface.Strokes.Clear();
        ShapeSurface.Children.Clear();
        _expirations.Clear();
        _fadeTimer.Stop();
    }

    // ------------------------------------------------------------------ Board

    public void ToggleBoard(bool dark) => SetBoard(_board == BoardKind.Transparent ? (dark ? BoardKind.Blackboard : BoardKind.Whiteboard) : BoardKind.Transparent);

    public void SetBoard(Color? color) => SetBoard(color is null ? BoardKind.Transparent : color == Colors.White ? BoardKind.Whiteboard : BoardKind.Blackboard);

    public void SetBoard(BoardKind kind)
    {
        _board = kind;
        UpdateBoardSurface();
        if (kind != BoardKind.Transparent && !IsVisible) Show();
    }

    private void UpdateBoardSurface()
    {
        BoardSurface.Background = _board switch
        {
            BoardKind.Whiteboard => Brushes.White,
            BoardKind.Blackboard => new SolidColorBrush(OverlayManager.BlackboardColor),
            BoardKind.Grid => GridBrush(Math.Max(5, _settings.GridSize)),
            _ => Brushes.Transparent
        };
    }

    /// <summary>Engineering grid: #1E293B 1px lines every <paramref name="step"/> px on #0F172A.</summary>
    private static System.Windows.Media.Brush GridBrush(int step)
    {
        var line = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(OverlayManager.GridBoardColor), null, new RectangleGeometry(new Rect(0, 0, step, step))));
        drawing.Children.Add(new GeometryDrawing(line, null, new RectangleGeometry(new Rect(0, 0, step, 1))));
        drawing.Children.Add(new GeometryDrawing(line, null, new RectangleGeometry(new Rect(0, 0, 1, step))));
        var brush = new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, step, step),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };
        brush.Freeze();
        return brush;
    }

    // ------------------------------------------------------------------ Vector tools

    private void InputRoot_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var screenPoint = PointToScreen(e.GetPosition(this));
        if (IsOverToolbar(screenPoint)) return;
        if (e.OriginalSource is DependencyObject source && IsInsideChrome(source)) return;
        _manager?.NotifyInteractionStarted();
        var point = SnapPoint(e.GetPosition(InputRoot));
        _erasedThisDrag.Clear();
        if (_settings.Tool == ToolKind.Select)
        {
            BeginSelection(point, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            e.Handled = true;
            return;
        }
        if (_settings.Tool == ToolKind.Eraser && TryEraseVector(e.GetPosition(InputRoot))) { e.Handled = true; return; }
        if (_settings.Tool == ToolKind.Shape)
        {
            _dragAnchor = point;
            _shapePreview = NewShapePath(_settings.Shape);
            ShapeSurface.Children.Add(_shapePreview);
            InputRoot.CaptureMouse();
            UpdateShape(_shapePreview, point, point, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
            e.Handled = true;
        }
        else if (_settings.Tool == ToolKind.NumberMarker)
        {
            AddMarker(point);
            e.Handled = true;
        }
        else if (_settings.Tool == ToolKind.Text)
        {
            BeginText(point);
            e.Handled = true;
        }
    }

    private void InputRoot_OnMouseMove(object sender, MouseEventArgs e)
    {
        var raw = e.GetPosition(InputRoot);
        var point = SnapPoint(raw);
        if (_settings.Tool == ToolKind.Select && e.LeftButton == MouseButtonState.Pressed && ContinueSelection(point))
        {
            e.Handled = true;
            return;
        }
        if (_settings.Tool == ToolKind.Eraser && e.LeftButton == MouseButtonState.Pressed)
        {
            var screenPoint = PointToScreen(e.GetPosition(this));
            if (!IsOverToolbar(screenPoint) && TryEraseVector(raw)) { e.Handled = true; return; }
        }
        if (_dragAnchor is not Point anchor || _shapePreview is null || e.LeftButton != MouseButtonState.Pressed) return;
        UpdateShape(_shapePreview, anchor, point, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
        e.Handled = true;
    }

    private void InputRoot_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _erasedThisDrag.Clear();
        if (_settings.Tool == ToolKind.Select)
        {
            EndSelection();
            e.Handled = true;
            return;
        }
        if (_dragAnchor is not Point anchor || _shapePreview is null) return;
        var end = SnapPoint(e.GetPosition(InputRoot));
        UpdateShape(_shapePreview, anchor, end, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
        var completed = _shapePreview;
        _shapePreview = null;
        _dragAnchor = null;
        InputRoot.ReleaseMouseCapture();
        UpdateGuides(e.GetPosition(InputRoot));
        if ((end - anchor).Length < 2)
        {
            ShapeSurface.Children.Remove(completed);
            e.Handled = true;
            return;
        }
        CommitAnnotation(completed);
        if (!_settings.StickyTools) _manager?.DeactivateCurrentTool(ToolDeactivationReason.CursorSelected);
        e.Handled = true;
    }

    private void InputRoot_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_shapePreview is not null && e.LeftButton != MouseButtonState.Pressed)
            CancelShapePreview();
        _erasedThisDrag.Clear();
    }

    private Path NewShapePath(ShapeKind kind)
    {
        var stroke = new SolidColorBrush(_settings.Color);
        return new Path
        {
            Stroke = stroke,
            StrokeThickness = _settings.Thickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Opacity = _settings.Opacity / 255d,
            Fill = ShapeGeometry.HasSolidHead(kind)
                ? stroke
                : _settings.ShapeFillEnabled
                    ? new SolidColorBrush(Color.FromArgb(_settings.ShapeFillOpacity, _settings.Color.R, _settings.Color.G, _settings.Color.B))
                    : Brushes.Transparent,
            Tag = kind,
            IsHitTestVisible = false
        };
    }

    private void UpdateShape(Path path, Point start, Point end, bool constrain, bool fromCenter)
    {
        if (constrain && _settings.Shape is ShapeKind.Line or ShapeKind.Arrow or ShapeKind.DoubleArrow)
        {
            var distance = (end - start).Length;
            var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            angle = Math.Round(angle / (Math.PI / 4)) * (Math.PI / 4);
            end = new Point(start.X + Math.Cos(angle) * distance, start.Y + Math.Sin(angle) * distance);
        }
        if (constrain && _settings.Shape is ShapeKind.Rectangle or ShapeKind.RoundedRectangle or ShapeKind.Ellipse or ShapeKind.Diamond)
        {
            var side = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
            end = new Point(start.X + Math.Sign(end.X - start.X) * side, start.Y + Math.Sign(end.Y - start.Y) * side);
        }
        if (fromCenter)
        {
            var delta = end - start;
            start -= delta;
        }
        path.Data = ShapeGeometry.Build(_settings.Shape, start, end, _settings.Thickness);
        _shapeDimensions = new Size(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
    }

    private void AddMarker(Point point)
    {
        if (_settings.MarkerNumber < 1) _settings.MarkerNumber = 1;
        var label = _settings.LetterMarkers ? MarkerLetter(_settings.MarkerNumber++) : (_settings.MarkerNumber++).ToString();
        // Design: 28px badge in the tool colour, 2px white ring, soft shadow, bold white label.
        const double size = 28;
        var border = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(_settings.SquareMarkers ? 7 : size / 2),
            Background = new SolidColorBrush(_settings.Color),
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(2),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 0, Opacity = 0.5, Color = Colors.Black },
            Child = new TextBlock
            {
                Text = label,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(border, point.X - size / 2);
        Canvas.SetTop(border, point.Y - size / 2);
        ShapeSurface.Children.Add(border);
        CommitAnnotation(border);
    }

    private static string MarkerLetter(int value)
    {
        var result = string.Empty;
        while (value > 0) { value--; result = (char)('A' + value % 26) + result; value /= 26; }
        return result;
    }

    // ------------------------------------------------------------------ Text

    private void BeginText(Point point)
    {
        CommitTextEditor();
        // Design: dark field with blue border, white semibold text, and a "Done" button.
        var editor = new TextBox
        {
            MinWidth = 200,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            CaretBrush = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x0F, 0x17, 0x2A)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 3, 6, 3),
            AcceptsReturn = false,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var placeholder = new TextBlock
        {
            Text = "Type annotation and press Enter...",
            Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
            FontSize = 14,
            Margin = new Thickness(9, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        editor.TextChanged += (_, _) => placeholder.Visibility = editor.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var field = new Grid { Children = { editor, placeholder } };
        var done = new System.Windows.Controls.Button
        {
            Content = new TextBlock { Text = "Done", FontSize = 12, Foreground = Brushes.White },
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(8, 4, 8, 4),
            Cursor = InputCursors.Hand,
            Template = RoundedButtonTemplate(Color.FromRgb(0x25, 0x63, 0xEB), Color.FromRgb(0x3B, 0x82, 0xF6))
        };
        var host = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Children = { field, done },
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 6, Opacity = 0.45, Color = Colors.Black },
            Tag = point
        };
        Canvas.SetLeft(host, point.X);
        Canvas.SetTop(host, point.Y - 12);
        ChromeSurface.Children.Add(host);
        _textEditor = editor;
        _textEditorHost = host;
        SetNoActivate(false);
        Activate();
        editor.Focus();
        Keyboard.Focus(editor);
        done.Click += (_, _) => CommitTextEditor();
        editor.LostKeyboardFocus += (_, e) =>
        {
            if (_textEditor != editor) return;
            if (e.NewFocus is DependencyObject d && IsDescendant(host, d)) return;
            CommitTextEditor();
        };
        editor.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { CancelTextEditor(); e.Handled = true; }
            else if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { CommitTextEditor(); e.Handled = true; }
            else if (e.Key == Key.Enter)
            {
                var caret = editor.CaretIndex;
                editor.Text = editor.Text.Insert(caret, Environment.NewLine);
                editor.CaretIndex = caret + Environment.NewLine.Length;
                e.Handled = true;
            }
        };
    }

    private static ControlTemplate RoundedButtonTemplate(Color normal, Color hover)
    {
        var template = new ControlTemplate(typeof(System.Windows.Controls.Button));
        var border = new FrameworkElementFactory(typeof(Border), "Surface");
        border.SetValue(Border.BackgroundProperty, new SolidColorBrush(normal));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(System.Windows.Controls.Control.PaddingProperty));
        border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        template.VisualTree = border;
        var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hover), "Surface"));
        template.Triggers.Add(trigger);
        return template;
    }

    private static bool IsDescendant(DependencyObject ancestor, DependencyObject node)
    {
        for (var current = node; current is not null; current = current is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }

    private void CommitTextEditor()
    {
        if (_textEditor is null) return;
        var editor = _textEditor;
        var host = _textEditorHost!;
        _textEditor = null;
        _textEditorHost = null;
        ChromeSurface.Children.Remove(host);
        if (!string.IsNullOrWhiteSpace(editor.Text))
        {
            var origin = (Point)host.Tag;
            // Committed text: tool colour, semibold, soft dark shadow (design).
            var textBlock = new TextBlock
            {
                Text = editor.Text,
                FontFamily = new System.Windows.Media.FontFamily(_settings.FontFamily),
                FontSize = _settings.FontSize,
                FontWeight = _settings.TextBold ? FontWeights.Bold : FontWeights.SemiBold,
                FontStyle = _settings.TextItalic ? FontStyles.Italic : FontStyles.Normal,
                TextDecorations = _settings.TextUnderline ? TextDecorations.Underline : null,
                Foreground = new SolidColorBrush(_settings.Color),
                Background = Brushes.Transparent,
                Padding = new Thickness(4, 2, 4, 2),
                IsHitTestVisible = false,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 4, ShadowDepth = 0, Opacity = 0.6, Color = Colors.Black }
            };
            Canvas.SetLeft(textBlock, origin.X);
            Canvas.SetTop(textBlock, origin.Y - _settings.FontSize * 0.2);
            ShapeSurface.Children.Add(textBlock);
            CommitAnnotation(textBlock);
        }
        Keyboard.ClearFocus();
        SetNoActivate(_settings.Tool != ToolKind.Select);
    }

    private void CancelTextEditor()
    {
        if (_textEditor is null) return;
        ChromeSurface.Children.Remove(_textEditorHost);
        _textEditor = null;
        _textEditorHost = null;
        Keyboard.ClearFocus();
        SetNoActivate(_settings.Tool != ToolKind.Select);
    }

    private void SetNoActivate(bool enabled)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == nint.Zero) return;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLong(handle, NativeMethods.GwlExStyle,
            enabled ? style | NativeMethods.WsExNoActivate : style & ~NativeMethods.WsExNoActivate);
    }

    // ------------------------------------------------------------------ Keyboard

    private void OverlayWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_textEditor is not null)
        {
            if (e.Key == Key.Escape) { CancelTextEditor(); _manager?.DeactivateCurrentTool(ToolDeactivationReason.Escape); e.Handled = true; }
            return;
        }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var isShift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var isAlt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

        if (_selection.Count > 0 && !isAlt)
        {
            if (key is Key.Delete or Key.Back && !isCtrl) { DeleteSelection(); e.Handled = true; return; }
            if (key == Key.D && isCtrl && !isShift) { DuplicateSelection(); e.Handled = true; return; }
        }

        if (key == Key.Escape)
        {
            if (_shapePreview is not null) CancelShapePreview();
            _manager?.DeactivateCurrentTool(ToolDeactivationReason.Escape);
            e.Handled = true;
            return;
        }

        if (isCtrl && !isShift && key == Key.Z) { _manager?.Undo(); e.Handled = true; return; }
        if (isCtrl && !isShift && key == Key.Y) { _manager?.Redo(); e.Handled = true; return; }
        if (isCtrl && isShift && key == Key.Delete) { _manager?.Clear(); e.Handled = true; return; }
        if (OverlayKeyRouter.TryHandle(key, Keyboard.Modifiers)) { e.Handled = true; }
    }

    // ------------------------------------------------------------------ Lifecycle helpers

    private void CancelShapePreview()
    {
        if (_shapePreview is not null) ShapeSurface.Children.Remove(_shapePreview);
        _shapePreview = null; _dragAnchor = null;
        if (Mouse.Captured == InputRoot) InputRoot.ReleaseMouseCapture();
    }

    public void CancelActiveInteraction()
    {
        CancelMouseStroke();
        CancelShapePreview();
        CancelTextEditor();
        _erasedThisDrag.Clear();
        ClearLaserTrail();
        if (Mouse.Captured is not null) Mouse.Capture(null);
        ClearSelection();
    }

    // Keeps the status pill above the taskbar: the overlay spans the whole monitor, the pill belongs to the work area.
    private void PlaceStatusPill()
    {
        var screen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == _display.DeviceName);
        if (screen is null) return;
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleY;
        var inset = Math.Max(0, screen.Bounds.Bottom - screen.WorkingArea.Bottom) / scale;
        StatusPill.Margin = new Thickness(12, 0, 0, 8 + inset);
    }

    private static Rect BoundsOf(UIElement element, Visual ancestor)
    {
        var local = VisualTreeHelper.GetDescendantBounds(element);
        if (local.IsEmpty && element is FrameworkElement f) local = new Rect(0, 0, f.ActualWidth, f.ActualHeight);
        return element.TransformToAncestor(ancestor).TransformBounds(local);
    }

    private bool TryEraseVector(Point point)
    {
        const double tolerance = 12;
        for (var index = ShapeSurface.Children.Count - 1; index >= 0; index--)
        {
            if (ShapeSurface.Children[index] is not UIElement element || _erasedThisDrag.Contains(element)) continue;
            Rect bounds;
            try { bounds = BoundsOf(element, ShapeSurface); }
            catch (InvalidOperationException) { continue; }
            bounds.Inflate(tolerance, tolerance);
            if (!bounds.Contains(point)) continue;
            if (element is Path path && path.Data is not null)
            {
                var localPoint = ShapeSurface.TranslatePoint(point, path);
                var pen = new MediaPen(path.Stroke, Math.Max(path.StrokeThickness, 1) + tolerance * 2);
                if (!path.Data.StrokeContains(pen, localPoint) &&
                    (path.Fill == Brushes.Transparent || !path.Data.FillContains(localPoint))) continue;
            }
            ShapeSurface.Children.Remove(element); _erasedThisDrag.Add(element);
            if (_expirations.TryGetValue(element, out var expiration)) element.Opacity = expiration.OriginalOpacity;
            _history.Add(new Removal(element)); _removed.Clear(); _expirations.Remove(element);
            return true;
        }
        return false;
    }

    private void UndoItem(object item)
    {
        switch (item)
        {
            case StrokeGroup group: foreach (var stroke in group.Strokes) RemoveAnnotation(stroke); break;
            case MoveOperation move: SetOffset(move.Item, move.From); break;
            case Removal removal: RestoreAnnotation(removal.Item); break;
            case ClearOperation clear: foreach (var annotation in clear.Items) RestoreAnnotation(annotation); break;
            case EditOperation edit: edit.Undo(); break;
            default: RemoveAnnotation(item); break;
        }
    }

    private void RedoItem(object item)
    {
        switch (item)
        {
            case StrokeGroup group: foreach (var stroke in group.Strokes) RestoreAnnotation(stroke); break;
            case MoveOperation move: SetOffset(move.Item, move.To); break;
            case Removal removal: RemoveAnnotation(removal.Item); break;
            case ClearOperation clear: foreach (var annotation in clear.Items) RemoveAnnotation(annotation); break;
            case EditOperation edit: edit.Redo(); break;
            default: RestoreAnnotation(item); break;
        }
    }

    private void RemoveAnnotation(object item)
    {
        if (item is Stroke stroke) InkSurface.Strokes.Remove(stroke);
        if (item is UIElement element) ShapeSurface.Children.Remove(element);
        _expirations.Remove(item);
    }

    private void RestoreAnnotation(object item)
    {
        if (item is Stroke stroke && !InkSurface.Strokes.Contains(stroke)) InkSurface.Strokes.Add(stroke);
        if (item is UIElement element && !ShapeSurface.Children.Contains(element)) ShapeSurface.Children.Add(element);
    }

    private static Vector GetOffset(UIElement element) => element.RenderTransform is TranslateTransform t ? new Vector(t.X, t.Y) : default;
    private static void SetOffset(UIElement element, Vector offset) => element.RenderTransform = new TranslateTransform(offset.X, offset.Y);

    private sealed record MoveOperation(UIElement Item, Vector From, Vector To);
    private sealed record StrokeGroup(StrokeCollection Strokes);
    /// <summary>Generic undoable edit (selection move, recolor, thickness, duplicate, delete).</summary>
    private sealed record EditOperation(Action Undo, Action Redo);

    private void ScheduleFade(object item)
    {
        if (_settings.FadeDuration is not TimeSpan duration || duration <= TimeSpan.Zero) return;
        _expirations[item] = (DateTimeOffset.UtcNow, duration,
            item is UIElement element ? element.Opacity : 1d,
            item is Stroke stroke ? stroke.DrawingAttributes.Color.A : byte.MaxValue);
        if (!_fadeTimer.IsEnabled) _fadeTimer.Start();
    }

    private void FadeTimer_OnTick(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _expirations.ToArray())
        {
            var elapsed = now - pair.Value.Start;
            if (pair.Key is UIElement element && elapsed > pair.Value.Duration * .75)
                element.Opacity = pair.Value.OriginalOpacity * Math.Max(0, 1 - (elapsed - pair.Value.Duration * .75).TotalMilliseconds / (pair.Value.Duration.TotalMilliseconds * .25));
            if (pair.Key is Stroke fadingStroke && elapsed > pair.Value.Duration * .75)
            {
                var factor = Math.Max(0, 1 - (elapsed - pair.Value.Duration * .75).TotalMilliseconds / (pair.Value.Duration.TotalMilliseconds * .25));
                var color = fadingStroke.DrawingAttributes.Color;
                fadingStroke.DrawingAttributes.Color = Color.FromArgb((byte)(pair.Value.OriginalAlpha * factor), color.R, color.G, color.B);
            }
            if (elapsed < pair.Value.Duration) continue;
            if (pair.Key is Stroke stroke) InkSurface.Strokes.Remove(stroke);
            if (pair.Key is UIElement visual) ShapeSurface.Children.Remove(visual);
            _selection.Remove(pair.Key);
            _history.Remove(pair.Key); _expirations.Remove(pair.Key);
        }
        if (_expirations.Count == 0) _fadeTimer.Stop();
        if (_settings.Tool == ToolKind.Cursor && !HasVisibleContent) Hide();
    }

    private sealed record Removal(object Item);
    private sealed record ClearOperation(IReadOnlyList<object> Items);
}
