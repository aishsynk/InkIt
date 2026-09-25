using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ScreenCanvas.Interop;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Colors = System.Windows.Media.Colors;

namespace ScreenCanvas.UI;

/// <summary>Bottom-centre notification banner (design: #inkit-toast, 2.5 s).</summary>
public static class Toast
{
    private static ToastWindow? _window;

    public static void Show(string message)
    {
        var app = System.Windows.Application.Current;
        if (app is null) return;
        if (!app.Dispatcher.CheckAccess()) { app.Dispatcher.BeginInvoke(() => Show(message)); return; }
        _window ??= new ToastWindow();
        _window.Display(message);
    }

    private sealed class ToastWindow : Window
    {
        private readonly System.Windows.Controls.TextBlock _text;
        private readonly System.Windows.Controls.Border _card;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(2500) };

        public ToastWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            Focusable = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            _text = DK.Text(string.Empty, 12, Tw.B(Colors.White), FontWeights.Medium);
            _text.TextTrimming = TextTrimming.None;
            _card = DK.Surface(DK.H(8, new LucideIcon("Sparkles", 16) { Foreground = Tw.B(Tw.Blue400) }, _text),
                Tw.B(Tw.Slate900, 0.95), Tw.B(Tw.Blue500, 0.6), 12, new Thickness(16, 8, 16, 8));
            _card.Margin = new Thickness(20, 12, 20, 28);
            _card.Effect = DK.Shadow(30, 10, 0.5);
            _card.RenderTransform = new TranslateTransform();
            Content = _card;
            _timer.Tick += (_, _) => { _timer.Stop(); Hide(); };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var handle = new WindowInteropHelper(this).Handle;
            var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
            NativeMethods.SetWindowLong(handle, NativeMethods.GwlExStyle,
                style | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate | NativeMethods.WsExTransparent);
        }

        public void Display(string message)
        {
            _text.Text = message;
            if (!IsVisible) Show();
            UpdateLayout();
            var area = SystemParameters.WorkArea;
            Left = area.Left + (area.Width - ActualWidth) / 2;
            Top = area.Bottom - ActualHeight - 32;
            _card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            _card.RenderTransform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(150)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            _timer.Stop();
            _timer.Start();
        }
    }
}
