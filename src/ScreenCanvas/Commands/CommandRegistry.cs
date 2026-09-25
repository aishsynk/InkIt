using System.Windows;
using ScreenCanvas.Core;
using ScreenCanvas.Overlay;
using ScreenCanvas.Capture;
using ScreenCanvas.Presentation;
using ScreenCanvas.Zoom;
using ScreenCanvas.Privacy;
using ScreenCanvas.UI;

namespace ScreenCanvas.Commands;

/// <summary>Services and toolbar the command registry drives.</summary>
public sealed class CommandContext
{
    public required IOverlayManager Overlay { get; init; }
    public required ICaptureService Capture { get; init; }
    public required Lazy<WindowsZoomEngine> Zoom { get; init; }
    public required Lazy<StaticZoomService> StaticZoom { get; init; }
    public required Lazy<BreakTimerService> BreakTimer { get; init; }
    public required Lazy<DemoTypeService> DemoType { get; init; }
    public required Lazy<CurtainService> Curtain { get; init; }
    public required Lazy<FreezeFrameService> Freeze { get; init; }
    public required Lazy<PointerEffectsService> PointerEffects { get; init; }
    public required Lazy<BlackoutRegionService> Blackout { get; init; }
    public required Lazy<CodeFocusService> CodeFocus { get; init; }
    public required Lazy<KeyVisualizerService> KeyVisualizer { get; init; }
    public required ToolbarWindow Toolbar { get; init; }
}

public sealed class CommandRegistry
{
    private readonly List<CommandItem> _commands = [];
    public IReadOnlyList<CommandItem> Commands => _commands;

    public void Register(CommandItem command) => _commands.Add(command);

    public CommandItem? Find(string id) =>
        _commands.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<CommandItem> GetByCategory(CapabilityCategory category) =>
        _commands.Where(c => c.Category == category);

    public IEnumerable<CommandItem> Search(string query) =>
        string.IsNullOrWhiteSpace(query) ? _commands : _commands.Where(c => c.Matches(query));

