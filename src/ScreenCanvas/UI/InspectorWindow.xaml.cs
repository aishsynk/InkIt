using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenCanvas.Core;
using ScreenCanvas.Overlay;
using MediaColor = System.Windows.Media.Color;
using MediaPoint = System.Windows.Point;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using FormsCursor = System.Windows.Forms.Cursor;
using FormsScreen = System.Windows.Forms.Screen;
using Button = System.Windows.Controls.Button;
using Panel = System.Windows.Controls.Panel;
using Orientation = System.Windows.Controls.Orientation;
using UniformGrid = System.Windows.Controls.Primitives.UniformGrid;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;

namespace ScreenCanvas.UI;

public partial class InspectorWindow : Window
{
    private readonly IOverlayManager _overlay;
    private readonly ToolbarWindow _owner;
    private string? _currentCategory;
    private Button? _anchorButton;

    public string? CurrentCategory => _currentCategory;

    public InspectorWindow(IOverlayManager overlay, ToolbarWindow owner)
    {
        InitializeComponent();
        _overlay = overlay;
        _owner = owner;
        Owner = owner;
        Closing += (s, e) =>
        {
            e.Cancel = true;
            CloseInspector();
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                e.Handled = true;
                _owner.EndCurrentTool();
            }
        };
    }

    public void ShowCategory(string category, Button anchorButton)
    {
        _currentCategory = category.ToLowerInvariant();
        _anchorButton = anchorButton;

        RebuildContent();
        Reposition();
        
        Opacity = 0;
        Show();
        Reposition();
        
        var anim = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, anim);
    }

    public void CloseInspector()
    {
        _currentCategory = null;
        _anchorButton = null;
        Hide();
    }

    public void Reposition()
    {
        if (_anchorButton is null || PresentationSource.FromVisual(_owner) is null)
            return;

        try
        {
            UpdateLayout();
            var dpi = VisualTreeHelper.GetDpi(_owner);

            var relPt = _anchorButton.TranslatePoint(new MediaPoint(0, 0), _owner);
            double btnX = _owner.Left + relPt.X;
            double btnY = _owner.Top + relPt.Y;
            double btnW = _anchorButton.ActualWidth > 0 ? _anchorButton.ActualWidth : 32;
            double btnH = _anchorButton.ActualHeight > 0 ? _anchorButton.ActualHeight : 32;

            InspectorChrome.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            var inspW = InspectorChrome.DesiredSize.Width > 0 ? InspectorChrome.DesiredSize.Width : (ActualWidth > 0 ? ActualWidth : 42);
            var inspH = InspectorChrome.DesiredSize.Height > 0 ? InspectorChrome.DesiredSize.Height : (ActualHeight > 0 ? ActualHeight : 395);

            // Screen containing the toolbar
            var ownerCenter = new System.Drawing.Point(
                (int)((_owner.Left + _owner.ActualWidth / 2) * dpi.DpiScaleX),
                (int)((_owner.Top + _owner.ActualHeight / 2) * dpi.DpiScaleY));
            var screen = FormsScreen.FromPoint(ownerCenter).WorkingArea;
            var screenLeft = screen.Left / dpi.DpiScaleX;
            var screenRight = screen.Right / dpi.DpiScaleX;
            var screenTop = screen.Top / dpi.DpiScaleY;
            var screenBottom = screen.Bottom / dpi.DpiScaleY;

            double targetX, targetY;
            if (_owner.IsHorizontal)
            {
                SizeToContent = SizeToContent.WidthAndHeight;
                InspectorChrome.Height = double.NaN;
                InspectorChrome.Width = double.NaN;
                ContainerGrid.Height = double.NaN;

                // Toolbar is horizontal: place inspector directly below toolbar, centered under button
                targetX = btnX + (btnW / 2) - (inspW / 2);
                targetX = Math.Clamp(targetX, screenLeft + 8, screenRight - inspW - 8);

                targetY = _owner.Top + _owner.ActualHeight + 4;
                if (targetY + inspH > screenBottom - 8)
                {
                    targetY = _owner.Top - inspH - 4;
                }
            }
            else
            {
                // Toolbar is vertical ("up to down"):
                // Sub-menu MUST be directly adjacent (right or left) to the toolbar,
                // and MUST have EXACT same Top and Height as the main toolbar (MANDATORY)!
                double desiredWidth = 32;
                Width = desiredWidth;
                InspectorChrome.Width = desiredWidth;

                _owner.ToolbarChrome.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                double chromeH = _owner.ToolbarChrome.ActualHeight > 0 ? _owner.ToolbarChrome.ActualHeight : _owner.ToolbarChrome.DesiredSize.Height;
                double ownerH = chromeH > 0 ? chromeH : (_owner.ActualHeight > 0 ? _owner.ActualHeight : 390);
                Height = ownerH;
                InspectorChrome.Height = ownerH;
                InspectorChrome.MaxHeight = ownerH;
                ContainerGrid.Height = Math.Max(100, ownerH - 8);

                if (_owner.Left + _owner.ActualWidth + 4 + desiredWidth <= screenRight - 4)
                {
                    // Placed directly to the right of the toolbar
                    targetX = _owner.Left + _owner.ActualWidth + 4;
                }
                else
                {
                    // Placed directly to the left of the toolbar
                    targetX = _owner.Left - desiredWidth - 4;
                }

                targetY = _owner.Top;
            }

            Left = targetX;
            Top = targetY;
        }
        catch (Exception) { }
    }

    public bool IsPointOverUi(MediaPoint screenPixelPoint)
    {
        if (!IsVisible || PresentationSource.FromVisual(InspectorChrome) is null)
            return false;

        try
        {
            var topLeft = InspectorChrome.PointToScreen(new MediaPoint(0, 0));
            var dpi = VisualTreeHelper.GetDpi(InspectorChrome);
            var bounds = new Rect(topLeft.X, topLeft.Y, InspectorChrome.ActualWidth * dpi.DpiScaleX, InspectorChrome.ActualHeight * dpi.DpiScaleY);
            return bounds.Contains(screenPixelPoint);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void RebuildContent()
    {
        ContentHost.Children.Clear();
        bool isHoriz = _owner.IsHorizontal;
        ContentHost.Orientation = isHoriz ? Orientation.Horizontal : Orientation.Vertical;
        InspectorChrome.Padding = isHoriz ? new Thickness(4, 3, 4, 3) : new Thickness(3, 3, 3, 3);
        InspectorChrome.Width = isHoriz ? double.NaN : 32;
        InspectorChrome.Height = isHoriz ? 32 : double.NaN;

        if (isHoriz)
        {
            BottomHost.Child = null;
            BottomHost.Visibility = Visibility.Collapsed;
        }
        else
        {
            var bottomStack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
            bottomStack.Children.Add(CreateDivider());
            bottomStack.Children.Add(CreateRealCloseButton(CloseInspector));
            BottomHost.Child = bottomStack;
            BottomHost.Visibility = Visibility.Visible;
        }

        switch (_currentCategory)
        {
            case "pen":
                BuildPenInspector();
                break;
            case "highlighter":
                BuildHighlighterInspector();
                break;
            case "shapes":
                BuildShapesInspector();
                break;
            case "text":
                BuildTextInspector();
                break;
            case "present":
            case "laser":
                BuildPresentInspector();
                break;
            case "zoom":
                BuildZoomInspector();
                break;
            case "color":
                BuildColorInspector();
                break;
            case "more":
                BuildMoreInspector();
                break;
            default:
                BuildPenInspector();
                break;
        }
    }

    // --- Sub-Menu Strip Builders (Vertical & Horizontal Adaptive) ---

    private void BuildPenInspector()
    {
        bool isHoriz = _owner.IsHorizontal;
        var nibs = new (string icon, string label, PenMode mode)[]
        {
            ("Fluent.Pen.Regular", "Ballpoint", PenMode.Ballpoint),
            ("Fluent.Pencil.Regular", "Pencil", PenMode.Pencil),
            ("Fluent.Highlight.Regular", "Marker", PenMode.Marker),
            ("Fluent.Pen.Dashed.Regular", "Dashed", PenMode.Dashed)
        };
        
        ContentHost.Children.Add(InspectorUIHelper.CreateComponentBar(_owner, _overlay, nibs.Select(n => (n.icon, n.label, (Action)(() => { _overlay.SetPenMode(n.mode); RebuildContent(); Reposition(); }), _overlay.Settings.PenMode == n.mode)).ToArray(), isHoriz));
        ContentHost.Children.Add(CreateDivider());
        ContentHost.Children.Add(InspectorUIHelper.CreateStrokeSelector(_owner, _overlay, new[] { (2.0, 2.5, "Fine"), (4.5, 4.5, "Medium"), (8.0, 7.5, "Bold"), (14.0, 11.0, "Heavy") }, isHoriz));
        ContentHost.Children.Add(CreateDivider());
        ContentHost.Children.Add(CreateColorPaletteRow(() =>
        {
            RebuildContent();
            _owner.UpdateColorChip();
        }));
        ContentHost.Children.Add(CreateCloseButton(CloseInspector));
    }

    private void BuildHighlighterInspector()
    {
        bool isHoriz = _owner.IsHorizontal;
        var modes = new (string icon, string label, PenMode mode)[]
        {
            ("Fluent.Highlight.Regular", "Freehand", PenMode.Highlighter),
            ("Fluent.Line.Regular", "Straight", PenMode.StraightHighlighter)
        };

        ContentHost.Children.Add(InspectorUIHelper.CreateComponentBar(_owner, _overlay, modes.Select(m => (m.icon, m.label, (Action)(() => { _overlay.SetPenMode(m.mode); RebuildContent(); Reposition(); }), _overlay.Settings.PenMode == m.mode)).ToArray(), isHoriz));
        ContentHost.Children.Add(CreateDivider());
        ContentHost.Children.Add(InspectorUIHelper.CreateStrokeSelector(_owner, _overlay, new[] { (6.0, 2.5, "Fine"), (12.0, 4.5, "Std"), (20.0, 7.0, "Hdg"), (30.0, 10.0, "Brd") }, isHoriz));
        ContentHost.Children.Add(CreateDivider());
        ContentHost.Children.Add(CreateColorPaletteRow(() =>
        {
            RebuildContent();
            _owner.UpdateColorChip();
        }, isHighlighter: true));
        ContentHost.Children.Add(CreateCloseButton(CloseInspector));
    }

    private void BuildShapesInspector()
    {
        bool isHoriz = _owner.IsHorizontal;
        var coreShapes = new (string icon, string label, ShapeKind kind)[]
        {
            ("Fluent.Line.Regular", "Line (L)", ShapeKind.Line),
            ("Fluent.ArrowRight.Regular", "Arrow (A)", ShapeKind.Arrow),
            ("Fluent.DoubleArrow.Regular", "Double Arrow", ShapeKind.DoubleArrow),
            ("Fluent.Rectangle.Regular", "Rectangle (R)", ShapeKind.Rectangle),
            ("Fluent.RoundRect.Regular", "Rounded Rect", ShapeKind.RoundedRectangle),
            ("Fluent.Circle.Regular", "Ellipse (O)", ShapeKind.Ellipse)};

        var shapeItems = coreShapes.Select(s => (s.icon, s.label, (Action)(() =>
        {
            _overlay.SetTool(ToolKind.Shape);
            _overlay.SetShape(s.kind);
            RebuildContent();
            Reposition();
        }), _overlay.Settings.Tool == ToolKind.Shape && _overlay.Settings.Shape == s.kind)).ToList();

        shapeItems.Add(("Fluent.TextNumberFormat.Regular", $"Step Marker (#{_overlay.Settings.MarkerNumber})", () =>
        {
            _overlay.SetTool(ToolKind.NumberMarker);
            RebuildContent();
            Reposition();
        }, _overlay.Settings.Tool == ToolKind.NumberMarker));

        ContentHost.Children.Add(InspectorUIHelper.CreateComponentBar(_owner, _overlay, shapeItems.ToArray(), isHoriz));


        ContentHost.Children.Add(CreateDivider());

        ContentHost.Children.Add(CreateColorPaletteRow(() =>
        {
            RebuildContent();
            _owner.UpdateColorChip();
        }));

        ContentHost.Children.Add(CreateDivider());

        var strokes = new (double width, double dotSize, string tip)[]
        {
            (2.0, 2.5, "Fine (2 px)"),
            (4.5, 4.5, "Medium (4.5 px)"),
            (8.0, 7.5, "Bold (8 px)"),
            (14.0, 11.0, "Heavy (14 px)")
        };
        ContentHost.Children.Add(InspectorUIHelper.CreateStrokeSelector(_owner, _overlay, strokes, isHoriz));

        ContentHost.Children.Add(CreateCloseButton(CloseInspector));
    }

    private void BuildTextInspector()
    {
        bool isHoriz = _owner.IsHorizontal;

        // Font style row: Bold, Italic, Underline
        var styleRow = new StackPanel
        {
            Orientation = isHoriz ? Orientation.Horizontal : Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = isHoriz ? new Thickness(0, 0, 6, 0) : new Thickness(0, 0, 0, 4)
        };
        styleRow.Children.Add(CreateTextStylePill("B", _overlay.Settings.TextBold, true, () =>
        {
            _overlay.Settings.TextBold = !_overlay.Settings.TextBold;
            RebuildContent();
            Reposition();
        }));
        styleRow.Children.Add(CreateTextStylePill("I", _overlay.Settings.TextItalic, false, () =>
        {
            _overlay.Settings.TextItalic = !_overlay.Settings.TextItalic;
            RebuildContent();
            Reposition();
        }));
        styleRow.Children.Add(CreateTextStylePill("U", _overlay.Settings.TextUnderline, false, () =>
        {
            _overlay.Settings.TextUnderline = !_overlay.Settings.TextUnderline;
            RebuildContent();
            Reposition();
        }));
        ContentHost.Children.Add(styleRow);

        ContentHost.Children.Add(CreateDivider());

        var sizes = new double[] { 16, 22, 30, 42 };
        Panel sizePanel = isHoriz
            ? new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 6, 0) }
            : new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };

        foreach (var s in sizes)
        {
            var isSel = Math.Abs(_overlay.Settings.FontSize - s) < 1.0;
            sizePanel.Children.Add(CreateTextPill($"{s} pt", isSel, () =>
            {
                _overlay.Settings.FontSize = s;
                RebuildContent();
                Reposition();
            }));
        }
        ContentHost.Children.Add(sizePanel);

        ContentHost.Children.Add(CreateDivider());

        ContentHost.Children.Add(CreateColorPaletteRow(() =>
        {
            RebuildContent();
            _owner.UpdateColorChip();
        }));

        ContentHost.Children.Add(CreateCloseButton(CloseInspector));
    }

    private void BuildPresentInspector()
    {
        bool isHoriz = _owner.IsHorizontal;
        var tools = new (string icon, string label, string id)[]
        {
            ("Fluent.Laser.Regular", "Laser", "laser"),
            ("Fluent.Target.Regular", "Spotlight", "spotlight"),
            ("Fluent.PanelLeft.Regular", "Code Slit", "codefocus"),
            ("Fluent.Keyboard.Regular", "Keys HUD", "keys")
        };

        var toolStack = new StackPanel
        {
            Orientation = isHoriz ? Orientation.Horizontal : Orientation.Vertical,
            Margin = isHoriz ? new Thickness(0, 0, 4, 0) : new Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        foreach (var (icon, label, id) in tools)
        {
            var isSel = id switch
            {
                "laser" => _overlay.Settings.Tool == ToolKind.Laser,
                "spotlight" => _overlay.Settings.Tool == ToolKind.Spotlight,
                "codefocus" => _owner.CodeFocus.IsVisible,
                "keys" => _owner.KeyVisualizer.IsEnabled,
                _ => false
            };

            toolStack.Children.Add(CreateNibPill(icon, label, isSel, () =>
            {
                switch (id)
                {
                    case "laser":
                        _overlay.SetTool(ToolKind.Laser);
                        break;
                    case "spotlight":
                        _overlay.SetTool(ToolKind.Spotlight);
                        break;
                    case "codefocus":
                        _owner.Registry.Find("code-focus")?.Execute();
                        break;
                    case "keys":
                        _owner.Registry.Find("key-visualizer")?.Execute();
                        break;
                }
                RebuildContent();
                Reposition();
            }));
        }
        ContentHost.Children.Add(toolStack);

        ContentHost.Children.Add(CreateDivider());

        var laserColors = new (string name, MediaColor col)[]
        {
            ("Red", MediaColor.FromRgb(0xE5, 0x48, 0x4D)),
            ("Green", MediaColor.FromRgb(0x16, 0xA3, 0x4A)),
            ("Blue", MediaColor.FromRgb(0x25, 0x63, 0xEB)),
            ("Amber", MediaColor.FromRgb(0xF2, 0xB7, 0x05))
        };
        var colorStack = new StackPanel
        {
            Orientation = isHoriz ? Orientation.Horizontal : Orientation.Vertical,
            Margin = isHoriz ? new Thickness(0, 0, 4, 0) : new Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        foreach (var (name, col) in laserColors)
        {
            var isSel = _overlay.Settings.Color == col;
            colorStack.Children.Add(CreateColorBead(col, isSel, () =>
            {
                _overlay.SetColor(col);
                RebuildContent();
                _owner.UpdateColorChip();
            }));
        }
        ContentHost.Children.Add(colorStack);

        ContentHost.Children.Add(CreateCloseButton(CloseInspector));
    }

    private void BuildZoomInspector()
    {
        bool isHoriz = _owner.IsHorizontal;
        var zoomStack = new StackPanel
        {
            Orientation = isHoriz ? Orientation.Horizontal : Orientation.Vertical,
            Margin = isHoriz ? new Thickness(0, 0, 4, 0) : new Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        zoomStack.Children.Add(CreateNibPill("Fluent.ZoomIn.Regular", "Zoom In (+)", false, () =>
        {
            _owner.LiveZoomIn();
        }));
        zoomStack.Children.Add(CreateNibPill("Fluent.ZoomOut.Regular", "Zoom Out (-)", false, () =>
        {
            _owner.LiveZoomOut();
        }));
        zoomStack.Children.Add(CreateTextPill("100%", false, () =>
        {
            _owner.LiveZoomReset();
        }));
        ContentHost.Children.Add(zoomStack);

        ContentHost.Children.Add(CreateDivider());

        var panBtn = CreateNibPill("Fluent.Drag.Regular", "Pan & Inspect", false, () =>
        {
            _owner.TriggerStaticZoom();
            CloseInspector();
        });
        ContentHost.Children.Add(panBtn);

        var freezeBtn = CreateNibPill("Fluent.Snowflake.Regular", "Freeze Screen", false, () =>
        {
            _owner.FreezeScreen();
            CloseInspector();
        });
        ContentHost.Children.Add(freezeBtn);

        ContentHost.Children.Add(CreateCloseButton(CloseInspector));
    }

    private void BuildColorInspector()
    {
        ContentHost.Children.Add(CreateColorPaletteRow(() =>
        {
            RebuildContent();
            _owner.UpdateColorChip();
        }));
        ContentHost.Children.Add(CreateCloseButton(CloseInspector));
    }

    private void BuildMoreInspector()
    {
        bool isHoriz = _owner.IsHorizontal;

        // 1. Board Modes (Screen Canvas / Whiteboard / Blackboard)
        var boardStack = new StackPanel
        {
            Orientation = isHoriz ? Orientation.Horizontal : Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = isHoriz ? new Thickness(0, 0, 4, 0) : new Thickness(0, 0, 0, 4)
        };

        bool isScreen = _overlay.CurrentBoardColor == null;
        bool isWhite = _overlay.CurrentBoardColor == MediaColor.FromRgb(255, 255, 255);
        bool isDark = _overlay.CurrentBoardColor == MediaColor.FromRgb(24, 24, 27) || _overlay.CurrentBoardColor == MediaColor.FromRgb(0, 0, 0);

        boardStack.Children.Add(CreateNibPill("Fluent.Desktop.Regular", "Screen Canvas (Transparent)", isScreen, () =>
        {
            _overlay.SetBoard(null);
            RebuildContent();
            Reposition();
        }));

        boardStack.Children.Add(CreateNibPill("Fluent.Whiteboard.Regular", "Whiteboard Canvas (Pure White)", isWhite, () =>
        {
            _overlay.SetBoard(MediaColor.FromRgb(255, 255, 255));
            _overlay.SetTool(ToolKind.Pen);
            RebuildContent();
            Reposition();
        }));

        boardStack.Children.Add(CreateNibPill("Fluent.Whiteboard.Filled", "Blackboard Canvas (Dark Chalkboard)", isDark, () =>
        {
            _overlay.SetBoard(MediaColor.FromRgb(24, 24, 27));
            _overlay.SetTool(ToolKind.Pen);
            RebuildContent();
            Reposition();
        }));

        ContentHost.Children.Add(boardStack);
        ContentHost.Children.Add(CreateDivider());

        // 2. Presenter Utilities (Orientation, Command Palette, Radial Menu)
        var utilStack = new StackPanel
        {
            Orientation = isHoriz ? Orientation.Horizontal : Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = isHoriz ? new Thickness(0, 0, 4, 0) : new Thickness(0, 0, 0, 4)
        };

        utilStack.Children.Add(CreateNibPill("Fluent.DoubleArrow.Regular", "Toggle Layout (↕ Vertical / ↔ Horizontal)", false, () =>
        {
            _owner.ToggleOrientation();
            RebuildContent();
            Reposition();
        }));

        utilStack.Children.Add(CreateNibPill("Fluent.Search.Regular", "Command Palette (Ctrl+Shift+P)", false, () =>
        {
            _owner.OpenCommandPalette();
        }));

        utilStack.Children.Add(CreateNibPill("Fluent.Target.Regular", "Radial Gestures Menu", false, () =>
        {
            _owner.OpenRadialMenu();
        }));

        ContentHost.Children.Add(utilStack);
        ContentHost.Children.Add(CreateDivider());

        // 3. Teaching Tools (Break Timer, Screen Freeze)
        var teachStack = new StackPanel
        {
            Orientation = isHoriz ? Orientation.Horizontal : Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = isHoriz ? new Thickness(0, 0, 4, 0) : new Thickness(0, 0, 0, 4)
        };

        teachStack.Children.Add(CreateNibPill("Fluent.Timer.Regular", "Break & Presentation Countdown Timer", false, () =>
        {
            _owner.Registry.Find("breaktimer")?.Execute();
            CloseInspector();
        }));

        teachStack.Children.Add(CreateNibPill("Fluent.Snowflake.Regular", "Freeze Screen Frame", false, () =>
        {
            _owner.FreezeScreen();
            CloseInspector();
        }));

        ContentHost.Children.Add(teachStack);
        ContentHost.Children.Add(CreateDivider());

        // 4. Advanced & Exit
        var sysStack = new StackPanel
        {
            Orientation = isHoriz ? Orientation.Horizontal : Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = isHoriz ? new Thickness(0, 0, 4, 0) : new Thickness(0, 0, 0, 4)
        };

        sysStack.Children.Add(CreateNibPill("Fluent.Shapes.Regular", "All Capabilities & Presets", false, () =>
        {
            _owner.OpenCapabilityCentre();
        }));

        sysStack.Children.Add(CreateNibPill("Fluent.Settings.Regular", "Application Settings", false, () =>
        {
            _owner.OpenSettings();
        }));

        sysStack.Children.Add(CreateDangerPill("Fluent.Dismiss.Regular", "Exit InkIt", () =>
        {
            System.Windows.Application.Current.Shutdown();
        }));

        ContentHost.Children.Add(sysStack);
        ContentHost.Children.Add(CreateCloseButton(CloseInspector));
    }

    // --- Component Helpers ---

    private Button CreateNibPill(string iconKey, string label, bool isSelected, Action onClick)
    {
        bool isHoriz = _owner.IsHorizontal;
        var btn = new Button
        {
            Width = isHoriz ? double.NaN : 22,
            Height = 22,
            Cursor = Cursors.Hand,
            Margin = isHoriz ? new Thickness(1, 0, 1, 0) : new Thickness(0, 0.5, 0, 0.5),
            HorizontalAlignment = HorizontalAlignment.Center,
            ToolTip = label
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = isHoriz ? new Thickness(5, 0, 6, 0) : new Thickness(0),
            Background = isSelected ? (MediaBrush)FindResource("PanelBrush") : MediaBrushes.Transparent
        };
        if (isSelected)
        {
            border.BorderThickness = new Thickness(1);
            border.BorderBrush = (MediaBrush)FindResource("BorderBrush");
        }

        var geom = TryFindResource(iconKey) as Geometry ?? (Geometry)FindResource("Fluent.Pen.Regular");
        var path = new Path
        {
            Data = geom,
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            Fill = isSelected ? (MediaBrush)FindResource("AccentBrush") : (MediaBrush)FindResource("PrimaryTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (isHoriz)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(path);
            var txt = new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isSelected ? (MediaBrush)FindResource("AccentBrush") : (MediaBrush)FindResource("PrimaryTextBrush"),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(txt);
            border.Child = stack;
        }
        else
        {
            border.Child = path;
        }

        btn.Content = border;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private Button CreateShapeButton(string iconKey, string label, bool isSelected, Action onClick)
    {
        bool isHoriz = _owner.IsHorizontal;
        var btn = new Button
        {
            Width = 22,
            Height = 22,
            Cursor = Cursors.Hand,
            Margin = isHoriz ? new Thickness(1, 0, 1, 0) : new Thickness(0.5),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = label
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = isSelected ? (MediaBrush)FindResource("SelectedBrush") : MediaBrushes.Transparent
        };
        if (isSelected)
        {
            border.BorderThickness = new Thickness(1);
            border.BorderBrush = (MediaBrush)FindResource("AccentBrush");
        }

        Geometry? geom = null;
        try
        {
            geom = TryFindResource(iconKey) as Geometry ?? (Geometry)FindResource("Fluent.Shapes.Regular");
        }
        catch
        {
            geom = (Geometry)FindResource("Fluent.Shapes.Regular");
        }
        
        var path = new Path
        {
            Data = geom,
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            Fill = isSelected ? (MediaBrush)FindResource("AccentBrush") : (MediaBrush)FindResource("PrimaryTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = path;

        btn.Content = border;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private Button CreateTextPill(string label, bool isSelected, Action onClick)
    {
        bool isHoriz = _owner.IsHorizontal;
        var btn = new Button
        {
            Width = isHoriz ? double.NaN : 22,
            Height = 20,
            Cursor = Cursors.Hand,
            Margin = isHoriz ? new Thickness(1.5, 0, 1.5, 0) : new Thickness(0.5),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = isHoriz ? new Thickness(6, 0, 6, 0) : new Thickness(1, 0, 1, 0),
            Background = isSelected ? (MediaBrush)FindResource("SelectedBrush") : MediaBrushes.Transparent
        };
        if (isSelected)
        {
            border.BorderThickness = new Thickness(1);
            border.BorderBrush = (MediaBrush)FindResource("AccentBrush");
        }
        var txt = new TextBlock
        {
            Text = label,
            FontSize = isHoriz ? 10 : 9,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = isSelected ? (MediaBrush)FindResource("AccentBrush") : (MediaBrush)FindResource("PrimaryTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = txt;
        btn.Content = border;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private Button CreateTextStylePill(string label, bool isActive, bool isBold, Action onClick)
    {
        var btn = new Button
        {
            Width = 22,
            Height = 22,
            Cursor = Cursors.Hand,
            Margin = new Thickness(1.5, 0, 1.5, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            ToolTip = label switch { "B" => "Bold", "I" => "Italic", "U" => "Underline", _ => label }
        };
        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = isActive ? (MediaBrush)FindResource("SelectedBrush") : MediaBrushes.Transparent
        };
        if (isActive)
        {
            border.BorderThickness = new Thickness(1);
            border.BorderBrush = (MediaBrush)FindResource("AccentBrush");
        }
        var txt = new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = label == "I" ? FontStyles.Italic : FontStyles.Normal,
            TextDecorations = label == "U" ? TextDecorations.Underline : null,
            Foreground = isActive ? (MediaBrush)FindResource("AccentBrush") : (MediaBrush)FindResource("PrimaryTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = txt;
        btn.Content = border;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private Panel CreateColorPaletteRow(Action onColorChanged, bool isHighlighter = false, bool forceTwoColumns = false)
    {
        bool isHoriz = _owner.IsHorizontal;
        Panel panel;
        if (isHoriz)
        {
            panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        }
        else if (forceTwoColumns)
        {
            panel = new UniformGrid { Columns = 2, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 2) };
        }
        else
        {
            panel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
        }

        var colors = isHighlighter
            ? new (string name, MediaColor col)[]
            {
                ("Yellow", MediaColor.FromRgb(0xFF, 0xE6, 0x00)),
                ("Green", MediaColor.FromRgb(0x22, 0xC5, 0x5E)),
                ("Cyan", MediaColor.FromRgb(0x06, 0xB6, 0xD4)),
                ("Pink", MediaColor.FromRgb(0xEC, 0x48, 0x99)),
                ("Orange", MediaColor.FromRgb(0xF9, 0x73, 0x16)),
                ("Purple", MediaColor.FromRgb(0xA8, 0x55, 0xF7))
            }
            : new (string name, MediaColor col)[]
            {
                ("Blue", MediaColor.FromRgb(0x25, 0x63, 0xEB)),
                ("Red", MediaColor.FromRgb(0xE5, 0x48, 0x4D)),
                ("Green", MediaColor.FromRgb(0x16, 0xA3, 0x4A)),
                ("Amber", MediaColor.FromRgb(0xF2, 0xB7, 0x05)),
                ("Purple", MediaColor.FromRgb(0x7C, 0x3A, 0xED)),
                ("Dark", MediaColor.FromRgb(0x0F, 0x17, 0x2A))
            };

        foreach (var (name, col) in colors)
        {
            var isSel = _overlay.Settings.Color == col;
            panel.Children.Add(CreateColorBead(col, isSel, () =>
            {
                _overlay.SetColor(col);
                onColorChanged();
            }, isHighlighter, name));
        }

        return panel;
    }

    private UIElement CreateColorBead(MediaColor col, bool isSelected, Action onClick, bool isHighlighter = false, string tooltip = "")
    {
        bool isHoriz = _owner.IsHorizontal;
        var outer = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Margin = isHoriz ? new Thickness(1.5, 0, 1.5, 0) : new Thickness(0.5, 1, 0.5, 1),
            Cursor = Cursors.Hand,
            ToolTip = tooltip,
            Background = MediaBrushes.Transparent,
            BorderThickness = new Thickness(isSelected ? 1.5 : 0),
            BorderBrush = (MediaBrush)FindResource("AccentBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var inner = new Border
        {
            Width = isSelected ? 12 : 13,
            Height = isSelected ? 12 : 13,
            CornerRadius = new CornerRadius(6.5),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(isHighlighter ? MediaColor.FromArgb(160, col.R, col.G, col.B) : col),
            BorderThickness = new Thickness(0.5),
            BorderBrush = new SolidColorBrush(MediaColor.FromArgb(40, 0, 0, 0))
        };

        if (isSelected)
        {
            var check = new Path
            {
                Data = (Geometry)FindResource("Fluent.Checkmark.Regular"),
                Width = 7,
                Height = 7,
                Stretch = Stretch.Uniform,
                Fill = MediaBrushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            inner.Child = check;
        }

        outer.Child = inner;
        outer.MouseLeftButtonUp += (_, _) => onClick();
        return outer;
    }

    private Button CreateStrokeDot(double diameter, double widthVal, bool isSelected, string tip, Action onClick)
    {
        bool isHoriz = _owner.IsHorizontal;
        var btn = new Button
        {
            Width = 20,
            Height = 20,
            Cursor = Cursors.Hand,
            Margin = isHoriz ? new Thickness(1, 0, 1, 0) : new Thickness(0, 0.5, 0, 0.5),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = tip
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = isSelected ? (MediaBrush)FindResource("SelectedBrush") : MediaBrushes.Transparent
        };
        if (isSelected)
        {
            border.BorderThickness = new Thickness(1);
            border.BorderBrush = (MediaBrush)FindResource("AccentBrush");
        }

        var dot = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = isSelected ? (MediaBrush)FindResource("AccentBrush") : (MediaBrush)FindResource("PrimaryTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = dot;

        btn.Content = border;
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private FrameworkElement CreateDivider()
    {
        bool isHoriz = _owner.IsHorizontal;
        return new Border
        {
            Width = isHoriz ? 1 : 16,
            Height = isHoriz ? 13 : 1,
            Background = (MediaBrush)FindResource("SeparatorBrush"),
            Margin = isHoriz ? new Thickness(4, 0, 4, 0) : new Thickness(0, 3, 0, 3),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private FrameworkElement CreateCloseButton(Action onClose)
    {
        if (!_owner.IsHorizontal)
        {
            // In vertical mode, the close button is pinned at the bottom in BottomHost
            return new Border { Width = 0, Height = 0, Visibility = Visibility.Collapsed };
        }
        return CreateRealCloseButton(onClose);
    }

    private Button CreateRealCloseButton(Action onClose)
    {
        bool isHoriz = _owner.IsHorizontal;
        var btn = new Button
        {
            Width = 18,
            Height = 18,
            Cursor = Cursors.Hand,
            Margin = isHoriz ? new Thickness(3, 0, 0, 0) : new Thickness(0, 1, 0, 1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Close (Esc)"
        };

        var path = new Path
        {
            Data = (Geometry)FindResource("Fluent.Dismiss.Regular"),
            Width = 9,
            Height = 9,
            Stretch = Stretch.Uniform,
            Fill = (MediaBrush)FindResource("SecondaryTextBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        btn.Content = path;
        btn.Click += (_, _) => onClose();
        return btn;
    }

    private Button CreateDangerPill(string iconKey, string label, Action onClick)
    {
        bool isHoriz = _owner.IsHorizontal;
        var btn = new Button
        {
            Width = isHoriz ? double.NaN : 22,
            Height = 22,
            Cursor = Cursors.Hand,
            Margin = isHoriz ? new Thickness(1, 0, 1, 0) : new Thickness(0, 0.5, 0, 0.5),
            HorizontalAlignment = HorizontalAlignment.Center,
            ToolTip = label
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = isHoriz ? new Thickness(5, 0, 6, 0) : new Thickness(0),
            Background = MediaBrushes.Transparent
        };

        var geom = TryFindResource(iconKey) as Geometry ?? (Geometry)FindResource("Fluent.Dismiss.Regular");
        var path = new Path
        {
            Data = geom,
            Width = 11,
            Height = 11,
            Stretch = Stretch.Uniform,
            Fill = new SolidColorBrush(MediaColor.FromRgb(0xE5, 0x48, 0x4D)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        border.Child = path;
        btn.Content = border;
        btn.Click += (_, _) => onClick();
        return btn;
    }
}
