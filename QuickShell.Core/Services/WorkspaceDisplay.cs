using QuickShell.Models;

namespace QuickShell.Services;

internal static class WorkspaceDisplay
{
    public static string BuildFavoriteSubtitle(Workspace workspace)
    {
        var enabledCount = workspace.Entries.Count(entry => entry.IsEnabled);
        var folderHint = WorkspaceHealth.NeedsFolder(workspace)
            ? " · Choose project folder"
            : string.Empty;
        return $"Workspace · {enabledCount} terminal{(enabledCount == 1 ? string.Empty : "s")}{folderHint}";
    }

    public static string BuildListSubtitle(Workspace workspace)
    {
        var enabledEntries = workspace.Entries
            .Where(entry => entry.IsEnabled)
            .OrderBy(entry => entry.Order)
            .Select(ShortcutDisplay.GetLaunchContextMenuTitle)
            .ToList();

        return string.Join(" · ", enabledEntries);
    }

    public static string BuildSearchSubtitle(Workspace workspace)
    {
        var parts = new List<string> { "Workspace" };
        parts.AddRange(workspace.Entries
            .Where(entry => entry.IsEnabled)
            .OrderBy(entry => entry.Order)
            .Select(ShortcutDisplay.GetLaunchContextMenuTitle));

        if (!WorkspaceHealth.NeedsFolder(workspace))
        {
            parts.Add(ShortcutDisplay.ShortenPathForDisplay(workspace.Directory));
        }

        return string.Join(" · ", parts);
    }

    public static string BuildEntrySubtitle(WorkspaceEntry entry)
    {
        var parts = new List<string>
        {
            TerminalCatalog.GetDisplayName(new TerminalShortcut
            {
                Terminal = entry.Terminal,
                WtProfile = entry.WtProfile,
            }),
        };

        if (!string.IsNullOrWhiteSpace(entry.Command))
        {
            parts.Add(entry.Command);
        }

        if (!entry.IsEnabled)
        {
            parts.Add("disabled");
        }

        return string.Join(" · ", parts);
    }

    public static TerminalShortcut ToLaunchShortcut(WorkspaceEntry entry, string directory) =>
        new()
        {
            Name = entry.Label,
            Directory = directory,
            Command = entry.Command,
            Terminal = entry.Terminal,
            WtProfile = entry.WtProfile,
            RunAsAdmin = entry.RunAsAdmin,
        };
}
