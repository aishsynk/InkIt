namespace ScreenCanvas.Commands;

public sealed class PresetDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string IconKey { get; init; }
    public required List<string> ToolbarCommandIds { get; init; }
    public string DefaultToolId { get; init; } = "cursor";
    public bool IsBuiltIn { get; init; } = true;
}

public sealed class PresetManager
{
    public static PresetManager Instance { get; } = new();

    private readonly List<PresetDefinition> _presets = [];
    public IReadOnlyList<PresetDefinition> Presets => _presets;

    public PresetDefinition ActivePreset { get; private set; }

    public event EventHandler<PresetDefinition>? ActivePresetChanged;

    private PresetManager()
    {
        var teaching = new PresetDefinition
        {
            Id = "teaching",
            Name = "Teaching",
            Description = "Standard trainer layout with pen, highlighter, shapes, text, zoom, and present tools.",
            IconKey = "Fluent.Pen.Regular",
            ToolbarCommandIds = ["cursor", "pen", "highlighter", "eraser", "shapes", "text", "present", "zoom", "undo", "redo", "clear", "more"],
            DefaultToolId = "pen"
        };

        var techDemo = new PresetDefinition
        {
            Id = "techdemo",
            Name = "Technical Demo",
            Description = "Optimized for coding, SSMS, Visual Studio, Azure Portal and terminal demonstrations.",
            IconKey = "Fluent.Desktop.Regular",
            ToolbarCommandIds = ["cursor", "pen", "arrow", "codefocus", "zoom", "freeze", "clickvis", "keyvis", "undo", "redo", "clear", "more"],
            DefaultToolId = "cursor"
        };

        var presentation = new PresetDefinition
        {
            Id = "presentation",
            Name = "Presentation",
            Description = "Focus on laser pointer, spotlight, timer, slide controls and clean attention direction.",
            IconKey = "Fluent.SlideShow.Regular",
            ToolbarCommandIds = ["cursor", "laser", "present.spotlight", "codefocus", "zoom", "breaktimer", "slidenext", "slideprev", "undo", "clear", "more"],
            DefaultToolId = "laser"
        };

        var whiteboard = new PresetDefinition
        {
            Id = "whiteboard",
            Name = "Whiteboard",
            Description = "Focus on blank canvas sketching, diagrams, connectors, shapes and structured notes.",
            IconKey = "Fluent.Board.Regular",
            ToolbarCommandIds = ["cursor", "pen", "highlighter", "eraser", "shapes", "text", "whiteboard", "undo", "redo", "clear", "more"],
            DefaultToolId = "pen"
        };

        _presets.AddRange([teaching, techDemo, presentation, whiteboard]);
        ActivePreset = teaching;
    }

    public void SetActivePreset(string id)
    {
        var preset = _presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (preset is not null && preset != ActivePreset)
        {
            ActivePreset = preset;
            ActivePresetChanged?.Invoke(this, preset);
        }
    }
}
