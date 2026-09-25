namespace ScreenCanvas.Commands;

public sealed class PresetDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string IconKey { get; init; }
    public string DefaultToolId { get; init; } = string.Empty;
}

/// <summary>Presenter presets from the design's Settings → Toolbar &amp; Presets tab (PresetManager.cs).</summary>
public sealed class PresetManager
{
    public static PresetManager Instance { get; } = new();

    private readonly List<PresetDefinition> _presets =
    [
        new()
        {
            Id = "teaching",
            Name = "Teaching Preset",
            Description = "Pen, Highlighter, Arrow Shapes, Whiteboard, Numbered Markers, Break Timer",
            IconKey = "Pen",
            DefaultToolId = "annot.pen"
        },
        new()
        {
            Id = "techdemo",
            Name = "TechDemo Preset",
            Description = "Laser Pointer, Code Focus Slit, Curtain, Live Zoom, Keystroke Visualizer, DemoType",
            IconKey = "Terminal",
            DefaultToolId = "present.laser"
        }
    ];

    public IReadOnlyList<PresetDefinition> Presets => _presets;
    public PresetDefinition ActivePreset { get; private set; }
    public event EventHandler<PresetDefinition>? ActivePresetChanged;

    private PresetManager() => ActivePreset = _presets[0];

    public void SetActivePreset(string id)
    {
        var preset = _presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (preset is null || preset == ActivePreset) return;
        ActivePreset = preset;
        ActivePresetChanged?.Invoke(this, preset);
    }
}
