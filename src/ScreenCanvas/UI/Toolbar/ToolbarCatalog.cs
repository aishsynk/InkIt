using ScreenCanvas.Settings;

namespace ScreenCanvas.UI.Toolbar;

public enum ToolbarItemKind { Tool, Inspector, Action }

public sealed record ToolbarItemDefinition(
    string Id,
    ToolbarItemKind Kind,
    string Label,
    string Icon,
    string? Shortcut,
    string Tooltip,
    bool DefaultVisible = true);

public sealed record ToolbarPreset(string Id, string Name, string Description, string[] ToolIds);

/// <summary>Customisable toolbar items and workflow presets from the InkIt design (toolbarToolsData.ts).</summary>
public static class ToolbarCatalog
{
    public static readonly IReadOnlyList<ToolbarItemDefinition> Items =
    [
        new("cursor", ToolbarItemKind.Tool, "Cursor", "MousePointer", "Esc", "Cursor / Passthrough Mode (Esc)"),
        new("select", ToolbarItemKind.Tool, "Multi-Select", "BoxSelect", "V", "Multi-Select Marquee (V) - Select multiple elements to batch move, recolor, or delete"),
        new("pen", ToolbarItemKind.Tool, "Pen", "Pen", "P", "Freehand Pen (P) - Windows Ink Stylus & Pressure"),
        new("highlighter", ToolbarItemKind.Tool, "Highlighter", "Highlighter", "H", "Highlighter (H) - 115 Alpha Translucent Ink"),
        new("eraser", ToolbarItemKind.Tool, "Eraser", "Eraser", "E", "Stroke Eraser (E) / Stylus Inverted Tip"),
        new("shape", ToolbarItemKind.Tool, "Shapes", "Shapes", "S", "Vector Geometry Shapes (Arrow, Rect, Ellipse, Diamond)"),
        new("text", ToolbarItemKind.Tool, "Text", "Type", "T", "Text Note (T) - Vector Typographic Layer"),
        new("marker", ToolbarItemKind.Tool, "Markers", "ListOrdered", "N", "Step Markers (N) - 1, 2, 3 numbered badges"),
        new("laser", ToolbarItemKind.Tool, "Laser", "Flame", "Ctrl+Shift+L", "Laser Pointer (Ctrl+Shift+L) - Decaying trail"),
        new("spotlight", ToolbarItemKind.Tool, "Spotlight", "SunMedium", "Ctrl+Shift+S", "Spotlight & Code Slit (Ctrl+Shift+S)"),
        new("zoom", ToolbarItemKind.Tool, "Magnifier", "ZoomIn", "Ctrl+Shift+5", "Windows Live Magnifier (1x - 16x glide)"),
        new("board", ToolbarItemKind.Inspector, "Board", "Layers", null, "Board Canvas (Transparent, Whiteboard, Blackboard, Grid)"),
        new("color", ToolbarItemKind.Inspector, "Colors", "Palette", null, "Teaching Color Palette (6 Presets)"),
        new("undo", ToolbarItemKind.Action, "Undo", "Undo2", "Ctrl+Shift+Z", "Undo Last Action (Ctrl+Shift+Z)"),
        new("clear", ToolbarItemKind.Action, "Clear", "Trash2", "Ctrl+Shift+Del", "Clear All Annotations"),
        new("palette", ToolbarItemKind.Action, "Search", "Search", "Ctrl+K", "Command Palette (Ctrl+K)"),
        new("capability", ToolbarItemKind.Action, "Commands", "Sparkles", "F10", "Capability Centre Registry (F10)"),
        // InkIt desktop extras (not in the design's default layout; enable them in Customize Toolbar).
        new("redo", ToolbarItemKind.Action, "Redo", "Redo2", "Ctrl+Shift+Y", "Redo Last Action (Ctrl+Shift+Y)", false),
        new("capture", ToolbarItemKind.Action, "Screenshot", "Camera", "Ctrl+Shift+4", "Capture Screen Region (Ctrl+Shift+4) - preview, save or copy", false),
        new("more", ToolbarItemKind.Inspector, "More", "MoreVertical", null, "More - boards, timer, freeze, settings and exit", false),
        new("collapse", ToolbarItemKind.Action, "Collapse", "Minimize2", null, "Collapse toolbar to a minimal tile", false),
    ];

    public static readonly IReadOnlyList<ToolbarPreset> Presets =
    [
        new("default", "Standard InkIt", "Complete balanced toolset for desktop inking",
            ["cursor", "select", "pen", "highlighter", "eraser", "shape", "text", "marker", "laser", "spotlight", "zoom", "board", "color", "undo", "clear", "palette", "capability"]),
        new("teaching", "Teaching & Lecture", "Optimized for live classroom teaching & explanations",
            ["cursor", "select", "pen", "highlighter", "eraser", "marker", "text", "color", "board", "undo", "clear"]),
        new("presenter", "Keynote & Tech Demo", "Focused on spotlight, laser pointer, live magnifier & code slit",
            ["cursor", "select", "laser", "spotlight", "zoom", "pen", "highlighter", "eraser", "undo", "palette"]),
        new("engineering", "Engineering & Diagrams", "Optimized for geometric diagrams, shapes, and grid blueprinting",
            ["cursor", "select", "shape", "pen", "text", "marker", "board", "eraser", "undo", "clear", "color"]),
    ];

    public static ToolbarItemDefinition? Find(string id) => Items.FirstOrDefault(i => i.Id == id);

    public static List<ToolbarItemState> DefaultLayout() =>
        Items.Select(i => new ToolbarItemState { Id = i.Id, Visible = i.DefaultVisible }).ToList();

    /// <summary>Keeps the saved order/visibility, drops unknown ids and appends newly added items.</summary>
    public static List<ToolbarItemState> Normalize(List<ToolbarItemState>? saved)
    {
        if (saved is null || saved.Count == 0) return DefaultLayout();
        var result = saved.Where(s => Find(s.Id) is not null).GroupBy(s => s.Id).Select(g => g.First())
            .Select(s => new ToolbarItemState { Id = s.Id, Visible = s.Visible }).ToList();
        foreach (var item in Items)
            if (result.All(r => r.Id != item.Id)) result.Add(new ToolbarItemState { Id = item.Id, Visible = item.DefaultVisible });
        return result;
    }

    /// <summary>Preset tools first (visible, in preset order), everything else after it, hidden.</summary>
    public static List<ToolbarItemState> ApplyPreset(IEnumerable<ToolbarItemState> current, ToolbarPreset preset)
    {
        var remaining = current.ToList();
        var ordered = new List<ToolbarItemState>();
        foreach (var id in preset.ToolIds)
        {
            var item = remaining.FirstOrDefault(r => r.Id == id);
            if (item is null) continue;
            ordered.Add(new ToolbarItemState { Id = id, Visible = true });
            remaining.Remove(item);
        }
        ordered.AddRange(remaining.Select(r => new ToolbarItemState { Id = r.Id, Visible = false }));
        return ordered;
    }
}
