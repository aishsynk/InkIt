using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ScreenCanvas.Core;
using ScreenCanvas.Interop;
using ScreenCanvas.Overlay;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Point = System.Windows.Point;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Cursors = System.Windows.Input.Cursors;
using Mouse = System.Windows.Input.Mouse;
using TextElement = System.Windows.Documents.TextElement;

namespace ScreenCanvas.UI.Toolbar;

/// <summary>A floating card window (popover / drawer) that does not steal focus from the presenter's app.</summary>
public abstract class FloatingCard : Window, IChromeHost
{
    protected readonly Border Card;
    protected readonly ToolbarWindow Toolbar;

    protected FloatingCard(ToolbarWindow toolbar, double width, Paint border, Thickness padding)
    {
        Toolbar = toolbar;
        Owner = toolbar;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowActivated = false;
        Card = DK.Surface(new Grid(), "Ink.Popover", border, 16, padding);
        Card.Width = width;
        Card.Margin = new Thickness(20, 12, 20, 28);
        Card.Effect = DK.Shadow(40, 14, 0.5);
        TextElement.SetFontFamily(Card, DK.Font);
        Content = Card;
        Closing += (_, e) => { e.Cancel = true; Hide(); };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLong(handle, NativeMethods.GwlExStyle, style | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate);
    }

    public bool IsPointOverChrome(Point screenPixelPoint)
    {
        if (!IsVisible || PresentationSource.FromVisual(Card) is null) return false;
        var topLeft = Card.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(Card);
        return new Rect(topLeft.X, topLeft.Y, Card.ActualWidth * dpi.DpiScaleX, Card.ActualHeight * dpi.DpiScaleY).Contains(screenPixelPoint);
    }

    /// <summary>Places the card's visible chrome at a DIP position, clamped to the working area.</summary>
    protected void PlaceCard(double left, double top)
    {
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        var w = Card.ActualWidth;
        var h = Card.ActualHeight;
        left = Math.Clamp(left, area.Left + 8, Math.Max(area.Left + 8, area.Right - w - 8));
        top = Math.Clamp(top, area.Top + 8, Math.Max(area.Top + 8, area.Bottom - h - 8));
        Left = left - Card.Margin.Left;
        Top = top - Card.Margin.Top;
    }

    protected static Rect ScreenRectDip(FrameworkElement element)
    {
        var topLeft = element.PointToScreen(new Point(0, 0));
        var dpi = VisualTreeHelper.GetDpi(element);
        return new Rect(topLeft.X / dpi.DpiScaleX, topLeft.Y / dpi.DpiScaleY, element.ActualWidth, element.ActualHeight);
    }

    public abstract void Reposition();
}

/// <summary>Multi-Tool Stacking popover (design: Toolbar.tsx isMultiToolMenuOpen).</summary>
public sealed class MultiToolWindow : FloatingCard
{
    private readonly IOverlayManager _overlay;
    private FrameworkElement? _anchor;

    public MultiToolWindow(IOverlayManager overlay, ToolbarWindow toolbar)
        : base(toolbar, 256, Tw.B(Tw.Purple500, 0.4), new Thickness(12))
    {
        _overlay = overlay;
        IsVisibleChanged += (_, _) => { if (!IsVisible) Toolbar.OnMultiToolClosed(); };
        Refresh();
    }

    public void ShowNear(FrameworkElement anchor)
    {
        _anchor = anchor;
        Refresh();
        Show();
        Reposition();
    }

    public override void Reposition()
    {
        if (!IsVisible || _anchor is null || PresentationSource.FromVisual(_anchor) is null) return;
        var a = ScreenRectDip(_anchor);
        var chrome = ScreenRectDip(Toolbar.ToolbarChrome);
        if (Toolbar.IsHorizontal) PlaceCard(a.Left, chrome.Bottom + 6);
        else PlaceCard(chrome.Right + 8, a.Top);
    }

