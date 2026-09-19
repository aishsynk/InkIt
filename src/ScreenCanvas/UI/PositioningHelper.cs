using System;
using System.Windows;
using System.Windows.Media;

namespace ScreenCanvas.UI;

public static class PositioningHelper
{
    public static (double Left, double Top, double Right, double Bottom) GetScreenBounds(Window window)
    {
        var dpi = VisualTreeHelper.GetDpi(window);
        var p = System.Windows.Forms.Cursor.Position;
        var bounds = System.Windows.Forms.Screen.FromPoint(p).WorkingArea;
        
        return (
            bounds.Left / dpi.DpiScaleX,
            bounds.Top / dpi.DpiScaleY,
            bounds.Right / dpi.DpiScaleX,
            bounds.Bottom / dpi.DpiScaleY
        );
    }

    public static void ClampToScreen(Window window, double visiblePadding = 28)
    {
        var (left, top, right, bottom) = GetScreenBounds(window);
        
        window.Left = Math.Clamp(window.Left, left - window.ActualWidth + visiblePadding, right - visiblePadding);
        window.Top = Math.Clamp(window.Top, top, bottom - visiblePadding);
        
        const double snap = 12;
        if (Math.Abs(window.Left - left) < snap) window.Left = left;
        if (Math.Abs(window.Left + window.ActualWidth - right) < snap) window.Left = right - window.ActualWidth;
        if (Math.Abs(window.Top - top) < snap) window.Top = top;
        if (Math.Abs(window.Top + window.ActualHeight - bottom) < snap) window.Top = bottom - window.ActualHeight;
    }
}
