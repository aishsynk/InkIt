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
        Add("annot.pen", "Pen", CapabilityCategory.Annotate, "Draw freehand; choose from 15 pen styles", "Pen", "P",
            ["pen", "draw", "ink", "ballpoint", "calligraphy"], () => { overlay.SetPenMode(PenMode.Ballpoint); toolbar.RememberPenMode(PenMode.Ballpoint); },
            () => s.Tool == ToolKind.Pen);
        Add("annot.highlighter", "Highlighter", CapabilityCategory.Annotate, "See-through marker for highlighting text without hiding it", "Highlighter", "H",
            ["highlight", "marker", "yellow", "translucent"], () => toolbar.SelectTool(ToolKind.Highlighter), () => s.Tool == ToolKind.Highlighter);
        Add("annot.eraser", "Eraser", CapabilityCategory.Annotate, "Remove a drawing by clicking or dragging over it", "Eraser", "E",
            ["erase", "delete", "rub", "clear stroke"], () => overlay.SetTool(ToolKind.Eraser), () => s.Tool == ToolKind.Eraser);
        Add("annot.undo", "Undo", CapabilityCategory.Annotate, "Take back the last drawing", "Undo", "Ctrl+Shift+Z",
            ["undo", "revert", "back"], overlay.Undo);
        Add("annot.redo", "Redo", CapabilityCategory.Annotate, "Bring back what Undo removed", "Redo", "Ctrl+Shift+Y",
            ["redo", "forward", "repeat"], overlay.Redo);
        Add("annot.clear", "Clear All Drawings", CapabilityCategory.Annotate, "Remove every drawing, shape and number from the screen", "Trash2", "Ctrl+Shift+Del",
            ["clear", "wipe", "reset canvas", "clean"], () => { overlay.Clear(); Toast.Show("All drawings cleared (Undo brings them back)"); });
        Add("annot.text", "Text Note", CapabilityCategory.Annotate, "Click anywhere on the screen and type", "Type", "T",
            ["text", "label", "type", "font", "notes"], () => overlay.SetTool(ToolKind.Text), () => s.Tool == ToolKind.Text);
        Add("annot.marker", "Step Numbers", CapabilityCategory.Annotate, "Place numbered badges (1, 2, 3...) to show the order of steps", "ListOrdered", "N",
            ["step", "number", "sequence", "badge", "marker"], () => overlay.SetTool(ToolKind.NumberMarker), () => s.Tool == ToolKind.NumberMarker);
        Add("annot.disappearing_pen", "Disappearing Ink", CapabilityCategory.Annotate, "Ink that fades away by itself after 3 seconds", "Sparkles", "D",
            ["fade", "disappear", "temporary", "ghost ink"], () => { overlay.SetPenMode(PenMode.Disappearing); toolbar.RememberPenMode(PenMode.Disappearing); },
            () => s.Tool == ToolKind.Pen && s.PenMode == PenMode.Disappearing);
        Add("annot.select", "Select Drawings", CapabilityCategory.Annotate, "Drag a box around drawings to move, recolour, copy or delete them", "BoxSelect", "V",
            ["select", "marquee", "move", "batch", "duplicate"], () => overlay.SetTool(ToolKind.Select), () => s.Tool == ToolKind.Select);

        // ---------------------------------------------------------------- Shapes
        Add("shape.line", "Straight Line", CapabilityCategory.Shapes, "Draw a straight line", "Minus", "L",
            ["line", "rule", "straight", "vector"], () => Shape(ShapeKind.Line));
        Add("shape.arrow", "Arrow", CapabilityCategory.Shapes, "Draw an arrow to point at something", "ArrowRight", "A",
            ["arrow", "pointer", "direct", "vector arrow"], () => Shape(ShapeKind.Arrow));
        Add("shape.double_arrow", "Double Arrow", CapabilityCategory.Shapes, "Arrow with heads on both ends", "MoveHorizontal", "Shift+A",
            ["double arrow", "span", "measurement", "dimension"], () => Shape(ShapeKind.DoubleArrow));
        Add("shape.rectangle", "Box", CapabilityCategory.Shapes, "Draw a box around something", "Square", "R",
            ["rect", "rectangle", "box", "callout", "frame"], () => Shape(ShapeKind.Rectangle));
        Add("shape.rounded_rect", "Rounded Box", CapabilityCategory.Shapes, "Box with soft rounded corners", "SquareDot", "Shift+R",
            ["rounded rect", "pill", "smooth frame"], () => Shape(ShapeKind.RoundedRectangle));
        Add("shape.ellipse", "Circle / Oval", CapabilityCategory.Shapes, "Draw a circle around something", "Circle", "O",
            ["circle", "ellipse", "round", "halo"], () => Shape(ShapeKind.Ellipse));
        Add("shape.diamond", "Diamond", CapabilityCategory.Shapes, "Flowchart decision shape", "Diamond", "Shift+D",
            ["diamond", "rhombus", "flowchart"], () => Shape(ShapeKind.Diamond));
        Add("shape.fill_toggle", "Fill Shapes", CapabilityCategory.Shapes, "Turn a light colour fill inside shapes on or off", "PaintBucket", "F",
            ["fill", "tint", "solid", "color fill"], () =>
            {
                var enabled = !s.ShapeFillEnabled;
                overlay.SetShapeFill(enabled);
                Toast.Show($"Shape fill {(enabled ? "enabled" : "disabled")}");
            }, () => s.ShapeFillEnabled);
        Add("shape.hub", "Shapes", CapabilityCategory.Shapes, "Pick a shape and its settings", "Shapes", "S",
            ["shapes", "geometry", "inspector"], () => { overlay.SetShape(s.Shape); toolbar.ShowInspector("shape"); }, () => s.Tool == ToolKind.Shape);

        // ---------------------------------------------------------------- Present
        Add("present.laser", "Laser Pointer", CapabilityCategory.Present, "A glowing dot with a fading trail that follows the mouse", "Flame", "Ctrl+Shift+L",
            ["laser", "pointer", "red dot", "tail", "presenter"], () => overlay.SetTool(ToolKind.Laser), () => s.Tool == ToolKind.Laser);
        Add("present.spotlight", "Spotlight", CapabilityCategory.Present, "Darken the whole screen except a circle around the mouse", "SunMedium", "Ctrl+Shift+S",
            ["spotlight", "dim", "aperture", "focus light"], () => overlay.SetTool(ToolKind.Spotlight), () => s.Tool == ToolKind.Spotlight);
        Add("present.follow_slides", "Drawings Follow PowerPoint Slides", CapabilityCategory.Present, "During a slide show each slide keeps its own drawings; they come back when you return to it", "Presentation", null,
            ["powerpoint", "slides", "slide show", "per slide", "keep drawings"], () => toolbar.FollowSlides = !toolbar.FollowSlides, () => toolbar.FollowSlides);
        Add("present.focus_box", "Focus Box", CapabilityCategory.Present, "Drag a box: everything else is dimmed while your apps keep working", "Focus", null,
            ["focus", "dim", "highlight area", "box", "attention"], toolbar.FocusOnArea, () => overlay.IsFocusBoxActive);
        Add("present.break_timer", "Break Timer", CapabilityCategory.Present, "Small countdown clock for breaks", "Timer", "Ctrl+Shift+B",
            ["timer", "break", "countdown", "clock"], () => toolbar.StartBreakTimer(toolbar.BreakTimerMinutes), () => ctx.BreakTimer.IsValueCreated && ctx.BreakTimer.Value.IsRunning);
        Add("present.key_visualizer", "Show Keys Pressed", CapabilityCategory.Present, "Show the keyboard shortcuts you press on screen, so learners can follow", "Keyboard", "Ctrl+Shift+K",
            ["keys", "keystroke", "hud", "shortcut display"], () =>
            {
                ctx.KeyVisualizer.Value.Toggle();
                Toast.Show($"Show keys pressed {(ctx.KeyVisualizer.Value.IsEnabled ? "on" : "off")}");
            }, () => ctx.KeyVisualizer.IsValueCreated && ctx.KeyVisualizer.Value.IsEnabled);
        Add("present.demo_type", "Type a Code Snippet", CapabilityCategory.Present, "Types prepared text into the app you choose, as if you were typing live", "Terminal", "Ctrl+Shift+T",
            ["demo", "type", "script", "auto type", "automation"], () => toolbar.RunDemoType("// Executing automated DemoType script in target window\nconsole.log(\"InkIt Live Demo v1.0.0\");"));
        Add("present.pointer_effects", "Click Ripples", CapabilityCategory.Present, "Show a ripple wherever you click, so learners see it", "MousePointerClick", "Alt+Shift+P",
            ["ripple", "click ripple", "cursor pulse", "rings"], () =>
            {
                s.ClickVisualizerEnabled = !(ctx.PointerEffects.IsValueCreated && ctx.PointerEffects.Value.IsVisible);
                if (s.ClickVisualizerEnabled) ctx.PointerEffects.Value.Show(new() { ClickPulse = true, LaserTrail = false });
                else ctx.PointerEffects.Value.Hide();
                Toast.Show($"Pointer click ripples {(s.ClickVisualizerEnabled ? "on" : "off")}");
            }, () => ctx.PointerEffects.IsValueCreated && ctx.PointerEffects.Value.IsVisible);

        // ---------------------------------------------------------------- Screen
        Add("screen.zoom_toggle", "Zoom In / Out", CapabilityCategory.Screen, "Zoom into a part of the screen (drag a box), or back out again", "ZoomIn", "Ctrl+Shift+5",
            ["zoom", "magnify", "scale", "lens", "enlarge"], toolbar.ToggleZoom, () => toolbar.IsZoomActive);
        Add("screen.zoom_area", "Zoom Into an Area", CapabilityCategory.Screen, "Drag a box around any part of the screen; it fills the screen and stays put so you can draw on it", "Crop", null,
            ["zoom area", "zoom region", "enlarge part", "focus area"], toolbar.ZoomToArea, () => toolbar.IsZoomActive);
        Add("screen.zoom_follow", "Zoom That Follows the Mouse", CapabilityCategory.Screen, "Live magnifier that moves with the mouse (1x to 16x)", "ZoomIn", null,
            ["live zoom", "follow mouse", "magnifier", "glide"], toolbar.StartLiveZoom, () => toolbar.IsZoomActive);
        Add("screen.capture_region", "Screenshot of an Area", CapabilityCategory.Screen, "Drag a box to screenshot part of the screen; it is copied, ready to paste", "Crop", "Ctrl+Shift+4",
            ["capture", "snip", "screenshot", "region", "crop"], toolbar.CaptureRegionWithPreview);
        Add("screen.capture_full", "Screenshot of Whole Screen", CapabilityCategory.Screen, "Screenshot everything on all monitors, drawings included", "Monitor", "Ctrl+Shift+PrintScreen",
            ["full screenshot", "desktop snip", "displays"], toolbar.CaptureFullDesktopWithPreview);
        Add("screen.freeze_frame", "Freeze Screen", CapabilityCategory.Screen, "Pause what is on screen so you can draw on it while things change behind", "PauseCircle", "Ctrl+Shift+F",
            ["freeze", "pause", "still frame", "lock screen"], toolbar.FreezeScreen, () => ctx.Freeze.IsValueCreated && ctx.Freeze.Value.IsFrozen);

        // ---------------------------------------------------------------- Privacy
        Add("privacy.code_focus", "Focus Band", CapabilityCategory.Privacy, "Keep one horizontal band bright and dim the rest; handy for code or long text", "ScanLine", "Ctrl+Shift+C",
            ["code focus", "slit", "highlight line", "shader band"], toolbar.ToggleCodeFocus, () => ctx.CodeFocus.IsValueCreated && ctx.CodeFocus.Value.IsVisible);
        Add("privacy.curtain", "Stage Curtain", CapabilityCategory.Privacy, "Cover the screen and reveal it bit by bit", "PanelTopClose", "Ctrl+Shift+U",
            ["curtain", "blind", "drape", "reveal", "shade"], () =>
            {
                overlay.UpdateOptions(o => o.CurtainProgress = o.CurtainProgress > 0 ? 0 : 40, persist: false);
                if (s.CurtainProgress > 0) toolbar.ShowInspector("laser");
            }, () => s.CurtainProgress > 0);
        Add("privacy.blackout", "Hide Private Info", CapabilityCategory.Privacy, "Draw black boxes over passwords or personal details", "EyeOff", "Ctrl+Shift+X",
            ["censor", "redact", "blackout", "privacy", "hide"], () => ctx.Blackout.Value.ShowInteractive());

        // ---------------------------------------------------------------- Board
        Add("board.transparent", "Draw on Screen", CapabilityCategory.Board, "Normal mode: draw over your apps", "Layers", "F1",
            ["transparent", "desktop", "overlay", "passthrough"], () => overlay.SetBoardKind(BoardKind.Transparent), () => overlay.CurrentBoard == BoardKind.Transparent);
        Add("board.whiteboard", "Whiteboard", CapabilityCategory.Board, "Cover the screen with a clean white board", "FileText", "F2",
            ["whiteboard", "clean canvas", "lecture", "teaching"], () => overlay.SetBoardKind(BoardKind.Whiteboard), () => overlay.CurrentBoard == BoardKind.Whiteboard);
        Add("board.blackboard", "Blackboard", CapabilityCategory.Board, "Cover the screen with a dark chalkboard", "Square", "F3",
            ["blackboard", "chalkboard", "dark mode canvas"], () => overlay.SetBoardKind(BoardKind.Blackboard), () => overlay.CurrentBoard == BoardKind.Blackboard);
        Add("board.next_page", "Next Page", CapabilityCategory.Board, "Go to the next page of drawings (adds a blank page after the last)", "ChevronRight", "Page Down",
            ["next page", "new page", "page down"], overlay.NextPage);
        Add("board.previous_page", "Previous Page", CapabilityCategory.Board, "Go back to the previous page of drawings", "ChevronLeft", "Page Up",
            ["previous page", "back", "page up"], overlay.PreviousPage);
        Add("board.new_page", "New Page", CapabilityCategory.Board, "Add a blank page after this one", "Plus", null,
            ["add page", "blank page", "new page"], overlay.AddPage);
        Add("board.export_pdf", "Export Pages as PDF", CapabilityCategory.Board, "Save every page of drawings as one PDF to share", "FileDown", null,
            ["pdf", "export", "handout", "share notes"], toolbar.ExportPagesPdf);
        Add("file.save", "Save Drawings", CapabilityCategory.Board, "Save your pages of drawings to open again later", "Save", "Ctrl+S",
            ["save", "keep", "store"], toolbar.SaveDrawings);
        Add("file.open", "Open Drawings", CapabilityCategory.Board, "Open drawings you saved earlier", "FolderOpen", "Ctrl+O",
            ["open", "load", "prepared"], toolbar.OpenDrawings);
        Add("board.grid", "Grid Paper", CapabilityCategory.Board, "Cover the screen with squared grid paper", "Grid", "F4",
            ["grid", "graph", "blueprint", "math", "coordinates"], () =>
            {
                overlay.SetBoardKind(BoardKind.Grid);
                overlay.UpdateOptions(o => o.SnapToGrid = true);
            }, () => overlay.CurrentBoard == BoardKind.Grid);

        // ---------------------------------------------------------------- Tools & System
        Add("tools.capability_centre", "All Features", CapabilityCategory.Tools, "Browse everything InkIt can do", "Compass", "F10",
            ["commands", "hub", "all tools", "directory"], () => toolbar.Dispatcher.BeginInvoke(toolbar.OpenCapabilityCentre));
        Add("tools.command_palette", "Search", CapabilityCategory.Tools, "Type to find any tool or feature", "Search", "Ctrl+Shift+P / Ctrl+K",
            ["palette", "quick open", "spotlight search", "run"], () => toolbar.Dispatcher.BeginInvoke(toolbar.OpenCommandPalette));
        Add("tools.radial_menu", "Tool Wheel", CapabilityCategory.Tools, "A ring of tools that opens right where the mouse is", "PieChart", "Middle-Click",
            ["radial", "pie menu", "wheel", "fast switch"], () => toolbar.Dispatcher.BeginInvoke(toolbar.OpenRadialMenu));
        Add("tools.orientation_toggle", "Turn Toolbar", CapabilityCategory.Tools, "Switch the toolbar between across and up-and-down", "RotateCw", "Ctrl+Shift+O",
            ["vertical", "horizontal", "flip", "orient toolbar"], () => toolbar.Dispatcher.BeginInvoke(toolbar.ToggleOrientation));
        Add("tools.settings", "Settings", CapabilityCategory.Tools, "Theme, toolbar, keyboard shortcuts and more", "Settings", "Ctrl+,",
            ["settings", "config", "preferences", "hotkey rebind"], () => toolbar.Dispatcher.BeginInvoke(toolbar.OpenSettings));
        Add("tools.emergency_stop", "Panic Key", CapabilityCategory.Tools, "Instantly close every drawing tool and overlay (Alt+Shift+X)", "ShieldAlert", "Alt+Shift+X",
            ["emergency", "abort", "kill overlay", "reset", "protected hotkey"], toolbar.EmergencyRelease);

        // ---------------------------------------------------------------- InkIt desktop extras
        Add("canvas.snap_toggle", "Snap to Grid", CapabilityCategory.Board, "Line drawings up neatly on an invisible grid", "Magnet", "Ctrl+Shift+G",
            ["snap", "grid", "magnet", "align"], () =>
            {
                overlay.UpdateOptions(o => o.SnapToGrid = !o.SnapToGrid);
                Toast.Show(s.SnapToGrid ? $"Snap-to-Grid Active ({s.GridSize}px)" : "Snap-to-Grid Disabled");
            }, () => s.SnapToGrid);
        Add("canvas.grid_settings", "Grid Settings", CapabilityCategory.Board, "Grid size and guides", "Crosshair", null,
            ["grid settings", "spacing", "crosshair", "alignment"], () => toolbar.ShowInspector("grid"));
        Add("pen.pencil", "Pencil", CapabilityCategory.Annotate, "Textured graphite sketch strokes", "Pen", null,
            ["pencil", "sketch", "graphite"], () => { overlay.SetPenMode(PenMode.Pencil); toolbar.RememberPenMode(PenMode.Pencil); });
        Add("pen.marker", "Marker Pen", CapabilityCategory.Annotate, "Broad felt tip ink for bold emphasis", "Highlighter", null,
            ["marker", "bold", "felt"], () => { overlay.SetPenMode(PenMode.Marker); toolbar.RememberPenMode(PenMode.Marker); });
        Add("pen.dashed", "Dashed Ink", CapabilityCategory.Annotate, "Draw dashed annotation strokes", "Minus", null,
            ["dashed", "dash", "broken"], () => { overlay.SetPenMode(PenMode.Dashed); toolbar.RememberPenMode(PenMode.Dashed); });
        Add("present.halo", "Mouse Highlight", CapabilityCategory.Present, "A bright ring around the mouse so learners can find it", "Circle", null,
            ["halo", "ring", "cursor", "highlight"], () =>
            {
                if (ctx.PointerEffects.Value.IsVisible) ctx.PointerEffects.Value.Hide();
                else ctx.PointerEffects.Value.Show(new() { Spotlight = false, CursorHalo = true, ClickPulse = true });
            });
        Add("present.slide_next", "Next Slide", CapabilityCategory.Present, "Go to the next PowerPoint or PDF slide", "ChevronRight", null,
            ["slide", "next", "powerpoint", "advance"], () => System.Windows.Forms.SendKeys.SendWait("{RIGHT}"));
        Add("present.slide_prev", "Previous Slide", CapabilityCategory.Present, "Go back one slide", "ChevronLeft", null,
            ["slide", "previous", "powerpoint", "back"], () => System.Windows.Forms.SendKeys.SendWait("{LEFT}"));
        Add("present.blank_curtain", "Blank Screen", CapabilityCategory.Present, "Black out the screen during a discussion", "EyeOff", null,
            ["curtain", "blank", "black", "hide"], () => ctx.Curtain.Value.Show(new() { Color = System.Windows.Media.Colors.Black }));
        Add("screen.static_zoom", "Freeze and Zoom", CapabilityCategory.Screen, "Freeze the whole screen, then zoom and pan with the mouse wheel", "ZoomIn", null,
            ["static", "zoom", "freeze", "inspect"], toolbar.TriggerStaticZoom);
        Add("screen.copy_desktop", "Copy Whole Screen", CapabilityCategory.Screen, "Copy a picture of the whole screen to the clipboard", "Copy", "PrintScreen",
            ["screenshot", "copy", "clipboard"], () => { ctx.Capture.CopyDesktopToClipboard(); Toast.Show("Screenshot copied to clipboard!"); });
        Add("screen.ocr", "Copy Text from Screen", CapabilityCategory.Screen, "Drag a box and InkIt copies the words it can read inside it", "ScanText", null,
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
        Add("tools.feedback", "Send Feedback", CapabilityCategory.Tools, "Tell us what works and what to improve (opens the review form)", "Star", null,
            ["feedback", "review", "suggest", "rate"], toolbar.SendFeedback);
        Add("tools.report_problem", "Report a Problem", CapabilityCategory.Tools, "Something did not work? Send a problem report", "AlertCircle", null,
            ["bug", "problem", "issue", "crash", "report"], toolbar.ReportProblem);
        Add("tools.check_updates", "Check for Updates", CapabilityCategory.Tools, "See whether a newer InkIt is available", "RotateCw", null,
            ["update", "upgrade", "new version", "download"], () => toolbar.CheckForUpdates(manual: true));
        Add("tools.tour", "Quick Tour", CapabilityCategory.Tools, "A 5-step tour of the buttons you need first", "Compass", null,
            ["tour", "help", "tutorial", "getting started", "how to"], toolbar.StartTour);
        Add("tools.exit", "Exit InkIt", CapabilityCategory.Tools, "Close every overlay and quit InkIt", "Power", null,
            ["exit", "quit", "close"], toolbar.ExitApplication);

        return reg;
    }
}
