using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;

namespace ScreenCanvas.Capture;

public sealed class RegionSelectionWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly WpfRectangle _border = new() { Stroke = WpfBrushes.DeepSkyBlue, StrokeThickness = 2, Fill = new SolidColorBrush(WpfColor.FromArgb(24, 0, 170, 255)) };
    private readonly Border _label = new() { Background = new SolidColorBrush(WpfColor.FromArgb(220, 20, 20, 20)), Padding = new System.Windows.Thickness(6, 3, 6, 3), Child = new TextBlock { Foreground = WpfBrushes.White } };
    private Point _start;
    private bool _dragging;

    public CaptureRegion? SelectedRegion { get; private set; }

    public RegionSelectionWindow()
    {
        var virtualScreen = Forms.SystemInformation.VirtualScreen;
        Left = virtualScreen.Left; Top = virtualScreen.Top; Width = virtualScreen.Width; Height = virtualScreen.Height;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
        AllowsTransparency = true; Background = new SolidColorBrush(WpfColor.FromArgb(55, 0, 0, 0)); Cursor = System.Windows.Input.Cursors.Cross;
        Content = _canvas; _canvas.Children.Add(_border); _canvas.Children.Add(_label);
        _border.Visibility = _label.Visibility = Visibility.Collapsed;
        MouseLeftButtonDown += Begin; MouseMove += Move; MouseLeftButtonUp += End;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            CancelSelection();
        };
        Loaded += (_, _) => { Activate(); Focus(); };
    }

    private void CancelSelection()
    {
        _dragging = false;
        SelectedRegion = null;
        _border.Visibility = _label.Visibility = Visibility.Collapsed;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (Stylus.Captured is not null) Stylus.Capture(null);
        DialogResult = false;
        Close();
    }

    private void Begin(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(_canvas); _dragging = true; CaptureMouse();
        _border.Visibility = _label.Visibility = Visibility.Visible; Update(_start);
    }

    private void Move(object sender, System.Windows.Input.MouseEventArgs e) { if (_dragging) Update(e.GetPosition(_canvas)); }

    private void End(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        var end = e.GetPosition(_canvas); Update(end); _dragging = false; ReleaseMouseCapture();
        var x = Math.Min(_start.X, end.X); var y = Math.Min(_start.Y, end.Y);
        var w = Math.Abs(end.X - _start.X); var h = Math.Abs(end.Y - _start.Y);
        if (w < 2 || h < 2) return;
        var topLeft = PointToScreen(new Point(x, y));
        var bottomRight = PointToScreen(new Point(x + w, y + h));
        SelectedRegion = new CaptureRegion(new System.Drawing.Rectangle(
            (int)Math.Round(topLeft.X), (int)Math.Round(topLeft.Y),
            (int)Math.Round(bottomRight.X - topLeft.X), (int)Math.Round(bottomRight.Y - topLeft.Y)));
        DialogResult = true; Close();
    }

    private void Update(Point end)
    {
        var x = Math.Min(_start.X, end.X); var y = Math.Min(_start.Y, end.Y);
        var w = Math.Abs(end.X - _start.X); var h = Math.Abs(end.Y - _start.Y);
        Canvas.SetLeft(_border, x); Canvas.SetTop(_border, y); _border.Width = w; _border.Height = h;
        ((TextBlock)_label.Child).Text = $"{Math.Round(w)} × {Math.Round(h)}";
        Canvas.SetLeft(_label, x); Canvas.SetTop(_label, Math.Max(0, y - 30));
    }
}
