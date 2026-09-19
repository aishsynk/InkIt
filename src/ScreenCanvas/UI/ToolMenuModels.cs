namespace ScreenCanvas.UI;

public sealed record ToolFamily(string Id,string Label,string IconKey,IReadOnlyList<ToolGroup> Groups);
public sealed record ToolGroup(string Id,string Label,string IconKey,IReadOnlyList<ToolCommand> Commands);
public sealed record ToolCommand(string Id,string Label,string IconKey,Action Execute,bool HasInkProperties=false);
