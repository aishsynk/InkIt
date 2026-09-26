using ScreenCanvas.Core;
using ScreenCanvas.Displays;
using ScreenCanvas.Settings;
using System.Windows.Threading;
using System.Windows.Media;
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
    private readonly AppSettings _appSettings;
    private readonly ISettingsStore _store;
    private readonly DispatcherTimer _saveTimer;
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
        _appSettings = settings;
        _store = store;
        var p = settings.Presentation;
        var c = settings.Canvas;
        Settings.SpotlightRadius = p.SpotlightRadius;
        Settings.SpotlightOverlayOpacity = p.SpotlightDimOpacity;
        Settings.CodeFocusBandHeight = p.CodeFocusHeight;
        Settings.CodeFocusDimOpacity = p.CodeFocusDimOpacity;
        Settings.ZoomFactor = p.ZoomFactor;
        Settings.ZoomFollowsMouse = p.ZoomFollowsMouse;
        Settings.SnapToGrid = c.SnapToGrid;
        Settings.GridSize = Math.Clamp(c.GridSize, 5, 100);
        Settings.ShowGridGuides = c.ShowGridGuides;
        Settings.SimultaneousLaser = c.SimultaneousLaser;
        Settings.SimultaneousSpotlight = c.SimultaneousSpotlight;
        Settings.AutoShapeAssist = c.AutoShapeAssist;
        Settings.ShowStatusPill = c.ShowStatusPill;
        Settings.IsHorizontalToolbar = settings.Toolbar.Horizontal;
        _monitorTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(settings.Advanced.IdleCpuOptimized ? 200 : 75)
        };
        _monitorTimer.Tick += OnMonitorTimerTick;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _ = _store.SaveAsync(_appSettings); };
    }

    public event EventHandler? OptionsChanged;
    public event EventHandler<double>? PinchZoomRequested;
    public void RequestPinchZoom(double factor) => PinchZoomRequested?.Invoke(this, factor);
    public BoardKind CurrentBoard { get; private set; } = BoardKind.Transparent;

    public void UpdateOptions(Action<ToolSettings> change, bool persist = true)
    {
        change(Settings);
        foreach (var window in _windows.Values) window.RefreshOptions();
        if (persist)
        {
            var p = _appSettings.Presentation;
            var c = _appSettings.Canvas;
            p.SpotlightRadius = Settings.SpotlightRadius;
            p.SpotlightDimOpacity = Settings.SpotlightOverlayOpacity;
            p.CodeFocusHeight = Settings.CodeFocusBandHeight;
            p.CodeFocusDimOpacity = Settings.CodeFocusDimOpacity;
            p.ZoomFactor = Settings.ZoomFactor;
            p.ZoomFollowsMouse = Settings.ZoomFollowsMouse;
            c.SnapToGrid = Settings.SnapToGrid;
            c.GridSize = Settings.GridSize;
            c.ShowGridGuides = Settings.ShowGridGuides;
            c.SimultaneousLaser = Settings.SimultaneousLaser;
            c.SimultaneousSpotlight = Settings.SimultaneousSpotlight;
            c.AutoShapeAssist = Settings.AutoShapeAssist;
            c.ShowStatusPill = Settings.ShowStatusPill;
            _appSettings.Toolbar.Horizontal = Settings.IsHorizontalToolbar;
            _saveTimer.Stop();
            _saveTimer.Start();
        }
        if (Settings.CurtainProgress > 0) EnsureOverlays();
        ApplyInputMode();
        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ------------------------------------------------------------------ Zoom to area

    public event EventHandler? ZoomAreaChanged;
    public event EventHandler? ZoomAreaRequested;
    public bool IsZoomAreaActive { get; private set; }
    public void RequestZoomArea() => ZoomAreaRequested?.Invoke(this, EventArgs.Empty);

    // ------------------------------------------------------------------ Pages and slides

    public event EventHandler? PagesChanged;

    /// <summary>The overlay on the monitor under the mouse (created if needed): pages belong to it.</summary>
    private OverlayWindow ActiveWindow()
    {
        var display = _displayManager.GetDisplayAtCursor() ?? throw new InvalidOperationException("No display found.");
        EnsureOverlay(display);
        return _windows[display.DeviceName];
    }

    public int PageCount => _windows.Count == 0 ? 1 : ActiveWindow().PageCount;
    public int PageIndex => _windows.Count == 0 ? 0 : ActiveWindow().PageIndex;
    public bool IsFollowingSlides => _windows.Values.Any(w => w.IsFollowingSlides);

    private void ChangePage(Action<OverlayWindow> change)
    {
        ExitZoomArea();
        change(ActiveWindow());
        PagesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NextPage() => ChangePage(w => { if (w.PageIndex < w.PageCount - 1) w.GoToPage(w.PageIndex + 1); else w.AddPage(); });
    public void PreviousPage() => ChangePage(w => w.GoToPage(w.PageIndex - 1));
    public void AddPage() => ChangePage(w => w.AddPage());
    public void DeletePage() => ChangePage(w => w.DeleteCurrentPage());

    /// <summary>Every page rendered as a picture on the board colour (white for the plain screen).</summary>
    public IReadOnlyList<System.Windows.Media.Imaging.BitmapSource> RenderPages()
    {
        var window = ActiveWindow();
        var background = window.PageBackground(forExport: true);
        return window.SnapshotPages().Select(p => window.RenderPage(p, background)).ToList();
    }

    public void SavePages(string path) => InkFile.Save(path, ActiveWindow().SnapshotPages());

    public void OpenPages(string path)
    {
        var pages = InkFile.Load(path);
        ChangePage(w => w.ReplacePages(pages));
        if (Settings.Tool == ToolKind.Cursor) SetTool(ToolKind.Pen);
    }

    /// <summary>A PowerPoint slide is showing on the monitor that contains <paramref name="screenPoint"/>.</summary>
    public void ShowSlide(string slideKey, System.Drawing.Point screenPoint)
    {
        var screen = System.Windows.Forms.Screen.FromPoint(screenPoint);
        var display = new DisplayInfo(screen.DeviceName, screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height, screen.Primary);
        EnsureOverlay(display);
        ExitZoomArea();
        _windows[display.DeviceName].ShowSlide(slideKey);
        PagesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void EndSlides()
    {
        foreach (var window in _windows.Values) window.EndSlides();
        PagesChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? FocusBoxChanged;
    public bool IsFocusBoxActive { get; private set; }

    public void ShowFocusBox(System.Drawing.Rectangle pixelBounds)
    {
        var center = new System.Drawing.Point(pixelBounds.Left + pixelBounds.Width / 2, pixelBounds.Top + pixelBounds.Height / 2);
        var screen = System.Windows.Forms.Screen.FromPoint(center);
        var display = new DisplayInfo(screen.DeviceName, screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height, screen.Primary);
        EnsureOverlay(display);
        foreach (var (name, window) in _windows)
        {
            if (name == display.DeviceName) window.ShowFocusBox(pixelBounds);
            else window.HideFocusBox();
        }
        IsFocusBoxActive = true;
        ApplyInputMode();
        FocusBoxChanged?.Invoke(this, EventArgs.Empty);
    }

    public void HideFocusBox()
    {
        if (!IsFocusBoxActive) return;
        IsFocusBoxActive = false;
        foreach (var window in _windows.Values) window.HideFocusBox();
        FocusBoxChanged?.Invoke(this, EventArgs.Empty);
    }
    public event EventHandler? ToolWheelRequested;
    public void RequestToolWheel() => ToolWheelRequested?.Invoke(this, EventArgs.Empty);

    public void ShowZoomArea(System.Windows.Media.ImageSource image, System.Drawing.Rectangle pixelBounds)
    {
        var center = new System.Drawing.Point(pixelBounds.Left + pixelBounds.Width / 2, pixelBounds.Top + pixelBounds.Height / 2);
        var screen = System.Windows.Forms.Screen.FromPoint(center);
        var display = new DisplayInfo(screen.DeviceName, screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height, screen.Primary);
        EnsureOverlay(display);
        foreach (var (name, window) in _windows)
        {
            if (name == display.DeviceName) window.EnterZoomView(image);
            else window.ExitZoomView();
        }
        IsZoomAreaActive = true;
        ApplyInputMode();
        ZoomAreaChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ExitZoomArea()
    {
        if (!IsZoomAreaActive) return;
        IsZoomAreaActive = false;
        foreach (var window in _windows.Values) window.ExitZoomView();
        ZoomAreaChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetBoardKind(BoardKind kind)
    {
        CurrentBoard = kind;
        CurrentBoardColor = kind switch
        {
            BoardKind.Whiteboard => Colors.White,
            BoardKind.Blackboard => BlackboardColor,
            BoardKind.Grid => GridBoardColor,
            _ => null
        };
        if (kind != BoardKind.Transparent && Settings.Tool == ToolKind.Cursor) SetTool(ToolKind.Pen);
        EnsureOverlays();
        foreach (var window in _windows.Values) window.SetBoard(kind);
        BoardChanged?.Invoke(this, EventArgs.Empty);
        OptionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public static readonly MediaColor BlackboardColor = MediaColor.FromRgb(0x0F, 0x14, 0x1C);
    public static readonly MediaColor GridBoardColor = MediaColor.FromRgb(0x0F, 0x17, 0x2A);

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
        window.SetBoard(CurrentBoard);
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
            ExitZoomArea();
            if (reason != ToolDeactivationReason.CursorSelected) HideFocusBox();
            if (CurrentBoard != BoardKind.Transparent)
            {
                SetBoardKind(BoardKind.Transparent);
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
            ExitZoomArea();
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
            Settings.FadeDuration = mode == PenMode.Disappearing ? TimeSpan.FromSeconds(3) : null;
            (Settings.Thickness, Settings.Opacity, Settings.PressureEnabled) = mode switch
            {
                PenMode.Fountain => (4, (byte)255, true), PenMode.Pencil => (2, (byte)155, true),
                PenMode.Marker => (7, (byte)255, false), PenMode.Brush => (9, (byte)235, true),
                PenMode.Calligraphy => (8, (byte)255, true), PenMode.FeltTip => (6, (byte)235, false),
                PenMode.Highlighter or PenMode.StraightHighlighter => (18, (byte)115, false),
                PenMode.Glow => (6, (byte)220, false), PenMode.Dashed => (4, (byte)255, false),
                PenMode.Dotted => (5, (byte)255, false), PenMode.Pressure => (7, (byte)255, true),
                PenMode.Airbrush => (12, (byte)210, false), PenMode.Chalk => (6, (byte)235, false),
                PenMode.Crayon => (6, (byte)240, false), PenMode.Rainbow => (5, (byte)255, false),
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

    public void SetPressureEnabled(bool enabled)
    {
        Settings.PressureEnabled = enabled;
        _profiles.Save(Settings);
        foreach (var window in _windows.Values) window.RefreshTool();
    }

    public void SetShapeFill(bool enabled)
    {
        Settings.ShapeFillEnabled = enabled;
        _profiles.Save(Settings);
        OptionsChanged?.Invoke(this, EventArgs.Empty);
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
    public void ToggleBoard(bool dark)
    {
        var kind = dark ? BoardKind.Blackboard : BoardKind.Whiteboard;
        SetBoardKind(CurrentBoard == kind ? BoardKind.Transparent : kind);
    }

    public void SetBoard(MediaColor? color) => SetBoardKind(color switch
    {
        null => BoardKind.Transparent,
        { } c when c == Colors.White => BoardKind.Whiteboard,
        _ => BoardKind.Blackboard
    });

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
        if (_saveTimer.IsEnabled) { _saveTimer.Stop(); _store.SaveAsync(_appSettings).GetAwaiter().GetResult(); }
        _monitorTimer.Stop();
        _monitorTimer.Tick -= OnMonitorTimerTick;
        foreach (var window in _windows.Values) window.Close();
        _windows.Clear();
    }

}
