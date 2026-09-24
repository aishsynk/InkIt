using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using ScreenCanvas.Capture;
using ScreenCanvas.Commands;
using ScreenCanvas.Core;
using ScreenCanvas.Overlay;
using ScreenCanvas.Presentation;
using ScreenCanvas.Privacy;
using ScreenCanvas.Hotkeys;
using ScreenCanvas.Settings;
using ScreenCanvas.Zoom;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using FormsCursor = System.Windows.Forms.Cursor;

namespace ScreenCanvas.UI;

public partial class ToolbarWindow : Window, IUiExclusionRegionService
{
    private readonly IOverlayManager _overlay;
    private readonly ICaptureService _capture;
    private readonly Lazy<WindowsZoomEngine> _zoom = new(() => new WindowsZoomEngine());
    private readonly Lazy<StaticZoomService> _staticZoom;
    private readonly Lazy<BreakTimerService> _breakTimer = new(() => new BreakTimerService());
    private readonly Lazy<DemoTypeService> _demoType = new(() => new DemoTypeService());
    private readonly Lazy<CurtainService> _curtain = new(() => new CurtainService());
    private readonly Lazy<FreezeFrameService> _freeze = new(() => new FreezeFrameService());
    private readonly Lazy<PointerEffectsService> _pointerEffects = new(() => new PointerEffectsService());
    private readonly Lazy<BlackoutRegionService> _blackout = new(() => new BlackoutRegionService());
    private readonly Lazy<CodeFocusService> _codeFocus = new(() => new CodeFocusService());
    private readonly Lazy<KeyVisualizerService> _keyVisualizer = new(() => new KeyVisualizerService());

    private readonly CommandRegistry _registry;
    private readonly PresetManager _presets = PresetManager.Instance;
    private readonly ISettingsStore _settingsStore;
    private readonly AppSettings _appSettings;

    private bool _temporaryModeActive;
    private readonly System.Windows.Threading.DispatcherTimer _positionSaveTimer;
    private bool _restoringPosition;
    public event EventHandler<bool>? TemporaryModeChanged;
    public event EventHandler? PaletteStateChanged;
    internal Action<HotkeyConfiguration>? HotkeysChanged { get; set; }
    private bool _collapsed;
    private bool _isHorizontal = false;
    public bool IsHorizontal => _isHorizontal;
    public CodeFocusService CodeFocus => _codeFocus.Value;
    public KeyVisualizerService KeyVisualizer => _keyVisualizer.Value;
    private string? _currentInspector;
    private Button? _currentInspectorButton;
    private InspectorWindow? _inspector;

    
    
    

    public bool IsPaletteOpen => _inspector?.IsVisible == true;
    public bool IsZoomActive =>
        (_zoom.IsValueCreated && _zoom.Value.State != ZoomState.Inactive) ||
        (_staticZoom.IsValueCreated && _staticZoom.Value.IsVisible);
    public CommandRegistry Registry => _registry;

