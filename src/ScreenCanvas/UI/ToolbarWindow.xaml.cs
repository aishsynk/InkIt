using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ScreenCanvas.Capture;
using ScreenCanvas.Commands;
using ScreenCanvas.Core;
using ScreenCanvas.Overlay;
using ScreenCanvas.Presentation;
using ScreenCanvas.Privacy;
using ScreenCanvas.Hotkeys;
using ScreenCanvas.Settings;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using ScreenCanvas.UI.Toolbar;
using ScreenCanvas.Zoom;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using VerticalAlignment = System.Windows.VerticalAlignment;
using FormsCursor = System.Windows.Forms.Cursor;
using TextBox = System.Windows.Controls.TextBox;
using Cursors = System.Windows.Input.Cursors;

namespace ScreenCanvas.UI;

/// <summary>Tier 1 floating core toolbar (design: Toolbar.tsx) with drag-to-reorder customisable tools.</summary>
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
    private readonly System.Windows.Threading.DispatcherTimer _positionSaveTimer;
    private List<ToolbarItemState> _layout;
    private InspectorWindow? _inspector;
    private MultiToolWindow? _multiTool;
    private CustomizeToolbarWindow? _customize;
    private bool _temporaryModeActive;
    private bool _restoringPosition;
    private bool _collapsed;
    private bool _isHorizontal = true;
    private PenMode _lastPenMode = PenMode.Ballpoint;
    private PenMode _lastHighlighterMode = PenMode.Highlighter;

    // Drag-and-drop reordering state
    private string? _dragCandidate;
    private Point _dragStart;
    private string? _draggedId;
    private string? _dropTargetId;
    private bool _dropAfter;
    private readonly Dictionary<string, Button> _toolButtons = [];

    private Button? _snapButton;
    private Button? _multiButton;
    private Border? _multiBadge;
    private TextBlock? _multiBadgeText;
    private Button? _customizeButton;

    public event EventHandler<bool>? TemporaryModeChanged;
    public event EventHandler? PaletteStateChanged;
    internal Action<HotkeyConfiguration>? HotkeysChanged { get; set; }

    public bool IsHorizontal => _isHorizontal;
    public CodeFocusService CodeFocus => _codeFocus.Value;
    public KeyVisualizerService KeyVisualizer => _keyVisualizer.Value;
    public bool IsPaletteOpen => _inspector?.IsVisible == true;
    public bool IsZoomActive => IsLiveZoomActive || _overlay.IsZoomAreaActive ||
        (_staticZoom.IsValueCreated && _staticZoom.Value.IsVisible);
    private bool IsLiveZoomActive => _zoom.IsValueCreated && _zoom.Value.State != ZoomState.Inactive;
    public CommandRegistry Registry => _registry;
    public IReadOnlyList<ToolbarItemState> Layout => _layout;
    public int BreakTimerMinutes => _appSettings.Presentation.BreakTimerMinutes;
    public double ZoomFactor => _zoom.IsValueCreated && _zoom.Value.State != ZoomState.Inactive ? _zoom.Value.State.Factor : _overlay.Settings.ZoomFactor;
    public AppSettings AppSettings => _appSettings;
    public PenMode LastPenMode => _lastPenMode;
    public PenMode LastHighlighterMode => _lastHighlighterMode;

    public ToolbarWindow(IOverlayManager overlay, AppSettings settings, ISettingsStore store)
    {
        _staticZoom = new Lazy<StaticZoomService>(() =>
        {
            var s = new StaticZoomService();
            s.Closed += (_, _) => SetTemporaryMode(false);
            return s;
        });

        InitializeComponent();
        _overlay = overlay;
        _overlay.ExclusionService = this;
        _settingsStore = store;
        _appSettings = settings;
        _layout = ToolbarCatalog.Normalize(settings.Toolbar.Items);
        _isHorizontal = settings.Toolbar.Horizontal;
        _capture = new CaptureService(() => _overlay.SuspendAnnotations());

        _registry = CommandRegistry.Create(new CommandContext
        {
            Overlay = _overlay,
            Capture = _capture,
            Zoom = _zoom,
            StaticZoom = _staticZoom,
            BreakTimer = _breakTimer,
            DemoType = _demoType,
            Curtain = _curtain,
            Freeze = _freeze,
            PointerEffects = _pointerEffects,
            Blackout = _blackout,
            CodeFocus = _codeFocus,
            KeyVisualizer = _keyVisualizer,
            Toolbar = this
        });

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                EndCurrentTool();
                return;
            }
            if (Keyboard.FocusedElement is TextBox) return;
            if (HandleShortcut(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers)) e.Handled = true;
        };
        OverlayKeyRouter.Handler = HandleShortcut;

        _positionSaveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _positionSaveTimer.Tick += (_, _) => { _positionSaveTimer.Stop(); if (IsLoaded && !_restoringPosition) SavePosition(); };
        LocationChanged += (_, _) =>
        {
            RepositionPopups();
            if (!IsLoaded || _restoringPosition) return;
            _positionSaveTimer.Stop();
            _positionSaveTimer.Start();
        };
        SizeChanged += (_, _) => RepositionPopups();
        Closing += (_, _) =>
        {
            _positionSaveTimer.Stop();
            SavePosition();
        };

        _overlay.ToolChanged += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            TrackPenModes();
            RefreshItemStates();
            Topmost = false;
            Topmost = true;
        });
        _overlay.OptionsChanged += (_, _) => Dispatcher.BeginInvoke(OnOptionsChanged);
        _overlay.BoardChanged += (_, _) => Dispatcher.BeginInvoke(RefreshItemStates);
        _overlay.PinchZoomRequested += (_, factor) => Dispatcher.BeginInvoke(() => SetZoomFactor(factor));
        _overlay.ZoomAreaChanged += (_, _) => Dispatcher.BeginInvoke(OnZoomAreaChanged);
        _overlay.ZoomAreaRequested += (_, _) => Dispatcher.BeginInvoke(ZoomToArea);
        _overlay.FocusBoxChanged += (_, _) => Dispatcher.BeginInvoke(RefreshItemStates);
        _overlay.ToolWheelRequested += (_, _) => Dispatcher.BeginInvoke(OpenRadialMenu);
        _presets.ActivePresetChanged += (_, _) => Dispatcher.BeginInvoke(ApplyActivePreset);
        ThemeManager.ThemeChanged += (_, _) => Dispatcher.BeginInvoke(() => { BuildStrip(); BuildFooter(); });

        SourceInitialized += (_, _) => RestorePosition();
        Loaded += (_, _) =>
        {
            ApplyOrientation(_isHorizontal);
            SubscribeZoomEvents();
        };
        ContentRendered += (_, _) =>
        {
            ApplyFollowSlides();
            if (!ShowStartupExtras) return;
            // First run: a short tour. Every run: a quiet daily update check (if allowed) a little after start-up.
            if (!_appSettings.Advanced.TourCompleted)
                Delay(TimeSpan.FromSeconds(1.5), StartTour);
            Delay(TimeSpan.FromSeconds(20), () => CheckForUpdates(manual: false));
        };
    }

    // ------------------------------------------------------------------ Exclusion zone

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
        if (_inspector?.IsVisible == true && _inspector.IsPointOverUi(screenPixelPoint)) return true;
        if (System.Windows.Application.Current is null) return false;
        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            if (window is OverlayWindow || ReferenceEquals(window, this) || ReferenceEquals(window, _inspector)) continue;
            if (!window.IsVisible || PresentationSource.FromVisual(window) is null) continue;
            if (window is IChromeHost host)
            {
                if (host.IsPointOverChrome(screenPixelPoint)) return true;
                continue;
            }
            try
            {
                var dpi = VisualTreeHelper.GetDpi(window);
                var winTopLeft = window.PointToScreen(new Point(0, 0));
                var winBounds = new Rect(winTopLeft.X, winTopLeft.Y, window.ActualWidth * dpi.DpiScaleX, window.ActualHeight * dpi.DpiScaleY);
                if (winBounds.Contains(screenPixelPoint)) return true;
            }
            catch (InvalidOperationException) { }
        }
        return false;
    }

    // ------------------------------------------------------------------ Position

    private void RestorePosition()
    {
        _restoringPosition = true;
        try
        {
            UpdateLayout();
            var margin = ToolbarChrome.Margin;
            var area = SystemParameters.WorkArea;
            var chromeWidth = ToolbarChrome.ActualWidth > 0 ? ToolbarChrome.ActualWidth : 560;
            var x = _appSettings.Toolbar.FloatingX ?? area.Left + (area.Width - chromeWidth) / 2;
            var y = _appSettings.Toolbar.FloatingY ?? area.Top + 12;
            Left = x - margin.Left;
            Top = y - margin.Top;
            ClampToCurrentScreen();
        }
        finally
        {
            _restoringPosition = false;
        }
    }

    private void ClampToCurrentScreen()
    {
        var (left, top, right, bottom) = PositioningHelper.GetScreenBounds(this);
        var m = ToolbarChrome.Margin;
        var chromeLeft = Left + m.Left;
        var chromeTop = Top + m.Top;
        var w = ToolbarChrome.ActualWidth;
        var h = ToolbarChrome.ActualHeight;
        chromeLeft = Math.Clamp(chromeLeft, left - w + 40, right - 40);
        chromeTop = Math.Clamp(chromeTop, top, bottom - 30);
        Left = chromeLeft - m.Left;
        Top = chromeTop - m.Top;
    }

    private void SavePosition()
    {
        if (!IsLoaded || PresentationSource.FromVisual(ToolbarChrome) is null) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var point = ToolbarChrome.PointToScreen(new Point());
        var x = point.X / dpi.DpiScaleX;
        var y = point.Y / dpi.DpiScaleY;
        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y)) return;
        _appSettings.Toolbar.FloatingX = x;
        _appSettings.Toolbar.FloatingY = y;
        SaveSettings();
    }

    private void SaveSettings() => _settingsStore.SaveAsync(_appSettings).GetAwaiter().GetResult();

    private void RepositionPopups()
    {
        _inspector?.Reposition();
        _multiTool?.Reposition();
        _customize?.Reposition();
        _notice?.Reposition();
    }

    // ------------------------------------------------------------------ Strip

    public void ToggleOrientation() => ApplyOrientation(!_isHorizontal);

    public void ApplyOrientation(bool isHorizontal)
    {
        _isHorizontal = isHorizontal;
        _overlay.UpdateOptions(o => o.IsHorizontalToolbar = isHorizontal);
        Strip.Orientation = isHorizontal ? Orientation.Horizontal : Orientation.Vertical;
        Strip.Width = isHorizontal ? double.NaN : 36;
        Strip.Height = isHorizontal ? 32 : double.NaN;
        BuildStrip();
        BuildFooter();
        UpdateLayout();
        RepositionPopups();
        if (IsLoaded) Dispatcher.BeginInvoke(ClampToCurrentScreen, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void BuildStrip()
    {
        Strip.Children.Clear();
        _toolButtons.Clear();
        Add(BuildGrip());
        if (_collapsed)
        {
            var expand = SmallButton("ChevronDown", 14, "Restore toolbar", () => { _collapsed = false; BuildStrip(); BuildFooter(); });
            Add(expand);
            return;
        }
        Add(Divider());
        // Tools in their groups (draw | add | present | edit | find); a divider marks each change of group.
        string? group = null;
        foreach (var state in _layout.Where(s => s.Visible))
        {
            if (ToolbarCatalog.Find(state.Id) is not { } def) continue;
            if (group is not null && def.Group != group) Add(Divider());
            group = def.Group;
            var button = CreateToolButton(def);
            _toolButtons[def.Id] = button;
            Add(button);
        }
        Add(Divider());
        // Expert switches sit at the end, away from the everyday tools.
        _snapButton = SmallButton("Magnet", 16, string.Empty, () => _overlay.UpdateOptions(o => o.SnapToGrid = !o.SnapToGrid));
        Add(_snapButton);
        Add(BuildMultiToolButton());
        Add(SmallButton(_isHorizontal ? "ArrowUpDown" : "ArrowLeftRight", 16, "Turn the toolbar " + (_isHorizontal ? "upright" : "sideways") + " (Ctrl+Shift+O)", ToggleOrientation));
        _customizeButton = DK.Button(new LucideIcon("SlidersHorizontal", 16) { StrokeWidth = 1.75 }, Tw.B(Colors.Transparent), "Ink.ToolbarMuted", "Ink.Hover", "Ink.Text200", 12, new Thickness(6));
        _customizeButton.ToolTip = "Customise the toolbar - choose, hide and reorder buttons";
        _customizeButton.Click += (_, _) => ToggleCustomize();
        Add(_customizeButton);
        RefreshItemStates();

        void Add(FrameworkElement element)
        {
            if (Strip.Children.Count > 0)
                element.Margin = _isHorizontal ? new Thickness(2, 0, 0, 0) : new Thickness(0, 2, 0, 0);
            element.HorizontalAlignment = HorizontalAlignment.Center;
            element.VerticalAlignment = VerticalAlignment.Center;
            Strip.Children.Add(element);
        }
    }

    private FrameworkElement BuildGrip()
    {
        // The InkIt logo doubles as the drag handle: the wordmark across, the "i" mark when upright.
        var logoFile = _isHorizontal
            ? (ThemeManager.IsDark ? "InkItLogoWhite.png" : "InkItLogo.png")
            : (ThemeManager.IsDark ? "InkItMarkWhite.png" : "InkItMarkDark.png");
        var logo = new System.Windows.Controls.Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri($"pack://application:,,,/Assets/{logoFile}")),
            Height = _isHorizontal ? 26 : 28,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
        var grip = DK.Button(logo, Tw.B(Colors.Transparent), "Ink.GripText", "Ink.Hover", "Ink.Text300", 8,
            _isHorizontal ? new Thickness(8, 5, 8, 5) : new Thickness(6));
        grip.Cursor = Cursors.SizeAll;
        grip.ToolTip = "InkIt - drag here to move the toolbar";
        grip.PreviewMouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            ChromeShadow.Color = Tw.Blue500;
            ChromeShadow.Opacity = 0.35;
            ToolbarChrome.SetValue(Border.BorderBrushProperty, Tw.B(Tw.Blue500, 0.5));
            try { DragMove(); }
            finally
            {
                ChromeShadow.Color = Colors.Black;
                ChromeShadow.Opacity = 0.5;
                ToolbarChrome.SetResourceReference(Border.BorderBrushProperty, "Ink.Border");
                ClampToCurrentScreen();
                SavePosition();
            }
        };
        return grip;
    }

    private Button SmallButton(string icon, double iconSize, string tooltip, Action click)
    {
        var b = DK.Button(new LucideIcon(icon, iconSize) { StrokeWidth = 1.75 }, Tw.B(Colors.Transparent), "Ink.ToolbarMuted", "Ink.Hover", "Ink.Text200", 8, new Thickness(6));
        if (!string.IsNullOrEmpty(tooltip)) b.ToolTip = tooltip;
        b.Click += (_, _) => click();
        return b;
    }

    private FrameworkElement Divider()
    {
        var line = new Border { SnapsToDevicePixels = true };
        line.SetResourceReference(Border.BackgroundProperty, "Ink.Divider");
        if (_isHorizontal) { line.Width = 1; line.Height = 24; line.Margin = new Thickness(4, 0, 2, 0); }
        else { line.Height = 1; line.Width = 32; line.Margin = new Thickness(0, 4, 0, 2); }
        return line;
    }

    private FrameworkElement BuildMultiToolButton()
    {
        _multiButton = DK.Button(new LucideIcon("WandSparkles", 16) { StrokeWidth = 1.75 }, Tw.B(Colors.Transparent), "Ink.ToolbarMuted", "Ink.Hover", "Ink.Text200", 8, new Thickness(6));
        _multiButton.ToolTip = "Multi-Tool Mode (Stack simultaneous tools: Laser Trail, Spotlight Follow, Grid Magnet, Auto-Shape)";
        _multiButton.Click += (_, _) => ToggleMultiTool();
        _multiBadgeText = new TextBlock { FontSize = 8, FontWeight = FontWeights.Bold, FontFamily = DK.Mono, Foreground = Tw.B(Colors.White), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _multiBadge = new Border
        {
            Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = Tw.B(Tw.Purple500),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -4, -4, 0), IsHitTestVisible = false, Child = _multiBadgeText,
            Effect = DK.Shadow(4, 1, 0.4)
        };
        return new Grid { Children = { _multiButton, _multiBadge } };
    }

    private Button CreateToolButton(ToolbarItemDefinition def)
    {
        FrameworkElement content;
        if (def.Id == "color")
        {
            var orb = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), BorderBrush = Tw.B(Colors.White, 0.4), BorderThickness = new Thickness(1), Tag = "orb" };
            content = new Grid { Width = 16, Height = 16, Children = { orb } };
        }
        else
        {
            var grid = new Grid { Width = 18, Height = 18 };
            grid.Children.Add(new LucideIcon(def.Icon, 18) { StrokeWidth = 1.75 });
            content = grid;
        }
        var button = DK.Button(content, Tw.B(Colors.Transparent), "Ink.ToolbarText", "Ink.Hover", "Ink.Text", 12, new Thickness(def.Id == "color" ? 8 : 7));
        button.Tag = def.Id;
        button.ToolTip = def.Tooltip;
        AutomationPropertiesHelper.SetName(button, def.Label + (def.Shortcut is null ? string.Empty : $" ({def.Shortcut})"));
        button.Name = def.Id.Replace('.', '_') + "Button";
        button.Click += (_, _) => OnItemClick(def.Id);
        button.PreviewMouseLeftButtonDown += (_, e) => { _dragCandidate = def.Id; _dragStart = e.GetPosition(this); };
        button.PreviewMouseLeftButtonUp += (_, _) => _dragCandidate = null;
        button.PreviewMouseMove += (_, e) => TryBeginToolDrag(button, def.Id, e);
        button.AllowDrop = true;
        button.DragOver += (_, e) => OnToolDragOver(button, def.Id, e);
        button.DragLeave += (_, _) => { if (_dropTargetId == def.Id) { _dropTargetId = null; RefreshItemStates(); } };
        button.Drop += (_, e) => OnToolDrop(def.Id, e);
        return button;
    }

    // ------------------------------------------------------------------ Item states

    /// <summary>Applies active/inactive colours (design Toolbar.tsx) to every toolbar control.</summary>
    public void RefreshItemStates()
    {
        var s = _overlay.Settings;
        foreach (var (id, button) in _toolButtons)
        {
            var (active, bg, fg, ring) = ActiveStyle(id, s);
            button.BeginAnimation(OpacityProperty, null);
            button.Opacity = 1;
            button.Effect = null;
            button.BorderThickness = new Thickness(0);
            if (active)
            {
                Paint? ringPaint = null;
                if (ring is { } r) ringPaint = Tw.B(r);
                DK.Recolor(button, Tw.B(bg), Tw.B(fg), Tw.B(bg), Tw.B(fg), ringPaint, ringPaint);
                if (ring is not null) button.BorderThickness = new Thickness(1);
                button.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = bg, BlurRadius = 14, ShadowDepth = 3, Opacity = 0.35 };
                if (id == "laser")
                {
                    var pulse = new DoubleAnimation(1, 0.5, TimeSpan.FromSeconds(1)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
                    button.BeginAnimation(OpacityProperty, pulse);
                }
            }
            else
            {
                switch (id)
                {
                    case "undo" or "redo" or "capture" or "more" or "collapse":
                        DK.Recolor(button, Tw.B(Colors.Transparent), "Ink.ToolbarMuted", "Ink.Hover", "Ink.Text200");
                        break;
                    case "clear":
                        DK.Recolor(button, Tw.B(Colors.Transparent), "Ink.ToolbarMuted", Tw.B(Tw.Red950, 0.6), Tw.B(Tw.Red300));
                        break;
                    case "palette":
                        DK.Recolor(button, Tw.B(Colors.Transparent), Tw.B(Tw.Blue400), Tw.B(Tw.Blue950, 0.6), Tw.B(Tw.Blue200));
                        break;
                    case "capability":
                        DK.Recolor(button, Tw.B(Colors.Transparent), Tw.B(Tw.Purple400), Tw.B(Tw.Purple950, 0.6), Tw.B(Tw.Purple300));
                        break;
                    default:
                        DK.Recolor(button, Tw.B(Colors.Transparent), "Ink.ToolbarText", "Ink.Hover", "Ink.Text");
                        break;
                }
            }
            if (id == "color" && button.Content is Grid g && g.Children[0] is Border orb) orb.Background = new SolidColorBrush(s.Color);
            if (id == "pen") DecoratePen(button, s);

            // Drag-and-drop feedback
            if (_draggedId == id)
            {
                button.Opacity = 0.3;
                button.BorderThickness = new Thickness(2);
                button.BorderBrush = Tw.B(Tw.Blue500);
            }
            else if (_dropTargetId == id)
            {
                button.BorderBrush = Tw.B(Tw.Blue400);
                button.BorderThickness = _isHorizontal
                    ? new Thickness(_dropAfter ? 0 : 2, 0, _dropAfter ? 2 : 0, 0)
                    : new Thickness(0, _dropAfter ? 0 : 2, 0, _dropAfter ? 2 : 0);
            }
        }
        RefreshControlStates(s);
        BuildFooterBadges();
    }

    private (bool Active, Color Background, Color Foreground, Color? Ring) ActiveStyle(string id, ToolSettings s) => id switch
    {
        "cursor" when s.Tool == ToolKind.Cursor => (true, Tw.Blue600, Colors.White, null),
        "select" when s.Tool == ToolKind.Select => (true, Tw.Indigo600, Colors.White, Tw.Indigo400),
        "pen" when s.Tool == ToolKind.Pen => (true, Tw.Blue600, Colors.White, null),
        "highlighter" when s.Tool == ToolKind.Highlighter => (true, Tw.Amber500, Tw.Slate950, null),
        "eraser" when s.Tool == ToolKind.Eraser => (true, Tw.Rose600, Colors.White, null),
        "shape" when s.Tool == ToolKind.Shape => (true, Tw.Blue600, Colors.White, null),
        "text" when s.Tool == ToolKind.Text => (true, Tw.Blue600, Colors.White, null),
        "marker" when s.Tool == ToolKind.NumberMarker => (true, Tw.Emerald600, Colors.White, null),
        "laser" when s.Tool == ToolKind.Laser => (true, Tw.Red600, Colors.White, null),
        "spotlight" when s.Tool == ToolKind.Spotlight => (true, Tw.Amber500, Tw.Slate950, null),
        "zoom" when IsZoomActive => (true, Tw.Indigo600, Colors.White, null),
        "focus" when _overlay.IsFocusBoxActive => (true, Tw.Amber500, Colors.White, null),
        "record" when IsRecording => (true, Tw.Red600, Colors.White, null),
        "mirror" when IsMirroring => (true, Tw.Blue600, Colors.White, null),
        "board" when _overlay.CurrentBoard != BoardKind.Transparent => (true, Tw.Slate700, Colors.White, Tw.Blue400),
        "more" when _inspector?.CurrentCategory == "more" && IsPaletteOpen => (true, Tw.Slate700, Colors.White, null),
        _ => (false, Colors.Transparent, Colors.Transparent, null)
    };

    /// <summary>Pen indicators: colour dot (bottom-right), laser trail (top-left pulse), smart shape (top-right).</summary>
    private static void DecoratePen(Button button, ToolSettings s)
    {
        if (button.Content is not Grid grid) return;
        while (grid.Children.Count > 1) grid.Children.RemoveAt(1);
        grid.ClipToBounds = false;
        Ellipse Dot(Color fill, HorizontalAlignment h, VerticalAlignment v) => new()
        {
            Width = 6, Height = 6, Fill = new SolidColorBrush(fill), Stroke = Tw.B(Tw.Slate900), StrokeThickness = 1,
            HorizontalAlignment = h, VerticalAlignment = v, Margin = new Thickness(-4), IsHitTestVisible = false
        };
        grid.Children.Add(Dot(s.Color, HorizontalAlignment.Right, VerticalAlignment.Bottom));
        if (s.SimultaneousLaser)
        {
            var laser = Dot(Tw.Rose500, HorizontalAlignment.Left, VerticalAlignment.Top);
            laser.ToolTip = "Concurrent Laser Glow Active";
            laser.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.4, TimeSpan.FromSeconds(1)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            grid.Children.Add(laser);
        }
    }

    private void RefreshControlStates(ToolSettings s)
    {
        if (_snapButton is not null)
        {
            _snapButton.ToolTip = $"Snap to Grid: {(s.SnapToGrid ? "ON (Active)" : "OFF")} - Click to toggle alignment";
            if (s.SnapToGrid)
            {
                DK.Recolor(_snapButton, Tw.B(Tw.Blue600), Tw.B(Colors.White), Tw.B(Tw.Blue600), Tw.B(Colors.White), Tw.B(Tw.Blue400), Tw.B(Tw.Blue400));
                _snapButton.BorderThickness = new Thickness(1);
            }
            else
            {
                DK.Recolor(_snapButton, Tw.B(Colors.Transparent), "Ink.ToolbarMuted", "Ink.Hover", "Ink.Text200");
                _snapButton.BorderThickness = new Thickness(0);
            }
        }
        if (_multiButton is not null && _multiBadge is not null && _multiBadgeText is not null)
        {
            var open = _multiTool?.IsVisible == true;
            var anyModifier = s.SimultaneousLaser || s.SimultaneousSpotlight;
            if (open)
            {
                DK.Recolor(_multiButton, Tw.B(Tw.Purple600), Tw.B(Colors.White), Tw.B(Tw.Purple600), Tw.B(Colors.White), Tw.B(Tw.Purple400), Tw.B(Tw.Purple400));
                _multiButton.BorderThickness = new Thickness(1);
            }
            else if (anyModifier)
            {
                DK.Recolor(_multiButton, Tw.B(Tw.Purple950, 0.8), Tw.B(Tw.Purple300), Tw.B(Tw.Purple950, 0.8), Tw.B(Tw.Purple300), Tw.B(Tw.Purple600, 0.6), Tw.B(Tw.Purple600, 0.6));
                _multiButton.BorderThickness = new Thickness(1);
            }
            else
            {
                DK.Recolor(_multiButton, Tw.B(Colors.Transparent), "Ink.ToolbarMuted", "Ink.Hover", "Ink.Text200");
                _multiButton.BorderThickness = new Thickness(0);
            }
            var count = s.ActiveModifierCount;
            _multiBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            _multiBadgeText.Text = count.ToString();
        }
        if (_customizeButton is not null)
        {
            if (_customize?.IsVisible == true) DK.Recolor(_customizeButton, Tw.B(Tw.Blue600), Tw.B(Colors.White), Tw.B(Tw.Blue600), Tw.B(Colors.White));
            else DK.Recolor(_customizeButton, Tw.B(Colors.Transparent), "Ink.ToolbarMuted", "Ink.Hover", "Ink.Text200");
        }
    }

    // ------------------------------------------------------------------ Footer

    private StackPanel? _footerBadges;

    private void BuildFooter()
    {
        var presetName = _appSettings.Toolbar.Preset;
        var dot = DK.Dot(6, Tw.B(Tw.Blue400));
        if (_isHorizontal && !_collapsed)
        {
            var label = DK.Text(char.ToUpperInvariant(presetName[0]) + presetName[1..], 10, "Ink.Text400", mono: true);
            _footerBadges = DK.H(4, dot, label);
            var link = new TextBlock
            {
                Text = "customize", FontSize = 10, FontFamily = DK.Mono, Foreground = Tw.B(Tw.Blue400),
                TextDecorations = TextDecorations.Underline, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            link.MouseEnter += (_, _) => link.Foreground = Tw.B(Tw.Blue300);
            link.MouseLeave += (_, _) => link.Foreground = Tw.B(Tw.Blue400);
            link.MouseLeftButtonUp += (_, _) => ShowCustomize();
            Footer.Child = DK.Between(_footerBadges, link);
        }
        else
        {
            _footerBadges = DK.V(2, dot);
            dot.HorizontalAlignment = HorizontalAlignment.Center;
            _footerBadges.ToolTip = $"Preset: {presetName}";
            Footer.Child = _footerBadges;
        }
        BuildFooterBadges();
    }

    private void BuildFooterBadges()
    {
        if (_footerBadges is null) return;
        var s = _overlay.Settings;
        while (_footerBadges.Children.Count > (_isHorizontal && !_collapsed ? 2 : 1)) _footerBadges.Children.RemoveAt(_footerBadges.Children.Count - 1);
        void Badge(string text, Color fg, Color bg, Color border)
        {
            if (!_isHorizontal || _collapsed)
            {
                _footerBadges.Children.Add(DK.Dot(4, Tw.B(fg)));
                ((FrameworkElement)_footerBadges.Children[^1]).HorizontalAlignment = HorizontalAlignment.Center;
                return;
            }
            var chip = DK.Chip(text, Tw.B(bg), Tw.B(fg), Tw.B(border), 9);
            chip.Margin = new Thickness(4, 0, 0, 0);
            _footerBadges.Children.Add(chip);
        }
        if (s.SnapToGrid) Badge($"Snap:{s.GridSize}", Tw.Blue300, Tw.WithAlpha(Tw.Blue900, 0.6), Tw.WithAlpha(Tw.Blue700, 0.5));
        if (s.SimultaneousLaser) Badge("+Laser", Tw.Rose300, Tw.WithAlpha(Tw.Rose950, 0.8), Tw.WithAlpha(Tw.Rose700, 0.5));
        if (s.SimultaneousSpotlight) Badge("+Spotlight", Tw.Amber300, Tw.WithAlpha(Tw.Amber950, 0.8), Tw.WithAlpha(Tw.Amber700, 0.5));
    }

    private void OnOptionsChanged()
    {
        RefreshItemStates();
        if (_codeFocus.IsValueCreated && _codeFocus.Value.IsVisible)
            _codeFocus.Value.Update(_overlay.Settings.CodeFocusBandHeight, _overlay.Settings.CodeFocusDimOpacity);
        _multiTool?.Refresh();
    }

    // ------------------------------------------------------------------ Item actions

    private void OnItemClick(string id)
    {
        var s = _overlay.Settings;
        // Live zoom is a tool in the design: picking any other tool ends it. A zoomed area stays so it can be drawn on.
        if (IsLiveZoomActive && id is "cursor" or "select" or "pen" or "highlighter" or "eraser" or "shape" or "text" or "marker" or "laser" or "spotlight")
            SetZoomFactor(1);
        switch (id)
        {
            case "cursor": CloseMenus(); _overlay.SetTool(ToolKind.Cursor); break;
            case "select": ToolClick(s.Tool == ToolKind.Select, null, false, () => _overlay.SetTool(ToolKind.Select)); break;
            case "pen": ToolClick(s.Tool == ToolKind.Pen, "pen", true, () => _overlay.SetPenMode(_lastPenMode)); break;
            case "highlighter": ToolClick(s.Tool == ToolKind.Highlighter, "highlighter", false, () => _overlay.SetPenMode(_lastHighlighterMode)); break;
            case "eraser": ToolClick(s.Tool == ToolKind.Eraser, null, false, () => _overlay.SetTool(ToolKind.Eraser)); break;
            case "shape": ToolClick(s.Tool == ToolKind.Shape, "shape", true, () => _overlay.SetShape(s.Shape)); break;
            case "text": ToolClick(s.Tool == ToolKind.Text, "text", false, () => _overlay.SetTool(ToolKind.Text)); break;
            case "marker": ToolClick(s.Tool == ToolKind.NumberMarker, "marker", true, () => _overlay.SetTool(ToolKind.NumberMarker)); break;
            case "laser": ToolClick(s.Tool == ToolKind.Laser, "laser", false, () => _overlay.SetTool(ToolKind.Laser)); break;
            case "spotlight": ToolClick(s.Tool == ToolKind.Spotlight, "spotlight", false, () => _overlay.SetTool(ToolKind.Spotlight)); break;
            case "zoom": ToolClick(IsZoomActive, "zoom", false, StartZoom); break;
            case "board": ToggleInspector("board"); break;
            case "color": ToggleInspector("color"); break;
            case "more": ToggleInspector("more"); break;
            case "undo": _overlay.Undo(); break;
            case "redo": _overlay.Redo(); break;
            case "clear": _overlay.Clear(); Toast.Show("All drawings cleared (Undo brings them back)"); break;
            case "palette": OpenCommandPalette(); break;
            case "capability": OpenCapabilityCentre(); break;
            case "capture": CaptureRegionWithPreview(); break;
            case "record": ToggleRecording(); break;
            case "mirror": ToggleMirror(); break;
            case "focus": if (_overlay.IsFocusBoxActive) _overlay.HideFocusBox(); else FocusOnArea(); break;
            case "collapse": _collapsed = true; CloseMenus(); BuildStrip(); BuildFooter(); break;
        }
    }

    /// <summary>Design handleToolClick: clicking the active tool toggles its inspector; activating pen/shape opens it.</summary>
    private void ToolClick(bool alreadyActive, string? inspector, bool opensOnActivate, Action activate)
    {
        if (alreadyActive)
        {
            if (inspector is not null) ToggleInspector(inspector);
            return;
        }
        activate();
        if (opensOnActivate && inspector is not null) ShowInspector(inspector);
        else CloseMenus();
    }

    public void RememberPenMode(PenMode mode)
    {
        if (mode is PenMode.Highlighter or PenMode.StraightHighlighter) _lastHighlighterMode = mode;
        else _lastPenMode = mode;
    }

    private void TrackPenModes()
    {
        var s = _overlay.Settings;
        if (s.Tool == ToolKind.Pen) _lastPenMode = s.PenMode;
        if (s.Tool == ToolKind.Highlighter) _lastHighlighterMode = s.PenMode;
    }

    public void ShowInspector(string key)
    {
        if (_inspector is null) _inspector = new InspectorWindow(_overlay, this);
        _inspector.ShowCategory(key);
        RefreshItemStates();
        PaletteStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleInspector(string key)
    {
        if (IsPaletteOpen && _inspector!.CurrentCategory == key) CloseMenus();
        else ShowInspector(key);
    }

    /// <summary>Opens an inspector by legacy or design category name (used by commands).</summary>
    public void ShowInspectorCategory(string category) => ShowInspector(category.ToLowerInvariant() switch
    {
        "shapes" => "shape",
        "present" => "laser",
        var other => other
    });

    public void CloseMenus()
    {
        _inspector?.CloseInspector();
        RefreshItemStates();
        PaletteStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateColorChip() => RefreshItemStates();

    // ------------------------------------------------------------------ Drag & drop reordering

    private void TryBeginToolDrag(Button button, string id, MouseEventArgs e)
    {
        // Only the button that received this press can start a drag, and only while it is still pressed;
        // otherwise a fast click on another button would be swallowed as a reorder drag.
        if (_dragCandidate != id || e.LeftButton != MouseButtonState.Pressed || !button.IsPressed) return;
        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _dragStart.X) < 5 && Math.Abs(pos.Y - _dragStart.Y) < 5) return;
        _draggedId = _dragCandidate;
        _dragCandidate = null;
        RefreshItemStates();
        try
        {
            DragDrop.DoDragDrop(button, new System.Windows.DataObject(DataFormats.StringFormat, _draggedId), DragDropEffects.Move);
        }
        finally
        {
            _draggedId = null;
            _dropTargetId = null;
            RefreshItemStates();
        }
    }

    private void OnToolDragOver(Button button, string id, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
        var source = e.Data.GetData(DataFormats.StringFormat) as string;
        if (source is null || source == id) return;
        var pos = e.GetPosition(button);
        var after = _isHorizontal ? pos.X > button.ActualWidth / 2 : pos.Y > button.ActualHeight / 2;
        if (_dropTargetId == id && _dropAfter == after) return;
        _dropTargetId = id;
        _dropAfter = after;
        RefreshItemStates();
    }

    private void OnToolDrop(string targetId, DragEventArgs e)
    {
        e.Handled = true;
        var sourceId = e.Data.GetData(DataFormats.StringFormat) as string;
        if (sourceId is null || sourceId == targetId) return;
        MoveItem(sourceId, targetId, _dropAfter);
    }

    /// <summary>Moves <paramref name="sourceId"/> before/after <paramref name="targetId"/> (same index maths as the design).</summary>
    public void MoveItem(string sourceId, string targetId, bool after)
    {
        var sourceIndex = _layout.FindIndex(t => t.Id == sourceId);
        var targetIndex = _layout.FindIndex(t => t.Id == targetId);
        if (sourceIndex < 0 || targetIndex < 0) return;
        var moved = _layout[sourceIndex];
        _layout.RemoveAt(sourceIndex);
        var insert = after
            ? (sourceIndex < targetIndex ? targetIndex : targetIndex + 1)
            : (sourceIndex < targetIndex ? targetIndex - 1 : targetIndex);
        _layout.Insert(Math.Clamp(insert, 0, _layout.Count), moved);
        CommitLayout();
    }

    public void MoveItemBy(int index, int direction)
    {
        var target = index + direction;
        if (target < 0 || target >= _layout.Count) return;
        (_layout[index], _layout[target]) = (_layout[target], _layout[index]);
        CommitLayout();
    }

    public void ToggleItemVisibility(string id)
    {
        var item = _layout.FirstOrDefault(i => i.Id == id);
        if (item is null) return;
        item.Visible = !item.Visible;
        CommitLayout();
    }

    public void ApplyLayoutPreset(ToolbarPreset preset)
    {
        _layout = ToolbarCatalog.ApplyPreset(_layout, preset);
        CommitLayout();
    }

    public void ResetLayout()
    {
        _layout = ToolbarCatalog.DefaultLayout();
        _appSettings.Toolbar.Items = null;
        BuildStrip();
        SaveSettings();
        _customize?.Refresh();
    }

    private void CommitLayout()
    {
        _appSettings.Toolbar.Items = _layout.Select(i => new ToolbarItemState { Id = i.Id, Visible = i.Visible }).ToList();
        BuildStrip();
        SaveSettings();
        _customize?.Refresh();
    }

    // ------------------------------------------------------------------ Popovers

    private void ToggleMultiTool()
    {
        _multiTool ??= new MultiToolWindow(_overlay, this);
        if (_multiTool.IsVisible) _multiTool.Hide();
        else _multiTool.ShowNear(_multiButton!);
        RefreshItemStates();
    }

    internal void OnMultiToolClosed() => RefreshItemStates();

    private void ToggleCustomize()
    {
        if (_customize?.IsVisible == true) HideCustomize();
        else ShowCustomize();
    }

    private void ShowCustomize()
    {
        _customize ??= new CustomizeToolbarWindow(this);
        _customize.Refresh();
        _customize.Show();
        _customize.Reposition();
        RefreshItemStates();
    }

    internal void HideCustomize()
    {
        _customize?.Hide();
        RefreshItemStates();
    }

    // ------------------------------------------------------------------ Tool lifecycle

    public void EndCurrentTool()
    {
        CloseMenus();
        _multiTool?.Hide();
        if (_zoom.IsValueCreated) _zoom.Value.Reset();
        if (_staticZoom.IsValueCreated) _staticZoom.Value.Hide();
        if (_demoType.IsValueCreated) _demoType.Value.EmergencyStop();
        if (_curtain.IsValueCreated) _curtain.Value.Hide();
        if (_freeze.IsValueCreated) _freeze.Value.Unfreeze();
        if (_pointerEffects.IsValueCreated) _pointerEffects.Value.Hide();
        if (_blackout.IsValueCreated) _blackout.Value.Hide();
        if (_codeFocus.IsValueCreated) _codeFocus.Value.Hide();
        if (_overlay.CurrentBoard != BoardKind.Transparent) _overlay.SetBoardKind(BoardKind.Transparent);
        if (_overlay.Settings.CurtainProgress > 0) _overlay.UpdateOptions(o => o.CurtainProgress = 0, persist: false);
        SetTemporaryMode(false);
        _overlay.DeactivateCurrentTool(ToolDeactivationReason.Escape);
        RefreshItemStates();
    }

    /// <summary>Protected emergency release: also stops timers and HUDs (design handleEmergencyStop).</summary>
    public void EmergencyRelease()
    {
        EndCurrentTool();
        HideCustomize();
        if (_breakTimer.IsValueCreated) _breakTimer.Value.EmergencyStop();
        Toast.Show("Panic key: all tools and overlays closed");
    }

    private void SetTemporaryMode(bool active)
    {
        if (_temporaryModeActive == active) return;
        _temporaryModeActive = active;
        TemporaryModeChanged?.Invoke(this, active);
    }

    // ------------------------------------------------------------------ Zoom

    private bool _zoomEventsSubscribed;

    private void SubscribeZoomEvents()
    {
        if (_zoomEventsSubscribed || !_zoom.IsValueCreated) return;
        _zoomEventsSubscribed = true;
        _zoom.Value.StateChanged += (_, _) => Dispatcher.BeginInvoke(RefreshItemStates);
    }

    private Point CursorDip()
    {
        var p = FormsCursor.Position;
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Point(p.X / dpi.DpiScaleX, p.Y / dpi.DpiScaleY);
    }

    public void StartLiveZoom() => SetZoomFactor(Math.Max(1.5, _overlay.Settings.ZoomFactor));

    /// <summary>Magnifier button / Ctrl+Shift+5: zoom into a dragged area (default) or live-follow the mouse.</summary>
    public void StartZoom()
    {
        if (_overlay.Settings.ZoomFollowsMouse) StartLiveZoom();
        else ZoomToArea();
    }

    /// <summary>
    /// Drag a box around part of the screen; that part is enlarged to fill the monitor and stays put
    /// (it does not follow the mouse), ready to draw or highlight on. Esc or Cursor returns to normal.
    /// </summary>
    public void ZoomToArea()
    {
        CloseMenus();
        if (IsLiveZoomActive) SetZoomFactor(1);
        // Always pick from the real screen, not from an already zoomed picture.
        var penWasAutomatic = _zoomTurnedPenOn;
        _zoomTurnedPenOn = false;
        _overlay.ExitZoomArea();
        if (_capture.SelectRegion(this, "Drag a box around the part you want to zoom into   ·   Esc to cancel") is not { } area)
        {
            if (penWasAutomatic && _overlay.Settings.Tool != ToolKind.Cursor) _overlay.SetTool(ToolKind.Cursor);
            RefreshItemStates();
            return;
        }
        using (var bitmap = _capture.Capture(new CaptureRequest(CaptureTarget.Region, area)))
            _overlay.ShowZoomArea(_capture.ToImage(bitmap), area);
        if (_overlay.Settings.Tool == ToolKind.Cursor)
        {
            _overlay.SetPenMode(_lastPenMode);
            _zoomTurnedPenOn = true;
        }
        else _zoomTurnedPenOn = penWasAutomatic;
        RefreshItemStates();
    }

    // Set when zooming switched from Cursor to the pen, so leaving the zoom gives the normal mouse back.
    private bool _zoomTurnedPenOn;

    private void OnZoomAreaChanged()
    {
        if (!_overlay.IsZoomAreaActive && _zoomTurnedPenOn)
        {
            _zoomTurnedPenOn = false;
            if (_overlay.Settings.Tool != ToolKind.Cursor) _overlay.SetTool(ToolKind.Cursor);
        }
        RefreshItemStates();
    }

    /// <summary>Applies a magnifier factor (1x resets) around the cursor and remembers it.</summary>
    public void SetZoomFactor(double factor)
    {
        factor = Math.Clamp(factor, 1, 16);
        if (factor > 1) _overlay.UpdateOptions(o => o.ZoomFactor = factor);
        if (factor <= 1)
        {
            if (_zoom.IsValueCreated) _zoom.Value.Reset();
            SetTemporaryMode(false);
        }
        else
        {
            SetTemporaryMode(true);
            _zoom.Value.TrySetLiveZoom(factor, CursorDip());
            SubscribeZoomEvents();
        }
        RefreshItemStates();
    }

    public void ToggleZoom()
    {
        if (_overlay.IsZoomAreaActive) _overlay.ExitZoomArea();
        else if (IsZoomActive) SetZoomFactor(1);
        else StartZoom();
    }

    public void LiveZoomIn() => SetZoomFactor(ZoomFactor + 0.5);

    public void LiveZoomOut()
    {
        if (!IsZoomActive) return;
        SetZoomFactor(ZoomFactor - 0.5);
    }

    public void LiveZoomReset() => SetZoomFactor(1);

    public void FreezeScreen()
    {
        SetTemporaryMode(true);
        _freeze.Value.Freeze();
        _overlay.SetTool(ToolKind.Pen);
        CloseMenus();
    }

    public void TriggerStaticZoom()
    {
        if (_zoom.IsValueCreated && _zoom.Value.State != ZoomState.Inactive) _zoom.Value.Reset();
        CloseMenus();
        SetTemporaryMode(true);
        _staticZoom.Value.Show(2.0);
    }

    // ------------------------------------------------------------------ Presenter helpers

    public void StartBreakTimer(int minutes)
    {
        _appSettings.Presentation.BreakTimerMinutes = minutes;
        _breakTimer.Value.Start(TimeSpan.FromMinutes(minutes), resetMinutes: 5);
    }

    public void RunDemoType(string text) => _demoType.Value.Open(text);

    public void ToggleCodeFocus() =>
        _codeFocus.Value.Toggle(_overlay.Settings.CodeFocusBandHeight, _overlay.Settings.CodeFocusDimOpacity);

    private static string DrawingsFolder()
    {
        var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "InkIt");
        System.IO.Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Every page as one PDF, to send to learners after class.</summary>
    public void ExportPagesPdf()
    {
        CloseMenus();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export pages as PDF", Filter = "PDF document (*.pdf)|*.pdf", DefaultExt = ".pdf",
            InitialDirectory = DrawingsFolder(), FileName = $"InkIt notes {DateTime.Now:yyyy-MM-dd HH.mm}"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var pages = _overlay.RenderPages();
            PdfWriter.Write(dialog.FileName, pages);
            Toast.Show($"Exported {pages.Count} page{(pages.Count == 1 ? "" : "s")} to {System.IO.Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException)
        {
            Toast.Show("Could not save the PDF: " + ex.Message);
        }
    }

    public void SaveDrawings()
    {
        CloseMenus();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save drawings", Filter = Overlay.InkFile.Filter, DefaultExt = Overlay.InkFile.Extension,
            InitialDirectory = DrawingsFolder(), FileName = $"InkIt drawings {DateTime.Now:yyyy-MM-dd HH.mm}"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _overlay.SavePages(dialog.FileName);
            Toast.Show($"Saved {System.IO.Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            Toast.Show("Could not save: " + ex.Message);
        }
    }

    public void OpenDrawings()
    {
        CloseMenus();
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Open drawings", Filter = Overlay.InkFile.Filter, InitialDirectory = DrawingsFolder() };
        if (dialog.ShowDialog(this) != true) return;
        OpenDrawings(dialog.FileName);
    }

    public void OpenDrawings(string path)
    {
        try
        {
            _overlay.OpenPages(path);
            Toast.Show($"Opened {System.IO.Path.GetFileName(path)} ({_overlay.PageCount} page{(_overlay.PageCount == 1 ? "" : "s")})");
        }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or System.Windows.Markup.XamlParseException)
        {
            Toast.Show("That file could not be opened: " + ex.Message);
        }
    }

    /// <summary>Focus box: drag a box; everything else is dimmed while the apps underneath keep working.</summary>
    public void FocusOnArea()
    {
        CloseMenus();
        _overlay.HideFocusBox();
        if (_capture.SelectRegion(this, "Drag a box around the part to keep bright   ·   Esc to cancel") is not { } area) return;
        _overlay.ShowFocusBox(area);
        Toast.Show("Focus box on - your apps still work. Press Esc to remove it.");
        RefreshItemStates();
    }

    public void CaptureRegionWithPreview()
    {
        CloseMenus();
        if (_capture.SelectRegion(this, "Drag a box around what you want to screenshot   ·   Esc to cancel") is not { } area) return;
        var bitmap = _capture.Capture(new CaptureRequest(CaptureTarget.Region, area));
        var preview = new CapturePreviewWindow(bitmap, _capture) { Owner = this };
        preview.ShowDialog();
    }

    public void CaptureFullDesktopWithPreview()
    {
        CloseMenus();
        var bitmap = _capture.Capture(new CaptureRequest(CaptureTarget.VirtualDesktop));
        var preview = new CapturePreviewWindow(bitmap, _capture) { Owner = this };
        preview.ShowDialog();
    }

    // ------------------------------------------------------------------ Feedback, updates, tour

    private NoticeCard? _notice;
    private NoticeCard Notice => _notice ??= new NoticeCard(this);

    /// <summary>Off for QA/automation runs (--qa-capture).</summary>
    public bool ShowStartupExtras { get; set; } = true;

    private void Delay(TimeSpan delay, Action action)
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) => { timer.Stop(); action(); };
        timer.Start();
    }

    // ------------------------------------------------------------------ Links, Stream Deck and command line

    /// <summary>Friendly names for inkit:// links and --command (any command id also works, e.g. inkit://screen.zoom_area).</summary>
    public static readonly IReadOnlyDictionary<string, string> CommandAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["cursor"] = "tools.cursor", ["pen"] = "annot.pen", ["highlighter"] = "annot.highlighter", ["eraser"] = "annot.eraser",
        ["text"] = "annot.text", ["numbers"] = "annot.marker", ["select"] = "annot.select", ["shapes"] = "shape.hub",
        ["arrow"] = "shape.arrow", ["line"] = "shape.line", ["box"] = "shape.rectangle", ["circle"] = "shape.ellipse",
        ["laser"] = "present.laser", ["spotlight"] = "present.spotlight", ["focus"] = "present.focus_box",
        ["zoom"] = "screen.zoom_area", ["zoom-follow"] = "screen.zoom_follow", ["screenshot"] = "screen.capture_region",
        ["freeze"] = "screen.freeze_frame", ["whiteboard"] = "board.whiteboard", ["blackboard"] = "board.blackboard",
        ["grid"] = "board.grid", ["screen"] = "board.transparent", ["next-page"] = "board.next_page",
        ["previous-page"] = "board.previous_page", ["new-page"] = "board.new_page", ["export-pdf"] = "board.export_pdf",
        ["save"] = "file.save", ["open"] = "file.open", ["undo"] = "annot.undo", ["redo"] = "annot.redo",
        ["clear"] = "annot.clear", ["timer"] = "present.break_timer", ["keys"] = "present.key_visualizer",
        ["search"] = "tools.command_palette", ["features"] = "tools.capability_centre", ["settings"] = "tools.settings",
        ["tour"] = "tools.tour", ["record"] = "screen.record", ["mirror"] = "screen.mirror",
    };

    /// <summary>
    /// Arguments from a second launch or from start-up: --show, a .inkit file, inkit://&lt;command&gt;, --command &lt;command&gt;,
    /// inkit://color/&lt;1-6 or name&gt;.
    /// </summary>
    public void HandleExternalArguments(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i].Trim();
            if (arg.Length == 0) continue;
            if (arg is "--show" or "--startup") { if (arg == "--show") ShowFromTray(); continue; }
            if (arg == "--command" && i + 1 < args.Count) { RunNamedCommand(args[++i]); continue; }
            if (arg.StartsWith("inkit:", StringComparison.OrdinalIgnoreCase))
            {
                var path = arg["inkit:".Length..].TrimStart('/').TrimEnd('/');
                RunNamedCommand(Uri.UnescapeDataString(path));
                continue;
            }
            if (arg.EndsWith(Overlay.InkFile.Extension, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(arg))
            {
                ShowFromTray();
                OpenDrawings(arg);
            }
        }
    }

    private void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Topmost = false;
        Topmost = true;
    }

    /// <summary>
    /// Pen-tablet express keys and quick shortcuts: switch tool instantly, without opening panels, keeping the
    /// pen and highlighter styles last used. Names: cursor, pen, highlighter, eraser, arrow, circle, box, line, text,
    /// numbers, laser, spotlight.
    /// </summary>
    public bool SelectToolQuick(string tool)
    {
        CloseMenus();
        switch (tool.ToLowerInvariant())
        {
            case "cursor" or "pointer": EndCurrentTool(); break;
            case "pen": _overlay.SetPenMode(_lastPenMode); break;
            case "highlighter": _overlay.SetPenMode(_lastHighlighterMode); break;
            case "eraser": _overlay.SetTool(ToolKind.Eraser); break;
            case "arrow": _overlay.SetShape(ShapeKind.Arrow); break;
            case "circle": _overlay.SetShape(ShapeKind.Ellipse); break;
            case "box": _overlay.SetShape(ShapeKind.Rectangle); break;
            case "line": _overlay.SetShape(ShapeKind.Line); break;
            case "text": _overlay.SetTool(ToolKind.Text); break;
            case "numbers": _overlay.SetTool(ToolKind.NumberMarker); break;
            case "laser": _overlay.SetTool(ToolKind.Laser); break;
            case "spotlight": _overlay.SetTool(ToolKind.Spotlight); break;
            default: return false;
        }
        RefreshItemStates();
        return true;
    }

    public void RunNamedCommand(string name)
    {
        var parts = name.Split('/', 2);
        if (parts[0].Equals("tool", StringComparison.OrdinalIgnoreCase) && parts.Length == 2)
        {
            if (!SelectToolQuick(parts[1])) Toast.Show($"InkIt does not know the tool \"{parts[1]}\"");
            return;
        }
        if (parts[0].Equals("color", StringComparison.OrdinalIgnoreCase) && parts.Length == 2)
        {
            var match = int.TryParse(parts[1], out var n) && n is >= 1 and <= 6
                ? InspectorWindow.TeachingColors[n - 1]
                : InspectorWindow.TeachingColors.FirstOrDefault(c => c.Name.Equals(parts[1], StringComparison.OrdinalIgnoreCase));
            if (match.Hex is null) { Toast.Show($"InkIt does not know the colour \"{parts[1]}\""); return; }
            _overlay.SetColor(Tw.Hex(match.Hex));
            RefreshItemStates();
            return;
        }
        var id = CommandAliases.TryGetValue(parts[0], out var alias) ? alias : parts[0];
        if (id == "tools.cursor") { EndCurrentTool(); return; }
        if (_registry.Find(id) is { } command) command.Execute();
        else Toast.Show($"InkIt does not know the command \"{name}\"");
    }

    // ------------------------------------------------------------------ PowerPoint slides

    private SlideShowWatcher? _slides;

    /// <summary>Starts or stops keeping drawings with each PowerPoint slide (Settings > Spotlight & Focus).</summary>
    public void ApplyFollowSlides()
    {
        if (_appSettings.Presentation.FollowSlides)
        {
            if (_slides is null)
            {
                _slides = new SlideShowWatcher();
                _slides.SlideChanged += (key, point) => _overlay.ShowSlide(key, point);
                _slides.SlideShowEnded += _overlay.EndSlides;
            }
            _slides.Start();
        }
        else
        {
            _slides?.Stop();
        }
    }

    public bool FollowSlides
    {
        get => _appSettings.Presentation.FollowSlides;
        set
        {
            _appSettings.Presentation.FollowSlides = value;
            SaveSettings();
            ApplyFollowSlides();
            Toast.Show(value ? "Drawings now stay with each PowerPoint slide" : "Drawings no longer follow PowerPoint slides");
        }
    }

    // ------------------------------------------------------------------ Mirror to a second screen

    private readonly Lazy<MirrorService> _mirror = new(() => new MirrorService());
    public bool IsMirroring => _mirror.IsValueCreated && _mirror.Value.IsActive;

    /// <summary>Show the screen, an area or one window on the projector / second monitor (or stop showing it).</summary>
    public void ToggleMirror()
    {
        CloseMenus();
        if (IsMirroring) { _mirror.Value.Stop(); Toast.Show("Stopped showing on the other screen"); RefreshItemStates(); return; }
        if (MirrorService.TargetScreen() is null) { Toast.Show("Connect a projector or second screen first"); return; }
        if (!_mirror.IsValueCreated) _mirror.Value.Changed += (_, _) => Dispatcher.BeginInvoke(RefreshItemStates);
        Notice.Present("Cast", "Show on the other screen", "Pick what the audience sees. Your other windows and notes stay private on this screen.",
        [
            new NoticeAction("One window", MirrorOneWindow),
            new NoticeAction("Choose area", () =>
            {
                if (_capture.SelectRegion(this, "Drag a box around what the audience should see   ·   Esc to cancel") is { } area) StartMirror(area);
            }),
            new NoticeAction("Whole screen", () => StartMirror(System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).Bounds), Primary: true)
        ]);
    }

    private void StartMirror(System.Drawing.Rectangle area)
    {
        _mirror.Value.MirrorArea(area);
        Toast.Show("Showing on the other screen (with your drawings). Run Mirror again to stop.");
    }

    private void MirrorOneWindow()
    {
        var picked = WindowPicker.Pick("Click the window the audience should see   ·   Esc to cancel");
        if (picked == nint.Zero) return;
        _mirror.Value.MirrorWindow(picked);
        Toast.Show("Showing that window on the other screen. Run Mirror again to stop.");
    }

    // ------------------------------------------------------------------ Lesson recording

    private RecordingBar? _recording;
    public bool IsRecording => _recording?.IsRecording == true;

    /// <summary>Record button / inkit://record: start (choose screen or area) or stop the lesson recording.</summary>
    public void ToggleRecording()
    {
        CloseMenus();
        _recording ??= new RecordingBar(this);
        if (_recording.IsRecording) _ = _recording.StopAsync();
        else _recording.ShowSetup();
    }

    internal void OnRecordingChanged() => RefreshItemStates();

    internal void SaveAppSettings() => SaveSettings();

    internal System.Drawing.Rectangle? SelectArea(string hint) => _capture.SelectRegion(this, hint);

    internal void ShowNotice(string icon, string title, string message, IReadOnlyList<NoticeAction> actions) =>
        Notice.Present(icon, title, message, actions);

    /// <summary>Finishes a running recording so the video file is complete (called when InkIt exits).</summary>
    public void FinishRecording()
    {
        if (_recording?.IsRecording == true) _recording.StopAsync().GetAwaiter().GetResult();
    }

    public void SendFeedback() => Support.Links.Open(Support.Links.Review);
    public void ReportProblem() => Support.Links.Open(Support.Links.Problem);

    /// <summary>Looks for a newer release. Automatic checks run at most once a day and only when allowed in Settings.</summary>
    public async void CheckForUpdates(bool manual)
    {
        var advanced = _appSettings.Advanced;
        if (!manual)
        {
            if (!advanced.CheckForUpdates || AppInfo.Version == "0.0.0.0") return; // local, unreleased builds do not nag
            if (advanced.LastUpdateCheckUtc is { } last && DateTime.UtcNow - last < TimeSpan.FromHours(24)) return;
        }
        advanced.LastUpdateCheckUtc = DateTime.UtcNow;
        SaveSettings();
        var update = await Support.UpdateChecker.CheckAsync();
        if (update is null)
        {
            if (manual) Toast.Show($"You have the latest version ({AppInfo.Version})");
            return;
        }
        Notice.Present("Download", $"InkIt {update.Version} is available",
            $"You have {AppInfo.Version}. Download the new installer and run it; your settings are kept.",
            [
                new NoticeAction("What's new", () => Support.Links.Open(update.PageUrl)),
                new NoticeAction("Download", () => Support.Links.Open(Support.Links.LatestInstaller), Primary: true)
            ]);
    }

    private static readonly (string Id, string Icon, string Title, string Message)[] TourSteps =
    [
        ("pen", "PenLine", "Draw on anything", "Click the pen (or press P) and draw over any app. Keys 1-6 change the colour; rough circles and arrows turn into clean shapes."),
        ("cursor", "MousePointer2", "Back to normal", "Press Esc or click the pointer to use your apps again. Your drawings stay until you clear them."),
        ("zoom", "ZoomIn", "Zoom into an area", "Press Ctrl+Shift+5 and drag a box. That part fills the screen and stays put while you explain it."),
        ("capture", "Camera", "Screenshot and paste", "Press Ctrl+Shift+4 and drag a box. The picture is copied, ready to paste into Teams, PowerPoint or email."),
        ("palette", "Search", "Find anything", "Press Ctrl+K and type what you want. While drawing, right-click to open the tool wheel."),
    ];

    /// <summary>Five short cards pointing at the buttons people need first.</summary>
    public void StartTour() => ShowTourStep(0);

    private void ShowTourStep(int index)
    {
        CloseMenus();
        if (index >= TourSteps.Length) { FinishTour(); return; }
        var (id, icon, title, message) = TourSteps[index];
        _toolButtons.TryGetValue(id, out var anchor);
        var last = index == TourSteps.Length - 1;
        Notice.Present(icon, title, message,
            last
                ? [new NoticeAction("Done", FinishTour, Primary: true)]
                : [new NoticeAction("Skip tour", FinishTour), new NoticeAction("Next", () => ShowTourStep(index + 1), Primary: true)],
            anchor, $"{index + 1} of {TourSteps.Length}", FinishTour);
    }

    private void FinishTour()
    {
        if (_notice?.IsVisible == true && _notice.Step is not null) _notice.Hide();
        if (_appSettings.Advanced.TourCompleted) return;
        _appSettings.Advanced.TourCompleted = true;
        SaveSettings();
    }

    public void ExitApplication()
    {
        CloseMenus();
        System.Windows.Application.Current.Shutdown();
    }

    // ------------------------------------------------------------------ Hubs

    public void OpenCapabilityCentre()
    {
        CloseMenus();
        var win = new CapabilityCentreWindow(_registry, _appSettings, SaveSettings) { Owner = this };
        win.ShowDialog();
    }

    public void OpenCommandPalette()
    {
        CloseMenus();
        var win = new CommandPaletteWindow(_registry) { Owner = this };
        win.ShowDialog();
    }

    public void OpenRadialMenu()
    {
        CloseMenus();
        var win = new RadialMenuWindow(this) { Owner = this };
        win.Show();
    }

    public void OpenSettings()
    {
        CloseMenus();
        CreateSettingsWindow().ShowDialog();
        BuildFooter();
    }

    internal SettingsWindow CreateSettingsWindow() => new(_appSettings, _settingsStore, _overlay, HotkeysChanged, this) { Owner = this };

    public void SetPreset(string presetId)
    {
        _appSettings.Toolbar.Preset = presetId;
        _presets.SetActivePreset(presetId);
        SaveSettings();
        BuildFooter();
    }

    private void ApplyActivePreset()
    {
        var preset = _presets.ActivePreset;
        if (_registry.Find(preset.DefaultToolId) is { } defCmd) defCmd.Execute();
    }

    /// <summary>Runs a tool selection coming from the radial menu or a command.</summary>
    public void SelectTool(ToolKind tool)
    {
        switch (tool)
        {
            case ToolKind.Pen: _overlay.SetPenMode(_lastPenMode); break;
            case ToolKind.Highlighter: _overlay.SetPenMode(_lastHighlighterMode); break;
            case ToolKind.Shape: _overlay.SetShape(_overlay.Settings.Shape); break;
            default: _overlay.SetTool(tool); break;
        }
    }

    // ------------------------------------------------------------------ In-app shortcuts

    /// <summary>Keyboard shortcuts active while an InkIt surface has focus (design App.tsx key handler).</summary>
    public bool HandleShortcut(Key key, ModifierKeys modifiers)
    {
        var ctrl = modifiers.HasFlag(ModifierKeys.Control);
        var shift = modifiers.HasFlag(ModifierKeys.Shift);
        var alt = modifiers.HasFlag(ModifierKeys.Alt);
        if (modifiers == ModifierKeys.None && HandleQuickStyleKey(key)) return true;
        string? command = (ctrl, shift, alt, key) switch
        {
            (true, true, false, Key.P) or (true, false, false, Key.K) => "tools.command_palette",
            (true, true, false, Key.O) => "tools.orientation_toggle",
            (true, true, false, Key.G) => "canvas.snap_toggle",
            (true, true, false, Key.L) => "present.laser",
            (true, true, false, Key.S) => "present.spotlight",
            (true, true, false, Key.B) => "present.break_timer",
            (true, true, false, Key.K) => "present.key_visualizer",
            (true, true, false, Key.T) => "present.demo_type",
            (true, true, false, Key.D5) => "screen.zoom_toggle",
            (true, true, false, Key.D4) => "screen.capture_region",
            (true, true, false, Key.F) => "screen.freeze_frame",
            (true, true, false, Key.C) => "privacy.code_focus",
            (true, true, false, Key.U) => "privacy.curtain",
            (true, true, false, Key.X) => "privacy.blackout",
            (true, true, false, Key.Y) => "annot.redo",
            (true, false, false, Key.OemComma) => "tools.settings",
            (true, false, false, Key.S) => "file.save",
            (true, false, false, Key.O) => "file.open",
            (false, false, false, Key.PageDown) => "board.next_page",
            (false, false, false, Key.PageUp) => "board.previous_page",
            (false, true, true, Key.P) => "present.pointer_effects",
            (false, false, false, Key.F10) => "tools.capability_centre",
            (false, false, false, Key.F1) => "board.transparent",
            (false, false, false, Key.F2) => "board.whiteboard",
            (false, false, false, Key.F3) => "board.blackboard",
            (false, false, false, Key.F4) => "board.grid",
            (false, false, false, Key.P) => "annot.pen",
            (false, false, false, Key.H) => "annot.highlighter",
            (false, false, false, Key.E) => "annot.eraser",
            (false, false, false, Key.T) => "annot.text",
            (false, false, false, Key.N) => "annot.marker",
            (false, false, false, Key.V) => "annot.select",
            (false, false, false, Key.S) => "shape.hub",
            (false, false, false, Key.D) => "annot.disappearing_pen",
            (false, false, false, Key.L) => "shape.line",
            (false, false, false, Key.A) => "shape.arrow",
            (false, true, false, Key.A) => "shape.double_arrow",
            (false, false, false, Key.R) => "shape.rectangle",
            (false, true, false, Key.R) => "shape.rounded_rect",
            (false, false, false, Key.O) => "shape.ellipse",
            (false, true, false, Key.D) => "shape.diamond",
            (false, false, false, Key.F) => "shape.fill_toggle",
            (false, false, false, Key.M) => "present.spotlight",
            _ => null
        };
        if (command is null || _registry.Find(command) is not { } item) return false;
        item.Execute();
        return true;
    }

    /// <summary>
    /// While drawing: 1-6 pick the six teaching colours, [ and ] make the stroke thinner or thicker.
    /// </summary>
    private bool HandleQuickStyleKey(Key key)
    {
        var s = _overlay.Settings;
        if (s.Tool is ToolKind.Cursor or ToolKind.Select or ToolKind.Eraser or ToolKind.Laser or ToolKind.Spotlight) return false;
        var colourIndex = key switch
        {
            >= Key.D1 and <= Key.D6 => key - Key.D1,
            >= Key.NumPad1 and <= Key.NumPad6 => key - Key.NumPad1,
            _ => -1
        };
        if (colourIndex >= 0)
        {
            var (name, hex) = InspectorWindow.TeachingColors[colourIndex];
            _overlay.SetColor(Tw.Hex(hex));
            Toast.Show($"Colour: {name}");
            RefreshItemStates();
            return true;
        }
        if (key is Key.OemOpenBrackets or Key.OemCloseBrackets)
        {
            var step = s.Thickness >= 10 ? 2 : 1;
            var thickness = Math.Clamp(s.Thickness + (key == Key.OemCloseBrackets ? step : -step), 1, 60);
            _overlay.SetThickness(thickness);
            Toast.Show($"Thickness: {thickness:0} px");
            return true;
        }
        return false;
    }
}

/// <summary>Floating windows that are larger than their visible chrome report only the chrome as UI.</summary>
public interface IChromeHost
{
    bool IsPointOverChrome(Point screenPixelPoint);
}

internal static class AutomationPropertiesHelper
{
    public static void SetName(DependencyObject element, string name) =>
        System.Windows.Automation.AutomationProperties.SetName(element, name);
}
