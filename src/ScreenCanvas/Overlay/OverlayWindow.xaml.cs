using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

namespace ScreenCanvas.Overlay;

public partial class OverlayWindow : Window
{
    private readonly ToolSettings _settings;
    private readonly DisplayInfo _display;
    private readonly Stack<object> _removed = new();
    private readonly List<object> _history = [];
    private bool _boardVisible;
    private Point? _dragAnchor;
    private Path? _shapePreview;
    private readonly System.Windows.Media.DrawingVisual _activeStrokeVisual = new();
    private readonly System.Windows.Media.DrawingVisual _activeShapeVisual = new();
    private Ellipse? _laserDot;
    private readonly DispatcherTimer _fadeTimer;
    private readonly Dictionary<object, (DateTimeOffset Start, TimeSpan Duration, double OriginalOpacity, byte OriginalAlpha)> _expirations = [];
    private readonly HashSet<UIElement> _erasedThisDrag = [];
    private Stroke? _mouseStroke;
    private TextBox? _textEditor;
    private UIElement? _selectedElement;
    private readonly System.Windows.Shapes.Rectangle _selectionVisual = new() { Stroke=Brushes.DeepSkyBlue, StrokeThickness=1.5, StrokeDashArray=new DoubleCollection([3,2]), Fill=Brushes.Transparent, IsHitTestVisible=false, Visibility=Visibility.Collapsed };
    private Point? _selectionAnchor;
    private Vector _selectionStartOffset;
    private readonly IOverlayManager? _manager;

    public OverlayWindow(DisplayInfo display, ToolSettings settings, IOverlayManager? manager = null)
    {
        InitializeComponent();
        ShapeSurface.Children.Add(_selectionVisual);
        _settings = settings;
        _display = display;
        _manager = manager;
        
_fadeTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        _fadeTimer.Tick += FadeTimer_OnTick;
        Left = display.Left;
        Top = display.Top;
        Width = display.Width;
        Height = display.Height;
        InkSurface.StrokeCollected += (_, e) =>
        {
            if (_settings.PenMode is PenMode.Dashed or PenMode.Dotted)
            {
                InkSurface.Strokes.Remove(e.Stroke);
                var pieces = PatternStroke(e.Stroke, _settings.PenMode == PenMode.Dotted);
                foreach (var piece in pieces) InkSurface.Strokes.Add(piece);
                _history.Add(new StrokeGroup(pieces));
                _removed.Clear();
                return;
            }
            _history.Add(e.Stroke);
            _removed.Clear();
            ScheduleFade(e.Stroke);
        };
        InkSurface.StrokeErasing += (_, e) =>
        {
            if (_expirations.TryGetValue(e.Stroke, out var expiration))
            {
                var color = e.Stroke.DrawingAttributes.Color;
                e.Stroke.DrawingAttributes.Color = Color.FromArgb(expiration.OriginalAlpha, color.R, color.G, color.B);
                _expirations.Remove(e.Stroke);
            }
            _history.Add(new Removal(e.Stroke));
            _removed.Clear();
        };
        MouseMove += OnMouseMove;
        PreviewKeyDown += OverlayWindow_OnPreviewKeyDown;
        Loaded += (_, _) => RefreshTool();
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
            if (Mouse.Captured == InputRoot || _mouseStroke is not null || _dragAnchor is not null)
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

    private bool HasVisibleContent => _boardVisible || InkSurface.Strokes.Count > 0 || ShapeSurface.Children.Cast<UIElement>().Any(element => element != _laserDot && element != _selectionVisual);

    public void RefreshTool()
    {
        CancelMouseStroke();
        CancelShapePreview();
        if (_settings.Tool != ToolKind.Text) CommitTextEditor();
        if (_settings.Tool != ToolKind.Laser && _laserDot is not null)
        {
            ShapeSurface.Children.Remove(_laserDot);
            _laserDot = null;
        }
        var attributes = new DrawingAttributes
        {
            Color = Color.FromArgb(_settings.Opacity, _settings.Color.R, _settings.Color.G, _settings.Color.B),
            Width = _settings.Tool == ToolKind.Highlighter ? _settings.Thickness * 4 : _settings.Thickness,
            Height = _settings.Tool == ToolKind.Highlighter ? _settings.Thickness * 4 : _settings.Thickness,
            IsHighlighter = _settings.Tool == ToolKind.Highlighter,
            FitToCurve = true,
            IgnorePressure = !_settings.PressureEnabled
        };
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
        InkSurface.DefaultDrawingAttributes = attributes;

        InkSurface.EditingMode = _settings.Tool switch
        {
            ToolKind.Pen or ToolKind.Highlighter => InkCanvasEditingMode.Ink,
            ToolKind.Eraser => InkCanvasEditingMode.EraseByStroke,
            _ => InkCanvasEditingMode.None
        };
        SpotlightSurface.Visibility = _settings.Tool == ToolKind.Spotlight ? Visibility.Visible : Visibility.Collapsed;
        var cursor = _settings.Tool switch
        {
            ToolKind.Pen or ToolKind.Highlighter => InputCursors.Pen,
            ToolKind.Text => InputCursors.IBeam,
            ToolKind.Select => InputCursors.SizeAll,
            ToolKind.Eraser or ToolKind.Shape or ToolKind.NumberMarker or ToolKind.Spotlight => InputCursors.Cross,
            ToolKind.Laser => InputCursors.None,
            _ => InputCursors.Arrow
        };
        InputRoot.Cursor = cursor;
        InkSurface.Cursor = cursor;
    }

    private bool IsOverToolbar(Point screenPoint) => _manager?.IsPointOverUi(screenPoint) ?? false;

    private void InputRoot_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var screenPoint = PointToScreen(e.GetPosition(this));
        if (IsOverToolbar(screenPoint)) return;
        _manager?.NotifyInteractionStarted();
        if (_settings.Tool is not (ToolKind.Pen or ToolKind.Highlighter) || e.StylusDevice is not null) return;
        var point=e.GetPosition(InkSurface);
        _mouseStroke=new Stroke(new StylusPointCollection([new StylusPoint(point.X,point.Y)]),InkSurface.DefaultDrawingAttributes.Clone());
        InkSurface.Strokes.Add(_mouseStroke); InputRoot.CaptureMouse(); e.Handled=true;
    }

