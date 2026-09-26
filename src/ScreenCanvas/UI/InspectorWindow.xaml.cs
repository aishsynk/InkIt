using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenCanvas.Core;
using ScreenCanvas.Overlay;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using VerticalAlignment = System.Windows.VerticalAlignment;
using FontStyle = System.Windows.FontStyle;

namespace ScreenCanvas.UI;

/// <summary>Attached contextual inspector (design: Inspectors.tsx, a 288px card beside the floating toolbar).</summary>
public partial class InspectorWindow : Window
{
    private readonly IOverlayManager _overlay;
    private readonly ToolbarWindow _owner;
    private string? _category;
    private bool _applying;

    private static readonly (PenMode Mode, string Name, string Description)[] PenModes =
    [
        (PenMode.Ballpoint, "Ballpoint", "Standard sharp vector line"),
        (PenMode.Calligraphy, "Calligraphy", "Angle-chiseled ribbon line"),
        (PenMode.Fountain, "Fountain", "Liquid pressure-sensitive ink"),
        (PenMode.Marker, "Marker", "Broad felt tip ink"),
        (PenMode.Pencil, "Pencil", "Textured graphite sketch"),
        (PenMode.Brush, "Brush", "Flowing artistic paint"),
        (PenMode.Airbrush, "Airbrush", "Soft diffused spray edge"),
        (PenMode.Chalk, "Chalk", "Porous chalkboard line"),
        (PenMode.Crayon, "Crayon", "Wax textured stroke"),
        (PenMode.Glow, "Neon Glow", "High-intensity luminous glow"),
        (PenMode.Rainbow, "Rainbow", "Chromatic shifting spectrum"),
        (PenMode.Disappearing, "Disappearing", "Auto-fades after 3 seconds"),
        (PenMode.FeltTip, "Felt Tip", "Soft round felt nib"),
        (PenMode.Dashed, "Dashed", "Broken dash annotation line"),
        (PenMode.Dotted, "Dotted", "Evenly spaced dot trail"),
    ];

    private static readonly (ShapeKind Kind, string Name, string Icon)[] ShapeKinds =
    [
        (ShapeKind.Line, "Line", "Minus"),
        (ShapeKind.Arrow, "Arrow", "ArrowRight"),
        (ShapeKind.DoubleArrow, "Double Arrow", "MoveHorizontal"),
        (ShapeKind.Rectangle, "Rectangle", "Square"),
        (ShapeKind.RoundedRectangle, "Rounded Rect", "SquareDot"),
        (ShapeKind.Ellipse, "Ellipse", "Circle"),
        (ShapeKind.Diamond, "Diamond", "Diamond"),
    ];

    public static readonly (string Name, string Hex)[] TeachingColors =
    [
        ("Blue", "#2563EB"), ("Red", "#E5484D"), ("Green", "#16A34A"),
        ("Amber", "#F2B705"), ("Purple", "#7C3AED"), ("Adaptive", "#0F172A")
    ];

    public string? CurrentCategory => _category;

    public InspectorWindow(IOverlayManager overlay, ToolbarWindow owner)
    {
        InitializeComponent();
        _overlay = overlay;
        _owner = owner;
        Owner = owner;
        Closing += (_, e) => { e.Cancel = true; CloseInspector(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && Keyboard.FocusedElement is not System.Windows.Controls.TextBox)
            {
                e.Handled = true;
                _owner.EndCurrentTool();
            }
        };
        _overlay.OptionsChanged += (_, _) => Dispatcher.BeginInvoke(RebuildIfExternal);
        _overlay.ToolChanged += (_, _) => Dispatcher.BeginInvoke(RebuildIfExternal);
        Settings.ThemeManager.ThemeChanged += (_, _) => Dispatcher.BeginInvoke(() => { if (IsVisible) Rebuild(); });
    }

    public void ShowCategory(string category)
    {
        _category = category.ToLowerInvariant();
        Rebuild();
        if (!IsVisible)
        {
            Opacity = 0;
            Show();
            BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        }
        UpdateLayout();
        Reposition();
    }

    public void CloseInspector()
    {
        _category = null;
        Hide();
    }

    private void RebuildIfExternal()
    {
        if (_applying || !IsVisible || _category is null) return;
        Rebuild();
    }

    /// <summary>Runs an options change from a control without rebuilding (keeps slider drags alive).</summary>
    private void Apply(Action action)
    {
        _applying = true;
        try { action(); }
        finally { _applying = false; }
    }

    // ------------------------------------------------------------------ Positioning

