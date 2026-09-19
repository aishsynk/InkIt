using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace ScreenCanvas.Presentation;

public sealed class BreakTimerService : IDisposable
{
    private BreakTimerWindow? _window;
    public bool IsRunning => _window?.IsRunning == true;
    public bool IsPaused => _window?.IsPaused == true;
    public TimeSpan Remaining => _window?.Remaining ?? TimeSpan.Zero;
    public event EventHandler? Completed;

    public void Start(TimeSpan duration, string? message = null)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        EmergencyStop();
        _window = new BreakTimerWindow(duration, message);
        _window.Completed += (_, _) => Completed?.Invoke(this, EventArgs.Empty);
        _window.Closed += (_, _) => _window = null;
        _window.Show();
        _window.Activate();
        _window.Start();
    }

    public void Pause() => _window?.Pause();
    public void Resume() => _window?.Resume();
    public void Reset() => _window?.Reset();
    public void EmergencyStop() { _window?.Close(); _window = null; }
    public void Dispose() => EmergencyStop();
}

internal sealed class BreakTimerWindow : EmergencySafeWindow
{
    private readonly TimeSpan _duration;
    private readonly DispatcherTimer _timer;
    private readonly TextBlock _clock;
    private DateTimeOffset _deadline;

    internal bool IsRunning => _timer.IsEnabled;
    internal bool IsPaused { get; private set; }
    internal TimeSpan Remaining { get; private set; }
    internal event EventHandler? Completed;

    internal BreakTimerWindow(TimeSpan duration, string? message)
    {
        _duration = duration;
        Remaining = duration;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Background = new SolidColorBrush(Color.FromRgb(14, 17, 24));

        _clock = new TextBlock
        {
            FontSize = 112,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        var messageBlock = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(message) ? "Break time" : message,
            FontSize = 30,
            Foreground = new SolidColorBrush(Color.FromRgb(190, 198, 214)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 900,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var hint = new TextBlock
        {
            Text = "Esc or Ctrl+Shift+F12 to stop",
            Foreground = new SolidColorBrush(Color.FromRgb(125, 135, 155)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Margin = new Thickness(0, 32, 0, 0)
        };
        Content = new StackPanel
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Children = { _clock, messageBlock, hint }
        };
        _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTimerTick;
        Closed += OnClosed;
        RenderTime();
    }

    internal void Start()
    {
        Remaining = _duration;
        IsPaused = false;
        _deadline = DateTimeOffset.UtcNow + Remaining;
        _timer.Start();
        RenderTime();
    }

    internal void Pause()
    {
        if (!_timer.IsEnabled) return;
        Remaining = MaxZero(_deadline - DateTimeOffset.UtcNow);
        _timer.Stop();
        IsPaused = true;
        RenderTime();
    }

    internal void Resume()
    {
        if (!IsPaused || Remaining <= TimeSpan.Zero) return;
        IsPaused = false;
        _deadline = DateTimeOffset.UtcNow + Remaining;
        _timer.Start();
    }

    internal void Reset()
    {
        _timer.Stop();
        Remaining = _duration;
        IsPaused = true;
        RenderTime();
    }

    private void Tick()
    {
        Remaining = MaxZero(_deadline - DateTimeOffset.UtcNow);
        RenderTime();
        if (Remaining > TimeSpan.Zero) return;
        _timer.Stop();
        IsPaused = false;
        Completed?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void RenderTime()
    {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(Remaining.TotalSeconds));
        _clock.Text = $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private static TimeSpan MaxZero(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;

    private void OnTimerTick(object? sender, EventArgs e) => Tick();

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        Closed -= OnClosed;
        Completed = null;
    }
}