    private void InputRoot_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if(_mouseStroke is null || e.LeftButton!=MouseButtonState.Pressed)return;
        var point=e.GetPosition(InkSurface);
        
        if (_settings.PenMode == PenMode.StraightHighlighter)
        {
            while (_mouseStroke.StylusPoints.Count > 1) _mouseStroke.StylusPoints.RemoveAt(1);
            _mouseStroke.StylusPoints.Add(new StylusPoint(point.X,point.Y));
        }
        else
        {
            var pressure = _settings.PenMode is PenMode.Brush or PenMode.Pressure
                ? (float)Math.Clamp(.3 + .7 / (1 + (point - new Point(_mouseStroke.StylusPoints[^1].X, _mouseStroke.StylusPoints[^1].Y)).Length / 5), .3, 1)
                : .5f;
            var stylusPoint = new StylusPoint(point.X, point.Y) { PressureFactor = pressure };
            _mouseStroke.StylusPoints.Add(stylusPoint);
        }
        
        // Zero-lag Visual update
        using (var dc = _activeStrokeVisual.RenderOpen())
        {
            dc.DrawGeometry(new SolidColorBrush(_settings.Color), new MediaPen(new SolidColorBrush(_settings.Color), _settings.Thickness), _mouseStroke.GetGeometry());
        }
        
