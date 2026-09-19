namespace ScreenCanvas.Commands;

public sealed class CommandItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required CapabilityCategory Category { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string IconKey { get; init; }
    public string? Shortcut { get; init; }
    public string[] SearchTags { get; init; } = [];
    public required Action Execute { get; init; }
    public Func<bool>? IsActive { get; init; }
    public bool IsFavorite { get; set; }
    public DateTime? LastUsed { get; set; }

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var q = query.Trim();
        if (Name.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        if (Description.Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        if (Category.ToString().Contains(q, StringComparison.OrdinalIgnoreCase)) return true;
        if (Shortcut?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) return true;
        return SearchTags.Any(tag => tag.Contains(q, StringComparison.OrdinalIgnoreCase));
    }
}
