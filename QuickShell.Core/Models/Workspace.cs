namespace QuickShell.Models;

internal sealed class Workspace
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Abbreviation { get; set; }

    public string Directory { get; set; } = string.Empty;

    public bool IsPinned { get; set; }

    public int? PinOrder { get; set; }

    public List<WorkspaceEntry> Entries { get; set; } = [];
}