    /// <summary>Builds the registry from the InkIt design (commandsData.ts) plus InkIt desktop extras.</summary>
    public static CommandRegistry Create(CommandContext ctx)
    {
        var reg = new CommandRegistry();
        var overlay = ctx.Overlay;
        var toolbar = ctx.Toolbar;
        var s = overlay.Settings;

        void Add(string id, string name, CapabilityCategory category, string description, string icon, string? shortcut, string[] tags,
            Action execute, Func<bool>? isActive = null) =>
            reg.Register(new CommandItem
            {
                Id = id, Name = name, Category = category, Description = description, IconKey = icon,
                Shortcut = shortcut, SearchTags = tags, Execute = execute, IsActive = isActive
            });

        void Shape(ShapeKind kind) => overlay.SetShape(kind);

        // ---------------------------------------------------------------- Annotate
        Add("annot.pen", "Freehand Pen", CapabilityCategory.Annotate, "Smooth vector drawing with pressure support and 13 pen modes", "Pen", "P",
            ["pen", "draw", "ink", "ballpoint", "calligraphy"], () => { overlay.SetPenMode(PenMode.Ballpoint); toolbar.RememberPenMode(PenMode.Ballpoint); },
            () => s.Tool == ToolKind.Pen);
        Add("annot.highlighter", "Broad Highlighter", CapabilityCategory.Annotate, "115-alpha translucent ink for marking text and diagrams without obscuring content", "Highlighter", "H",
            ["highlight", "marker", "yellow", "translucent"], () => toolbar.SelectTool(ToolKind.Highlighter), () => s.Tool == ToolKind.Highlighter);
        Add("annot.eraser", "Stroke / Point Eraser", CapabilityCategory.Annotate, "Remove individual ink strokes or point-erase annotations", "Eraser", "E",
            ["erase", "delete", "rub", "clear stroke"], () => overlay.SetTool(ToolKind.Eraser), () => s.Tool == ToolKind.Eraser);
        Add("annot.undo", "Undo Annotation", CapabilityCategory.Annotate, "Revert the last stroke or shape action from the undo stack", "Undo", "Ctrl+Shift+Z",
            ["undo", "revert", "back"], overlay.Undo);
        Add("annot.redo", "Redo Annotation", CapabilityCategory.Annotate, "Re-apply previously undone stroke or shape", "Redo", "Ctrl+Shift+Y",
            ["redo", "forward", "repeat"], overlay.Redo);
        Add("annot.clear", "Clear All Annotations", CapabilityCategory.Annotate, "Wipe all drawings, shapes, and markers on current overlay", "Trash2", "Ctrl+Shift+Del",
            ["clear", "wipe", "reset canvas", "clean"], () => { overlay.Clear(); Toast.Show("Overlay Canvas Cleared"); });
        Add("annot.text", "Floating Text Box", CapabilityCategory.Annotate, "Click anywhere on screen to type crisp vector text annotations", "Type", "T",
            ["text", "label", "type", "font", "notes"], () => overlay.SetTool(ToolKind.Text), () => s.Tool == ToolKind.Text);
        Add("annot.marker", "Numbered Step Marker", CapabilityCategory.Annotate, "Stamp sequential circular badges (1, 2, 3...) for presentation steps", "ListOrdered", "N",
            ["step", "number", "sequence", "badge", "marker"], () => overlay.SetTool(ToolKind.NumberMarker), () => s.Tool == ToolKind.NumberMarker);
        Add("annot.disappearing_pen", "Disappearing Ink", CapabilityCategory.Annotate, "Temporary ink that smoothly fades out after 3 seconds", "Sparkles", "D",
            ["fade", "disappear", "temporary", "ghost ink"], () => { overlay.SetPenMode(PenMode.Disappearing); toolbar.RememberPenMode(PenMode.Disappearing); },
            () => s.Tool == ToolKind.Pen && s.PenMode == PenMode.Disappearing);
        Add("annot.select", "Multi-Select Marquee", CapabilityCategory.Annotate, "Select multiple elements to batch move, recolor, duplicate, or delete", "BoxSelect", "V",
            ["select", "marquee", "move", "batch", "duplicate"], () => overlay.SetTool(ToolKind.Select), () => s.Tool == ToolKind.Select);

        // ---------------------------------------------------------------- Shapes
        Add("shape.line", "Straight Line", CapabilityCategory.Shapes, "Draw snapped straight lines with configurable thickness", "Minus", "L",
            ["line", "rule", "straight", "vector"], () => Shape(ShapeKind.Line));
        Add("shape.arrow", "Directional Arrow", CapabilityCategory.Shapes, "Precision vector arrow with dynamic chevron head calculation", "ArrowRight", "A",
            ["arrow", "pointer", "direct", "vector arrow"], () => Shape(ShapeKind.Arrow));
        Add("shape.double_arrow", "Bidirectional Double Arrow", CapabilityCategory.Shapes, "Arrow with arrowhead indicators on both endpoints", "MoveHorizontal", "Shift+A",
            ["double arrow", "span", "measurement", "dimension"], () => Shape(ShapeKind.DoubleArrow));
        Add("shape.rectangle", "Bounding Rectangle", CapabilityCategory.Shapes, "Box callout with optional semi-transparent fill", "Square", "R",
            ["rect", "rectangle", "box", "callout", "frame"], () => Shape(ShapeKind.Rectangle));
        Add("shape.rounded_rect", "Rounded Rectangle", CapabilityCategory.Shapes, "Smooth pill-corner container box for UI annotations", "SquareDot", "Shift+R",
            ["rounded rect", "pill", "smooth frame"], () => Shape(ShapeKind.RoundedRectangle));
        Add("shape.ellipse", "Ellipse / Circle", CapabilityCategory.Shapes, "Circular or oval highlight focus ring", "Circle", "O",
            ["circle", "ellipse", "round", "halo"], () => Shape(ShapeKind.Ellipse));
        Add("shape.diamond", "Decision Diamond", CapabilityCategory.Shapes, "Flowchart decision node polygon marker", "Diamond", "Shift+D",
            ["diamond", "rhombus", "flowchart"], () => Shape(ShapeKind.Diamond));
        Add("shape.fill_toggle", "Toggle Shape Fill", CapabilityCategory.Shapes, "Enable or disable 20% interior tint on drawn geometric shapes", "PaintBucket", "F",
            ["fill", "tint", "solid", "color fill"], () =>
            {
                var enabled = !s.ShapeFillEnabled;
                overlay.SetShapeFill(enabled);
                Toast.Show($"Shape fill {(enabled ? "enabled" : "disabled")}");
            }, () => s.ShapeFillEnabled);
        Add("shape.hub", "Shapes Hub", CapabilityCategory.Shapes, "Activate vector shapes and open the Shape & Geometry inspector", "Shapes", "S",
            ["shapes", "geometry", "inspector"], () => { overlay.SetShape(s.Shape); toolbar.ShowInspector("shape"); }, () => s.Tool == ToolKind.Shape);

        // ---------------------------------------------------------------- Present
        Add("present.laser", "Laser Pointer", CapabilityCategory.Present, "Glowing laser point with smooth decaying physics tail", "Flame", "Ctrl+Shift+L",
            ["laser", "pointer", "red dot", "tail", "presenter"], () => overlay.SetTool(ToolKind.Laser), () => s.Tool == ToolKind.Laser);
        Add("present.spotlight", "Focus Spotlight", CapabilityCategory.Present, "Dim the entire monitor except a movable circular spotlight lens", "SunMedium", "Ctrl+Shift+S",
            ["spotlight", "dim", "aperture", "focus light"], () => overlay.SetTool(ToolKind.Spotlight), () => s.Tool == ToolKind.Spotlight);
        Add("present.break_timer", "Presenter Break Timer", CapabilityCategory.Present, "Floating countdown clock for talk recesses and intervals", "Timer", "Ctrl+Shift+B",
            ["timer", "break", "countdown", "clock"], () => toolbar.StartBreakTimer(toolbar.BreakTimerMinutes), () => ctx.BreakTimer.IsValueCreated && ctx.BreakTimer.Value.IsRunning);
        Add("present.key_visualizer", "Keystroke Visualizer HUD", CapabilityCategory.Present, "Non-activating overlay HUD broadcasting shortcuts as typed", "Keyboard", "Ctrl+Shift+K",
            ["keys", "keystroke", "hud", "shortcut display"], () =>
            {
                ctx.KeyVisualizer.Value.Toggle();
                Toast.Show($"Keystroke Visualizer {(ctx.KeyVisualizer.Value.IsEnabled ? "on" : "off")}");
            }, () => ctx.KeyVisualizer.IsValueCreated && ctx.KeyVisualizer.Value.IsEnabled);
        Add("present.demo_type", "Demo Type Simulator", CapabilityCategory.Present, "Simulate realistic human typing into target window for live demos", "Terminal", "Ctrl+Shift+T",
            ["demo", "type", "script", "auto type", "automation"], () => toolbar.RunDemoType("// Executing automated DemoType script in target window\nconsole.log(\"InkIt Live Demo v1.0.0\");"));
        Add("present.pointer_effects", "Pointer Click Ripples", CapabilityCategory.Present, "Emit animated pulse rings on left, right, and drag mouse events", "MousePointerClick", "Alt+Shift+P",
            ["ripple", "click ripple", "cursor pulse", "rings"], () =>
            {
                s.ClickVisualizerEnabled = !(ctx.PointerEffects.IsValueCreated && ctx.PointerEffects.Value.IsVisible);
                if (s.ClickVisualizerEnabled) ctx.PointerEffects.Value.Show(new() { ClickPulse = true, LaserTrail = false });
                else ctx.PointerEffects.Value.Hide();
                Toast.Show($"Pointer click ripples {(s.ClickVisualizerEnabled ? "on" : "off")}");
            }, () => ctx.PointerEffects.IsValueCreated && ctx.PointerEffects.Value.IsVisible);

        // ---------------------------------------------------------------- Screen
        Add("screen.zoom_toggle", "Toggle Live Magnifier", CapabilityCategory.Screen, "Magnification API 60FPS cursor-following live zoom (1x to 16x)", "ZoomIn", "Ctrl+Shift+5",
            ["zoom", "magnify", "scale", "lens", "glide"], toolbar.ToggleZoom, () => toolbar.IsZoomActive);
        Add("screen.capture_region", "Capture Screen Region", CapabilityCategory.Screen, "Drag crosshair selection to capture rectangle to clipboard or file", "Crop", "Ctrl+Shift+4",
            ["capture", "snip", "screenshot", "region", "crop"], toolbar.CaptureRegionWithPreview);
        Add("screen.capture_full", "Capture Full Desktop", CapabilityCategory.Screen, "Instant snapshot of all connected displays including annotations", "Monitor", "Ctrl+Shift+PrintScreen",
            ["full screenshot", "desktop snip", "displays"], toolbar.CaptureFullDesktopWithPreview);
        Add("screen.freeze_frame", "Freeze Frame Screen", CapabilityCategory.Screen, "Pause live display buffer in place while annotating freely", "PauseCircle", "Ctrl+Shift+F",
            ["freeze", "pause", "still frame", "lock screen"], toolbar.FreezeScreen, () => ctx.Freeze.IsValueCreated && ctx.Freeze.Value.IsFrozen);

        // ---------------------------------------------------------------- Privacy
        Add("privacy.code_focus", "Code Focus Slit Band", CapabilityCategory.Privacy, "Isolate code lines inside a bright horizontal slit while shading surrounding code", "ScanLine", "Ctrl+Shift+C",
            ["code focus", "slit", "highlight line", "shader band"], toolbar.ToggleCodeFocus, () => ctx.CodeFocus.IsValueCreated && ctx.CodeFocus.Value.IsVisible);
        Add("privacy.curtain", "Presentation Stage Curtain", CapabilityCategory.Privacy, "Adjustable curtain shade revealing screen content step-by-step", "PanelTopClose", "Ctrl+Shift+U",
            ["curtain", "blind", "drape", "reveal", "shade"], () =>
            {
                overlay.UpdateOptions(o => o.CurtainProgress = o.CurtainProgress > 0 ? 0 : 40, persist: false);
                if (s.CurtainProgress > 0) toolbar.ShowInspector("laser");
            }, () => s.CurtainProgress > 0);
        Add("privacy.blackout", "Blackout Privacy Censor", CapabilityCategory.Privacy, "Draw dark opaque redact boxes over passwords, tokens, and PII", "EyeOff", "Ctrl+Shift+X",
            ["censor", "redact", "blackout", "privacy", "hide"], () => ctx.Blackout.Value.ShowInteractive());

        // ---------------------------------------------------------------- Board
        Add("board.transparent", "Transparent Desktop", CapabilityCategory.Board, "Standard mode: write and draw directly over desktop and open apps", "Layers", "F1",
            ["transparent", "desktop", "overlay", "passthrough"], () => overlay.SetBoardKind(BoardKind.Transparent), () => overlay.CurrentBoard == BoardKind.Transparent);
        Add("board.whiteboard", "Whiteboard Canvas", CapabilityCategory.Board, "Solid clean white backdrop for lecturing, diagrams, and notes", "FileText", "F2",
            ["whiteboard", "clean canvas", "lecture", "teaching"], () => overlay.SetBoardKind(BoardKind.Whiteboard), () => overlay.CurrentBoard == BoardKind.Whiteboard);
        Add("board.blackboard", "Dark Blackboard", CapabilityCategory.Board, "Chalkboard black backdrop with high-contrast color palette", "Square", "F3",
            ["blackboard", "chalkboard", "dark mode canvas"], () => overlay.SetBoardKind(BoardKind.Blackboard), () => overlay.CurrentBoard == BoardKind.Blackboard);
        Add("board.grid", "Engineering Grid Paper", CapabilityCategory.Board, "Graph coordinate grid for math sketches and architecture blueprints", "Grid", "F4",
            ["grid", "graph", "blueprint", "math", "coordinates"], () =>
            {
                overlay.SetBoardKind(BoardKind.Grid);
                overlay.UpdateOptions(o => o.SnapToGrid = true);
            }, () => overlay.CurrentBoard == BoardKind.Grid);

        // ---------------------------------------------------------------- Tools & System
        Add("tools.capability_centre", "Open Capability Centre", CapabilityCategory.Tools, "Browse, search, and launch every command across all categories", "Compass", "F10",
            ["commands", "hub", "all tools", "directory"], () => toolbar.Dispatcher.BeginInvoke(toolbar.OpenCapabilityCentre));
        Add("tools.command_palette", "Command Palette Quick Launcher", CapabilityCategory.Tools, "Fuzzy-search any tool, shape, mode, or presenter function instantly", "Search", "Ctrl+Shift+P / Ctrl+K",
            ["palette", "quick open", "spotlight search", "run"], () => toolbar.Dispatcher.BeginInvoke(toolbar.OpenCommandPalette));
        Add("tools.radial_menu", "Quick Radial Pie Menu", CapabilityCategory.Tools, "Circular tool wheel spawned directly at cursor location", "PieChart", "Middle-Click",
            ["radial", "pie menu", "wheel", "fast switch"], () => toolbar.Dispatcher.BeginInvoke(toolbar.OpenRadialMenu));
        Add("tools.orientation_toggle", "Flip Toolbar Orientation", CapabilityCategory.Tools, "Toggle floating toolbar layout between horizontal and vertical", "RotateCw", "Ctrl+Shift+O",
            ["vertical", "horizontal", "flip", "orient toolbar"], () => toolbar.Dispatcher.BeginInvoke(toolbar.ToggleOrientation));
        Add("tools.settings", "InkIt Settings Editor", CapabilityCategory.Tools, "820x620 configuration hub for appearance, hotkeys, and profiles", "Settings", "Ctrl+,",
            ["settings", "config", "preferences", "hotkey rebind"], () => toolbar.Dispatcher.BeginInvoke(toolbar.OpenSettings));
        Add("tools.emergency_stop", "Emergency Release Dismissal", CapabilityCategory.Tools, "Protected global release hook dismissing all active overlays immediately", "ShieldAlert", "Alt+Shift+X",
            ["emergency", "abort", "kill overlay", "reset", "protected hotkey"], toolbar.EmergencyRelease);

        // ---------------------------------------------------------------- InkIt desktop extras
        Add("canvas.snap_toggle", "Toggle Snap-to-Grid", CapabilityCategory.Board, "Lock strokes, shapes, text and markers to the alignment grid", "Magnet", "Ctrl+Shift+G",
            ["snap", "grid", "magnet", "align"], () =>
            {
                overlay.UpdateOptions(o => o.SnapToGrid = !o.SnapToGrid);
                Toast.Show(s.SnapToGrid ? $"Snap-to-Grid Active ({s.GridSize}px)" : "Snap-to-Grid Disabled");
            }, () => s.SnapToGrid);
        Add("canvas.grid_settings", "Grid & Alignment", CapabilityCategory.Board, "Configure grid spacing, magnetic crosshairs and the engineering grid", "Crosshair", null,
            ["grid settings", "spacing", "crosshair", "alignment"], () => toolbar.ShowInspector("grid"));
        Add("pen.pencil", "Pencil", CapabilityCategory.Annotate, "Textured graphite sketch strokes", "Pen", null,
            ["pencil", "sketch", "graphite"], () => { overlay.SetPenMode(PenMode.Pencil); toolbar.RememberPenMode(PenMode.Pencil); });
        Add("pen.marker", "Marker Pen", CapabilityCategory.Annotate, "Broad felt tip ink for bold emphasis", "Highlighter", null,
            ["marker", "bold", "felt"], () => { overlay.SetPenMode(PenMode.Marker); toolbar.RememberPenMode(PenMode.Marker); });
        Add("pen.dashed", "Dashed Ink", CapabilityCategory.Annotate, "Draw dashed annotation strokes", "Minus", null,
            ["dashed", "dash", "broken"], () => { overlay.SetPenMode(PenMode.Dashed); toolbar.RememberPenMode(PenMode.Dashed); });
        Add("present.halo", "Cursor Halo", CapabilityCategory.Present, "High-contrast ring around the mouse cursor with click pulses", "Circle", null,
            ["halo", "ring", "cursor", "highlight"], () =>
            {
                if (ctx.PointerEffects.Value.IsVisible) ctx.PointerEffects.Value.Hide();
                else ctx.PointerEffects.Value.Show(new() { Spotlight = false, CursorHalo = true, ClickPulse = true });
            });
        Add("present.slide_next", "Slide Next", CapabilityCategory.Present, "Advance the PowerPoint / PDF presentation slide", "ChevronRight", null,
            ["slide", "next", "powerpoint", "advance"], () => System.Windows.Forms.SendKeys.SendWait("{RIGHT}"));
        Add("present.slide_prev", "Slide Previous", CapabilityCategory.Present, "Return to the previous presentation slide", "ChevronLeft", null,
            ["slide", "previous", "powerpoint", "back"], () => System.Windows.Forms.SendKeys.SendWait("{LEFT}"));
        Add("present.blank_curtain", "Blank Screen Curtain", CapabilityCategory.Present, "Black out the display for discussions without disconnecting the projector", "EyeOff", null,
            ["curtain", "blank", "black", "hide"], () => ctx.Curtain.Value.Show(new() { Color = System.Windows.Media.Colors.Black }));
        Add("screen.static_zoom", "Static Frozen Zoom", CapabilityCategory.Screen, "Freeze the screen and zoom in for detailed inspection", "ZoomIn", null,
            ["static", "zoom", "freeze", "inspect"], toolbar.TriggerStaticZoom);
        Add("screen.copy_desktop", "Copy Screen to Clipboard", CapabilityCategory.Screen, "Copy the full virtual desktop straight to the clipboard", "Copy", "PrintScreen",
            ["screenshot", "copy", "clipboard"], () => { ctx.Capture.CopyDesktopToClipboard(); Toast.Show("Screenshot copied to clipboard!"); });
        Add("screen.ocr", "OCR Screen Text", CapabilityCategory.Screen, "Select a screen region and extract its text with Windows OCR", "ScanText", null,
            ["ocr", "recognize", "text", "extract"], async () =>
            {
                var bmp = ctx.Capture.CaptureInteractiveRegion();
                if (bmp is null) return;
                var result = await new ScreenCanvas.Ocr.LocalOcrService().RecognizeAsync(bmp);
                if (!string.IsNullOrEmpty(result.Text))
                {
                    System.Windows.Clipboard.SetText(result.Text);
                    Toast.Show("Recognised text copied to clipboard");
                }
            });
        Add("tools.exit", "Exit InkIt", CapabilityCategory.Tools, "Close every overlay and quit InkIt", "Power", null,
            ["exit", "quit", "close"], toolbar.ExitApplication);

        return reg;
    }
}
