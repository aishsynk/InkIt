using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using ScreenCanvas.Interop;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace ScreenCanvas.Presentation;

public sealed class PointerEffectsService : IDisposable
{
    private PointerEffectsWindow? _window;

    public bool IsVisible => _window?.IsVisible == true;

    public void Show(PointerEffectsOptions? options = null)
    {
        Hide();
        _window = new PointerEffectsWindow(options ?? new PointerEffectsOptions());
        _window.Closed += OnWindowClosed;
        _window.Show();
    }

    public void UpdatePointer(Point screenPoint) => _window?.UpdatePointer(screenPoint);
    public void Pulse(Point screenPoint) => _window?.Pulse(screenPoint);

    public void Hide()
    {
        if (_window is not null)
        {
            _window.Closed -= OnWindowClosed;
            _window.Close();
        }
        _window = null;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(_window, sender)) _window = null;
    }

    public void Dispose() => Hide();
}

internal sealed class PointerEffectsWindow : Window
{
    private readonly PointerEffectsOptions _options;
    private readonly Canvas _surface = new() { Background = Brushes.Transparent, IsHitTestVisible = false };
    private readonly Path _spotlight = new() { IsHitTestVisible = false };
    private readonly Ellipse _halo;
    private readonly Ellipse _dot;
    private readonly List<(Ellipse Visual, DateTimeOffset Created)> _trail = [];
    private readonly DispatcherTimer _timer;
    private Point? _lastTrailPoint;
    private DateTimeOffset _lastTrailAt;
    private bool _leftButtonDown;
    private bool _escapeDown;

    internal PointerEffectsWindow(PointerEffectsOptions options)
    {
        _options = options;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        Topmost = true;
        Content = _surface;

        _spotlight.Fill = new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(options.SpotlightDimOpacity, 0, 1) * 255), 0, 0, 0));
        _spotlight.Visibility = options.Spotlight ? Visibility.Visible : Visibility.Collapsed;
        _surface.Children.Add(_spotlight);
        _halo = NewCircle(options.HaloRadius * 2, Brushes.Transparent, new SolidColorBrush(options.AccentColor), 3);
        _halo.Visibility = options.CursorHalo ? Visibility.Visible : Visibility.Collapsed;
        _surface.Children.Add(_halo);
        _dot = NewCircle(options.PointerRadius * 2, new SolidColorBrush(options.AccentColor), null, 0);
        _dot.Visibility = options.LaserTrail ? Visibility.Visible : Visibility.Collapsed;
        _surface.Children.Add(_dot);

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
        Closed += OnClosed;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLong(handle, NativeMethods.GwlExStyle,
            style | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate | NativeMethods.WsExTransparent);
    }

    internal void UpdatePointer(Point screenPoint)
    {
        var point = new Point(screenPoint.X - Left, screenPoint.Y - Top);
        UpdatePointerLocal(point);
    }

    private void UpdatePointerLocal(Point point)
    {
        Position(_dot, point);
        Position(_halo, point);
        if (_options.Spotlight)
        {
            var outer = new RectangleGeometry(new Rect(0, 0, Width, Height));
            var hole = new EllipseGeometry(point, _options.SpotlightRadius, _options.SpotlightRadius);
            _spotlight.Data = new CombinedGeometry(GeometryCombineMode.Exclude, outer, hole);
        }
        if (!_options.LaserTrail || !ShouldAddTrail(point)) return;
        var trailDot = NewCircle(_options.PointerRadius * 1.4, new SolidColorBrush(_options.AccentColor), null, 0);
        Position(trailDot, point);
        _surface.Children.Insert(Math.Max(1, _surface.Children.Count - 2), trailDot);
        _trail.Add((trailDot, DateTimeOffset.UtcNow));
        _lastTrailPoint = point;
        _lastTrailAt = DateTimeOffset.UtcNow;
        while (_trail.Count > 96)
        {
            _surface.Children.Remove(_trail[0].Visual);
            _trail.RemoveAt(0);
        }
    }

    private bool ShouldAddTrail(Point point)
    {
        if (_lastTrailPoint is null) return true;
        var delta = point - _lastTrailPoint.Value;
        return delta.LengthSquared >= 16 || DateTimeOffset.UtcNow - _lastTrailAt >= TimeSpan.FromMilliseconds(80);
    }

    internal void Pulse(Point screenPoint)
    {
        if (!_options.ClickPulse) return;
        var point = new Point(screenPoint.X - Left, screenPoint.Y - Top);
        var pulse = NewCircle(_options.HaloRadius * 2, Brushes.Transparent, new SolidColorBrush(_options.AccentColor), 4);
        pulse.Tag = DateTimeOffset.UtcNow;
        Position(pulse, point);
        _surface.Children.Add(pulse);
    }

    private void Animate()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in _trail.ToArray())
        {
            var progress = (now - item.Created).TotalMilliseconds / Math.Max(1, _options.TrailLifetime.TotalMilliseconds);
            item.Visual.Opacity = Math.Max(0, 1 - progress);
            if (progress < 1) continue;
            _surface.Children.Remove(item.Visual);
            _trail.Remove(item);
        }
        foreach (var pulse in _surface.Children.OfType<Ellipse>().Where(e => e.Tag is DateTimeOffset).ToArray())
        {
            var progress = (now - (DateTimeOffset)pulse.Tag).TotalMilliseconds / 450;
            pulse.RenderTransformOrigin = new Point(.5, .5);
            pulse.RenderTransform = new ScaleTransform(1 + progress * 1.6, 1 + progress * 1.6);
            pulse.Opacity = Math.Max(0, 1 - progress);
            if (progress >= 1) _surface.Children.Remove(pulse);
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (GetCursorPos(out var cursor))
            UpdatePointerLocal(PointFromScreen(new Point(cursor.X, cursor.Y)));

        var leftDown = (GetAsyncKeyState(0x01) & 0x8000) != 0;
        if (leftDown && !_leftButtonDown && GetCursorPos(out cursor))
            Pulse(PointFromScreen(new Point(cursor.X, cursor.Y)) + new Vector(Left, Top));
        _leftButtonDown = leftDown;

        var escapeDown = (GetAsyncKeyState(0x1B) & 0x8000) != 0;
        if (escapeDown && !_escapeDown) Close();
        _escapeDown = escapeDown;
        Animate();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        Closed -= OnClosed;
        _trail.Clear();
        _surface.Children.Clear();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private static Ellipse NewCircle(double size, Brush fill, Brush? stroke, double thickness) => new()
        { Width = size, Height = size, Fill = fill, Stroke = stroke, StrokeThickness = thickness, IsHitTestVisible = false };

    private static void Position(FrameworkElement element, Point point)
    {
        Canvas.SetLeft(element, point.X - element.Width / 2);
        Canvas.SetTop(element, point.Y - element.Height / 2);
    }
}
