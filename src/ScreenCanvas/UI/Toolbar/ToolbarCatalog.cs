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
    bool DefaultVisible = true,
    string Group = "");

public sealed record ToolbarPreset(string Id, string Name, string Description, string[] ToolIds);

/// <summary>
/// Toolbar items in their canonical order, grouped by job so the strip reads left to right:
/// draw (pointer first, pen second) | add | present | edit | find. A divider is drawn wherever the group changes.
/// </summary>
public static class ToolbarCatalog
{
    public const string GroupDraw = "draw", GroupAdd = "add", GroupPresent = "present", GroupEdit = "edit", GroupFind = "find";

    public static readonly IReadOnlyList<ToolbarItemDefinition> Items =
    [
        // Draw - the everyday tools, pen right after the pointer.
        new("cursor", ToolbarItemKind.Tool, "Cursor", "MousePointer2", "Esc", "Cursor (Esc) - stop drawing and use your apps normally", Group: GroupDraw),
        new("pen", ToolbarItemKind.Tool, "Pen", "PenLine", "P", "Pen (P) - draw freehand with the mouse or a stylus", Group: GroupDraw),
        new("highlighter", ToolbarItemKind.Tool, "Highlighter", "Highlighter", "H", "Highlighter (H) - see-through marker for highlighting text", Group: GroupDraw),
        new("eraser", ToolbarItemKind.Tool, "Eraser", "Eraser", "E", "Eraser (E) - click or drag over a drawing to remove it", Group: GroupDraw),
        new("color", ToolbarItemKind.Inspector, "Colour", "Palette", null, "Colour - choose the drawing colour", Group: GroupDraw),
        // Add - things you place on the screen.
        new("shape", ToolbarItemKind.Tool, "Shapes", "Shapes", "S", "Shapes (S) - arrows, lines, boxes, circles and diamonds", Group: GroupAdd),
        new("text", ToolbarItemKind.Tool, "Text", "Type", "T", "Text (T) - click anywhere and type a note", Group: GroupAdd),
        new("marker", ToolbarItemKind.Tool, "Step Numbers", "ListOrdered", "N", "Step numbers & stamps (N) - place 1, 2, 3 badges or tick, cross, ? and ! stamps", Group: GroupAdd),
        // Present - guide the audience's eyes.
        new("laser", ToolbarItemKind.Tool, "Laser", "CircleDot", "Ctrl+Shift+L", "Laser pointer (Ctrl+Shift+L) - a glowing dot with a fading trail", Group: GroupPresent),
        new("spotlight", ToolbarItemKind.Tool, "Spotlight", "Flashlight", "Ctrl+Shift+S", "Spotlight (Ctrl+Shift+S) - darken the screen except around the mouse", Group: GroupPresent),
        new("focus", ToolbarItemKind.Action, "Focus Box", "Focus", null, "Focus box - drag a box; everything else is dimmed while your apps keep working (Esc removes it)", false, GroupPresent),
        new("zoom", ToolbarItemKind.Tool, "Zoom", "ZoomIn", "Ctrl+Shift+5", "Zoom (Ctrl+Shift+5) - drag a box around the part to enlarge; it stays put so you can draw on it", Group: GroupPresent),
        new("capture", ToolbarItemKind.Action, "Screenshot", "Camera", "Ctrl+Shift+4", "Screenshot (Ctrl+Shift+4) - drag a box to snap part of the screen; it is copied, ready to paste", Group: GroupPresent),
        new("record", ToolbarItemKind.Action, "Record", "Video", null, "Record a lesson - the screen (or an area), your drawings and your voice, saved as an MP4 video", Group: GroupPresent),
        new("board", ToolbarItemKind.Inspector, "Whiteboard", "Presentation", null, "Whiteboard - draw on a whiteboard, blackboard or grid instead of the screen", Group: GroupPresent),
        // Edit - change what is already drawn.
        new("select", ToolbarItemKind.Tool, "Select", "SquareDashedMousePointer", "V", "Select (V) - drag a box around drawings to move, recolour or delete them", Group: GroupEdit),
        new("undo", ToolbarItemKind.Action, "Undo", "Undo2", "Ctrl+Shift+Z", "Undo (Ctrl+Shift+Z) - take back the last drawing", Group: GroupEdit),
        new("redo", ToolbarItemKind.Action, "Redo", "Redo2", "Ctrl+Shift+Y", "Redo (Ctrl+Shift+Y) - bring back what Undo removed", false, GroupEdit),
        new("clear", ToolbarItemKind.Action, "Clear", "Trash2", "Ctrl+Shift+Del", "Clear (Ctrl+Shift+Del) - remove all drawings (Undo brings them back)", Group: GroupEdit),
        // Find - reach everything else.
        new("palette", ToolbarItemKind.Action, "Search", "Search", "Ctrl+K", "Search (Ctrl+K) - type to find any feature", Group: GroupFind),
        new("capability", ToolbarItemKind.Action, "All Features", "LayoutGrid", "F10", "All features (F10) - browse everything InkIt can do", Group: GroupFind),
        new("more", ToolbarItemKind.Inspector, "More", "MoreVertical", null, "More - break timer, freeze screen, settings and exit", false, GroupFind),
        new("collapse", ToolbarItemKind.Action, "Collapse", "Minimize2", null, "Shrink the toolbar to a small button", false, GroupFind),
    ];

    /// <summary>Preset tool sets; every preset keeps the canonical order so buttons never jump around.</summary>
    public static readonly IReadOnlyList<ToolbarPreset> Presets =
    [
        new("default", "Everything", "Every tool on the toolbar",
            Ordered("cursor", "pen", "highlighter", "eraser", "color", "shape", "text", "marker", "laser", "spotlight", "zoom", "capture", "board", "select", "undo", "clear", "palette", "capability")),
        new("teaching", "Trainer", "Draw, point, zoom and screenshot while you teach",
            Ordered("cursor", "pen", "highlighter", "eraser", "color", "shape", "text", "marker", "laser", "zoom", "capture", "board", "undo", "clear", "palette")),
        new("presenter", "Presenter", "Laser, spotlight and zoom for talks and demos",
            Ordered("cursor", "pen", "highlighter", "eraser", "laser", "spotlight", "zoom", "capture", "undo", "palette")),
        new("engineering", "Diagrams", "Shapes, text and grid boards for drawing diagrams",
            Ordered("cursor", "pen", "eraser", "color", "shape", "text", "marker", "board", "zoom", "capture", "select", "undo", "clear")),
    ];

    private static string[] Ordered(params string[] ids)
    {
        var order = Items.Select(i => i.Id).ToList();
        return ids.OrderBy(order.IndexOf).ToArray();
    }

    /// <summary>Saved layout re-sorted into the canonical grouped order, keeping each item's visibility.</summary>
    public static List<ToolbarItemState> Canonicalize(IEnumerable<ToolbarItemState> saved)
    {
        var visible = saved.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First().Visible);
        return Items.Select(i => new ToolbarItemState { Id = i.Id, Visible = visible.TryGetValue(i.Id, out var v) ? v : i.DefaultVisible }).ToList();
    }

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
