using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;

namespace ScreenCanvas.Presentation;

public sealed class CurtainService : IDisposable
{
    private CurtainWindow? _window;
    public bool IsVisible => _window?.IsVisible == true;

    public void Show(CurtainOptions? options = null)
    {
        Hide();
        _window = new CurtainWindow(options ?? new CurtainOptions());
        _window.Closed += (_, _) => _window = null;
        _window.Show();
        _window.Activate();
    }

    public void Hide() { _window?.Close(); _window = null; }
    public void Dispose() => Hide();
}

internal sealed class CurtainWindow : EmergencySafeWindow
{
    internal CurtainWindow(CurtainOptions options)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Background = new SolidColorBrush(options.Color) { Opacity = Math.Clamp(options.Opacity, 0, 1) };
        Content = new TextBlock
        {
            Text = options.Message ?? string.Empty,
            Foreground = Brushes.White,
            FontSize = 32,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 900
        };
    }
}