    public void Reposition()
    {
        if (!IsVisible || PresentationSource.FromVisual(_owner.ToolbarChrome) is null) return;
        var chrome = _owner.ToolbarChrome;
        var topLeft = chrome.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(_owner);
        var x = topLeft.X / dpi.DpiScaleX;
        var y = topLeft.Y / dpi.DpiScaleY;
        var w = chrome.ActualWidth;
        var h = chrome.ActualHeight;
        var area = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)topLeft.X, (int)topLeft.Y)).WorkingArea;
        var screen = new Rect(area.Left / dpi.DpiScaleX, area.Top / dpi.DpiScaleY, area.Width / dpi.DpiScaleX, area.Height / dpi.DpiScaleY);
        var margin = InspectorChrome.Margin;
        var cardW = InspectorChrome.ActualWidth > 0 ? InspectorChrome.ActualWidth : 288;
        var cardH = InspectorChrome.ActualHeight > 0 ? InspectorChrome.ActualHeight : 360;

        double cardLeft, cardTop;
        if (_owner.IsHorizontal)
        {
            cardLeft = x;
            cardTop = y + h + 8;
            if (cardTop + cardH > screen.Bottom - 8) cardTop = y - cardH - 8;
        }
        else
        {
            cardLeft = x + w + 12;
            if (cardLeft + cardW > screen.Right - 8) cardLeft = x - cardW - 12;
            cardTop = y;
        }
        cardLeft = Math.Clamp(cardLeft, screen.Left + 8, Math.Max(screen.Left + 8, screen.Right - cardW - 8));
        cardTop = Math.Clamp(cardTop, screen.Top + 8, Math.Max(screen.Top + 8, screen.Bottom - cardH - 8));
        Left = cardLeft - margin.Left;
        Top = cardTop - margin.Top;
    }

    public bool IsPointOverUi(Point screenPixelPoint)
    {
        if (!IsVisible || PresentationSource.FromVisual(InspectorChrome) is null) return false;
        try
        {
            var topLeft = InspectorChrome.PointToScreen(new Point(0, 0));
            var dpi = VisualTreeHelper.GetDpi(InspectorChrome);
            return new Rect(topLeft.X, topLeft.Y, InspectorChrome.ActualWidth * dpi.DpiScaleX, InspectorChrome.ActualHeight * dpi.DpiScaleY).Contains(screenPixelPoint);
        }
        catch (InvalidOperationException) { return false; }
    }

    // ------------------------------------------------------------------ Content

    private void Rebuild()
    {
        var s = _overlay.Settings;
        HeaderTitle.Text = _category switch
        {
            "pen" => "PEN",
            "shape" => "SHAPE & GEOMETRY",
            "color" => "COLOURS",
            "laser" => "PRESENTER & FOCUS",
            "spotlight" => "SPOTLIGHT LENS",
            "zoom" => "ZOOM",
            "board" => "BOARD CANVAS",
            "grid" => "GRID & ALIGNMENT",
            "highlighter" => "HIGHLIGHTER",
            "text" => "TEXT ANNOTATION",
            "more" => "MORE TOOLS",
            _ => string.Empty
        };
        BuildHeaderActions(s);
        Body.Content = _category switch
        {
            "pen" => BuildPen(s),
            "shape" => BuildShape(s),
            "color" => BuildColor(s),
            "laser" or "spotlight" => BuildPresenter(s),
            "zoom" => BuildZoom(),
            "board" => BuildBoard(s),
            "grid" => BuildGrid(s),
            "highlighter" => BuildHighlighter(s),
            "text" => BuildText(s),
            "more" => BuildMore(),
            _ => null
        };
        if (IsVisible) Dispatcher.BeginInvoke(Reposition, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void BuildHeaderActions(ToolSettings s)
    {
        HeaderActions.Children.Clear();
        var on = s.SnapToGrid;
        var snapContent = DK.H(4, new LucideIcon("Magnet", 12), DK.Plain(on ? "Snap ON" : "Snap", 10, on ? FontWeights.Bold : FontWeights.Normal, mono: true));
        var snap = on
            ? DK.Button(snapContent, Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), 4, new Thickness(6, 4, 6, 4), Tw.Blue400, Tw.Blue400, 1)
            : DK.Button(snapContent, "Ink.Control", "Ink.Text400", "Ink.Control", "Ink.Text200", 4, new Thickness(6, 4, 6, 4));
        snap.ToolTip = $"Snap to Grid: {(on ? "Active (ON)" : "Disabled (OFF)")}";
        snap.Click += (_, _) => _overlay.UpdateOptions(o => o.SnapToGrid = !o.SnapToGrid);
        var close = DK.IconButton("X", 14, "Ink.Text500", "Ink.Text", "Ink.Hover", 4, 8);
        close.Margin = new Thickness(4, 0, 0, 0);
        close.Click += (_, _) => _owner.CloseMenus();
        HeaderActions.Children.Add(snap);
        HeaderActions.Children.Add(close);
    }

    // ---- Pen -----------------------------------------------------------------

    private UIElement BuildPen(ToolSettings s)
    {
        var thickness = DK.SliderBlock("Stroke Thickness", 1, 32, 1, Math.Clamp(s.Thickness, 1, 32), v => $"{v:0}px", Tw.Blue400,
            v => Apply(() => _overlay.SetThickness(v)));
        ((Slider)thickness.Children[1]).Foreground = Tw.B(Tw.Blue500);
        var opacity = DK.SliderBlock("Opacity", 0.1, 1.0, 0.05, Math.Clamp(s.Opacity / 255d, 0.1, 1), v => $"{Math.Round(v * 100)}%", Tw.Blue400,
            v => Apply(() => _overlay.SetOpacity((byte)Math.Round(v * 255))));
        ((Slider)opacity.Children[1]).Foreground = Tw.B(Tw.Blue500);

        var pressure = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 1),
            Padding = new Thickness(0, 4, 0, 4),
            Child = DK.Between(DK.Text("Pressure Sensitivity", 12, "Ink.Text300"),
                DK.Check(s.PressureEnabled, v => Apply(() => _overlay.SetPressureEnabled(v)), Tw.Blue500))
        };
        pressure.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");

        var smartText = DK.V(0, DK.Text("Smart Shapes", 12, "Ink.Text300"),
            DK.Text("Rough circles, boxes, triangles and arrows become clean shapes. Undo once to keep your freehand drawing.", 10, "Ink.Text400").Wrap());
        smartText.Margin = new Thickness(0, 0, 12, 0);
        var smartSwitch = DK.Switch(s.AutoShapeAssist, v => _overlay.UpdateOptions(o => o.AutoShapeAssist = v), Tw.Purple500);
        smartSwitch.VerticalAlignment = VerticalAlignment.Top;
        smartSwitch.ToolTip = "Turn smart shapes on or off";
        var smart = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        smart.Children.Add(smartText);
        Grid.SetColumn(smartSwitch, 1);
        smart.Children.Add(smartSwitch);

        var cards = PenModes.Select(m =>
        {
            var selected = _owner.LastPenMode == m.Mode;
            var content = DK.V(0,
                DK.Text(m.Name, 11, selected ? Tw.B(System.Windows.Media.Colors.White) : "Ink.Text300", FontWeights.Medium),
                DK.Text(m.Description, 9, selected ? Tw.B(Tw.Blue100) : "Ink.Text400"));
            var card = selected
                ? DK.Button(content, Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), 4, new Thickness(6))
                : DK.Button(content, "Ink.Raised", "Ink.Text300", "Ink.Hover", "Ink.Text300", 4, new Thickness(6));
            card.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            card.Click += (_, _) => { _overlay.SetPenMode(m.Mode); _owner.RememberPenMode(m.Mode); };
            return (UIElement)card;
        }).ToList();
        var grid = DK.Columns(2, 4, cards);
        var scroll = new ScrollViewer
        {
            Style = (Style)FindResource("Ink.ScrollViewer"),
            MaxHeight = 144,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(0, 0, 6, 0), Child = grid }
        };
        var modes = DK.V(6, DK.Text($"Pen Mode ({PenModes.Length})", 12, "Ink.Text400"), scroll);
        return DK.V(12, thickness, opacity, pressure, smart, modes);
    }

    // ---- Shape ---------------------------------------------------------------

    private UIElement BuildShape(ToolSettings s)
    {
        var buttons = ShapeKinds.Select(k =>
        {
            var selected = s.Shape == k.Kind;
            var content = DK.IconLabel(k.Icon, 14, k.Name, 12);
            var btn = selected
                ? DK.Button(content, Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), 12, new Thickness(8))
                : DK.Button(content, "Ink.Raised", "Ink.Text300", "Ink.Hover", "Ink.Text300", 12, new Thickness(8));
            if (selected) btn.Effect = DK.Shadow(10, 3, 0.35);
            btn.HorizontalContentAlignment = HorizontalAlignment.Left;
            btn.Click += (_, _) => _overlay.SetShape(k.Kind);
            return (UIElement)btn;
        }).ToList();
        var select = DK.V(6, DK.Text("Select Shape", 12, "Ink.Text400"), DK.Columns(2, 6, buttons));

        var thickness = DK.SliderBlock("Border Thickness", 1, 24, 1, Math.Clamp(s.Thickness, 1, 24), v => $"{v:0}px", Tw.Blue400,
            v => Apply(() => _overlay.SetThickness(v)));
        ((Slider)thickness.Children[1]).Foreground = Tw.B(Tw.Blue500);

        var fill = DK.TopRule(DK.Between(
            DK.H(6, new LucideIcon("PaintBucket", 14) { Foreground = Tw.B(Tw.Blue400) }, DK.Text("Fill Interior (20% tint)", 12, "Ink.Text300")),
            DK.Check(s.ShapeFillEnabled, v => Apply(() => _overlay.SetShapeFill(v)), Tw.Blue500)), 4);

        return DK.V(12, select, thickness, fill, DK.TopRule(SnapBlock(s, "Snap to Grid", "Grid Spacing", true)));
    }

    /// <summary>Snap-to-grid switch with the spacing chips that appear when it is on.</summary>
    private UIElement SnapBlock(ToolSettings s, string title, string spacingLabel, bool guidesOption)
    {
        var header = DK.Between(
            DK.H(6, new LucideIcon("Magnet", 14) { Foreground = Tw.B(Tw.Blue400) }, DK.Text(title, 12, "Ink.Text200", FontWeights.Medium)),
            DK.Switch(s.SnapToGrid, v => _overlay.UpdateOptions(o => o.SnapToGrid = v), Tw.Blue600));
        if (!s.SnapToGrid) return header;

        var chips = new[] { 10, 20, 30, 40, 50 }.Select(size =>
        {
            var selected = s.GridSize == size;
            var text = DK.Plain($"{size}px", 10, selected ? FontWeights.Bold : FontWeights.Normal, mono: true, center: true);
            var chip = selected
                ? DK.Button(text, Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), 4, new Thickness(0, 4, 0, 4))
                : DK.Button(text, "Ink.Raised70", "Ink.Text400", "Ink.ControlHover", "Ink.Text", 4, new Thickness(0, 4, 0, 4));
            chip.Click += (_, _) => _overlay.UpdateOptions(o => o.GridSize = size);
            return (UIElement)chip;
        }).ToList();

        var inner = DK.V(8,
            DK.Between(DK.Text(spacingLabel, 11, "Ink.Text400"), DK.Text($"{s.GridSize}px", 11, Tw.B(Tw.Blue400), FontWeights.SemiBold, mono: true)),
            DK.Columns(5, 4, chips));
        if (guidesOption)
        {
            var guides = DK.Between(
                DK.H(4, new LucideIcon("Crosshair", 12) { Foreground = Tw.B(Tw.Slate400) }, DK.Text("Magnetic Snap Guides", 11, "Ink.Text300")),
                DK.Check(s.ShowGridGuides, v => Apply(() => _overlay.UpdateOptions(o => o.ShowGridGuides = v)), Tw.Blue500));
            guides.Margin = new Thickness(0, 4, 0, 0);
            inner.Children.Add(guides);
        }
        var nested = new Border
        {
            BorderThickness = new Thickness(2, 0, 0, 0),
            BorderBrush = Tw.B(Tw.Blue500, 0.4),
            Padding = new Thickness(6, 0, 0, 0),
            Margin = new Thickness(4, 4, 0, 0),
            Child = inner
        };
        return DK.V(8, header, nested);
    }

    // ---- Color ---------------------------------------------------------------

    private UIElement BuildColor(ToolSettings s)
    {
        var intro = DK.Text("High-contrast colours that stay readable on slides, code and video:", 11, "Ink.Text400").Wrap();
        var current = $"#{s.Color.R:X2}{s.Color.G:X2}{s.Color.B:X2}";
        var cards = TeachingColors.Select(c =>
        {
            var selected = string.Equals(current, c.Hex, StringComparison.OrdinalIgnoreCase);
            var color = Tw.Hex(c.Hex);
            var swatch = new Border
            {
                Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(color),
                BorderBrush = Tw.B(System.Windows.Media.Colors.Black, 0.3), BorderThickness = new Thickness(1),
                Child = selected ? new LucideIcon("Check", 14) { Foreground = Tw.B(System.Windows.Media.Colors.White), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } : null
            };
            var name = DK.Text(c.Name, 10, "Ink.Text300", FontWeights.Medium);
            var hex = DK.Text(c.Hex, 9, "Ink.Text500", mono: true);
            name.HorizontalAlignment = hex.HorizontalAlignment = swatch.HorizontalAlignment = HorizontalAlignment.Center;
            var content = DK.V(4, swatch, name, hex);
            var card = selected
                ? DK.Button(content, "Ink.Control", "Ink.Text300", "Ink.Control", "Ink.Text300", 12, new Thickness(8), Tw.B(System.Windows.Media.Colors.White), Tw.B(System.Windows.Media.Colors.White), 1)
                : DK.Button(content, "Ink.Raised40", "Ink.Text300", "Ink.Hover", "Ink.Text300", 12, new Thickness(8), "Ink.BorderStrong", "Ink.BorderStrong", 1);
            if (selected) card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Tw.Blue500, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.9 };
            card.Click += (_, _) => { _overlay.SetColor(color); _owner.RefreshItemStates(); };
            return (UIElement)card;
        }).ToList();

        var hexBox = DK.Input(current, "#RRGGBB", Tw.B(Tw.Blue500), 4, 12, mono: true);
        hexBox.Padding = new Thickness(6, 3, 6, 3);
        hexBox.TextChanged += (_, _) =>
        {
            try
            {
                var text = hexBox.Text.Trim();
                if (!text.StartsWith('#') || text.Length is not (7 or 9)) return;
                var parsed = (Color)System.Windows.Media.ColorConverter.ConvertFromString(text);
                Apply(() => _overlay.SetColor(Color.FromRgb(parsed.R, parsed.G, parsed.B)));
                _owner.RefreshItemStates();
            }
            catch (FormatException) { }
        };
        var custom = new DockPanel();
        var label = DK.Text("Custom Hex:", 12, "Ink.Text400");
        label.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(label, Dock.Left);
        custom.Children.Add(label);
        custom.Children.Add(hexBox);
        return DK.V(12, intro, DK.Columns(3, 8, cards), DK.TopRule(custom));
    }

    // ---- Presenter (laser / spotlight) ------------------------------------------

    private UIElement BuildPresenter(ToolSettings s)
    {
        StackPanel Block(string label, double min, double max, double value, Func<double, string> fmt, Color valueColor, Color accent, Action<double> changed)
        {
            var block = DK.SliderBlock(label, min, max, 1, value, fmt, Tw.B(valueColor), changed);
            ((Slider)block.Children[1]).Foreground = Tw.B(accent);
            return block;
        }

        var radius = Block("Spotlight Lens Radius", 60, 350, Math.Clamp(s.SpotlightRadius, 60, 350), v => $"{v:0}px", Tw.Amber400, Tw.Amber500,
            v => Apply(() => _overlay.UpdateOptions(o => o.SpotlightRadius = v)));
        var slit = Block("Code Focus Slit Height", 40, 300, Math.Clamp(s.CodeFocusBandHeight, 40, 300), v => $"{v:0}px", Tw.Blue400, Tw.Blue500,
            v => Apply(() => _overlay.UpdateOptions(o => o.CodeFocusBandHeight = v)));
        var curtain = Block("Stage Curtain Drape", 0, 100, Math.Clamp(s.CurtainProgress, 0, 100), v => $"{v:0}%", Tw.Purple400, Tw.Purple500,
            v => Apply(() => _overlay.UpdateOptions(o => o.CurtainProgress = v, persist: false)));

        var timers = new[] { 5, 10, 15 }.Select(minutes =>
        {
            var text = DK.Plain($"{minutes} min", 12, FontWeights.Medium, center: true);
            var b = DK.Button(text, "Ink.Control", "Ink.Text200", "Ink.ControlHover", "Ink.Text200", 8, new Thickness(0, 6, 0, 6));
            b.Click += (_, _) => _owner.StartBreakTimer(minutes);
            return (UIElement)b;
        }).ToList();
        var timer = DK.TopRule(DK.V(6, DK.Text("Start Break Timer", 12, "Ink.Text400"), DK.Columns(3, 6, timers)));

        var demo = DK.Button(DK.IconLabel("Terminal", 14, "Type Code Snippet", 12, FontWeights.Medium),
            Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), Tw.Blue500, Tw.B(System.Windows.Media.Colors.White), 8, new Thickness(0, 6, 0, 6));
        demo.Click += (_, _) => _owner.RunDemoType("// Live Demo Script\nconst canvas = new OverlayCanvas();\ncanvas.enableHardwareAcceleration();\nconsole.log(\"Zero-NuGet verified!\");");
        var demoBlock = DK.TopRule(DK.V(6, DK.Text("DemoType Simulator", 12, "Ink.Text400"), demo));

        return DK.V(12, radius, slit, curtain, timer, demoBlock);
    }

    // ---- Zoom ------------------------------------------------------------------

    private UIElement BuildZoom()
    {
        var follows = _overlay.Settings.ZoomFollowsMouse;
        var modes = new (bool Follow, string Name, string Description)[]
        {
            (false, "Zoom into an area", "Drag a box. That part fills the screen and stays put, ready to draw on."),
            (true, "Follow the mouse", "Live magnifier that moves wherever the mouse goes."),
        };
        var modeCards = DK.V(6, modes.Select(m =>
        {
            var selected = follows == m.Follow;
            var content = DK.V(0, DK.Text(m.Name, 12, selected ? "Ink.Text" : "Ink.Text300", FontWeights.Medium), DK.Text(m.Description, 10, "Ink.Text400").Wrap());
            var card = selected
                ? DK.Button(content, "Ink.Selected", "Ink.Text", "Ink.Selected", "Ink.Text", 12, new Thickness(8), Tw.B(Tw.Indigo500), Tw.B(Tw.Indigo500), 1)
                : DK.Button(content, "Ink.Raised40", "Ink.Text300", "Ink.Hover", "Ink.Text300", 12, new Thickness(8), "Ink.Divider", "Ink.Divider", 1);
            card.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            card.Click += (_, _) =>
            {
                if (_overlay.Settings.ZoomFollowsMouse == m.Follow) return;
                _overlay.UpdateOptions(o => o.ZoomFollowsMouse = m.Follow);
                if (_owner.IsZoomActive) { _owner.ToggleZoom(); _owner.StartZoom(); }
                Rebuild();
            };
            return (UIElement)card;
        }).ToArray());

        if (!follows)
        {
            var pick = DK.Button(DK.IconLabel("Crop", 14, _overlay.IsZoomAreaActive ? "Zoom another area" : "Choose area to zoom", 12, spacing: 6),
                Tw.Indigo600, Tw.B(System.Windows.Media.Colors.White), Tw.B(Tw.Indigo500), Tw.B(System.Windows.Media.Colors.White), 8, new Thickness(10, 7, 10, 7));
            pick.HorizontalContentAlignment = HorizontalAlignment.Center;
            pick.Click += (_, _) => _owner.ZoomToArea();
            var parts = new List<UIElement> { modeCards, pick };
            if (_overlay.IsZoomAreaActive)
            {
                var exit = DK.Button(DK.IconLabel("X", 14, "Exit zoom (Esc)", 12, spacing: 6), "Ink.Control", "Ink.Text200", "Ink.ControlHover", "Ink.Text", 8, new Thickness(10, 7, 10, 7));
                exit.HorizontalContentAlignment = HorizontalAlignment.Center;
                exit.Click += (_, _) => { _overlay.ExitZoomArea(); Rebuild(); };
                parts.Add(exit);
            }
            parts.Add(DK.Text("Tip: press Ctrl+Shift+5 any time to zoom, and Esc to go back.", 10, "Ink.Text400").Wrap());
            return DK.V(10, parts.ToArray());
        }

        var factor = _owner.ZoomFactor;
        var intro = DK.Text("How much to enlarge while following the mouse:", 11, "Ink.Text400").Wrap();
        var slider = DK.SliderBlock("Zoom Factor", 1, 16, 0.5, Math.Clamp(factor, 1, 16), v => $"{v:0.#}x", Tw.B(Tw.Indigo400),
            v => Apply(() => _owner.SetZoomFactor(v)), FontWeights.Bold);
        ((Slider)slider.Children[1]).Foreground = Tw.B(Tw.Indigo500);
        var presets = new[] { 1d, 2d, 4d, 8d }.Select(f =>
        {
            var selected = Math.Abs(factor - f) < 0.01;
            var text = DK.Plain($"{f:0}x", 12, FontWeights.Medium, mono: true, center: true);
            var b = selected
                ? DK.Button(text, Tw.Indigo600, Tw.B(System.Windows.Media.Colors.White), Tw.Indigo600, Tw.B(System.Windows.Media.Colors.White), 4, new Thickness(0, 4, 0, 4))
                : DK.Button(text, "Ink.Control", "Ink.Text300", "Ink.ControlHover", "Ink.Text300", 4, new Thickness(0, 4, 0, 4));
            b.Click += (_, _) => { _owner.SetZoomFactor(f); Rebuild(); };
            return (UIElement)b;
        }).ToList();
        var chips = DK.Columns(4, 4, presets);
        chips.Margin = new Thickness(0, 4, 0, 0);
        return DK.V(12, modeCards, intro, slider, chips);
    }

    // ---- Board -------------------------------------------------------------------

    private UIElement BuildBoard(ToolSettings s)
    {
        var intro = DK.Text("Switch the underlying canvas surface for lectures or notes:", 11, "Ink.Text400").Wrap();
        var options = new (BoardKind Kind, string Name, string Description)[]
        {
            (BoardKind.Transparent, "Transparent Desktop", "Annotate directly over running apps"),
            (BoardKind.Whiteboard, "Whiteboard", "Clean white background for lecture diagrams"),
            (BoardKind.Blackboard, "Blackboard", "High-contrast dark chalkboard"),
            (BoardKind.Grid, "Engineering Grid", "Coordinate blueprint grid paper"),
        };
        var cards = DK.V(6, options.Select(o =>
        {
            var selected = _overlay.CurrentBoard == o.Kind;
            var content = DK.V(0, DK.Text(o.Name, 12, selected ? "Ink.Text" : "Ink.Text300", FontWeights.Medium), DK.Text(o.Description, 10, "Ink.Text400"));
            var card = selected
                ? DK.Button(content, "Ink.Selected", "Ink.Text", "Ink.Selected", "Ink.Text", 12, new Thickness(8), Tw.B(Tw.Blue500), Tw.B(Tw.Blue500), 1)
                : DK.Button(content, "Ink.Raised40", "Ink.Text300", "Ink.Hover", "Ink.Text300", 12, new Thickness(8), "Ink.Divider", "Ink.Divider", 1);
            card.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            card.Click += (_, _) =>
            {
                _overlay.SetBoardKind(o.Kind);
                if (o.Kind == BoardKind.Grid) _overlay.UpdateOptions(x => x.SnapToGrid = true);
            };
            return (UIElement)card;
        }).ToArray());
        return DK.V(12, intro, cards, DK.TopRule(SnapBlock(s, "Snap to Grid Alignment", "Grid Step Size", false)));
    }

    // ---- Grid ---------------------------------------------------------------------

    private UIElement BuildGrid(ToolSettings s)
    {
        var intro = DK.Text("Configure magnetic snapping for strokes, shapes, and annotation markers:", 11, "Ink.Text400").Wrap();
        var row = DK.Surface(DK.Between(
                DK.H(8, new LucideIcon("Magnet", 16) { Foreground = Tw.B(Tw.Blue400) },
                    DK.V(0, DK.Text("Snap-to-Grid", 12, "Ink.Text", FontWeights.Medium), DK.Text("Lock points to coordinate grid", 10, "Ink.Text400"))),
                DK.Switch(s.SnapToGrid, v => _overlay.UpdateOptions(o => o.SnapToGrid = v), Tw.Blue600, 36, 20, "Ink.Track")),
            "Ink.Raised70", "Ink.BorderStrong", 12, new Thickness(8));
        var spacing = DK.SliderBlock("Grid Cell Spacing", 10, 60, 5, Math.Clamp(s.GridSize, 10, 60), v => $"{v:0}px", Tw.B(Tw.Blue400),
            v => Apply(() => _overlay.UpdateOptions(o => o.GridSize = (int)v)), FontWeights.SemiBold);
        ((Slider)spacing.Children[1]).Foreground = Tw.B(Tw.Blue500);
        // Scale labels spread edge to edge (flex justify-between).
        var scale = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        var labels = new[] { "10px (Fine)", "20px (Std)", "40px", "60px (Coarse)" };
        for (var i = 0; i < labels.Length; i++)
        {
            if (i > 0) scale.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            scale.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var t = DK.Text(labels[i], 10, "Ink.Text500", mono: true);
            Grid.SetColumn(t, scale.ColumnDefinitions.Count - 1);
            scale.Children.Add(t);
        }
        spacing.Children.Add(scale);
        var guides = DK.TopRule(DK.Between(
            DK.H(6, new LucideIcon("Crosshair", 14) { Foreground = Tw.B(Tw.Blue400) }, DK.Text("Magnetic Alignment Crosshairs", 12, "Ink.Text300")),
            DK.Check(s.ShowGridGuides, v => Apply(() => _overlay.UpdateOptions(o => o.ShowGridGuides = v)), Tw.Blue500)), 4);
        var enable = DK.Button(DK.IconLabel("Grid", 14, "Enable Engineering Grid Background", 12, FontWeights.Medium),
            Tw.B(Tw.Blue600, 0.3), Tw.B(Tw.Blue200), Tw.B(Tw.Blue600, 0.5), Tw.B(Tw.Blue200), 8, new Thickness(8, 6, 8, 6), Tw.B(Tw.Blue500, 0.5), Tw.B(Tw.Blue500, 0.5), 1);
        enable.Click += (_, _) => { _overlay.SetBoardKind(BoardKind.Grid); _overlay.UpdateOptions(o => o.SnapToGrid = true); };
        return DK.V(12, intro, row, spacing, guides, enable);
    }

    // ---- Highlighter (InkIt extra, design styled) ----------------------------------

    private UIElement BuildHighlighter(ToolSettings s)
    {
        var modes = new[] { (PenMode.Highlighter, "Freehand", "Follows your stroke"), (PenMode.StraightHighlighter, "Straight Line", "Locks to a straight bar") }
            .Select(m =>
            {
                var selected = _owner.LastHighlighterMode == m.Item1;
                var content = DK.V(0, DK.Text(m.Item2, 11, selected ? Tw.B(System.Windows.Media.Colors.White) : "Ink.Text300", FontWeights.Medium),
                    DK.Text(m.Item3, 9, selected ? Tw.B(Tw.Blue100) : "Ink.Text400"));
                var card = selected
                    ? DK.Button(content, Tw.Amber500, Tw.B(Tw.Slate950), Tw.Amber500, Tw.B(Tw.Slate950), 4, new Thickness(6))
                    : DK.Button(content, "Ink.Raised", "Ink.Text300", "Ink.Hover", "Ink.Text300", 4, new Thickness(6));
                if (selected) ((TextBlock)((StackPanel)card.Content).Children[0]).Foreground = Tw.B(Tw.Slate950);
                if (selected) ((TextBlock)((StackPanel)card.Content).Children[1]).Foreground = Tw.B(Tw.Slate800);
                card.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                card.Click += (_, _) => { _overlay.SetPenMode(m.Item1); _owner.RememberPenMode(m.Item1); };
                return (UIElement)card;
            }).ToList();
        var thickness = DK.SliderBlock("Stroke Thickness", 6, 48, 1, Math.Clamp(s.Thickness, 6, 48), v => $"{v:0}px", Tw.Amber400,
            v => Apply(() => _overlay.SetThickness(v)));
        ((Slider)thickness.Children[1]).Foreground = Tw.B(Tw.Amber500);
        var opacity = DK.SliderBlock("Opacity", 0.1, 1.0, 0.05, Math.Clamp(s.Opacity / 255d, 0.1, 1), v => $"{Math.Round(v * 100)}%", Tw.Amber400,
            v => Apply(() => _overlay.SetOpacity((byte)Math.Round(v * 255))));
        ((Slider)opacity.Children[1]).Foreground = Tw.B(Tw.Amber500);
        return DK.V(12, DK.V(6, DK.Text("Highlighter Mode", 12, "Ink.Text400"), DK.Columns(2, 4, modes)), thickness, opacity);
    }

    // ---- Text (InkIt extra, design styled) ------------------------------------------

    private UIElement BuildText(ToolSettings s)
    {
        Button Toggle(string label, bool on, FontWeight weight, FontStyle style, bool underline, Action toggle)
        {
            var text = new TextBlock { Text = label, FontSize = 13, FontWeight = weight, FontStyle = style, HorizontalAlignment = HorizontalAlignment.Center, TextDecorations = underline ? TextDecorations.Underline : null };
            var b = on
                ? DK.Button(text, Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), 8, new Thickness(0, 5, 0, 5))
                : DK.Button(text, "Ink.Raised", "Ink.Text300", "Ink.Hover", "Ink.Text", 8, new Thickness(0, 5, 0, 5));
            b.Click += (_, _) => { toggle(); Rebuild(); };
            return b;
        }
        var style = DK.Columns(3, 6, new UIElement[]
        {
            Toggle("B", s.TextBold, FontWeights.Bold, FontStyles.Normal, false, () => s.TextBold = !s.TextBold),
            Toggle("I", s.TextItalic, FontWeights.Normal, FontStyles.Italic, false, () => s.TextItalic = !s.TextItalic),
            Toggle("U", s.TextUnderline, FontWeights.Normal, FontStyles.Normal, true, () => s.TextUnderline = !s.TextUnderline),
        });
        var sizes = DK.Columns(4, 4, new[] { 16d, 22d, 30d, 42d }.Select(size =>
        {
            var selected = Math.Abs(s.FontSize - size) < 0.1;
            var text = DK.Plain($"{size:0} pt", 11, FontWeights.Medium, mono: true, center: true);
            var b = selected
                ? DK.Button(text, Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), Tw.Blue600, Tw.B(System.Windows.Media.Colors.White), 4, new Thickness(0, 4, 0, 4))
                : DK.Button(text, "Ink.Control", "Ink.Text300", "Ink.ControlHover", "Ink.Text300", 4, new Thickness(0, 4, 0, 4));
            b.Click += (_, _) => { s.FontSize = size; Rebuild(); };
            return (UIElement)b;
        }).ToList());
        var hint = DK.Text("Click anywhere on screen to type. Enter commits, Shift+Enter adds a line, Esc cancels.", 11, "Ink.Text400").Wrap();
        return DK.V(12, DK.V(6, DK.Text("Text Style", 12, "Ink.Text400"), style), DK.V(6, DK.Text("Font Size", 12, "Ink.Text400"), sizes), DK.TopRule(hint));
    }

    // ---- More (InkIt extra) ---------------------------------------------------------

    private UIElement BuildMore()
    {
        Button Row(string icon, string title, string description, Action action, Color? iconColor = null, bool danger = false)
        {
            var content = DK.H(8,
                new LucideIcon(icon, 16) { Foreground = Tw.B(iconColor ?? Tw.Slate400), VerticalAlignment = VerticalAlignment.Center },
                DK.V(0, DK.Text(title, 12, danger ? Tw.B(Tw.Red400) : "Ink.Text200", FontWeights.Medium), DK.Text(description, 10, "Ink.Text400")));
            var b = DK.Button(content, "Ink.Raised40", "Ink.Text300", danger ? Tw.B(Tw.Red950, 0.6) : "Ink.Hover", "Ink.Text300", 12, new Thickness(8), "Ink.Divider", "Ink.Divider", 1);
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            b.Click += (_, _) => action();
            return b;
        }
        return DK.V(6,
            Row("PauseCircle", "Freeze Frame Screen", "Pause the display and annotate over it", _owner.FreezeScreen, Tw.Sky400),
            Row("Timer", "Break Timer", $"Start a {_owner.BreakTimerMinutes}-minute countdown", () => _owner.StartBreakTimer(_owner.BreakTimerMinutes), Tw.Blue400),
            Row("RotateCw", "Flip Toolbar Orientation", "Horizontal ⇄ vertical (Ctrl+Shift+O)", _owner.ToggleOrientation),
            Row("PieChart", "Quick Radial Pie Menu", "Circular tool wheel at the cursor", _owner.OpenRadialMenu, Tw.Purple400),
            Row("Search", "Command Palette", "Search every command (Ctrl+K)", _owner.OpenCommandPalette, Tw.Blue400),
            Row("Compass", "Capability Centre", "Browse all capabilities (F10)", _owner.OpenCapabilityCentre, Tw.Purple400),
            Row("Settings", "InkIt Settings", "Appearance, hotkeys, profiles (Ctrl+,)", _owner.OpenSettings),
            Row("Power", "Exit InkIt", "Close all overlays and quit", _owner.ExitApplication, Tw.Red400, danger: true));
    }
}