    public ToolbarWindow(IOverlayManager overlay, AppSettings settings, ISettingsStore store)
    {
        _staticZoom = new Lazy<StaticZoomService>(() =>
        {
            var s = new StaticZoomService();
            s.Closed += (_, _) => SetTemporaryMode(false);
            return s;
        });

        InitializeComponent();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                EndCurrentTool();
            }
        };
        _overlay = overlay;
        _overlay.ExclusionService = this;
        _settingsStore = store;
        _appSettings = settings;
        _capture = new CaptureService(() => _overlay.SuspendAnnotations());

        _registry = CommandRegistry.Create(
            _overlay,
            _capture,
            _zoom,
            _staticZoom,
            _breakTimer,
            _demoType,
            _curtain,
            _freeze,
            _pointerEffects,
            _blackout,
            _codeFocus,
            _keyVisualizer,
            onInspectCategory: cat => Dispatcher.BeginInvoke(() => ShowInspectorCategory(cat)),
            onOpenSettings: () => Dispatcher.BeginInvoke(OpenSettings),
            onOpenCommandPalette: () => Dispatcher.BeginInvoke(OpenCommandPalette),
            onOpenRadialMenu: () => Dispatcher.BeginInvoke(OpenRadialMenu),
            onToggleOrientation: () => Dispatcher.BeginInvoke(ToggleOrientation));

        _positionSaveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _positionSaveTimer.Tick += (_, _) => { _positionSaveTimer.Stop(); if (IsLoaded && !_restoringPosition) SavePosition(); };
        LocationChanged += (_, _) =>
        {
            if (!IsLoaded || _restoringPosition) return;
            _positionSaveTimer.Stop();
            _positionSaveTimer.Start();
            _inspector?.Reposition();
        };
        Closing += (_, _) =>
        {
            _positionSaveTimer.Stop();
            SavePosition();
            _inspector?.Close();
        };

        _overlay.ToolChanged += (_, _) => Dispatcher.BeginInvoke(() => { UpdateActiveTool(); UpdateColorChip(); Topmost = false; Topmost = true; });

        _presets.ActivePresetChanged += (_, _) => Dispatcher.BeginInvoke(ApplyActivePreset);

        SourceInitialized += (_, _) => { RestorePosition(); };
        Loaded += (_, _) =>
        {
            ApplyOrientation(_overlay.Settings.IsHorizontalToolbar);
            UpdateActiveTool();
            UpdateColorChip();
            SubscribeZoomEvents();
        };
    }

    public bool IsPointOverUi(Point screenPixelPoint)
    {
        if (!IsVisible) return false;

        if (ToolbarChrome.IsVisible && PresentationSource.FromVisual(ToolbarChrome) is not null)
        {
            try
            {
                var chromeTopLeft = ToolbarChrome.PointToScreen(new Point(0, 0));
                var dpi = VisualTreeHelper.GetDpi(ToolbarChrome);
                var chromeBounds = new Rect(chromeTopLeft.X, chromeTopLeft.Y, ToolbarChrome.ActualWidth * dpi.DpiScaleX, ToolbarChrome.ActualHeight * dpi.DpiScaleY);
                if (chromeBounds.Contains(screenPixelPoint)) return true;
            }
            catch (InvalidOperationException) { }
        }

        if (_inspector?.IsVisible == true && _inspector.IsPointOverUi(screenPixelPoint))
        {
            return true;
        }

        if (System.Windows.Application.Current is not null)
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is not OverlayWindow && window.IsVisible && PresentationSource.FromVisual(window) is not null)
                {
                    if (ReferenceEquals(window, this) || ReferenceEquals(window, _inspector)) continue;
                    try
                    {
                        var dpi = VisualTreeHelper.GetDpi(window);
                        var winTopLeft = window.PointToScreen(new Point(0, 0));
                        var winBounds = new Rect(winTopLeft.X, winTopLeft.Y, window.ActualWidth * dpi.DpiScaleX, window.ActualHeight * dpi.DpiScaleY);
                        if (winBounds.Contains(screenPixelPoint)) return true;
                    }
                    catch (InvalidOperationException) { }
                }
            }
        }

        return false;
    }

    private void RestorePosition()
    {
        _restoringPosition = true;
        try
        {
            Left = _appSettings.Toolbar.FloatingX ?? Math.Max(SystemParameters.WorkArea.Left + 20, SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - ActualWidth) / 2);
            Top = _appSettings.Toolbar.FloatingY ?? Math.Max(SystemParameters.WorkArea.Top + 20, SystemParameters.WorkArea.Top + 20);
            ClampToCurrentScreen();
        }
        finally
        {
            _restoringPosition = false;
        }
    }

    private void UpdateActiveTool()
    {
        var allButtons = new[] { CursorButton, PenButton, HighlighterButton, EraserButton, ShapesButton, TextButton, PresentButton, SpotlightButton, ZoomButton, CaptureButton, MoreButton };
        foreach (var button in allButtons)
        {
            if (button is null) continue;
            button.Background = Brushes.Transparent;
            button.Foreground = (Brush)FindResource("PrimaryTextBrush");
        }
        
        if (CursorIcon is not null) CursorIcon.Data = (Geometry)FindResource("Fluent.Cursor.Regular");
        if (PenIcon is not null) PenIcon.Data = (Geometry)FindResource("Fluent.Pen.Regular");
        if (HighlighterIcon is not null) HighlighterIcon.Data = (Geometry)FindResource("Fluent.Highlight.Regular");
        if (EraserIcon is not null) EraserIcon.Data = (Geometry)FindResource("Fluent.EraserTool.Regular");
        if (ShapesIcon is not null) ShapesIcon.Data = (Geometry)FindResource("Fluent.Shapes.Regular");
        if (TextIcon is not null) TextIcon.Data = (Geometry)FindResource("Fluent.Text.Regular");
        if (PresentIcon is not null) PresentIcon.Data = (Geometry)FindResource("Fluent.Laser.Regular");
        if (ZoomIcon is not null) ZoomIcon.Data = (Geometry)FindResource("Fluent.ZoomIn.Regular");
        
        var active = GetActiveToolButton();
        if (active is not null)
        {
            active.Background = (Brush)FindResource("SelectedBrush");
            active.Foreground = (Brush)FindResource("AccentBrush");
        }
        
        if (_currentInspectorButton is not null)
        {
            _currentInspectorButton.Background = (Brush)FindResource("SelectedBrush");
            _currentInspectorButton.Foreground = (Brush)FindResource("AccentBrush");
        }
    }

    private Button? GetActiveToolButton() => _overlay.Settings.Tool switch
    {
        ToolKind.Cursor => CursorButton,
        ToolKind.Pen => PenButton,
        ToolKind.Highlighter => HighlighterButton,
        ToolKind.Eraser => EraserButton,

        ToolKind.Shape => ShapesButton,
        ToolKind.NumberMarker => ShapesButton,
        ToolKind.Text => TextButton,
        ToolKind.Laser => PresentButton,
        ToolKind.Spotlight => SpotlightButton,

        _ => null
    };

    public void UpdateColorChip()
    {
        if (ColorChipOrb is not null)
        {
            ColorChipOrb.Background = new SolidColorBrush(_overlay.Settings.Color);
        }
    }

    private bool _zoomEventsSubscribed;

    private void SubscribeZoomEvents()
    {
        if (_zoomEventsSubscribed || !_zoom.IsValueCreated) return;
        _zoomEventsSubscribed = true;
        _zoom.Value.StateChanged += (_, s) => Dispatcher.BeginInvoke(() => UpdateZoomLevel(s));
    }

    private void UpdateZoomLevel(ZoomState state)
    {
        if (ZoomLevelText is null) return;
        if (state.Mode == ZoomMode.None || state.Factor <= 1.0)
        {
            ZoomLevelText.Visibility = Visibility.Collapsed;
        }
        else
        {
            ZoomLevelText.Text = $"{state.Factor * 100:0.#}%";
            ZoomLevelText.Visibility = Visibility.Visible;
        }
    }

    public void EndCurrentTool()
    {
        CloseMenus();
        if (_zoom.IsValueCreated) _zoom.Value.Reset();
        if (_staticZoom.IsValueCreated) _staticZoom.Value.Hide();
        if (_breakTimer.IsValueCreated) _breakTimer.Value.EmergencyStop();
        if (_demoType.IsValueCreated) _demoType.Value.EmergencyStop();
        if (_curtain.IsValueCreated) _curtain.Value.Hide();
        if (_freeze.IsValueCreated) _freeze.Value.Unfreeze();
        if (_pointerEffects.IsValueCreated) _pointerEffects.Value.Hide();
        if (_blackout.IsValueCreated) _blackout.Value.Hide();
        if (_codeFocus.IsValueCreated) _codeFocus.Value.Hide();
        if (_overlay.CurrentBoardColor != null) _overlay.SetBoard(null);
        SetTemporaryMode(false);
        _overlay.DeactivateCurrentTool(ToolDeactivationReason.Escape);
        UpdateActiveTool();
    }

    private Point _brandDragStart;
    private bool _brandIsDragging;

    private void DragGrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        e.Handled = true;
        CloseMenus();
        try { DragMove(); }
        finally { ClampToCurrentScreen(); SavePosition(); }
    }

    private void InkitBrandButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        _brandDragStart = e.GetPosition(this);
        _brandIsDragging = false;
    }

    private void InkitBrandButton_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !_brandIsDragging)
        {
            var pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _brandDragStart.X) > 3 || Math.Abs(pos.Y - _brandDragStart.Y) > 3)
            {
                _brandIsDragging = true;
                CloseMenus();
                try { DragMove(); }
                finally { ClampToCurrentScreen(); SavePosition(); }
            }
        }
    }

    private void InkitBrandButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_brandIsDragging)
        {
            e.Handled = true;
            _brandIsDragging = false;
        }
    }

    private void InkitBrand_Click(object sender, RoutedEventArgs e)
    {
        if (_brandIsDragging) return;
        CloseMenus();
        OpenCapabilityCentre();
    }

    private void ToolbarChrome_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (e.OriginalSource is DependencyObject dep && FindVisualParent<Button>(dep) is not null) return;
        e.Handled = true;
        CloseMenus();
        try { DragMove(); }
        finally { ClampToCurrentScreen(); SavePosition(); }
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T) parent = VisualTreeHelper.GetParent(parent);
        return parent as T;
    }

    private void ClampToCurrentScreen()
    {
        PositioningHelper.ClampToScreen(this);
    }

    private void SavePosition()
    {
        var x = Left;
        var y = Top;
        if (IsLoaded && PresentationSource.FromVisual(ToolbarChrome) is not null)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            var point = ToolbarChrome.PointToScreen(new Point());
            x = point.X / dpi.DpiScaleX;
            y = point.Y / dpi.DpiScaleY;
        }
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y)) return;
        _appSettings.Toolbar.FloatingX = x;
        _appSettings.Toolbar.FloatingY = y;
        _settingsStore.SaveAsync(_appSettings).GetAwaiter().GetResult();
    }

    private void SetTemporaryMode(bool active)
    {
        if (_temporaryModeActive == active) return;
        _temporaryModeActive = active;
        TemporaryModeChanged?.Invoke(this, active);
    }

    // --- Orientation Support (Horizontal ~32 px high vs Vertical ~32 px wide) ---

    public void ToggleOrientation() => ApplyOrientation(!_isHorizontal);

    public void ApplyOrientation(bool isHorizontal)
    {
        _isHorizontal = isHorizontal;
        _overlay.Settings.IsHorizontalToolbar = isHorizontal;

        var separators = new[] { Sep0, Sep1, Sep2, Sep3, Sep4, Sep5 };

        if (isHorizontal)
        {
            ToolStack.Orientation = Orientation.Horizontal;
            ToolbarChrome.Height = 32;
            ToolbarChrome.Width = double.NaN;
            DragGrip.Width = 9;
            DragGrip.Height = 20;
            DragGrip.Margin = new Thickness(1, 0, 2, 0);
            DragGripDots.Orientation = Orientation.Horizontal;
            foreach (var s in separators)
            {
                s.Width = 1;
                s.Height = 13;
                s.Margin = new Thickness(2, 0, 2, 0);
            }
        }
        else
        {
            ToolStack.Orientation = Orientation.Vertical;
            ToolbarChrome.Width = 32;
            ToolbarChrome.Height = double.NaN;
            DragGrip.Width = 20;
            DragGrip.Height = 9;
            DragGrip.Margin = new Thickness(0, 1, 0, 2);
            DragGripDots.Orientation = Orientation.Vertical;
            foreach (var s in separators)
            {
                s.Height = 1;
                s.Width = 13;
                s.Margin = new Thickness(0, 2, 0, 2);
            }
        }

        CloseMenus();
    }

    // --- Direct Toolbar Actions ---

    private void Cursor_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        CloseMenus();
        _registry.Find("cursor")?.Execute();
    }
    private void Pen_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        if (IsPaletteOpen && _currentInspector == "pen")
        {
            CloseMenus();
            return;
        }
        _registry.Find("pen")?.Execute();
        OpenInspector(PenButton, "pen");
    }
    private void Highlighter_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        if (IsPaletteOpen && _currentInspector == "highlighter")
        {
            CloseMenus();
            return;
        }
        _registry.Find("highlighter")?.Execute();
        OpenInspector(HighlighterButton, "highlighter");
    }
    private void Eraser_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        CloseMenus();
        _registry.Find("eraser")?.Execute();
    }
    private void Shapes_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        if (IsPaletteOpen && _currentInspector == "shapes")
        {
            CloseMenus();
            return;
        }
        _registry.Find("shapes")?.Execute();
        OpenInspector(ShapesButton, "shapes");
    }
    private void Text_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        if (IsPaletteOpen && _currentInspector == "text")
        {
            CloseMenus();
            return;
        }
        _registry.Find("text")?.Execute();
        OpenInspector(TextButton, "text");
    }
    private void Present_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        if (IsPaletteOpen && _currentInspector == "present")
        {
            CloseMenus();
            return;
        }
        // Set the tool directly: the "present" command also requests the inspector,
        // which would re-enter this handler and toggle the just-opened palette closed.
        _overlay.SetTool(ToolKind.Laser);
        OpenInspector(PresentButton, "present");
    }
    private void Spotlight_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        CloseMenus();
        _registry.Find("present.spotlight")?.Execute();
    }
    private void Zoom_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        _registry.Find("zoom")?.Execute();
        OpenInspector(ZoomButton, "zoom");
    }

    public void LiveZoomIn()
    {
        var currentFactor = _zoom.IsValueCreated && _zoom.Value.State != ZoomState.Inactive ? _zoom.Value.State.Factor : 1.0;
        var nextFactor = Math.Min(currentFactor + 0.5, 6.0);
        var p = FormsCursor.Position;
        var dpi = VisualTreeHelper.GetDpi(this);
        SetTemporaryMode(true);
        _zoom.Value.TrySetLiveZoom(nextFactor, new System.Windows.Point(p.X / dpi.DpiScaleX, p.Y / dpi.DpiScaleY));
    }

    public void LiveZoomOut()
    {
        if (!_zoom.IsValueCreated || _zoom.Value.State == ZoomState.Inactive) return;
        var nextFactor = _zoom.Value.State.Factor - 0.5;
        if (nextFactor <= 1.0)
        {
            _zoom.Value.Reset();
            SetTemporaryMode(false);
        }
        else
        {
            var p = FormsCursor.Position;
            var dpi = VisualTreeHelper.GetDpi(this);
            _zoom.Value.TrySetLiveZoom(nextFactor, new System.Windows.Point(p.X / dpi.DpiScaleX, p.Y / dpi.DpiScaleY));
        }
    }

    public void LiveZoomReset()
    {
        if (_zoom.IsValueCreated && _zoom.Value.State != ZoomState.Inactive)
        {
            _zoom.Value.Reset();
            SetTemporaryMode(false);
        }
    }

    public void FreezeScreen()
    {
        SetTemporaryMode(true);
        _freeze.Value.Freeze();
        _overlay.SetTool(ToolKind.Pen);
        CloseMenus();
    }

    public void TriggerStaticZoom()
    {
        if (_zoom.IsValueCreated && _zoom.Value.State != ZoomState.Inactive)
        {
            _zoom.Value.Reset();
        }
        CloseMenus();
        SetTemporaryMode(true);
        _staticZoom.Value.Show(2.0);
    }

    private void ColorChip_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        if (IsPaletteOpen && _currentInspector == "color")
        {
            CloseMenus();
            return;
        }
        OpenInspector(ColorChipButton, "color");
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        _overlay.Undo();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        _overlay.Redo();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        _overlay.Clear();
    }

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        CloseMenus();
        var bitmap = _capture.CaptureInteractiveRegion(this);
        if (bitmap is null) return;
        var preview = new CapturePreviewWindow(bitmap, _capture) { Owner = this };
        preview.ShowDialog();
    }



    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (_collapsed) { _collapsed = false; UpdateCollapseState(); return; }
        if (IsPaletteOpen && _currentInspector == "more")
        {
            CloseMenus();
            return;
        }
        OpenInspector(MoreButton, "more");
    }

    private void Collapse_Click(object sender, RoutedEventArgs e)
    {
        _collapsed = !_collapsed;
        UpdateCollapseState();
    }

    private void UpdateCollapseState()
    {
        CloseMenus();
        var buttons = new FrameworkElement[] { Sep0, CursorButton, Sep1, PenButton, HighlighterButton, EraserButton, Sep2, ShapesButton, TextButton, Sep3, PresentButton, SpotlightButton, ZoomButton, CaptureButton, Sep4, UndoButton, RedoButton, ColorChipButton, ClearButton, Sep5, MoreButton };
        foreach (var b in buttons) b.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;

        if (_collapsed)
        {
            if (_isHorizontal)
            {
                ToolbarChrome.Width = double.NaN;
                ToolbarChrome.Height = 32;
            }
            else
            {
                ToolbarChrome.Width = 32;
                ToolbarChrome.Height = double.NaN;
            }
            CollapseChevron.Data = (Geometry)FindResource("Fluent.ChevronUp.Regular");
            CollapseButton.ToolTip = "Restore toolbar";
        }
        else
        {
            if (_isHorizontal)
            {
                ToolbarChrome.Width = double.NaN;
                ToolbarChrome.Height = 32;
            }
            else
            {
                ToolbarChrome.Width = 32;
                ToolbarChrome.Height = double.NaN;
            }
            CollapseChevron.Data = (Geometry)FindResource("Fluent.ChevronDown.Regular");
            CollapseButton.ToolTip = "Collapse toolbar";
        }
    }

    // --- Contextual Progressive-Disclosure Inspectors ---

    public void ShowInspectorCategory(string cat)
    {
        switch (cat.ToLowerInvariant())
        {
            case "pen": Pen_Click(PenButton, new RoutedEventArgs()); break;
            case "highlighter": Highlighter_Click(HighlighterButton, new RoutedEventArgs()); break;
            case "shapes": Shapes_Click(ShapesButton, new RoutedEventArgs()); break;
            case "text": Text_Click(TextButton, new RoutedEventArgs()); break;
            case "laser":
            case "present": Present_Click(PresentButton, new RoutedEventArgs()); break;
            case "zoom": Zoom_Click(ZoomButton, new RoutedEventArgs()); break;
            case "color": ColorChip_Click(ColorChipButton, new RoutedEventArgs()); break;
            case "more": More_Click(MoreButton, new RoutedEventArgs()); break;
            default: Pen_Click(PenButton, new RoutedEventArgs()); break;
        }
    }

    private void OpenInspector(Button anchorButton, string inspectorKey)
    {
        _currentInspector = inspectorKey;
        _currentInspectorButton = anchorButton;

        if (_inspector is null)
        {
            _inspector = new InspectorWindow(_overlay, this);
            _inspector.Owner = this;
        }

        _inspector.ShowCategory(inspectorKey, anchorButton);
        UpdateActiveTool();
        PaletteStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CloseMenus()
    {
        _inspector?.CloseInspector();
        _currentInspector = null;
        _currentInspectorButton = null;
        UpdateActiveTool();
        PaletteStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // --- Capability Centre, Command Palette & Radial Menu Openers ---

    public void OpenCapabilityCentre()
    {
        CloseMenus();
        var win = new CapabilityCentreWindow(_registry,
            onOpenSettings: OpenSettings,
            onOpenPresets: () => Dispatcher.BeginInvoke(OpenPresetsDialog));
        win.Owner = this;
        win.ShowDialog();
    }

    public void OpenCommandPalette()
    {
        CloseMenus();
        var win = new CommandPaletteWindow(_registry);
        win.Owner = this;
        win.ShowDialog();
    }

    public void OpenRadialMenu()
    {
        CloseMenus();
        var win = new RadialMenuWindow(_registry);
        win.Owner = this;
        win.Show();
    }

    public void OpenSettings()
    {
        CloseMenus();
        var win = new SettingsWindow(_appSettings, _settingsStore, HotkeysChanged);
        win.Owner = this;
        win.ShowDialog();
    }

    public void OpenPresetsDialog()
    {
        CloseMenus();
        OpenCapabilityCentre();
    }

    private void ApplyActivePreset()
    {
        var preset = _presets.ActivePreset;
        // Activate default tool of preset
        if (_registry.Find(preset.DefaultToolId) is { } defCmd)
        {
            defCmd.Execute();
        }
    }
}