    public void Refresh()
    {
        var s = _overlay.Settings;
        var close = DK.IconButton("X", 12, "Ink.Text500", "Ink.Text", "Ink.Hover", 4, 4);
        close.Click += (_, _) => Hide();
        var header = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 8),
            Margin = new Thickness(0, 0, 0, 10),
            Child = DK.Between(
                DK.H(6, new LucideIcon("Sparkles", 14) { Foreground = Tw.B(Tw.Purple400) },
                    DK.Caps("Multi-Tool Stacking", 12, Tw.B(Tw.Purple300), FontWeights.Bold)),
                close)
        };
        header.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");

        var intro = DK.Rich(11, "Ink.Text400",
            ("Combine secondary modifiers to run ", null, false),
            ("simultaneously", Tw.B(Tw.Purple300), false),
            (" with your active tool:", null, false));
        ((System.Windows.Documents.Run)intro.Inlines.ElementAt(1)).FontWeight = FontWeights.SemiBold;
        intro.Margin = new Thickness(0, 0, 0, 8);

        var rows = DK.V(6,
            Row("Flame", "Laser Glow Inking", "Radiant laser trail follows pen tip", s.SimultaneousLaser, Tw.Rose400, Tw.Rose600, Tw.Rose950, Tw.Rose500,
                () => _overlay.UpdateOptions(o => o.SimultaneousLaser = !o.SimultaneousLaser)),
            Row("SunMedium", "Spotlight Follow", "Dim screen & illuminate cursor", s.SimultaneousSpotlight, Tw.Amber400, Tw.Amber500, Tw.Amber950, Tw.Amber500,
                () => _overlay.UpdateOptions(o => o.SimultaneousSpotlight = !o.SimultaneousSpotlight)),
            Row("Magnet", "Snap-to-Grid Magnet", $"Lock points to {s.GridSize}px grid", s.SnapToGrid, Tw.Blue400, Tw.Blue600, Tw.Blue950, Tw.Blue500,
                () => _overlay.UpdateOptions(o => o.SnapToGrid = !o.SnapToGrid)),
            Row("Wand2", "Auto-Shape Assist", "Auto-beautifies rough loops & lines", s.AutoShapeAssist, Tw.Purple400, Tw.Purple600, Tw.Purple950, Tw.Purple500,
                () => _overlay.UpdateOptions(o => o.AutoShapeAssist = !o.AutoShapeAssist)));

        Card.Child = DK.V(0, header, intro, rows);
        if (IsVisible) Dispatcher.BeginInvoke(Reposition, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static Button Row(string icon, string title, string description, bool on, Color iconOn, Color switchOn, Color bgOn, Color borderOn, Action toggle)
    {
        var knob = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = Tw.B(Colors.White), HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left, Effect = DK.Shadow(3, 1, 0.3) };
        var track = new Border { Width = 28, Height = 16, CornerRadius = new CornerRadius(8), Padding = new Thickness(2), Child = knob };
        if (on) track.Background = Tw.B(switchOn); else track.SetResourceReference(Border.BackgroundProperty, "Ink.Track");
        var text = DK.V(0, DK.Plain(title, 12, FontWeights.Medium), DK.Text(description, 10, "Ink.Text400"));
        var left = DK.H(8, new LucideIcon(icon, 16) { Foreground = on ? Tw.B(iconOn) : Tw.B(Tw.Slate400), VerticalAlignment = VerticalAlignment.Center }, text);
        var content = DK.Between(left, track);
        var button = on
            ? DK.Button(content, Tw.B(bgOn, 0.4), Tw.B(Colors.White), Tw.B(bgOn, 0.4), Tw.B(Colors.White), 12, new Thickness(8), Tw.B(borderOn, 0.6), Tw.B(borderOn, 0.6), 1)
            : DK.Button(content, "Ink.Raised40", "Ink.Text300", "Ink.Hover", "Ink.Text300", 12, new Thickness(8), "Ink.Divider", "Ink.Divider", 1);
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.Click += (_, _) => toggle();
        return button;
    }
}

/// <summary>Customize Toolbar drawer (design: Toolbar.tsx isCustomizeOpen).</summary>
public sealed class CustomizeToolbarWindow : FloatingCard
{
    private string? _dragId;
    private Point _dragStart;
    private string? _dropTarget;
    private bool _dropAfter;

    public CustomizeToolbarWindow(ToolbarWindow toolbar)
        : base(toolbar, 336, "Ink.BorderStrong", new Thickness(16))
    {
    }

