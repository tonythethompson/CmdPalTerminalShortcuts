namespace QuickShell.Models;

/// <summary>JSON read shape including legacy <c>ProjectShortcutId</c>.</summary>
internal sealed class WorkspaceDiskRecord
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Abbreviation { get; set; }

    public string? Directory { get; set; }

    public string? ProjectShortcutId { get; set; }

    public bool IsPinned { get; set; }

    public int? PinOrder { get; set; }

    public List<WorkspaceEntry>? Entries { get; set; }
}
