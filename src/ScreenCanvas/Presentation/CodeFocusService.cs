using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenCanvas.Interop;
using Point = System.Windows.Point;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;

namespace ScreenCanvas.Presentation;

public sealed class CodeFocusService : IDisposable
{
    private CodeFocusWindow? _window;
    public bool IsVisible => _window?.IsVisible == true;

    public void Toggle(double bandHeight = 90, double opacity = 0.65)
    {
        if (IsVisible) Hide();
        else Show(bandHeight, opacity);
    }

    public void Show(double bandHeight = 90, double opacity = 0.65)
    {
        Hide();
        _window = new CodeFocusWindow(bandHeight, opacity);
        _window.Closed += (_, _) => _window = null;
        _window.Show();
    }

    public void Hide()
    {
        if (_window is not null)
        {
            _window.Close();
            _window = null;
        }
    }

    public void Dispose() => Hide();
}

internal sealed class CodeFocusWindow : Window
{
    private double _bandHeight;
    private double _bandCenterY;
    private readonly double _dimOpacity;
    private readonly Canvas _canvas = new() { Background = Brushes.Transparent };
    private readonly Path _dimMask = new();
    private readonly System.Windows.Shapes.Rectangle _topBorder = new() { Fill = new SolidColorBrush(Color.FromArgb(180, 37, 99, 235)), Height = 2 };
    private readonly System.Windows.Shapes.Rectangle _bottomBorder = new() { Fill = new SolidColorBrush(Color.FromArgb(180, 37, 99, 235)), Height = 2 };
    private bool _isDragging;
    private Point _lastMouse;

    internal CodeFocusWindow(double bandHeight, double dimOpacity)
    {
        _bandHeight = bandHeight;
        _dimOpacity = dimOpacity;

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.SizeNS;

        _dimMask.Fill = new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(_dimOpacity, 0, 1) * 255), 0, 0, 0));
        _canvas.Children.Add(_dimMask);
        _canvas.Children.Add(_topBorder);
        _canvas.Children.Add(_bottomBorder);
        Content = _canvas;

        _bandCenterY = Height / 2;
        UpdateMask();

        MouseMove += OnMouseMove;
        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseWheel += OnMouseWheel;
        KeyDown += OnKeyDown;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLong(handle, NativeMethods.GwlExStyle,
            style | NativeMethods.WsExToolWindow);
    }

    private void UpdateMask()
    {
        var top = Math.Max(0, _bandCenterY - _bandHeight / 2);
        var bottom = Math.Min(Height, _bandCenterY + _bandHeight / 2);

        var outer = new RectangleGeometry(new Rect(0, 0, Width, Height));
        var slot = new RectangleGeometry(new Rect(0, top, Width, Math.Max(0, bottom - top)));
        _dimMask.Data = new CombinedGeometry(GeometryCombineMode.Exclude, outer, slot);

        Canvas.SetLeft(_topBorder, 0);
        Canvas.SetTop(_topBorder, top - 1);
        _topBorder.Width = Width;

        Canvas.SetLeft(_bottomBorder, 0);
        Canvas.SetTop(_bottomBorder, bottom - 1);
        _bottomBorder.Width = Width;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDragging = true;
            _lastMouse = e.GetPosition(this);
            CaptureMouse();
        }
        else if (e.RightButton == MouseButtonState.Pressed)
        {
            Close();
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (_isDragging)
        {
            var dy = pos.Y - _lastMouse.Y;
            _bandCenterY = Math.Clamp(_bandCenterY + dy, 0, Height);
            _lastMouse = pos;
            UpdateMask();
        }
        else if (!_isDragging && e.LeftButton != MouseButtonState.Pressed)
        {
            // Follow cursor smoothly if not explicitly dragging
            _bandCenterY = pos.Y;
            UpdateMask();
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ReleaseMouseCapture();
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _bandHeight = Math.Clamp(_bandHeight + (e.Delta > 0 ? 16 : -16), 32, Height / 2);
        UpdateMask();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Q)
        {
            Close();
            e.Handled = true;
        }
    }
}