    public override void Reposition()
    {
        if (!IsVisible || PresentationSource.FromVisual(Toolbar.ToolbarChrome) is null) return;
        var chrome = ScreenRectDip(Toolbar.ToolbarChrome);
        if (Toolbar.IsHorizontal) PlaceCard(chrome.Left, chrome.Bottom + 8);
        else PlaceCard(chrome.Right + 12, chrome.Top);
    }

    public void Refresh()
    {
        var close = DK.IconButton("X", 16, "Ink.Text500", "Ink.Text", "Ink.Hover", 4, 8);
        close.Click += (_, _) => Toolbar.HideCustomize();
        var header = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 12),
            Margin = new Thickness(0, 0, 0, 12),
            Child = DK.Between(DK.H(8, new LucideIcon("Sliders", 16) { Foreground = Tw.B(Tw.Blue400) }, DK.Caps("Customize Toolbar", 12, "Ink.Text200")), close)
        };
        header.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");

        var intro = DK.Text("Drag tools directly on the toolbar or reorder below. Toggle visibility to prioritize your most frequently used tools.", 11, "Ink.Text400").Wrap(16);
        intro.Margin = new Thickness(0, 0, 0, 12);

        var presets = ToolbarCatalog.Presets.Select(p =>
        {
            var content = DK.V(0, DK.Text(p.Name, 11, "Ink.Text", FontWeights.Medium), DK.Text(p.Description, 9, "Ink.Text400"));
            var b = DK.Button(content, "Ink.Raised70", "Ink.Text300", "Ink.Hover", "Ink.Text300", 8, new Thickness(6), Tw.B(Tw.Slate700, 0.5), Tw.B(Tw.Blue500), 1);
            b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            b.Click += (_, _) => Toolbar.ApplyLayoutPreset(p);
            return (UIElement)b;
        }).ToList();
        var presetBlock = DK.V(6, DK.Caps("Workflow Presets", 10, "Ink.Text400"), DK.Columns(2, 6, presets));
        presetBlock.Margin = new Thickness(0, 0, 0, 12);

        var layout = Toolbar.Layout;
        var reset = DK.Button(DK.IconLabel("RotateCcw", 12, "Reset", 10, spacing: 4), Tw.B(Colors.Transparent), Tw.B(Tw.Blue400), Tw.B(Colors.Transparent), Tw.B(Tw.Blue300), 4);
        reset.Click += (_, _) => Toolbar.ResetLayout();
        var listHeader = DK.Between(DK.Caps($"Tools Order ({layout.Count(i => i.Visible)} visible)", 10, "Ink.Text400"), reset);
        listHeader.Margin = new Thickness(0, 0, 0, 6);

        var list = new StackPanel();
        for (var index = 0; index < layout.Count; index++)
        {
            var row = BuildRow(layout[index], index, layout.Count);
            if (index > 0) row.Margin = new Thickness(0, 4, 0, 0);
            list.Children.Add(row);
        }
        var scroll = new ScrollViewer
        {
            Style = (Style)FindResource("Ink.ScrollViewer"),
            MaxHeight = 224,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Padding = new Thickness(0, 0, 6, 0), Child = list }
        };

        var done = DK.Button(DK.Plain("Done Customizing", 12, FontWeights.Medium), Tw.B(Tw.Blue600), Tw.B(Colors.White), Tw.B(Tw.Blue500), Tw.B(Colors.White), 8, new Thickness(12, 6, 12, 6));
        done.Effect = DK.Shadow(8, 2, 0.35);
        done.HorizontalAlignment = HorizontalAlignment.Right;
        done.Click += (_, _) => Toolbar.HideCustomize();
        var footer = DK.TopRule(done);
        footer.Margin = new Thickness(0, 12, 0, 0);

        Card.Child = DK.V(0, header, intro, presetBlock, listHeader, scroll, footer);
        if (IsVisible) Dispatcher.BeginInvoke(Reposition, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private FrameworkElement BuildRow(Settings.ToolbarItemState state, int index, int count)
    {
        var def = ToolbarCatalog.Find(state.Id)!;
        var label = DK.Text(def.Label, 11, state.Visible ? "Ink.Text200" : "Ink.Text500", FontWeights.Medium);
        var left = DK.H(8, new LucideIcon("GripVertical", 14) { Foreground = Tw.B(Tw.Slate500), Cursor = Cursors.SizeAll }, label);
        if (def.Shortcut is not null) left.Children.Add(new Border
        {
            Margin = new Thickness(8, 0, 0, 0),
            Child = DK.Kbd(def.Shortcut, "Ink.Text400", "Ink.Kbd950", "Ink.Divider", 9, new Thickness(4, 1, 4, 1))
        });

        Button Arrow(string glyph, bool enabled, int direction)
        {
            var b = DK.Button(DK.Plain(glyph, 10), Tw.B(Colors.Transparent), "Ink.Text400", Tw.B(Colors.Transparent), "Ink.Text", 4, new Thickness(4, 2, 4, 2));
            b.IsEnabled = enabled;
            b.ToolTip = direction < 0 ? "Move Up" : "Move Down";
            b.Click += (_, _) => Toolbar.MoveItemBy(index, direction);
            return b;
        }
        var eye = DK.Button(new LucideIcon(state.Visible ? "Eye" : "EyeOff", 14), Tw.B(Colors.Transparent),
            state.Visible ? Tw.B(Tw.Blue400) : Tw.B(Tw.Slate500), "Ink.ControlHover", state.Visible ? Tw.B(Tw.Blue400) : Tw.B(Tw.Slate500), 4, new Thickness(4));
        eye.ToolTip = state.Visible ? "Hide from toolbar" : "Show in toolbar";
        eye.Click += (_, _) => Toolbar.ToggleItemVisibility(state.Id);
        var right = DK.H(4, Arrow("▲", index > 0, -1), Arrow("▼", index < count - 1, 1), eye);

        var row = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Child = DK.Between(left, right),
            Opacity = state.Visible ? 1 : 0.6,
            AllowDrop = true,
            Tag = state.Id
        };
        if (state.Visible)
        {
            row.Background = Tw.B(Tw.Slate800, 0.8);
            row.SetResourceReference(Border.BorderBrushProperty, "Ink.BorderStrong");
            if (!Settings.ThemeManager.IsDark) row.SetResourceReference(Border.BackgroundProperty, "Ink.Raised");
        }
        else
        {
            row.SetResourceReference(Border.BackgroundProperty, "Ink.Sunken");
            row.SetResourceReference(Border.BorderBrushProperty, "Ink.Divider");
        }
        if (_dropTarget == state.Id)
        {
            row.BorderBrush = Tw.B(Tw.Blue400);
            row.BorderThickness = new Thickness(1, _dropAfter ? 1 : 2, 1, _dropAfter ? 2 : 1);
        }

        row.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject d && FindButton(d) is not null) return;
            _dragId = state.Id;
            _dragStart = e.GetPosition(this);
        };
        row.PreviewMouseLeftButtonUp += (_, _) => _dragId = null;
        row.PreviewMouseMove += (_, e) =>
        {
            if (_dragId != state.Id || e.LeftButton != MouseButtonState.Pressed || Mouse.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(this);
            if (Math.Abs(p.Y - _dragStart.Y) < 4 && Math.Abs(p.X - _dragStart.X) < 4) return;
            var id = _dragId;
            _dragId = null;
            row.Opacity = 0.4;
            DragDrop.DoDragDrop(row, new System.Windows.DataObject(DataFormats.StringFormat, id), DragDropEffects.Move);
            _dropTarget = null;
            Refresh();
        };
        row.DragOver += (_, e) =>
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            var after = e.GetPosition(row).Y > row.ActualHeight / 2;
            if (_dropTarget == state.Id && _dropAfter == after) return;
            _dropTarget = state.Id;
            _dropAfter = after;
            row.BorderBrush = Tw.B(Tw.Blue400);
            row.BorderThickness = new Thickness(1, after ? 1 : 2, 1, after ? 2 : 1);
        };
        row.Drop += (_, e) =>
        {
            e.Handled = true;
            if (e.Data.GetData(DataFormats.StringFormat) is string source && source != state.Id)
                Toolbar.MoveItem(source, state.Id, _dropAfter);
        };
        return row;
    }

    private static Button? FindButton(DependencyObject node)
    {
        for (var current = node; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is Button b) return b;
        return null;
    }
}
