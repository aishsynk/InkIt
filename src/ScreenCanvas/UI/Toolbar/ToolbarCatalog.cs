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
        new("cursor", ToolbarItemKind.Tool, "Cursor", "MousePointer", "Esc", "Cursor (Esc) - stop drawing and use your apps normally"),
        new("select", ToolbarItemKind.Tool, "Select", "BoxSelect", "V", "Select (V) - drag a box around drawings to move, recolour or delete them"),
        new("pen", ToolbarItemKind.Tool, "Pen", "Pen", "P", "Pen (P) - draw freehand with the mouse or a stylus"),
        new("highlighter", ToolbarItemKind.Tool, "Highlighter", "Highlighter", "H", "Highlighter (H) - see-through marker for highlighting text"),
        new("eraser", ToolbarItemKind.Tool, "Eraser", "Eraser", "E", "Eraser (E) - click or drag over a drawing to remove it"),
        new("shape", ToolbarItemKind.Tool, "Shapes", "Shapes", "S", "Shapes (S) - arrows, lines, boxes, circles and diamonds"),
        new("text", ToolbarItemKind.Tool, "Text", "Type", "T", "Text (T) - click anywhere and type a note"),
        new("marker", ToolbarItemKind.Tool, "Step Numbers", "ListOrdered", "N", "Step numbers (N) - click to place 1, 2, 3 badges in order"),
        new("laser", ToolbarItemKind.Tool, "Laser", "Flame", "Ctrl+Shift+L", "Laser pointer (Ctrl+Shift+L) - a glowing dot with a fading trail"),
        new("spotlight", ToolbarItemKind.Tool, "Spotlight", "SunMedium", "Ctrl+Shift+S", "Spotlight (Ctrl+Shift+S) - darken the screen except around the mouse"),
        new("zoom", ToolbarItemKind.Tool, "Zoom", "ZoomIn", "Ctrl+Shift+5", "Zoom (Ctrl+Shift+5) - drag a box around the part to enlarge; it stays put so you can draw on it"),
        new("capture", ToolbarItemKind.Action, "Screenshot", "Camera", "Ctrl+Shift+4", "Screenshot (Ctrl+Shift+4) - drag a box to snap part of the screen; it is copied, ready to paste"),
        new("board", ToolbarItemKind.Inspector, "Board", "Layers", null, "Board - draw on a whiteboard, blackboard or grid instead of the screen"),
        new("color", ToolbarItemKind.Inspector, "Colours", "Palette", null, "Colours - choose the drawing colour"),
        new("undo", ToolbarItemKind.Action, "Undo", "Undo2", "Ctrl+Shift+Z", "Undo (Ctrl+Shift+Z) - take back the last drawing"),
        new("clear", ToolbarItemKind.Action, "Clear", "Trash2", "Ctrl+Shift+Del", "Clear (Ctrl+Shift+Del) - remove all drawings"),
        new("palette", ToolbarItemKind.Action, "Search", "Search", "Ctrl+K", "Search (Ctrl+K) - type to find any feature"),
        new("capability", ToolbarItemKind.Action, "All Features", "Sparkles", "F10", "All features (F10) - browse everything InkIt can do"),
        // InkIt desktop extras (not in the design's default layout; enable them in Customize Toolbar).
        new("redo", ToolbarItemKind.Action, "Redo", "Redo2", "Ctrl+Shift+Y", "Redo (Ctrl+Shift+Y) - bring back what Undo removed", false),
        new("more", ToolbarItemKind.Inspector, "More", "MoreVertical", null, "More - break timer, freeze screen, settings and exit", false),
        new("collapse", ToolbarItemKind.Action, "Collapse", "Minimize2", null, "Shrink the toolbar to a small button", false),
    ];

    public static readonly IReadOnlyList<ToolbarPreset> Presets =
    [
        new("default", "Standard InkIt", "Complete balanced toolset for desktop inking",
            ["cursor", "select", "pen", "highlighter", "eraser", "shape", "text", "marker", "laser", "spotlight", "zoom", "capture", "board", "color", "undo", "clear", "palette", "capability"]),
        new("teaching", "Teaching & Lecture", "Optimized for live classroom teaching & explanations",
            ["cursor", "select", "pen", "highlighter", "eraser", "marker", "text", "zoom", "capture", "color", "board", "undo", "clear"]),
        new("presenter", "Keynote & Tech Demo", "Focused on spotlight, laser pointer, live magnifier & code slit",
            ["cursor", "select", "laser", "spotlight", "zoom", "capture", "pen", "highlighter", "eraser", "undo", "palette"]),
        new("engineering", "Engineering & Diagrams", "Optimized for geometric diagrams, shapes, and grid blueprinting",
            ["cursor", "select", "shape", "pen", "text", "marker", "board", "zoom", "capture", "eraser", "undo", "clear", "color"]),
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
