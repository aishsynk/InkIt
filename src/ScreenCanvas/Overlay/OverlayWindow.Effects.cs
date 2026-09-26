using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ScreenCanvas.Core;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace ScreenCanvas.Overlay;

public partial class OverlayWindow
{
    // ---- Laser trail (design: 350 ms decay, <=26 points, red glow, white head) ----
    private const double LaserDecayMs = 350;
    private readonly List<(Point Point, long Time)> _laserTrail = [];
    private readonly Canvas _laserSegments = new() { IsHitTestVisible = false };
    private readonly Ellipse _laserHead = new() { Width = 14, Height = 14, Fill = Brushes.White, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private bool _laserRendering;
    private Point _pointer;
    private bool _pointerInside;

    // ---- Snap guides ----
    private readonly Line _guideH = GuideLine();
    private readonly Line _guideV = GuideLine();
    private readonly Ellipse _snapRing = new() { Width = 10, Height = 10, Stroke = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)), StrokeThickness = 2, Opacity = 0.85 };
    private readonly Ellipse _snapDot = new() { Width = 4, Height = 4, Fill = new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD)), Opacity = 0.85 };
    private readonly TextBlock _dimensionLabel = new() { FontSize = 10, FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas"), Foreground = new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD)) };
    private Size _shapeDimensions;

    // ---- Status pill ----
    private readonly Ellipse _statusDot = new() { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _statusText = StatusText(Color.FromRgb(0x94, 0xA3, 0xB8));
    private readonly Border _badgeSelected = StatusBadge(Color.FromRgb(0xA5, 0xB4, 0xFC), Color.FromArgb(0xCC, 0x1E, 0x1B, 0x4B), Color.FromArgb(0x99, 0x43, 0x38, 0xCA), "BoxSelect", true);
    private readonly Border _badgePalm = StatusBadge(Color.FromRgb(0x93, 0xC5, 0xFD), Color.FromArgb(0xB3, 0x17, 0x25, 0x54), Color.FromArgb(0x99, 0x1E, 0x40, 0xAF), null, false, "Palm Rejection Active");
    private readonly Border _badgeEraser = StatusBadge(Color.FromRgb(0xFD, 0xA4, 0xAF), Color.FromArgb(0xB3, 0x4C, 0x05, 0x19), Color.FromArgb(0x99, 0x9F, 0x12, 0x39), null, false, "Stylus Eraser Tip");
    private readonly Border _badgeSnap = StatusBadge(Color.FromRgb(0xFC, 0xD3, 0x4D), Color.FromArgb(0xB3, 0x45, 0x1A, 0x03), Color.FromArgb(0x99, 0x92, 0x40, 0x0E), null, false);
    private readonly Border _badgeLaser = StatusBadge(Color.FromRgb(0xFD, 0xA4, 0xAF), Color.FromArgb(0xCC, 0x4C, 0x05, 0x19), Color.FromArgb(0x99, 0xBE, 0x12, 0x3C), "Flame", true, "+Laser");
    private readonly Border _badgeSpotlight = StatusBadge(Color.FromRgb(0xFC, 0xD3, 0x4D), Color.FromArgb(0xCC, 0x45, 0x1A, 0x03), Color.FromArgb(0x99, 0xB4, 0x53, 0x09), "SunMedium", true, "+Spotlight");
    private readonly Border _badgeSmartShape = StatusBadge(Color.FromRgb(0xD8, 0xB4, 0xFE), Color.FromArgb(0xCC, 0x3B, 0x07, 0x64), Color.FromArgb(0x99, 0x7E, 0x22, 0xCE), "Wand2", true, "+Smart Shape");

    // ---- Digitizer state ----
    private enum PointerKind { Mouse, Pen, Touch }
    private PointerKind _pointerKind = PointerKind.Mouse;
    private double _pressure = 0.5;
    private double _tilt;
    private bool _penDown;
    private bool _eraserTip;
    private readonly Dictionary<int, Point> _touchContacts = [];
    // Multi-finger taps: two fingers = Undo, three = Redo (quick, without moving).
    private readonly Dictionary<int, Point> _tapStart = [];
    private DateTime _tapBegan;
    private int _tapMaxContacts;
    private bool _tapSpoiled;
    private bool _pinchActive;
    private double _pinchStartDistance;
    private double _pinchStartZoom;

    private static Line GuideLine() => new()
    {
        Stroke = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
        StrokeThickness = 1,
        StrokeDashArray = new DoubleCollection([4, 4]),
        Opacity = 0.4,
        SnapsToDevicePixels = true
    };

    private static TextBlock StatusText(Color color) => new()
    {
        FontSize = 10,
        FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas"),
        Foreground = new SolidColorBrush(color),
        VerticalAlignment = VerticalAlignment.Center
    };

    private static Border StatusBadge(Color text, Color background, Color border, string? icon, bool pill, string label = "")
    {
        var content = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        if (icon is not null)
            content.Children.Add(new UI.Controls.LucideIcon(icon, 12) { Foreground = new SolidColorBrush(text), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
        var tb = StatusText(text);
        tb.FontWeight = FontWeights.SemiBold;
        tb.Text = label;
        content.Children.Add(tb);
        return new Border
        {
            Child = content,
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(pill ? 999 : 4),
            Padding = pill ? new Thickness(6, 2, 6, 2) : new Thickness(4, 0, 4, 0),
            Margin = new Thickness(8, 0, 0, 0),
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static void SetBadgeText(Border badge, string text) => ((TextBlock)((StackPanel)badge.Child).Children[^1]).Text = text;

    private void InitializeEffects()
    {
        _laserSegments.Effect = new DropShadowEffect { Color = Color.FromRgb(0xEF, 0x44, 0x44), BlurRadius = 14, ShadowDepth = 0, Opacity = 1 };
        _laserHead.Effect = new DropShadowEffect { Color = Color.FromRgb(0xEF, 0x44, 0x44), BlurRadius = 20, ShadowDepth = 0, Opacity = 1 };
        LaserSurface.Children.Add(_laserSegments);
        LaserSurface.Children.Add(_laserHead);

        foreach (var element in new UIElement[] { _guideH, _guideV, _snapRing, _snapDot, _dimensionLabel })
        {
            element.Visibility = Visibility.Collapsed;
            GuideSurface.Children.Add(element);
        }

        StatusContent.Children.Add(_statusDot);
        _statusText.Margin = new Thickness(8, 0, 0, 0);
        StatusContent.Children.Add(_statusText);
        foreach (var badge in new[] { _badgeSelected, _badgePalm, _badgeEraser, _badgeSnap, _badgeLaser, _badgeSpotlight, _badgeSmartShape })
            StatusContent.Children.Add(badge);
    }

    /// <summary>Re-applies canvas options: board, spotlight, curtain, guides and the status pill.</summary>
    public void RefreshOptions()
    {
        UpdateBoardSurface();
        var spotlight = _settings.Tool == ToolKind.Spotlight || (_settings.SimultaneousSpotlight && _settings.Tool != ToolKind.Cursor);
        SpotlightSurface.Visibility = spotlight && _pointerInside ? Visibility.Visible : Visibility.Collapsed;
        if (spotlight) UpdateSpotlight(_pointer);

        var curtainHeight = ActualHeight * Math.Clamp(_settings.CurtainProgress, 0, 100) / 100;
        CurtainSurface.Height = Math.Max(0, curtainHeight);
        CurtainSurface.Visibility = curtainHeight > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (curtainHeight > 0 && !IsVisible) Show();

        UpdateGuides(_pointer);
        UpdateStatusPill();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        _pointerInside = true;
        var raw = e.GetPosition(InputRoot);
        _pointer = raw;
        if (e.StylusDevice is null) { _pointerKind = PointerKind.Mouse; }
        var overUi = IsOverToolbar(PointToScreen(e.GetPosition(this)));

        var laserActive = _settings.Tool == ToolKind.Laser || (_settings.SimultaneousLaser && _settings.Tool != ToolKind.Cursor);
        if (laserActive && !overUi) AddLaserPoint(_settings.SnapToGrid ? SnapPoint(raw) : raw);
        else if (overUi) _laserHead.Visibility = Visibility.Collapsed;

        var spotlight = _settings.Tool == ToolKind.Spotlight || (_settings.SimultaneousSpotlight && _settings.Tool != ToolKind.Cursor);
        if (spotlight)
        {
            SpotlightSurface.Visibility = Visibility.Visible;
            UpdateSpotlight(raw);
        }
        UpdateGuides(raw);
        UpdateStatusPill();
    }

    private void HidePointerEffects()
    {
        _pointerInside = false;
        SpotlightSurface.Visibility = Visibility.Collapsed;
        _laserHead.Visibility = Visibility.Collapsed;
        foreach (var element in new UIElement[] { _guideH, _guideV, _snapRing, _snapDot, _dimensionLabel }) element.Visibility = Visibility.Collapsed;
    }

    // ---------------------------------------------------------------- Spotlight

    private void UpdateSpotlight(Point point)
    {
        var radius = _settings.SpotlightRadius;
        var alpha = (byte)Math.Clamp(_settings.SpotlightOverlayOpacity * 255, 0, 255);
        SpotlightPath.Fill = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
        var outer = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
        var hole = new EllipseGeometry(point, radius, radius);
        SpotlightPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude, outer, hole);
        SpotlightRing.Width = SpotlightRing.Height = radius * 2;
        Canvas.SetLeft(SpotlightRing, point.X - radius);
        Canvas.SetTop(SpotlightRing, point.Y - radius);
    }

    // ---------------------------------------------------------------- Laser trail

    private void AddLaserPoint(Point point)
    {
        _laserTrail.Add((point, Environment.TickCount64));
        if (_laserTrail.Count > 26) _laserTrail.RemoveRange(0, _laserTrail.Count - 26);
        _laserHead.Visibility = Visibility.Visible;
        Canvas.SetLeft(_laserHead, point.X - 7);
        Canvas.SetTop(_laserHead, point.Y - 7);
        if (!_laserRendering)
        {
            _laserRendering = true;
            CompositionTarget.Rendering += OnLaserFrame;
        }
    }

    private void OnLaserFrame(object? sender, EventArgs e)
    {
        var now = Environment.TickCount64;
        _laserTrail.RemoveAll(p => now - p.Time >= LaserDecayMs);
        _laserSegments.Children.Clear();
        var color = _settings.Tool == ToolKind.Laser ? _settings.Color : Color.FromRgb(0xEF, 0x44, 0x44);
        for (var i = 1; i < _laserTrail.Count; i++)
        {
            var alpha = Math.Max(0, 1 - (now - _laserTrail[i].Time) / LaserDecayMs);
            _laserSegments.Children.Add(new Line
            {
                X1 = _laserTrail[i - 1].Point.X, Y1 = _laserTrail[i - 1].Point.Y,
                X2 = _laserTrail[i].Point.X, Y2 = _laserTrail[i].Point.Y,
                Stroke = new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), color.R, color.G, color.B)),
                StrokeThickness = Math.Max(0.5, 6 * alpha),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            });
        }
        if (_laserTrail.Count == 0)
        {
            CompositionTarget.Rendering -= OnLaserFrame;
            _laserRendering = false;
            if (_settings.Tool != ToolKind.Laser && !_settings.SimultaneousLaser) _laserHead.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearLaserTrail()
    {
        _laserTrail.Clear();
        _laserSegments.Children.Clear();
        _laserHead.Visibility = Visibility.Collapsed;
        if (_laserRendering)
        {
            CompositionTarget.Rendering -= OnLaserFrame;
            _laserRendering = false;
        }
    }

    // ---------------------------------------------------------------- Snap guides

    private void UpdateGuides(Point raw)
    {
        var active = _settings.SnapToGrid && _settings.Tool != ToolKind.Cursor && _pointerInside;
        if (!active)
        {
            foreach (var element in new UIElement[] { _guideH, _guideV, _snapRing, _snapDot, _dimensionLabel }) element.Visibility = Visibility.Collapsed;
            return;
        }
        var snapped = SnapPoint(raw);
        var drawing = _dragAnchor is not null || _mouseStroke is not null;
        var showLines = _settings.ShowGridGuides || drawing;
        _guideH.X1 = 0; _guideH.X2 = ActualWidth; _guideH.Y1 = _guideH.Y2 = snapped.Y;
        _guideV.Y1 = 0; _guideV.Y2 = ActualHeight; _guideV.X1 = _guideV.X2 = snapped.X;
        _guideH.Visibility = _guideV.Visibility = showLines ? Visibility.Visible : Visibility.Collapsed;
        Canvas.SetLeft(_snapRing, snapped.X - 5); Canvas.SetTop(_snapRing, snapped.Y - 5);
        Canvas.SetLeft(_snapDot, snapped.X - 2); Canvas.SetTop(_snapDot, snapped.Y - 2);
        _snapRing.Visibility = _snapDot.Visibility = Visibility.Visible;
        if (_dragAnchor is not null && _settings.Tool == ToolKind.Shape)
        {
            _dimensionLabel.Text = $"{_shapeDimensions.Width:0} × {_shapeDimensions.Height:0} px";
            Canvas.SetLeft(_dimensionLabel, snapped.X + 8);
            Canvas.SetTop(_dimensionLabel, snapped.Y - 20);
            _dimensionLabel.Visibility = Visibility.Visible;
        }
        else _dimensionLabel.Visibility = Visibility.Collapsed;
    }

    // ---------------------------------------------------------------- Status pill

    private void UpdateStatusPill()
    {
        var visible = _settings.ShowStatusPill && _settings.Tool != ToolKind.Cursor;
        StatusPill.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (!visible) return;
        var (dot, text) = _pointerKind switch
        {
            PointerKind.Pen => (Color.FromRgb(0x60, 0xA5, 0xFA), $"Windows Ink: Stylus Pen · Pressure: {Math.Round(_pressure * 100)}% · Tilt: {Math.Round(_tilt)}°"),
            PointerKind.Touch => (Color.FromRgb(0x34, 0xD3, 0x99), $"Windows Ink: Multi-Touch Digitizer · {Math.Max(1, _touchContacts.Count)} Contact(s)"),
            _ => (Color.FromRgb(0x64, 0x74, 0x8B), "Windows Ink: Ready")
        };
        _statusDot.Fill = new SolidColorBrush(dot);
        _statusText.Text = text;
        _badgeSelected.Visibility = _selection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_selection.Count > 0) SetBadgeText(_badgeSelected, $"{_selection.Count} Selected");
        _badgePalm.Visibility = _penDown ? Visibility.Visible : Visibility.Collapsed;
        _badgeEraser.Visibility = _eraserTip ? Visibility.Visible : Visibility.Collapsed;
        _badgeSnap.Visibility = _settings.SnapToGrid ? Visibility.Visible : Visibility.Collapsed;
        if (_settings.SnapToGrid)
        {
            var s = SnapPoint(_pointer);
            SetBadgeText(_badgeSnap, $"Snap: {_settings.GridSize}px  [{s.X:0}, {s.Y:0}]");
        }
        _badgeLaser.Visibility = _settings.SimultaneousLaser ? Visibility.Visible : Visibility.Collapsed;
        _badgeSpotlight.Visibility = _settings.SimultaneousSpotlight ? Visibility.Visible : Visibility.Collapsed;
        _badgeSmartShape.Visibility = _settings.AutoShapeAssist ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------------------------------------------------------------- Stylus & touch

    private void InitializeInputDevices()
    {
        InputRoot.PreviewStylusDown += (_, e) =>
        {
            var type = e.StylusDevice.TabletDevice?.Type;
            if (type == TabletDeviceType.Touch)
            {
                // Palm rejection: ignore touch contacts while the pen is on the screen.
                if (_penDown) { e.Handled = true; return; }
                _pointerKind = PointerKind.Touch;
            }
            else
            {
                _penDown = true;
                _pointerKind = PointerKind.Pen;
                _eraserTip = e.StylusDevice.Inverted;
            }
            UpdateStatusPill();
        };
        InputRoot.PreviewStylusMove += (_, e) =>
        {
            if (e.StylusDevice.TabletDevice?.Type == TabletDeviceType.Touch)
            {
                if (_penDown) e.Handled = true;
                return;
            }
            _pointerKind = PointerKind.Pen;
            _eraserTip = e.StylusDevice.Inverted;
            var points = e.GetStylusPoints(InputRoot);
            if (points.Count > 0)
            {
                var last = points[^1];
                _pressure = last.PressureFactor;
                if (last.HasProperty(StylusPointProperties.XTiltOrientation))
                    _tilt = last.GetPropertyValue(StylusPointProperties.XTiltOrientation) / 100d;
            }
            UpdateStatusPill();
        };
        InputRoot.PreviewStylusUp += (_, e) =>
        {
            if (e.StylusDevice.TabletDevice?.Type != TabletDeviceType.Touch)
            {
                _penDown = false;
                _eraserTip = false;
                UpdateStatusPill();
            }
        };
        InputRoot.PreviewStylusInRange += (_, e) =>
        {
            if (e.StylusDevice.TabletDevice?.Type != TabletDeviceType.Touch) { _pointerKind = PointerKind.Pen; UpdateStatusPill(); }
        };

        InputRoot.TouchDown += (_, e) =>
        {
            if (_penDown) return;
            var down = e.GetTouchPoint(InputRoot).Position;
            _touchContacts[e.TouchDevice.Id] = down;
            if (_tapStart.Count == 0) { _tapBegan = DateTime.UtcNow; _tapMaxContacts = 0; _tapSpoiled = false; }
            _tapStart[e.TouchDevice.Id] = down;
            _tapMaxContacts = Math.Max(_tapMaxContacts, _tapStart.Count);
            _pointerKind = PointerKind.Touch;
            if (_touchContacts.Count == 2)
            {
                var pts = _touchContacts.Values.ToArray();
                _pinchStartDistance = Math.Max(1, (pts[0] - pts[1]).Length);
                _pinchStartZoom = _settings.ZoomFactor;
                _pinchActive = true;
                CancelMouseStroke();
            }
            UpdateStatusPill();
        };
        InputRoot.TouchMove += (_, e) =>
        {
            if (!_touchContacts.ContainsKey(e.TouchDevice.Id)) return;
            var moved = e.GetTouchPoint(InputRoot).Position;
            _touchContacts[e.TouchDevice.Id] = moved;
            if (_tapStart.TryGetValue(e.TouchDevice.Id, out var start) && (moved - start).Length > 18) _tapSpoiled = true;
            if (_pinchActive && _touchContacts.Count == 2)
            {
                var pts = _touchContacts.Values.ToArray();
                var scale = (pts[0] - pts[1]).Length / _pinchStartDistance;
                // Only a real pinch zooms; small wobbles during a two-finger tap do not.
                if (Math.Abs(scale - 1) < 0.12) return;
                _tapSpoiled = true;
                var factor = Math.Round(Math.Clamp(_pinchStartZoom * scale, 1, 16), 1);
                _manager?.RequestPinchZoom(factor);
            }
        };
        EventHandler<TouchEventArgs> touchEnded = (_, e) =>
        {
            _touchContacts.Remove(e.TouchDevice.Id);
            if (_touchContacts.Count < 2) _pinchActive = false;
            _tapStart.Remove(e.TouchDevice.Id);
            if (_tapStart.Count == 0 && !_tapSpoiled && _tapMaxContacts >= 2 && DateTime.UtcNow - _tapBegan < TimeSpan.FromMilliseconds(400))
            {
                if (_tapMaxContacts == 2) { _manager?.Undo(); UI.Toast.Show("Undo (two-finger tap)"); }
                else { _manager?.Redo(); UI.Toast.Show("Redo (three-finger tap)"); }
                _tapSpoiled = true;
            }
            UpdateStatusPill();
        };
        InputRoot.TouchUp += touchEnded;
        InputRoot.LostTouchCapture += touchEnded;
    }
}
