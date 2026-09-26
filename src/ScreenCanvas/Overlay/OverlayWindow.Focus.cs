using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenCanvas.UI.Theme;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace ScreenCanvas.Overlay;

/// <summary>
/// Focus box: everything outside a chosen area is dimmed. Unlike zoom, the area stays live - videos play and
/// the mouse keeps working in the apps below (the overlay stays click-through in Cursor mode).
/// </summary>
public partial class OverlayWindow
{
    private Canvas? _focusLayer;
    private Rect? _focusRect;

    public bool IsFocusBoxActive => _focusRect is not null;

    /// <summary>Shows the focus box for an area given in screen pixels.</summary>
    public void ShowFocusBox(System.Drawing.Rectangle pixels)
    {
        var topLeft = PointFromScreen(new Point(pixels.Left, pixels.Top));
        var bottomRight = PointFromScreen(new Point(pixels.Right, pixels.Bottom));
        _focusRect = new Rect(topLeft, bottomRight);
        EnsureFocusLayer();
        RenderFocusBox();
        if (!IsVisible) Show();
    }

    public void HideFocusBox()
    {
        if (_focusRect is null) return;
        _focusRect = null;
        if (_focusLayer is not null) _focusLayer.Visibility = Visibility.Collapsed;
        if (_settings.Tool == Core.ToolKind.Cursor && !HasVisibleContent) Hide();
    }

    private void EnsureFocusLayer()
    {
        if (_focusLayer is not null) return;
        // Above the board and zoom picture, below the ink, so drawings on the dimmed part stay visible.
        _focusLayer = new Canvas { IsHitTestVisible = false };
        InputRoot.Children.Insert(InputRoot.Children.IndexOf(InkSurface), _focusLayer);
        SizeChanged += (_, _) => RenderFocusBox();
    }

    private void RenderFocusBox()
    {
        if (_focusLayer is null || _focusRect is not { } focus) return;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        var outside = new CombinedGeometry(GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0, 0, width, height)),
            new RectangleGeometry(focus, 10, 10));
        _focusLayer.Children.Clear();
        _focusLayer.Children.Add(new Path { Data = outside, Fill = new SolidColorBrush(Color.FromArgb(0xC0, 0x05, 0x08, 0x0E)) });
        var frame = new Rectangle
        {
            Width = focus.Width + 6, Height = focus.Height + 6, RadiusX = 12, RadiusY = 12,
            Stroke = Tw.B(Tw.Amber400), StrokeThickness = 3,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Tw.Amber500, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.8 }
        };
        Canvas.SetLeft(frame, focus.X - 3);
        Canvas.SetTop(frame, focus.Y - 3);
        _focusLayer.Children.Add(frame);
        _focusLayer.Visibility = Visibility.Visible;
    }
}
