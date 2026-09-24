using ScreenCanvas.Core;
using ScreenCanvas.Displays;
using ScreenCanvas.Settings;
using System.Windows.Threading;
using MediaColor = System.Windows.Media.Color;

namespace ScreenCanvas.Overlay;

public sealed class OverlayManager : IOverlayManager, IDisposable
{
    public event EventHandler? ToolChanged;
    private readonly DisplayManager _displayManager = new();
    private readonly Dictionary<string, OverlayWindow> _windows =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _monitorTimer;
    private readonly ToolProfileStore _profiles;
    public IUiExclusionRegionService? ExclusionService { get; set; }
    public bool IsPointOverUi(System.Windows.Point screenPixelPoint) =>
        ExclusionService?.IsPointOverUi(screenPixelPoint) ?? false;
    public ToolSettings Settings { get; } = new();
    public MediaColor? CurrentBoardColor { get; private set; }
    public bool IsDrawing => Settings.Tool != ToolKind.Cursor;
    
    // Zoom state access (delegated to toolbar/zoom engine via events in practice)
    public double CurrentZoomFactor => 1.0;
    public System.Windows.Point CurrentZoomOrigin => new(0, 0);

    public OverlayManager(AppSettings settings, ISettingsStore store)
    {
        _profiles = new ToolProfileStore(settings, store);
        Settings.SpotlightRadius = settings.Spotlight.Radius;
        Settings.SpotlightOverlayOpacity = settings.Spotlight.OverlayOpacity;
        _monitorTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(75)
        };
        _monitorTimer.Tick += OnMonitorTimerTick;
    }

    public event EventHandler? BoardChanged;
    public event EventHandler? InteractionStarted;
    public void NotifyInteractionStarted() => InteractionStarted?.Invoke(this, EventArgs.Empty);

    private void EnsureOverlays()
    {
        var display = _displayManager.GetDisplayAtCursor();
        if (display is not null) EnsureOverlay(display);
    }

    private void EnsureOverlay(DisplayInfo display)
    {
        if (_windows.ContainsKey(display.DeviceName)) return;

        var window = new OverlayWindow(display, Settings, this);
        _windows.Add(display.DeviceName, window);
        window.InitializeHidden();
        window.SetClickThrough(!IsDrawing);
        EnsureToolbarTopmost();
    }

    public void EnsureToolbarTopmost()
    {
        if (System.Windows.Application.Current?.MainWindow is System.Windows.Window main && main.IsVisible)
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(main).Handle;
            if (handle != nint.Zero)
            {
                ScreenCanvas.Interop.NativeMethods.SetWindowPos(handle, ScreenCanvas.Interop.NativeMethods.HwndTopMost, 0, 0, 0, 0,
                    ScreenCanvas.Interop.NativeMethods.SwpNoMove | ScreenCanvas.Interop.NativeMethods.SwpNoSize | ScreenCanvas.Interop.NativeMethods.SwpNoActivate);
            }
        }
    }

    private void OnMonitorTimerTick(object? sender, EventArgs e) => EnsureOverlays();

    public void DeactivateCurrentTool(ToolDeactivationReason reason)
    {
        _profiles.Save(Settings);
        foreach (var window in _windows.Values) window.CancelActiveInteraction();

        if (reason is ToolDeactivationReason.Escape or ToolDeactivationReason.CursorSelected or ToolDeactivationReason.EmergencyRelease)
        {
            if (CurrentBoardColor != null)
            {
                SetBoard(null);
            }
            Settings.Tool = ToolKind.Cursor;
            _monitorTimer.Stop();
            ApplyInputMode();
            foreach (var window in _windows.Values) window.RefreshTool();
            ToolChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ActivateTool(ToolKind newTool)
    {
        if (Settings.Tool != newTool && Settings.Tool != ToolKind.Cursor)
        {
            DeactivateCurrentTool(ToolDeactivationReason.ToolSwitch);
        }
        _profiles.Restore(Settings, newTool, Settings.PenMode, Settings.Shape);
        Settings.Tool = newTool;
        if (newTool != ToolKind.Cursor)
        {
            EnsureOverlays();
            _monitorTimer.Start();
        }
        else
        {
            _monitorTimer.Stop();
        }
        ApplyInputMode();
        foreach (var window in _windows.Values) window.RefreshTool();
        ToolChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetTool(ToolKind tool) => ActivateTool(tool);

    public void SetPenMode(PenMode mode)
    {
        if (Settings.Tool != ToolKind.Cursor)
        {
            DeactivateCurrentTool(ToolDeactivationReason.ToolSwitch);
        }
        Settings.PenMode = mode;
        var tool = mode is PenMode.Highlighter or PenMode.StraightHighlighter ? ToolKind.Highlighter : ToolKind.Pen;
        if (!_profiles.Restore(Settings, tool, mode, Settings.Shape))
        {
            Settings.FadeDuration = mode == PenMode.Disappearing ? TimeSpan.FromSeconds(5) : null;
            (Settings.Thickness, Settings.Opacity, Settings.PressureEnabled) = mode switch
            {
                PenMode.Fountain => (4, (byte)255, true), PenMode.Pencil => (2, (byte)155, true),
                PenMode.Marker => (7, (byte)255, false), PenMode.Brush => (9, (byte)235, true),
                PenMode.Calligraphy => (8, (byte)255, true), PenMode.FeltTip => (6, (byte)235, false),
                PenMode.Highlighter or PenMode.StraightHighlighter => (18, (byte)115, false),
                PenMode.Glow => (6, (byte)220, false), PenMode.Dashed => (4, (byte)255, false),
                PenMode.Dotted => (5, (byte)255, false), PenMode.Pressure => (7, (byte)255, true),
                _ => (4, (byte)255, false)
            };
        }
        Settings.Tool = tool;
        EnsureOverlays();
        _monitorTimer.Start();
        ApplyInputMode();
        foreach (var window in _windows.Values) window.RefreshTool();
        ToolChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetShape(ShapeKind shape)
    {
        if (Settings.Tool != ToolKind.Cursor)
        {
            DeactivateCurrentTool(ToolDeactivationReason.ToolSwitch);
        }
        Settings.Shape = shape;
        _profiles.Restore(Settings, ToolKind.Shape, Settings.PenMode, shape);
        Settings.Tool = ToolKind.Shape;
        EnsureOverlays();
        _monitorTimer.Start();
        ApplyInputMode();
        foreach (var window in _windows.Values) window.RefreshTool();
        ToolChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetColor(MediaColor color)
    {
        Settings.Color = color;
        _profiles.Save(Settings);
        foreach (var window in _windows.Values) window.RefreshTool();
    }

    public void SetThickness(double thickness)
    {
        Settings.Thickness = thickness;
        _profiles.Save(Settings);
        foreach (var window in _windows.Values) window.RefreshTool();
    }

    public void SetOpacity(byte opacity)
    {
        Settings.Opacity = opacity;
        _profiles.Save(Settings);
        foreach (var window in _windows.Values) window.RefreshTool();
    }

    public void SetFade(TimeSpan? duration) => Settings.FadeDuration = duration;

    public void ToggleDrawing() => SetTool(IsDrawing ? ToolKind.Cursor : ToolKind.Pen);
    public void Undo() { foreach (var window in _windows.Values) window.Undo(); ApplyInputMode(); }
    public void Redo() { foreach (var window in _windows.Values) window.Redo(); ApplyInputMode(); }
    public void Clear() { foreach (var window in _windows.Values) window.ClearInk(); ApplyInputMode(); }
    public void ResetMarkerSequence() => Settings.MarkerNumber = 1;
    public void ConfigureMarkers(bool letters, bool square, int start)
    {
        Settings.LetterMarkers = letters;
        Settings.SquareMarkers = square;
        Settings.MarkerNumber = Math.Max(1, start);
    }
    public void ToggleBoard(bool dark) { SetBoard(CurrentBoardColor == (dark ? MediaColor.FromRgb(24, 24, 27) : MediaColor.FromRgb(255, 255, 255)) ? null : (dark ? MediaColor.FromRgb(24, 24, 27) : MediaColor.FromRgb(255, 255, 255))); }
    public void SetBoard(MediaColor? color)
    {
        CurrentBoardColor = color;
        if (color.HasValue && Settings.Tool == ToolKind.Cursor) SetTool(ToolKind.Pen);
        EnsureOverlays();
        foreach (var window in _windows.Values) window.SetBoard(color);
        BoardChanged?.Invoke(this, EventArgs.Empty);
    }
    public void EmergencyStop() => DeactivateCurrentTool(ToolDeactivationReason.EmergencyRelease);

    private void ApplyInputMode()
    {
        foreach (var window in _windows.Values) window.SetClickThrough(!IsDrawing);
        EnsureToolbarTopmost();
    }

    public IDisposable SuspendAnnotations()
    {
        foreach (var window in _windows.Values)
            window.Visibility = System.Windows.Visibility.Hidden;
        return new AnnotationRestorer(_windows.Values);
    }

    private sealed class AnnotationRestorer : IDisposable
    {
        private readonly ICollection<OverlayWindow> _windows;
        private bool _disposed;
        public AnnotationRestorer(ICollection<OverlayWindow> windows) => _windows = windows;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var w in _windows) w.Visibility = System.Windows.Visibility.Visible;
        }
    }

    public void Dispose()
    {
        _monitorTimer.Stop();
        _monitorTimer.Tick -= OnMonitorTimerTick;
        foreach (var window in _windows.Values) window.Close();
        _windows.Clear();
    }

}
