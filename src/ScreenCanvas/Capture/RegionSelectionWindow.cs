using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;

namespace ScreenCanvas.Capture;

public sealed class RegionSelectionWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly WpfRectangle _border = new() { Stroke = WpfBrushes.DeepSkyBlue, StrokeThickness = 2, Fill = new SolidColorBrush(WpfColor.FromArgb(24, 0, 170, 255)) };
    private readonly Border _label = new() { Background = new SolidColorBrush(WpfColor.FromArgb(220, 20, 20, 20)), Padding = new System.Windows.Thickness(6, 3, 6, 3), Child = new TextBlock { Foreground = WpfBrushes.White } };
    private readonly Border _hint;
    private Point _start;
    private bool _dragging;

    public CaptureRegion? SelectedRegion { get; private set; }

    public RegionSelectionWindow(string hint = "Drag to select an area  ·  Esc to cancel")
    {
        // WPF positions windows in DIPs, so use SystemParameters rather than the pixel-based Forms values.
        Left = SystemParameters.VirtualScreenLeft; Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth; Height = SystemParameters.VirtualScreenHeight;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
        AllowsTransparency = true; Background = new SolidColorBrush(WpfColor.FromArgb(55, 0, 0, 0)); Cursor = System.Windows.Input.Cursors.Cross;
        Content = _canvas; _canvas.Children.Add(_border); _canvas.Children.Add(_label);
        _border.Visibility = _label.Visibility = Visibility.Collapsed;
        _hint = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromArgb(235, 15, 23, 42)), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 8, 16, 8), IsHitTestVisible = false,
            Child = new TextBlock { Text = hint, Foreground = WpfBrushes.White, FontSize = 15, FontWeight = FontWeights.SemiBold }
        };
        _canvas.Children.Add(_hint);
        Loaded += (_, _) => PlaceHint();
        MouseLeftButtonDown += Begin; MouseMove += Move; MouseLeftButtonUp += End;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            CancelSelection();
        };
        Loaded += (_, _) => { Activate(); Focus(); };
    }

    /// <summary>Centres the instruction near the top of the monitor under the cursor.</summary>
    private void PlaceHint()
    {
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position).Bounds;
        var topLeft = PointFromScreen(new Point(screen.Left, screen.Top));
        var bottomRight = PointFromScreen(new Point(screen.Right, screen.Bottom));
        _hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(_hint, topLeft.X + (bottomRight.X - topLeft.X - _hint.DesiredSize.Width) / 2);
        Canvas.SetTop(_hint, topLeft.Y + 110);
    }

    /// <summary>Waits until this dimmed window has left the screen so it is not in the snapshot.</summary>
    public static void WaitUntilGone()
    {
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
        _ = DwmFlush();
        _ = DwmFlush();
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

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
        _start = e.GetPosition(_canvas); _dragging = true; CaptureMouse(); _hint.Visibility = Visibility.Collapsed;
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
