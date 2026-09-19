using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfImage = System.Windows.Controls.Image;
using WpfBrushes = System.Windows.Media.Brushes;

namespace ScreenCanvas.Webcam;

public sealed class WebcamOverlayWindow : Window
{
    private readonly WpfImage _image = new() { Stretch = Stretch.UniformToFill };
    private WebcamOverlayShape _shape = WebcamOverlayShape.Circle;

    public WebcamOverlayShape Shape
    {
        get => _shape;
        set { _shape = value; UpdateClip(); }
    }

    public WebcamOverlayWindow()
    {
        Width = 260; Height = 200; MinWidth = 120; MinHeight = 90; Topmost = true; ShowInTaskbar = false;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResizeWithGrip; AllowsTransparency = true; Background = WpfBrushes.Transparent;
        Content = new Border { BorderBrush = WpfBrushes.White, BorderThickness = new Thickness(2), Child = _image };
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        SizeChanged += (_, _) => UpdateClip();
    }

    public void Attach(IWebcamService service)
    {
        service.FrameReady += OnFrameReady;
        Closed += (_, _) => service.FrameReady -= OnFrameReady;
    }

    public void SetFrame(BitmapSource frame)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => SetFrame(frame)); return; }
        _image.Source = frame;
    }

    public void ShowOverlay() { if (!IsVisible) Show(); Activate(); }
    public void HideOverlay() => Hide();

    private void OnFrameReady(object? sender, BitmapSource frame) => SetFrame(frame);

    private void UpdateClip()
    {
        var width = Math.Max(0, ActualWidth); var height = Math.Max(0, ActualHeight);
        _image.Clip = Shape switch
        {
            WebcamOverlayShape.Circle => new EllipseGeometry(new Rect(0, 0, width, height)),
            WebcamOverlayShape.RoundedRectangle => new RectangleGeometry(new Rect(0, 0, width, height), 22, 22),
            _ => new RectangleGeometry(new Rect(0, 0, width, height))
        };
    }
}
