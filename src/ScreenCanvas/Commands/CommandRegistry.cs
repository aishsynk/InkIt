using System.Windows;
using ScreenCanvas.Core;
using ScreenCanvas.Overlay;
using ScreenCanvas.Capture;
using ScreenCanvas.Presentation;
using ScreenCanvas.Zoom;
using ScreenCanvas.Privacy;

namespace ScreenCanvas.Commands;

public sealed class CommandRegistry
{
    private readonly List<CommandItem> _commands = [];
    public IReadOnlyList<CommandItem> Commands => _commands;

    public void Register(CommandItem command) => _commands.Add(command);

    public CommandItem? Find(string id) =>
        _commands.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<CommandItem> GetByCategory(CapabilityCategory category) =>
        _commands.Where(c => c.Category == category);

    public IEnumerable<CommandItem> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return _commands;
        return _commands.Where(c => c.Matches(query));
    }

    public static CommandRegistry Create(
        IOverlayManager overlay,
        ICaptureService capture,
        Lazy<WindowsZoomEngine> zoom,
        Lazy<StaticZoomService> staticZoom,
        Lazy<BreakTimerService> breakTimer,
        Lazy<DemoTypeService> demoType,
        Lazy<CurtainService> curtain,
        Lazy<FreezeFrameService> freeze,
        Lazy<PointerEffectsService> pointerEffects,
        Lazy<BlackoutRegionService> blackout,
        Lazy<CodeFocusService> codeFocus,
        Lazy<KeyVisualizerService> keyVisualizer,
        Action<string>? onInspectCategory = null,
        Action? onOpenSettings = null,
        Action? onOpenCommandPalette = null,
        Action? onOpenRadialMenu = null,
        Action? onToggleOrientation = null,
        Action? onRecordScreen = null)
    {
        var reg = new CommandRegistry();

        // -------------------------------------------------------------
        // 1. ANNOTATE (Category: Annotate)
        // -------------------------------------------------------------
        reg.Register(new()
        {
            Id = "cursor",
            Name = "Cursor / Pointer",
            Category = CapabilityCategory.Annotate,
            Description = "Normal interactive desktop cursor with transparent click-through",
            IconKey = "Fluent.Cursor.Regular",
            Shortcut = "Esc",
            SearchTags = ["mouse", "pointer", "arrow", "click", "interact", "escape"],
            Execute = () => overlay.SetTool(ToolKind.Cursor),
            IsActive = () => overlay.Settings.Tool == ToolKind.Cursor
        });

        reg.Register(new()
        {
            Id = "pen",
            Name = "Ballpoint Pen",
            Category = CapabilityCategory.Annotate,
            Description = "Smooth freehand drawing with vector curve fitting",
            IconKey = "Fluent.Pen.Regular",
            Shortcut = "P",
            SearchTags = ["draw", "write", "ink", "ballpoint", "sketch"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Pen);
                overlay.SetPenMode(PenMode.Ballpoint);
            },
            IsActive = () => overlay.Settings.Tool == ToolKind.Pen && overlay.Settings.PenMode == PenMode.Ballpoint
        });

        reg.Register(new()
        {
            Id = "pen.pencil",
            Name = "Pencil",
            Category = CapabilityCategory.Annotate,
            Description = "Subtle translucent sketch strokes",
            IconKey = "Fluent.Pencil.Regular",
            SearchTags = ["draw", "sketch", "pencil", "graphite"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Pen);
                overlay.SetPenMode(PenMode.Pencil);
            },
            IsActive = () => overlay.Settings.Tool == ToolKind.Pen && overlay.Settings.PenMode == PenMode.Pencil
        });

        reg.Register(new()
        {
            Id = "pen.marker",
            Name = "Marker",
            Category = CapabilityCategory.Annotate,
            Description = "Bold opaque marker strokes for emphasis",
            IconKey = "Fluent.Highlight.Regular",
            SearchTags = ["bold", "marker", "draw"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Pen);
                overlay.SetPenMode(PenMode.Marker);
            },
            IsActive = () => overlay.Settings.Tool == ToolKind.Pen && overlay.Settings.PenMode == PenMode.Marker
        });

        reg.Register(new()
        {
            Id = "pen.dashed",
            Name = "Dashed Ink",
            Category = CapabilityCategory.Annotate,
            Description = "Draw dashed annotation strokes",
            IconKey = "Fluent.Pen.Dashed.Regular",
            SearchTags = ["dashed", "dash", "broken"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Pen);
                overlay.SetPenMode(PenMode.Dashed);
            }
        });

        reg.Register(new()
        {
            Id = "highlighter",
            Name = "Highlighter",
            Category = CapabilityCategory.Annotate,
            Description = "Translucent highlighting over text, code, or images",
            IconKey = "Fluent.Highlight.Regular",
            Shortcut = "H",
            SearchTags = ["highlight", "yellow", "translucent", "glow"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Highlighter);
                overlay.SetPenMode(PenMode.Highlighter);
            },
            IsActive = () => overlay.Settings.Tool == ToolKind.Highlighter
        });

        reg.Register(new()
        {
            Id = "eraser",
            Name = "Eraser",
            Category = CapabilityCategory.Annotate,
            Description = "Stroke and vector object eraser",
            IconKey = "Fluent.EraserTool.Regular",
            Shortcut = "E",
            SearchTags = ["erase", "delete", "remove", "rub"],
            Execute = () => overlay.SetTool(ToolKind.Eraser),
            IsActive = () => overlay.Settings.Tool == ToolKind.Eraser
        });

        reg.Register(new()
        {
            Id = "tool.eyedropper",
            Name = "Eyedropper",
            Category = CapabilityCategory.Annotate,
            Description = "Pick a colour from anywhere on screen to use as pen colour",
            IconKey = "Fluent.Eyedropper.Regular",
            Shortcut = "I",
            SearchTags = ["eyedropper", "color", "picker", "pipette", "dropper", "sample", "colour"],
            Execute = () => overlay.SetTool(ToolKind.Eyedropper),
            IsActive = () => overlay.Settings.Tool == ToolKind.Eyedropper
        });

        reg.Register(new()
        {
            Id = "select",
            Name = "Selection Tool",
            Category = CapabilityCategory.Annotate,
            Description = "Select, move, and transform drawn annotations",
            IconKey = "Fluent.Crop.Regular",
            SearchTags = ["select", "move", "drag", "transform"],
            Execute = () => overlay.SetTool(ToolKind.Select),
            IsActive = () => overlay.Settings.Tool == ToolKind.Select
        });

        reg.Register(new()
        {
            Id = "undo",
            Name = "Undo",
            Category = CapabilityCategory.Annotate,
            Description = "Revert last drawn stroke, shape or action",
            IconKey = "Fluent.ArrowUndo.Regular",
            Shortcut = "Ctrl+Z",
            SearchTags = ["undo", "back", "revert"],
            Execute = () => overlay.Undo()
        });

        reg.Register(new()
        {
            Id = "redo",
            Name = "Redo",
            Category = CapabilityCategory.Annotate,
            Description = "Reapply previously undone stroke or shape",
            IconKey = "Fluent.ArrowRedo.Regular",
            Shortcut = "Ctrl+Y",
            SearchTags = ["redo", "repeat", "forward"],
            Execute = () => overlay.Redo()
        });

        reg.Register(new()
        {
            Id = "clear",
            Name = "Clear Screen",
            Category = CapabilityCategory.Annotate,
            Description = "Undoable erase of all annotations across all monitors",
            IconKey = "Fluent.Dismiss.Regular",
            Shortcut = "Ctrl+Shift+Del",
            SearchTags = ["clear", "wipe", "clean", "reset"],
            Execute = () => overlay.Clear()
        });

        // -------------------------------------------------------------
        // 2. SHAPES (Category: Shapes)
        // -------------------------------------------------------------
        reg.Register(new()
        {
            Id = "shapes",
            Name = "Shapes Hub",
            Category = CapabilityCategory.Shapes,
            Description = "Vector diagram shapes, arrows, markers, and callouts",
            IconKey = "Fluent.Shapes.Regular",
            Shortcut = "S",
            SearchTags = ["shapes", "rect", "arrow", "circle", "diagram"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                onInspectCategory?.Invoke("shapes");
            },
            IsActive = () => overlay.Settings.Tool == ToolKind.Shape
        });

        reg.Register(new()
        {
            Id = "arrow",
            Name = "Process Arrow",
            Category = CapabilityCategory.Shapes,
            Description = "Straight directional arrow with clean arrowhead",
            IconKey = "Fluent.ArrowRight.Regular",
            Shortcut = "A",
            SearchTags = ["arrow", "point", "direction", "process"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                overlay.Settings.Shape = ShapeKind.Arrow;
            },
            IsActive = () => overlay.Settings.Tool == ToolKind.Shape && overlay.Settings.Shape == ShapeKind.Arrow
        });

        reg.Register(new()
        {
            Id = "doublearrow",
            Name = "Double-Headed Arrow",
            Category = CapabilityCategory.Shapes,
            Description = "Bidirectional connector arrow",
            IconKey = "Fluent.DoubleArrow.Regular",
            SearchTags = ["double", "arrow", "both", "bidirectional"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                overlay.Settings.Shape = ShapeKind.DoubleArrow;
            }
        });

        reg.Register(new()
        {
            Id = "line",
            Name = "Line",
            Category = CapabilityCategory.Shapes,
            Description = "Clean straight line segment",
            IconKey = "Fluent.Line.Regular",
            SearchTags = ["line", "straight", "rule"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                overlay.Settings.Shape = ShapeKind.Line;
            }
        });

        reg.Register(new()
        {
            Id = "rectangle",
            Name = "Rectangle / Frame",
            Category = CapabilityCategory.Shapes,
            Description = "Bounding rectangle for framing code or UI elements",
            IconKey = "Fluent.Rectangle.Regular",
            Shortcut = "R",
            SearchTags = ["rectangle", "box", "square", "frame"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                overlay.Settings.Shape = ShapeKind.Rectangle;
            },
            IsActive = () => overlay.Settings.Tool == ToolKind.Shape && overlay.Settings.Shape == ShapeKind.Rectangle
        });

        reg.Register(new()
        {
            Id = "roundrect",
            Name = "Rounded Rectangle",
            Category = CapabilityCategory.Shapes,
            Description = "Smooth rounded corner box",
            IconKey = "Fluent.RoundRect.Regular",
            SearchTags = ["rounded", "rectangle", "pill", "box"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                overlay.Settings.Shape = ShapeKind.RoundedRectangle;
            }
        });

        reg.Register(new()
        {
            Id = "ellipse",
            Name = "Ellipse / Circle",
            Category = CapabilityCategory.Shapes,
            Description = "Oval or circular emphasis ring",
            IconKey = "Fluent.Circle.Regular",
            Shortcut = "O",
            SearchTags = ["circle", "ellipse", "oval", "round"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                overlay.Settings.Shape = ShapeKind.Ellipse;
            }
        });

        reg.Register(new()
        {
            Id = "connector",
            Name = "Smart Connector",
            Category = CapabilityCategory.Shapes,
            Description = "Flowchart line connector between diagram elements",
            IconKey = "Fluent.Connector.Regular",
            SearchTags = ["connector", "elbow", "link", "flowchart"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                overlay.Settings.Shape = ShapeKind.Connector;
            }
        });

        reg.Register(new()
        {
            Id = "cloud",
            Name = "Cloud",
            Category = CapabilityCategory.Shapes,
            Description = "Cloud diagram shape for network/cloud architecture",
            IconKey = "Fluent.Cloud.Regular",
            SearchTags = ["cloud", "azure", "network", "aws"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                overlay.Settings.Shape = ShapeKind.Cloud;
            }
        });

        reg.Register(new()
        {
            Id = "database",
            Name = "Database Cylinder",
            Category = CapabilityCategory.Shapes,
            Description = "Database cylinder for storage and data flow diagrams",
            IconKey = "Fluent.Database.Regular",
            SearchTags = ["database", "sql", "storage", "table"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                overlay.Settings.Shape = ShapeKind.Database;
            }
        });

        reg.Register(new()
        {
            Id = "callout",
            Name = "Callout Bubble",
            Category = CapabilityCategory.Shapes,
            Description = "Speech / annotation bubble with pointer tail",
            IconKey = "Fluent.Comment.Regular",
            SearchTags = ["callout", "bubble", "speech", "note"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                overlay.Settings.Shape = ShapeKind.Callout;
            }
        });

        reg.Register(new()
        {
            Id = "stepmarker",
            Name = "Numbered Step Marker",
            Category = CapabilityCategory.Shapes,
            Description = "Click to place sequential numbers (1, 2, 3...) for tutorial steps",
            IconKey = "Fluent.TextNumberFormat.Regular",
            Shortcut = "1",
            SearchTags = ["number", "step", "marker", "sequence", "tutorial"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.NumberMarker);
            },
            IsActive = () => overlay.Settings.Tool == ToolKind.NumberMarker
        });

        reg.Register(new()
        {
            Id = "check",
            Name = "Checkmark Badge",
            Category = CapabilityCategory.Shapes,
            Description = "Stamp a green/accent checkmark icon",
            IconKey = "Fluent.Checkmark.Regular",
            SearchTags = ["check", "tick", "correct", "pass"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                overlay.Settings.Shape = ShapeKind.Check;
            }
        });

        reg.Register(new()
        {
            Id = "cross",
            Name = "Cross Badge",
            Category = CapabilityCategory.Shapes,
            Description = "Stamp a red cross icon",
            IconKey = "Fluent.Dismiss.Regular",
            SearchTags = ["cross", "wrong", "x", "error"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Shape);
                overlay.Settings.Shape = ShapeKind.Cross;
            }
        });

        reg.Register(new()
        {
            Id = "text",
            Name = "Text / Callout",
            Category = CapabilityCategory.Shapes,
            Description = "Inline typography text annotations with background styling",
            IconKey = "Fluent.Text.Regular",
            Shortcut = "T",
            SearchTags = ["text", "font", "type", "label", "words"],
            Execute = () => overlay.SetTool(ToolKind.Text),
            IsActive = () => overlay.Settings.Tool == ToolKind.Text
        });

        // -------------------------------------------------------------
        // 3. PRESENT (Category: Present)
        // -------------------------------------------------------------
        reg.Register(new()
        {
            Id = "present",
            Name = "Present Hub",
            Category = CapabilityCategory.Present,
            Description = "Audience focus tools: laser, spotlight, click pulse, code band",
            IconKey = "Fluent.Target.Regular",
            Shortcut = "L",
            SearchTags = ["present", "laser", "spotlight", "focus"],
            Execute = () =>
            {
                overlay.SetTool(ToolKind.Laser);
                onInspectCategory?.Invoke("laser");
            },
            IsActive = () => overlay.Settings.Tool == ToolKind.Laser
        });

        reg.Register(new()
        {
            Id = "laser",
            Name = "Laser Pointer",
            Category = CapabilityCategory.Present,
            Description = "Glowing Keynote-grade laser dot with subtle trailing bloom",
            IconKey = "Fluent.Laser.Regular",
            Shortcut = "Ctrl+L",
            SearchTags = ["laser", "pointer", "dot", "trail"],
            Execute = () => overlay.SetTool(ToolKind.Laser),
            IsActive = () => overlay.Settings.Tool == ToolKind.Laser
        });

        reg.Register(new()
        {
            Id = "present.spotlight",
            Name = "Screen Spotlight",
            Category = CapabilityCategory.Present,
            Description = "Dims the screen with a circular light following the cursor",
            IconKey = "Fluent.Circle.Regular",
            Shortcut = "M",
            SearchTags = ["spotlight", "dim", "focus", "dark", "present"],
            Execute = () => overlay.SetTool(ToolKind.Spotlight),
            IsActive = () => overlay.Settings.Tool == ToolKind.Spotlight
        });

        reg.Register(new()
        {
            Id = "halo",
            Name = "Cursor Halo",
            Category = CapabilityCategory.Present,
            Description = "High-contrast colored ring around the mouse cursor",
            IconKey = "Fluent.Circle.Regular",
            SearchTags = ["halo", "ring", "cursor", "highlight"],
            Execute = () =>
            {
                if (pointerEffects.Value.IsVisible) pointerEffects.Value.Hide();
                else pointerEffects.Value.Show(new() { Spotlight = false, CursorHalo = true, ClickPulse = true });
            }
        });

        reg.Register(new()
        {
            Id = "codefocus",
            Name = "Code Focus Band",
            Category = CapabilityCategory.Present,
            Description = "Horizontal highlight slit that dims lines above and below in code/terminals",
            IconKey = "Fluent.Crop.Regular",
            Shortcut = "F",
            SearchTags = ["code", "focus", "band", "line", "terminal", "slit"],
            Execute = () => codeFocus.Value.Toggle(overlay.Settings.CodeFocusBandHeight, overlay.Settings.CodeFocusDimOpacity),
            IsActive = () => codeFocus.IsValueCreated && codeFocus.Value.IsVisible
        });

        reg.Register(new()
        {
            Id = "clickvis",
            Name = "Mouse Click Visualizer",
            Category = CapabilityCategory.Present,
            Description = "Visual pulse ripples when clicking mouse during demos",
            IconKey = "Fluent.CursorClick.Regular",
            SearchTags = ["click", "ripple", "visualizer", "mouse"],
            Execute = () =>
            {
                overlay.Settings.ClickVisualizerEnabled = !overlay.Settings.ClickVisualizerEnabled;
                if (overlay.Settings.ClickVisualizerEnabled)
                    pointerEffects.Value.Show(new() { ClickPulse = true, LaserTrail = false });
                else
                    pointerEffects.Value.Hide();
            },
            IsActive = () => overlay.Settings.ClickVisualizerEnabled
        });

        reg.Register(new()
        {
            Id = "keyvis",
            Name = "Keyboard Shortcut Visualizer",
            Category = CapabilityCategory.Present,
            Description = "Floating HUD displaying pressed shortcut keys (e.g. Ctrl+C, F5)",
            IconKey = "Fluent.Desktop.Regular",
            Shortcut = "K",
            SearchTags = ["keyboard", "key", "shortcut", "hud", "keys"],
            Execute = () => keyVisualizer.Value.Toggle(),
            IsActive = () => keyVisualizer.IsValueCreated && keyVisualizer.Value.IsEnabled
        });

        reg.Register(new()
        {
            Id = "breaktimer",
            Name = "Presentation Timer / Clock",
            Category = CapabilityCategory.Present,
            Description = "Countdown break timer for workshop pauses and deadlines",
            IconKey = "Fluent.Clock.Regular",
            SearchTags = ["timer", "clock", "countdown", "break", "pause"],
            Execute = () => breakTimer.Value.Start(TimeSpan.FromMinutes(10))
        });

        reg.Register(new()
        {
            Id = "curtain",
            Name = "Screen Curtain",
            Category = CapabilityCategory.Present,
            Description = "Blanks the display temporarily for discussions without disconnecting projector",
            IconKey = "Fluent.EyeOff.Regular",
            SearchTags = ["curtain", "blank", "black", "hide"],
            Execute = () => curtain.Value.Show(new() { Color = System.Windows.Media.Colors.Black })
        });

        reg.Register(new()
        {
            Id = "demotype",
            Name = "DemoType Simulation",
            Category = CapabilityCategory.Present,
            Description = "Simulates authentic human typing into target editors",
            IconKey = "Fluent.Text.Regular",
            SearchTags = ["type", "demo", "automated", "typing"],
            Execute = () => demoType.Value.Open("Console.WriteLine(\"ScreenCanvas Trainer Demo\");")
        });

        reg.Register(new()
        {
            Id = "slidenext",
            Name = "Slide Next",
            Category = CapabilityCategory.Present,
            Description = "Advances PowerPoint / PDF presentation slide",
            IconKey = "Fluent.ArrowRight.Regular",
            SearchTags = ["slide", "next", "powerpoint", "advance"],
            Execute = () => System.Windows.Forms.SendKeys.SendWait("{RIGHT}")
        });

        reg.Register(new()
        {
            Id = "slideprev",
            Name = "Slide Previous",
            Category = CapabilityCategory.Present,
            Description = "Moves to previous PowerPoint / PDF presentation slide",
            IconKey = "Fluent.ArrowUndo.Regular",
            SearchTags = ["slide", "previous", "powerpoint", "back"],
            Execute = () => System.Windows.Forms.SendKeys.SendWait("{LEFT}")
        });

        // -------------------------------------------------------------
        // 4. SCREEN (Category: Screen)
        // -------------------------------------------------------------
        reg.Register(new()
        {
            Id = "zoom",
            Name = "Live Zoom",
            Category = CapabilityCategory.Screen,
            Description = "Hardware-accelerated live desktop magnification with pan",
            IconKey = "Fluent.ZoomIn.Regular",
            Shortcut = "Z",
            SearchTags = ["zoom", "magnify", "live", "scale"],
            Execute = () =>
            {
                if (zoom.Value.State != ZoomState.Inactive) zoom.Value.Reset();
                else zoom.Value.TrySetLiveZoom(2.0, new System.Windows.Point(SystemParameters.PrimaryScreenWidth / 2, SystemParameters.PrimaryScreenHeight / 2));
            },
            IsActive = () => zoom.IsValueCreated && zoom.Value.State != ZoomState.Inactive
        });

        reg.Register(new()
        {
            Id = "staticzoom",
            Name = "Static Frozen Zoom",
            Category = CapabilityCategory.Screen,
            Description = "Freeze screen and zoom in for detailed inspection",
            IconKey = "Fluent.ZoomIn.Regular",
            SearchTags = ["static", "zoom", "freeze", "inspect"],
            Execute = () => staticZoom.Value.Show(2.0)
        });

        reg.Register(new()
        {
            Id = "freeze",
            Name = "Freeze + Annotate",
            Category = CapabilityCategory.Screen,
            Description = "Freeze desktop frame and immediately draw annotations over it",
            IconKey = "Fluent.Snowflake.Regular",
            Shortcut = "Ctrl+F",
            SearchTags = ["freeze", "pause", "frame", "draw", "hold"],
            Execute = () =>
            {
                freeze.Value.Freeze();
                overlay.SetTool(ToolKind.Pen);
            }
        });

        reg.Register(new()
        {
            Id = "capture.screen",
            Name = "Capture Screen",
            Category = CapabilityCategory.Screen,
            Description = "Full virtual desktop snapshot copied to clipboard",
            IconKey = "Fluent.Camera.Regular",
            Shortcut = "PrintScreen",
            SearchTags = ["screenshot", "snip", "capture", "copy"],
            Execute = () => capture.CopyDesktopToClipboard()
        });

        reg.Register(new()
        {
            Id = "capture.region",
            Name = "Region Snip",
            Category = CapabilityCategory.Screen,
            Description = "Crosshair rectangular region capture to clipboard",
            IconKey = "Fluent.Crop.Regular",
            Shortcut = "Ctrl+Shift+S",
            SearchTags = ["snip", "crop", "region", "area"],
            Execute = () =>
            {
                var bmp = capture.CaptureInteractiveRegion();
                if (bmp is not null) capture.CopyToClipboard(bmp);
            }
        });

        reg.Register(new()
        {
            Id = "ocr.region",
            Name = "OCR Screen Text",
            Category = CapabilityCategory.Screen,
            Description = "Select screen region and extract text via Windows native OCR",
            IconKey = "Fluent.Text.Regular",
            SearchTags = ["ocr", "recognize", "text", "extract"],
            Execute = async () =>
            {
                var ocr = new ScreenCanvas.Ocr.LocalOcrService();
                var bmp = capture.CaptureInteractiveRegion();
                if (bmp is not null)
                {
                    var res = await ocr.RecognizeAsync(bmp);
                    if (!string.IsNullOrEmpty(res.Text)) System.Windows.Clipboard.SetText(res.Text);
                }
            }
        });

        // -------------------------------------------------------------
        // 5. PRIVACY (Category: Privacy)
        // -------------------------------------------------------------
        reg.Register(new()
        {
            Id = "privacy.blackout",
            Name = "Blackout Region",
            Category = CapabilityCategory.Privacy,
            Description = "Cover confidential keys, emails, or credentials with black box",
            IconKey = "Fluent.EyeOff.Regular",
            SearchTags = ["privacy", "blackout", "cover", "hide", "censor"],
            Execute = () => blackout.Value.ShowInteractive()
        });

        reg.Register(new()
        {
            Id = "privacy.blur",
            Name = "Blur / Pixelate",
            Category = CapabilityCategory.Privacy,
            Description = "Draw a rectangle to pixelate screen content underneath for privacy",
            IconKey = "Fluent.EyeOff.Regular",
            SearchTags = ["blur", "pixelate", "privacy", "censor", "obscure", "redact"],
            Execute = () => overlay.SetTool(ToolKind.BlurPixelate),
            IsActive = () => overlay.Settings.Tool == ToolKind.BlurPixelate
        });

        // -------------------------------------------------------------
        // 6. BOARD (Category: Board)
        // -------------------------------------------------------------
        reg.Register(new()
        {
            Id = "whiteboard",
            Name = "Whiteboard",
            Category = CapabilityCategory.Board,
            Description = "Clean full-screen white canvas for diagramming and teaching",
            IconKey = "Fluent.Whiteboard.Regular",
            Shortcut = "W",
            SearchTags = ["whiteboard", "white", "canvas", "sketch"],
            Execute = () =>
            {
                overlay.SetBoard(System.Windows.Media.Colors.White);
                overlay.SetTool(ToolKind.Pen);
            }
        });

        reg.Register(new()
        {
            Id = "blackboard",
            Name = "Blackboard",
            Category = CapabilityCategory.Board,
            Description = "Dark matte canvas for high-contrast teaching notes",
            IconKey = "Fluent.Board.Regular",
            Shortcut = "B",
            SearchTags = ["blackboard", "black", "dark", "chalkboard"],
            Execute = () =>
            {
                overlay.SetBoard(System.Windows.Media.Color.FromRgb(24, 24, 27));
                overlay.SetTool(ToolKind.Pen);
            }
        });

        reg.Register(new()
        {
            Id = "transparentboard",
            Name = "Clear Canvas Overlay",
            Category = CapabilityCategory.Board,
            Description = "Return to transparent desktop annotation surface",
            IconKey = "Fluent.Desktop.Regular",
            SearchTags = ["transparent", "desktop", "clear", "surface"],
            Execute = () => overlay.SetBoard(null)
        });

        // -------------------------------------------------------------
        // 7. RECORD (Category: Record)
        // -------------------------------------------------------------
        reg.Register(new()
        {
            Id = "record.screen",
            Name = "Record Screen",
            Category = CapabilityCategory.Record,
            Description = "Record desktop video with cursor and live annotations",
            IconKey = "Fluent.Target.Regular",
            SearchTags = ["record", "video", "gif", "capture", "screen"],
            Execute = () => onRecordScreen?.Invoke()
        });

        // -------------------------------------------------------------
        // 8. TOOLS & NAVIGATION (Category: Tools)
        // -------------------------------------------------------------
        reg.Register(new()
        {
            Id = "commandpalette",
            Name = "Command Palette",
            Category = CapabilityCategory.Tools,
            Description = "Search and execute any tool, preset or setting instantly",
            IconKey = "Fluent.Search.Regular",
            Shortcut = "Ctrl+Shift+P",
            SearchTags = ["palette", "command", "search", "spotlight", "find"],
            Execute = () => onOpenCommandPalette?.Invoke()
        });

        reg.Register(new()
        {
            Id = "radialmenu",
            Name = "Radial Quick Menu",
            Category = CapabilityCategory.Tools,
            Description = "Circular HUD menu around cursor for rapid tool switching",
            IconKey = "Fluent.Circle.Regular",
            Shortcut = "` (Grave)",
            SearchTags = ["radial", "pie", "menu", "quick", "wheel"],
            Execute = () => onOpenRadialMenu?.Invoke()
        });

        reg.Register(new()
        {
            Id = "settings",
            Name = "Preferences & Settings",
            Category = CapabilityCategory.Tools,
            Description = "Configure hotkeys, colors, appearance, and display parameters",
            IconKey = "Fluent.Settings.Regular",
            Shortcut = "Ctrl+,",
            SearchTags = ["settings", "preferences", "config", "options"],
            Execute = () => onOpenSettings?.Invoke()
        });

        reg.Register(new()
        {
            Id = "orientation",
            Name = "Toggle Horizontal / Vertical",
            Category = CapabilityCategory.Tools,
            Description = "Switch toolbar between horizontal bar and vertical strip",
            IconKey = "Fluent.Shapes.Regular",
            SearchTags = ["horizontal", "vertical", "orientation", "rotate", "layout"],
            Execute = () => onToggleOrientation?.Invoke()
        });

        return reg;
    }
}
