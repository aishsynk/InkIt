using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ScreenCanvas.UI;

/// <summary>Lets the user click any window on screen and returns its top-level handle (0 if cancelled).</summary>
public static class WindowPicker
{
    public static nint Pick(string hint)
    {
        System.Drawing.Point? clicked = null;
        var picker = new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true,
            Left = SystemParameters.VirtualScreenLeft, Top = SystemParameters.VirtualScreenTop,
            Width = SystemParameters.VirtualScreenWidth, Height = SystemParameters.VirtualScreenHeight,
            Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), Cursor = Cursors.Hand,
            Content = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 110, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 8, 16, 8),
                Child = new TextBlock { Text = hint, Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.SemiBold }
            }
        };
        picker.MouseLeftButtonDown += (_, _) => { clicked = System.Windows.Forms.Cursor.Position; picker.Close(); };
        picker.KeyDown += (_, e) => { if (e.Key == Key.Escape) picker.Close(); };
        picker.Loaded += (_, _) => { picker.Activate(); picker.Focus(); };
        picker.ShowDialog();
        if (clicked is not { } point) return nint.Zero;
        var window = GetAncestor(WindowFromPoint(point), 2 /*GA_ROOT*/);
        GetWindowThreadProcessId(window, out var pid);
        return pid == Environment.ProcessId ? nint.Zero : window;
    }

    [DllImport("user32.dll")] private static extern nint WindowFromPoint(System.Drawing.Point point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out int processId);
}