        e.Handled=true;
    }

    private void InputRoot_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if(_mouseStroke is null)return;
        var stroke=_mouseStroke;_mouseStroke=null;InputRoot.ReleaseMouseCapture();
        if (_settings.PenMode is PenMode.Dashed or PenMode.Dotted)
        {
            InkSurface.Strokes.Remove(stroke);
            var pieces=PatternStroke(stroke,_settings.PenMode==PenMode.Dotted);
            foreach(var piece in pieces)InkSurface.Strokes.Add(piece);
            _history.Add(new StrokeGroup(pieces));
        }
        else {_history.Add(stroke);ScheduleFade(stroke);}
        _removed.Clear();e.Handled=true;
    }

    private void CancelMouseStroke()
    {
        if(_mouseStroke is null)return;InkSurface.Strokes.Remove(_mouseStroke);_mouseStroke=null;if(Mouse.Captured==InputRoot)InputRoot.ReleaseMouseCapture();
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

    public void Undo()
    {
        if (_history.Count == 0) return;
        var item = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        UndoItem(item);
        _removed.Push(item);
    }

    public void Redo()
    {
        if (_removed.Count == 0) return;
        var item = _removed.Pop();
        RedoItem(item);
        _history.Add(item);
    }

    public void ClearInk()
    {
        CancelShapePreview();
        CancelTextEditor();
        var snapshot = InkSurface.Strokes.Cast<object>()
            .Concat(ShapeSurface.Children.Cast<UIElement>().Where(element => element != _laserDot && element != _selectionVisual))
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
        foreach (var element in ShapeSurface.Children.Cast<UIElement>().Where(element => element != _laserDot).ToArray())
            ShapeSurface.Children.Remove(element);
        _expirations.Clear();
        _fadeTimer.Stop();
    }

    public void ToggleBoard(bool dark)
    {
        _boardVisible = !_boardVisible;
        BoardSurface.Background = _boardVisible
            ? new SolidColorBrush(dark ? Colors.Black : Colors.White)
            : System.Windows.Media.Brushes.Transparent;
    }

    public void SetBoard(Color? color)
    {
        _boardVisible = color.HasValue;
        BoardSurface.Background = color.HasValue ? new SolidColorBrush(color.Value) : Brushes.Transparent;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_settings.Tool != ToolKind.Spotlight) return;
        var point = e.GetPosition(this);
        var alpha = (byte)Math.Clamp(_settings.SpotlightOverlayOpacity * 255, 0, 255);
        SpotlightPath.Fill = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
        var outer = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        var hole = new EllipseGeometry(point, _settings.SpotlightRadius, _settings.SpotlightRadius);
        SpotlightPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude, outer, hole);
    }

    private void InputRoot_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var screenPoint = PointToScreen(e.GetPosition(this));
        if (IsOverToolbar(screenPoint)) return;
        _manager?.NotifyInteractionStarted();
        var point = e.GetPosition(InputRoot);
        _erasedThisDrag.Clear();
        if (_settings.Tool == ToolKind.Select)
        {
            SelectAt(point);
            if (_selectedElement is not null) { _selectionAnchor=point;_selectionStartOffset=GetOffset(_selectedElement);InputRoot.CaptureMouse(); }
            e.Handled=true;return;
        }
        if (_settings.Tool == ToolKind.Eraser && TryEraseVector(point)) { e.Handled = true; return; }
        if (_settings.Tool == ToolKind.Shape)
        {
            _dragAnchor = point;
            _shapePreview = NewShapePath();
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
        var point = e.GetPosition(InputRoot);
        if (_settings.Tool==ToolKind.Select && _selectedElement is not null && _selectionAnchor is Point selectionAnchor && e.LeftButton==MouseButtonState.Pressed)
        {
            var delta=point-selectionAnchor;SetOffset(_selectedElement,_selectionStartOffset+delta);UpdateSelectionVisual();e.Handled=true;return;
        }
        if (_settings.Tool == ToolKind.Eraser && e.LeftButton == MouseButtonState.Pressed)
        {
            var screenPoint = PointToScreen(e.GetPosition(this));
            if (!IsOverToolbar(screenPoint) && TryEraseVector(point)) { e.Handled=true; return; }
        }
        if (_settings.Tool == ToolKind.Laser)
        {
            var screenPoint = PointToScreen(e.GetPosition(this));
            if (IsOverToolbar(screenPoint))
            {
                if (_laserDot is not null) _laserDot.Visibility = Visibility.Collapsed;
            }
            else
            {
                _laserDot ??= CreateLaserDot();
                _laserDot.Visibility = Visibility.Visible;
                Canvas.SetLeft(_laserDot, point.X - _laserDot.Width / 2);
                Canvas.SetTop(_laserDot, point.Y - _laserDot.Height / 2);
            }
        }
        if (_dragAnchor is not Point anchor || _shapePreview is null || e.LeftButton != MouseButtonState.Pressed) return;
        UpdateShape(_shapePreview, anchor, point, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
        e.Handled = true;
    }

    private void InputRoot_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _erasedThisDrag.Clear();
        if (_settings.Tool==ToolKind.Select && _selectedElement is not null && _selectionAnchor is not null)
        {
            var movedOffset=GetOffset(_selectedElement);if((movedOffset-_selectionStartOffset).Length>.5){_history.Add(new MoveOperation(_selectedElement,_selectionStartOffset,movedOffset));_removed.Clear();}
            _selectionAnchor=null;if(Mouse.Captured==InputRoot)InputRoot.ReleaseMouseCapture();e.Handled=true;return;
        }
        if (_dragAnchor is not Point anchor || _shapePreview is null) return;
        var end = e.GetPosition(InputRoot);
        UpdateShape(_shapePreview, anchor, end, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
        var completed = _shapePreview;
        _shapePreview = null;
        _dragAnchor = null;
        InputRoot.ReleaseMouseCapture();
        if ((end - anchor).Length < 2)
        {
            ShapeSurface.Children.Remove(completed);
            e.Handled = true;
            return;
        }
        _history.Add(completed);
        ScheduleFade(completed);
        _removed.Clear();
        if (!_settings.StickyTools) _manager?.DeactivateCurrentTool(ToolDeactivationReason.CursorSelected);
        e.Handled = true;
    }

    private void InputRoot_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_shapePreview is not null && e.LeftButton != MouseButtonState.Pressed)
            CancelShapePreview();
        _erasedThisDrag.Clear();
    }

    private Path NewShapePath() => new()
    {
        Stroke = new SolidColorBrush(_settings.Color),
        StrokeThickness = _settings.Thickness,
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        Opacity = _settings.Opacity / 255d,
        Fill = _settings.ShapeFillEnabled ? new SolidColorBrush(Color.FromArgb(_settings.ShapeFillOpacity, _settings.Color.R, _settings.Color.G, _settings.Color.B)) : Brushes.Transparent,
        IsHitTestVisible = false
    };

    private void UpdateShape(Path path, Point start, Point end, bool constrain, bool fromCenter)
    {
        if (constrain && _settings.Shape is ShapeKind.Line or ShapeKind.Arrow or ShapeKind.DoubleArrow)
        {
            var distance = (end - start).Length;
            var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            angle = Math.Round(angle / (Math.PI / 4)) * (Math.PI / 4);
            end = new Point(start.X + Math.Cos(angle) * distance, start.Y + Math.Sin(angle) * distance);
        }
        if (constrain && _settings.Shape is ShapeKind.Rectangle or ShapeKind.RoundedRectangle or ShapeKind.Ellipse)
        {
            var side = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
            end = new Point(start.X + Math.Sign(end.X-start.X) * side, start.Y + Math.Sign(end.Y-start.Y) * side);
        }
        if (fromCenter)
        {
            var delta = end - start;
            start -= delta;
        }
        var rect = new Rect(start, end);
        path.Data = _settings.Shape switch
        {
            ShapeKind.Line => new LineGeometry(start, end),
            ShapeKind.Arrow => ArrowGeometry(start, end, false, _settings.Thickness),
            ShapeKind.DoubleArrow => ArrowGeometry(start, end, true, _settings.Thickness),
            ShapeKind.Rectangle => new RectangleGeometry(rect),
            ShapeKind.RoundedRectangle => new RectangleGeometry(rect, 14, 14),
            ShapeKind.Ellipse => new EllipseGeometry(rect),
            ShapeKind.Diamond => PolygonGeometry([new Point(rect.Left + rect.Width / 2, rect.Top), new Point(rect.Right, rect.Top + rect.Height / 2), new Point(rect.Left + rect.Width / 2, rect.Bottom), new Point(rect.Left, rect.Top + rect.Height / 2)]),
            _ => new RectangleGeometry(rect)
        };
    }

    private static Geometry PolygonGeometry(IReadOnlyList<Point> points)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = true };
        figure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
        return new PathGeometry([figure]);
    }

    private static Geometry ArrowGeometry(Point start, Point end, bool startHead, double strokeWidth)
    {
        var vector = start - end;
        if (vector.Length < 1) return new LineGeometry(start, end);
        vector.Normalize();
        var normal = new Vector(-vector.Y, vector.X);
        var length = Math.Clamp(10 + strokeWidth * 1.8, 12, 28);
        var width = Math.Clamp(5 + strokeWidth, 6, 16);
        var group = new GeometryGroup();
        group.Children.Add(new LineGeometry(start, end));
        group.Children.Add(new LineGeometry(end, end + vector * length + normal * width));
        group.Children.Add(new LineGeometry(end, end + vector * length - normal * width));
        if (startHead)
        {
            vector = -vector;
            group.Children.Add(new LineGeometry(start, start + vector * length + normal * width));
            group.Children.Add(new LineGeometry(start, start + vector * length - normal * width));
        }
        return group;
    }

    private static Geometry CurvedArrowGeometry(Point start, Point end, double width)
    {
        var middle = new Point((start.X+end.X)/2, Math.Min(start.Y,end.Y)-Math.Max(24,Math.Abs(end.X-start.X)*.25));
        var figure = new PathFigure { StartPoint=start };
        figure.Segments.Add(new QuadraticBezierSegment(middle,end,true));
        var group=new GeometryGroup(); group.Children.Add(new PathGeometry([figure])); group.Children.Add(ArrowGeometry(new Point((middle.X+end.X)/2,(middle.Y+end.Y)/2),end,false,width)); return group;
    }

    private static Geometry ElbowGeometry(Point start, Point end, bool arrow, double width)
    {
        var bend=new Point(end.X,start.Y); var figure=new PathFigure { StartPoint=start }; figure.Segments.Add(new PolyLineSegment([bend,end],true));
        var group=new GeometryGroup(); group.Children.Add(new PathGeometry([figure])); if(arrow) group.Children.Add(ArrowGeometry(bend,end,false,width)); return group;
    }

    private static Geometry DatabaseGeometry(Rect r) { var g=new GeometryGroup(); g.Children.Add(new RectangleGeometry(new Rect(r.Left,r.Top+r.Height*.12,r.Width,r.Height*.76))); g.Children.Add(new EllipseGeometry(new Rect(r.Left,r.Top,r.Width,r.Height*.24))); g.Children.Add(new EllipseGeometry(new Rect(r.Left,r.Bottom-r.Height*.24,r.Width,r.Height*.24))); return g; }
    private static Geometry CloudGeometry(Rect r) { var g=new GeometryGroup(); foreach(var e in new[]{new Rect(r.Left,r.Top+r.Height*.35,r.Width,r.Height*.55),new Rect(r.Left+r.Width*.12,r.Top+r.Height*.2,r.Width*.42,r.Height*.55),new Rect(r.Left+r.Width*.4,r.Top,r.Width*.45,r.Height*.7),new Rect(r.Left+r.Width*.65,r.Top+r.Height*.27,r.Width*.35,r.Height*.55)}) g.Children.Add(new EllipseGeometry(e)); return g; }
    private static Geometry CalloutGeometry(Rect r) => PolygonGeometry([r.TopLeft,r.TopRight,r.BottomRight,new Point(r.Left+r.Width*.35,r.Bottom),new Point(r.Left+r.Width*.18,r.Bottom+r.Height*.22),new Point(r.Left+r.Width*.22,r.Bottom),r.BottomLeft]);
    private static Geometry CheckGeometry(Rect r) { var f=new PathFigure{StartPoint=new Point(r.Left,r.Top+r.Height*.55)}; f.Segments.Add(new PolyLineSegment([new Point(r.Left+r.Width*.38,r.Bottom),r.TopRight],true)); return new PathGeometry([f]); }
    private static Geometry CrossGeometry(Rect r) { var g=new GeometryGroup(); g.Children.Add(new LineGeometry(r.TopLeft,r.BottomRight)); g.Children.Add(new LineGeometry(r.TopRight,r.BottomLeft)); return g; }
    private static Geometry WarningGeometry(Rect r) { var g=new GeometryGroup(); g.Children.Add(PolygonGeometry([new Point(r.Left+r.Width/2,r.Top),r.BottomRight,r.BottomLeft])); g.Children.Add(new LineGeometry(new Point(r.Left+r.Width/2,r.Top+r.Height*.3),new Point(r.Left+r.Width/2,r.Top+r.Height*.68))); return g; }
    private static Geometry QuestionGeometry(Rect r) { var g=new GeometryGroup(); g.Children.Add(new EllipseGeometry(r)); g.Children.Add(new EllipseGeometry(new Point(r.Left+r.Width/2,r.Bottom-r.Height*.2),Math.Max(2,r.Width*.03),Math.Max(2,r.Width*.03))); return g; }
    private static Geometry StarGeometry(Rect r) { var pts=new List<Point>(); for(var i=0;i<10;i++){var a=-Math.PI/2+i*Math.PI/5;var radius=i%2==0?1:.42;pts.Add(new Point(r.Left+r.Width/2+Math.Cos(a)*r.Width/2*radius,r.Top+r.Height/2+Math.Sin(a)*r.Height/2*radius));} return PolygonGeometry(pts); }

    private void AddMarker(Point point)
    {
        var size = _settings.MarkerSize;
        if (_settings.MarkerNumber < 1) _settings.MarkerNumber = 1;
        var label = _settings.LetterMarkers ? MarkerLetter(_settings.MarkerNumber++) : (_settings.MarkerNumber++).ToString();
        var border = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(_settings.SquareMarkers ? 7 : size / 2),
            Background = new SolidColorBrush(_settings.Color),
            Child = new TextBlock { Text = label, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = size * .48, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(border, point.X - size / 2);
        Canvas.SetTop(border, point.Y - size / 2);
        ShapeSurface.Children.Add(border);
        _history.Add(border);
        ScheduleFade(border);
        _removed.Clear();
    }

    private static string MarkerLetter(int value)
    {
        var result = string.Empty;
        while (value > 0) { value--; result = (char)('A' + value % 26) + result; value /= 26; }
        return result;
    }

    private void BeginText(Point point)
    {
        CommitTextEditor();
        var editor = new TextBox
        {
            MinWidth = 100,
            FontFamily = new System.Windows.Media.FontFamily(_settings.FontFamily),
            FontSize = _settings.FontSize,
            Foreground = new SolidColorBrush(_settings.Color),
            CaretBrush = new SolidColorBrush(_settings.Color),
            FontWeight = _settings.TextBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = _settings.TextItalic ? FontStyles.Italic : FontStyles.Normal,
            TextDecorations = _settings.TextUnderline ? TextDecorations.Underline : null,
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.FromArgb(160, _settings.Color.R, _settings.Color.G, _settings.Color.B)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2, 4, 2),
            AcceptsReturn = true
        };
        Canvas.SetLeft(editor, point.X); Canvas.SetTop(editor, point.Y);
        ShapeSurface.Children.Add(editor);
        _textEditor = editor;
        SetNoActivate(false);
        Activate();
        editor.Focus();
        Keyboard.Focus(editor);
        void Commit(object? _, RoutedEventArgs __)
        {
            if (_textEditor != editor) return;
            _textEditor = null;
            editor.LostKeyboardFocus -= Commit;
            if (string.IsNullOrWhiteSpace(editor.Text))
            {
                ShapeSurface.Children.Remove(editor);
                SetNoActivate(true);
                return;
            }

            var textBlock = new TextBlock
            {
                Text = editor.Text,
                FontFamily = editor.FontFamily,
                FontSize = editor.FontSize,
                FontWeight = editor.FontWeight,
                FontStyle = editor.FontStyle,
                Foreground = editor.Foreground,
                TextDecorations = editor.TextDecorations,
                Background = Brushes.Transparent,
                Padding = new Thickness(4, 2, 4, 2),
                IsHitTestVisible = false
            };

            var luminance = (0.299 * _settings.Color.R + 0.587 * _settings.Color.G + 0.114 * _settings.Color.B);
            textBlock.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 3,
                ShadowDepth = 1,
                Opacity = 0.85,
                Color = luminance > 160 ? Colors.Black : Colors.White
            };

            Canvas.SetLeft(textBlock, Canvas.GetLeft(editor));
            Canvas.SetTop(textBlock, Canvas.GetTop(editor));
            ShapeSurface.Children.Remove(editor);
            ShapeSurface.Children.Add(textBlock);

            _history.Add(textBlock);
            _removed.Clear();
            ScheduleFade(textBlock);
            SetNoActivate(true);
        }
        editor.LostKeyboardFocus += Commit;
        editor.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { CancelTextEditor(); e.Handled = true; }
            else if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Commit(editor, e); Keyboard.ClearFocus(); e.Handled = true; }
        };
    }

    private void CommitTextEditor()
    {
        if (_textEditor is null) return;
        var editor = _textEditor;
        _textEditor = null;
        if (string.IsNullOrWhiteSpace(editor.Text))
        {
            ShapeSurface.Children.Remove(editor);
        }
        else
        {
            var textBlock = new TextBlock
            {
                Text = editor.Text,
                FontFamily = editor.FontFamily,
                FontSize = editor.FontSize,
                FontWeight = editor.FontWeight,
                FontStyle = editor.FontStyle,
                Foreground = editor.Foreground,
                TextDecorations = editor.TextDecorations,
                Background = Brushes.Transparent,
                Padding = new Thickness(4, 2, 4, 2),
                IsHitTestVisible = false
            };

            var luminance = (0.299 * _settings.Color.R + 0.587 * _settings.Color.G + 0.114 * _settings.Color.B);
            textBlock.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 3,
                ShadowDepth = 1,
                Opacity = 0.85,
                Color = luminance > 160 ? Colors.Black : Colors.White
            };

            Canvas.SetLeft(textBlock, Canvas.GetLeft(editor));
            Canvas.SetTop(textBlock, Canvas.GetTop(editor));
            ShapeSurface.Children.Remove(editor);
            ShapeSurface.Children.Add(textBlock);

            _history.Add(textBlock);
            _removed.Clear();
            ScheduleFade(textBlock);
        }
        SetNoActivate(true);
    }

    private void CancelTextEditor()
    {
        if (_textEditor is null) return;
        ShapeSurface.Children.Remove(_textEditor);
        _textEditor = null;
        Keyboard.ClearFocus();
        SetNoActivate(true);
    }

    private void SetNoActivate(bool enabled)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == nint.Zero) return;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLong(handle, NativeMethods.GwlExStyle,
            enabled ? style | NativeMethods.WsExNoActivate : style & ~NativeMethods.WsExNoActivate);
    }

    private Ellipse CreateLaserDot()
    {
        var dot = new Ellipse
        {
            Width = 16, Height = 16, Fill = new SolidColorBrush(_settings.Color), IsHitTestVisible = false,
            Stroke = new SolidColorBrush(Colors.White), StrokeThickness = 2.5,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = _settings.Color, BlurRadius = 24, ShadowDepth = 0, Opacity = 0.95 }
        };
        ShapeSurface.Children.Add(dot);
        return dot;
    }

    private void OverlayWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_textEditor is not null)
        {
            if (e.Key == Key.Escape) { CancelTextEditor(); _manager?.DeactivateCurrentTool(ToolDeactivationReason.Escape); e.Handled = true; }
            return;
        }

        if (e.Key == Key.Delete && _settings.Tool == ToolKind.Select && _selectedElement is not null)
        {
            var removed = _selectedElement; Deselect(); ShapeSurface.Children.Remove(removed); _history.Add(new Removal(removed)); _removed.Clear(); e.Handled = true; return;
        }

        if (e.Key == Key.Escape)
        {
            if (_shapePreview is not null) CancelShapePreview();
            _manager?.DeactivateCurrentTool(ToolDeactivationReason.Escape);
            e.Handled = true;
            return;
        }

        var isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var isShift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;

        if (isCtrl && e.Key == Key.Z) { _manager?.Undo(); e.Handled = true; return; }
        if (isCtrl && e.Key == Key.Y) { _manager?.Redo(); e.Handled = true; return; }
        if (isCtrl && isShift && e.Key == Key.Delete) { _manager?.Clear(); e.Handled = true; return; }

        if (!isCtrl && !isShift && (Keyboard.Modifiers & ModifierKeys.Alt) == 0)
        {
            switch (e.Key)
            {
                case Key.P: _manager?.SetTool(ToolKind.Pen); _manager?.SetPenMode(PenMode.Ballpoint); e.Handled = true; break;
                case Key.H: _manager?.SetTool(ToolKind.Highlighter); _manager?.SetPenMode(PenMode.Highlighter); e.Handled = true; break;
                case Key.E: _manager?.SetTool(ToolKind.Eraser); e.Handled = true; break;
                case Key.A: _manager?.SetTool(ToolKind.Shape); _settings.Shape = ShapeKind.Arrow; RefreshTool(); e.Handled = true; break;
                case Key.R: _manager?.SetTool(ToolKind.Shape); _settings.Shape = ShapeKind.Rectangle; RefreshTool(); e.Handled = true; break;
                case Key.O: _manager?.SetTool(ToolKind.Shape); _settings.Shape = ShapeKind.Ellipse; RefreshTool(); e.Handled = true; break;
                case Key.T: _manager?.SetTool(ToolKind.Text); e.Handled = true; break;
                case Key.L: _manager?.SetTool(ToolKind.Laser); e.Handled = true; break;
                case Key.M: _manager?.SetTool(ToolKind.Spotlight); e.Handled = true; break;
                
                
                case Key.D1: _manager?.SetTool(ToolKind.NumberMarker); e.Handled = true; break;
            }
        }
    }

    private void CancelShapePreview()
    {
        if (_shapePreview is not null) ShapeSurface.Children.Remove(_shapePreview);
        _shapePreview=null; _dragAnchor=null;
        if (Mouse.Captured == InputRoot) InputRoot.ReleaseMouseCapture();
    }

    private Point GetScaledPoint(Point point)
    {
        if (_manager == null) return point;
        
        // Check if zoom is active via the ToolbarWindow reference in manager or similar
        // Since we don't have direct access to the zoom engine here, we check the state
        // If the WindowsZoomEngine is active, we need to translate screen coordinates 
        // to the magnified source coordinates.
        
        // This is a placeholder for the coordinate scaling logic 
        // which will be fully integrated with the WindowsZoomEngine's current factor and origin.
        return point; 
    }

    public void CancelActiveInteraction()
    {
        CancelMouseStroke();
        CancelShapePreview();
        CancelTextEditor();
        _erasedThisDrag.Clear();
        if (_laserDot is not null) { ShapeSurface.Children.Remove(_laserDot); _laserDot=null; }
        if (Mouse.Captured is not null) Mouse.Capture(null);
        Deselect();
    }

    private void SelectAt(Point point)
    {
        Deselect();
        for(var i=ShapeSurface.Children.Count-1;i>=0;i--)
        {
            var element=ShapeSurface.Children[i];if(element==_selectionVisual||element==_laserDot)continue;
            try { var local=VisualTreeHelper.GetDescendantBounds(element);if(local.IsEmpty&&element is FrameworkElement f)local=new Rect(0,0,f.ActualWidth,f.ActualHeight);var bounds=element.TransformToAncestor(ShapeSurface).TransformBounds(local);bounds.Inflate(8,8);if(!bounds.Contains(point))continue;_selectedElement=element;UpdateSelectionVisual();break; } catch(InvalidOperationException) { }
        }
    }

    private void Deselect(){_selectedElement=null;_selectionAnchor=null;_selectionVisual.Visibility=Visibility.Collapsed;}
    private static Vector GetOffset(UIElement element)=>element.RenderTransform is TranslateTransform t?new Vector(t.X,t.Y):default;
    private static void SetOffset(UIElement element,Vector offset)=>element.RenderTransform=new TranslateTransform(offset.X,offset.Y);
    private void UpdateSelectionVisual()
    {
        if(_selectedElement is null){_selectionVisual.Visibility=Visibility.Collapsed;return;}
        var bounds=_selectedElement.TransformToAncestor(ShapeSurface).TransformBounds(VisualTreeHelper.GetDescendantBounds(_selectedElement));bounds.Inflate(4,4);
        Canvas.SetLeft(_selectionVisual,bounds.Left);Canvas.SetTop(_selectionVisual,bounds.Top);_selectionVisual.Width=Math.Max(1,bounds.Width);_selectionVisual.Height=Math.Max(1,bounds.Height);_selectionVisual.Visibility=Visibility.Visible;
    }

    private bool TryEraseVector(Point point)
    {
        const double tolerance=12;
        for(var index=ShapeSurface.Children.Count-1;index>=0;index--)
        {
            if(ShapeSurface.Children[index] is not UIElement element || _erasedThisDrag.Contains(element)) continue;
            Rect bounds;
            try
            {
                var local=VisualTreeHelper.GetDescendantBounds(element);
                if(local.IsEmpty && element is FrameworkElement framework) local=new Rect(0,0,framework.ActualWidth,framework.ActualHeight);
                bounds=element.TransformToAncestor(ShapeSurface).TransformBounds(local);
            }
            catch(InvalidOperationException) { continue; }
            bounds.Inflate(tolerance,tolerance);
            if(!bounds.Contains(point)) continue;
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
        switch(item)
        {
            case StrokeGroup group: foreach(var stroke in group.Strokes) RemoveAnnotation(stroke);break;
            case MoveOperation move: SetOffset(move.Item,move.From);break;
            case Removal removal: RestoreAnnotation(removal.Item); break;
            case ClearOperation clear: foreach(var annotation in clear.Items) RestoreAnnotation(annotation); break;
            default: RemoveAnnotation(item); break;
        }
    }

    private void RedoItem(object item)
    {
        switch(item)
        {
            case StrokeGroup group: foreach(var stroke in group.Strokes) RestoreAnnotation(stroke);break;
            case MoveOperation move: SetOffset(move.Item,move.To);break;
            case Removal removal: RemoveAnnotation(removal.Item); break;
            case ClearOperation clear: foreach(var annotation in clear.Items) RemoveAnnotation(annotation); break;
            default: RestoreAnnotation(item); break;
        }
    }

    private void RemoveAnnotation(object item)
    {
        if(item is Stroke stroke) InkSurface.Strokes.Remove(stroke);
        if(item is UIElement element) ShapeSurface.Children.Remove(element);
        _expirations.Remove(item);
    }

    private void RestoreAnnotation(object item)
    {
        if(item is Stroke stroke && !InkSurface.Strokes.Contains(stroke)) InkSurface.Strokes.Add(stroke);
        if(item is UIElement element && !ShapeSurface.Children.Contains(element)) ShapeSurface.Children.Add(element);
    }

    private sealed record MoveOperation(UIElement Item,Vector From,Vector To);
    private sealed record StrokeGroup(StrokeCollection Strokes);

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
        var now=DateTimeOffset.UtcNow;
        foreach(var pair in _expirations.ToArray())
        {
            var elapsed=now-pair.Value.Start;
            if(pair.Key is UIElement element && elapsed > pair.Value.Duration*.75)
                element.Opacity=pair.Value.OriginalOpacity*Math.Max(0,1-(elapsed-pair.Value.Duration*.75).TotalMilliseconds/(pair.Value.Duration.TotalMilliseconds*.25));
            if(pair.Key is Stroke fadingStroke && elapsed > pair.Value.Duration*.75)
            {
                var factor=Math.Max(0,1-(elapsed-pair.Value.Duration*.75).TotalMilliseconds/(pair.Value.Duration.TotalMilliseconds*.25));
                var color=fadingStroke.DrawingAttributes.Color;
                fadingStroke.DrawingAttributes.Color=Color.FromArgb((byte)(pair.Value.OriginalAlpha*factor),color.R,color.G,color.B);
            }
            if(elapsed < pair.Value.Duration) continue;
            if(pair.Key is Stroke stroke) InkSurface.Strokes.Remove(stroke);
            if(pair.Key is UIElement visual) ShapeSurface.Children.Remove(visual);
            _history.Remove(pair.Key); _expirations.Remove(pair.Key);
        }
        if(_expirations.Count==0) _fadeTimer.Stop();
        if(_settings.Tool==ToolKind.Cursor && !HasVisibleContent) Hide();
    }

    private sealed record Removal(object Item);
    private sealed record ClearOperation(IReadOnlyList<object> Items);
}
