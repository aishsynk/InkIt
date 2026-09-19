using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;

namespace ScreenCanvas.Presentation;

/// <summary>Edits a script, then safely types it into the window that was active before the editor opened.</summary>
public sealed class DemoTypeService : IDisposable
{
    private DemoTypeWindow? _window;
    public bool IsRunning => _window?.IsRunning == true;
    public bool IsPaused => _window?.IsPaused == true;

    public void Open(string text = "", int charactersPerSecond = 18)
    {
        EmergencyStop();
        var target = GetForegroundWindow();
        _window = new DemoTypeWindow(target, text, charactersPerSecond);
        _window.Closed += (_, _) => _window = null;
        _window.Show();
        _window.Activate();
    }

    public void Start() => _window?.StartTyping();
    public void Pause() => _window?.Pause();
    public void Resume() => _window?.Resume();
    public void Reset() => _window?.Reset();
    public void EmergencyStop() { _window?.Close(); _window = null; }
    public void Dispose() => EmergencyStop();

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}

internal sealed class DemoTypeWindow : EmergencySafeWindow
{
    private readonly nint _targetWindow;
    private readonly TextBox _editor;
    private readonly Slider _speed;
    private readonly DispatcherTimer _timer;
    private string _script = string.Empty;
    private int _position;

    internal bool IsRunning => _timer.IsEnabled;
    internal bool IsPaused { get; private set; }

    internal DemoTypeWindow(nint targetWindow, string text, int charactersPerSecond)
    {
        _targetWindow = targetWindow;
        Width = 620;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(24, 27, 36));
        Title = "DemoType";

        _editor = new TextBox
        {
            Text = text,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Cascadia Mono"),
            FontSize = 17,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(14, 17, 24)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12)
        };
        _speed = new Slider { Minimum = 1, Maximum = 60, Value = Math.Clamp(charactersPerSecond, 1, 60), Width = 180 };
        var start = MakeButton("Start", (_, _) => StartTyping());
        var pause = MakeButton("Pause", (_, _) => Pause());
        var resume = MakeButton("Resume", (_, _) => Resume());
        var reset = MakeButton("Reset", (_, _) => Reset());
        var stop = MakeButton("Emergency stop", (_, _) => Close());
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        controls.Children.Add(new TextBlock { Text = "Speed", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        controls.Children.Add(_speed);
        foreach (var button in new[] { start, pause, resume, reset, stop }) controls.Children.Add(button);
        Content = new DockPanel
        {
            Margin = new Thickness(16),
            LastChildFill = true,
            Children = { controls, _editor }
        };
        DockPanel.SetDock(controls, Dock.Bottom);

        _timer = new DispatcherTimer(DispatcherPriority.Input);
        _timer.Tick += OnTimerTick;
        Closed += OnClosed;
    }

    internal void StartTyping()
    {
        _script = _editor.Text;
        _position = 0;
        IsPaused = false;
        if (_script.Length == 0) return;
        BeginOutput();
    }

    internal void Pause()
    {
        if (!_timer.IsEnabled) return;
        _timer.Stop();
        IsPaused = true;
    }

    internal void Resume()
    {
        if (!IsPaused || _position >= _script.Length) return;
        IsPaused = false;
        BeginOutput();
    }

    internal void Reset()
    {
        _timer.Stop();
        _position = 0;
        IsPaused = false;
        Show();
        Activate();
        _editor.Focus();
    }

    private void BeginOutput()
    {
        Hide();
        if (_targetWindow != nint.Zero) SetForegroundWindow(_targetWindow);
        _timer.Interval = TimeSpan.FromMilliseconds(1000 / Math.Max(1, _speed.Value));
        _timer.Start();
    }

    private void TypeNext()
    {
        // This window is hidden while emitting text, so routed key events cannot
        // reach it. Poll Escape on the timer to preserve the safety exit.
        if ((GetAsyncKeyState(0x1B) & 0x8000) != 0) { Close(); return; }
        if (_position >= _script.Length) { _timer.Stop(); return; }
        SendUnicode(_script[_position++]);
    }

    private void OnTimerTick(object? sender, EventArgs e) => TypeNext();

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        Closed -= OnClosed;
    }

    private static Button MakeButton(string text, RoutedEventHandler click)
    {
        var button = new Button { Content = text, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(10, 5, 10, 5) };
        button.Click += click;
        return button;
    }

    private static void SendUnicode(char character)
    {
        var inputs = new[]
        {
            new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Scan = character, Flags = 0x0004 } } },
            new Input { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Scan = character, Flags = 0x0004 | 0x0002 } } }
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey; public ushort Scan; public uint Flags; public uint Time; public nint ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
}
