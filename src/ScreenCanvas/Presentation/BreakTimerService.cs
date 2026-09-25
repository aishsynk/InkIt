using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using ScreenCanvas.Interop;
using ScreenCanvas.UI;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Button = System.Windows.Controls.Button;
using Colors = System.Windows.Media.Colors;

namespace ScreenCanvas.Presentation;

/// <summary>Presenter break timer: a floating top-right countdown widget (design: PresenterHUD break timer).</summary>
public sealed class BreakTimerService : IDisposable
{
    private BreakTimerWidget? _window;
    public bool IsRunning => _window?.IsRunning == true;
    public bool IsPaused => _window?.IsPaused == true;
    public TimeSpan Remaining => _window?.Remaining ?? TimeSpan.Zero;
    public event EventHandler? Completed;

    public void Start(TimeSpan duration, int resetMinutes = 5)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        EmergencyStop();
        _window = new BreakTimerWidget(duration, TimeSpan.FromMinutes(resetMinutes));
        _window.Completed += (_, _) => Completed?.Invoke(this, EventArgs.Empty);
        _window.Closed += (_, _) => _window = null;
        _window.Show();
    }

    public void Pause() => _window?.Pause();
    public void Resume() => _window?.Resume();
    public void Reset() => _window?.Reset();
    public void EmergencyStop() { _window?.Close(); _window = null; }
    public void Dispose() => EmergencyStop();
}

internal sealed class BreakTimerWidget : Window, IChromeHost
{
    private readonly TimeSpan _resetDuration;
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly TextBlock _clock;
    private readonly Button _pauseButton;
    private readonly LucideIcon _pauseIcon = new("Pause", 14);
    private readonly Border _card;
    private DateTimeOffset _deadline;

    internal bool IsRunning => _timer.IsEnabled;
    internal bool IsPaused { get; private set; }
    internal TimeSpan Remaining { get; private set; }
    internal event EventHandler? Completed;

    internal BreakTimerWidget(TimeSpan duration, TimeSpan resetDuration)
    {
        _resetDuration = resetDuration;
        Remaining = duration;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Title = "InkIt Break Timer";

        _clock = DK.Text("00:00", 20, "Ink.Text", FontWeights.Bold, mono: true);
        var tile = DK.Surface(new LucideIcon("Timer", 20) { Foreground = Tw.B(Tw.Blue400) }, Tw.B(Tw.Blue500, 0.2), Tw.B(Colors.Transparent), 12, new Thickness(8), 0);
        var labels = DK.V(0, DK.Caps("Break Timer", 10, "Ink.Text400", FontWeights.Normal), _clock);
        ((TextBlock)labels.Children[0]).FontFamily = DK.Mono;

        _pauseButton = DK.Button(_pauseIcon, Tw.B(Colors.Transparent), "Ink.Text300", "Ink.Hover", "Ink.Text", 8, new Thickness(6));
        _pauseButton.ToolTip = "Pause Timer";
        _pauseButton.Click += (_, _) => { if (IsPaused) Resume(); else Pause(); };
        var reset = DK.IconButton("RotateCcw", 14, "Ink.Text300", "Ink.Text", "Ink.Hover", 6, 8, $"Reset to {resetDuration.TotalMinutes:0} Minutes");
        reset.Click += (_, _) => { Remaining = _resetDuration; _deadline = DateTimeOffset.UtcNow + Remaining; if (!IsPaused) _timer.Start(); Render(); };
        var close = DK.IconButton("X", 14, "Ink.Text400", Tw.B(Tw.Red300), Tw.B(Tw.Red950, 0.6), 6, 8, "Close Timer");
        close.Click += (_, _) => Close();
        var controls = new Border { BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(8, 0, 0, 0), Child = DK.H(4, _pauseButton, reset, close) };
        controls.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");

        _card = DK.Surface(DK.H(12, tile, labels, controls), "Ink.Surface", "Ink.BorderStrong", 16, new Thickness(12));
        _card.Margin = new Thickness(20, 12, 20, 28);
        _card.Effect = DK.Shadow(40, 14, 0.5);
        Content = _card;

        _timer.Tick += (_, _) => Tick();
        Closed += (_, _) => _timer.Stop();
        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 4;
            Top = area.Top + 44;
        };
        _deadline = DateTimeOffset.UtcNow + Remaining;
        _timer.Start();
        Render();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLong(handle, NativeMethods.GwlExStyle, style | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate);
    }

    public bool IsPointOverChrome(System.Windows.Point screenPixelPoint)
    {
        if (!IsVisible || PresentationSource.FromVisual(_card) is null) return false;
        var topLeft = _card.PointToScreen(new System.Windows.Point(0, 0));
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(_card);
        return new Rect(topLeft.X, topLeft.Y, _card.ActualWidth * dpi.DpiScaleX, _card.ActualHeight * dpi.DpiScaleY).Contains(screenPixelPoint);
    }

    internal void Pause()
    {
        if (IsPaused) return;
        Remaining = Max0(_deadline - DateTimeOffset.UtcNow);
        _timer.Stop();
        IsPaused = true;
        Render();
    }

    internal void Resume()
    {
        if (!IsPaused || Remaining <= TimeSpan.Zero) return;
        IsPaused = false;
        _deadline = DateTimeOffset.UtcNow + Remaining;
        _timer.Start();
        Render();
    }

    internal void Reset()
    {
        _timer.Stop();
        Remaining = _resetDuration;
        IsPaused = true;
        Render();
    }

    private void Tick()
    {
        Remaining = Max0(_deadline - DateTimeOffset.UtcNow);
        Render();
        if (Remaining > TimeSpan.Zero) return;
        _timer.Stop();
        Completed?.Invoke(this, EventArgs.Empty);
        Toast.Show("Break is over");
    }

    private void Render()
    {
        var total = Math.Max(0, (int)Math.Ceiling(Remaining.TotalSeconds));
        _clock.Text = $"{total / 60:00}:{total % 60:00}";
        _pauseIcon.Kind = IsPaused ? "Play" : "Pause";
        _pauseButton.ToolTip = IsPaused ? "Resume Timer" : "Pause Timer";
    }

    private static TimeSpan Max0(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
