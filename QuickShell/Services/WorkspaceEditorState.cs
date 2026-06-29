using QuickShell.Models;

namespace QuickShell.Services;

internal static class WorkspaceEditorState
{
    public static Workspace CreateNew(TerminalShortcut projectShortcut)
    {
        var workspaceName = QuickShellRuntimeServices.Workspaces.ResolveAvailableName($"{projectShortcut.Name} — Agents");
        var directory = projectShortcut.Directory;
        if (WorkspacePath.TryNormalizeLexical(projectShortcut.Directory, out var normalized, out _))
        {
            directory = normalized;
        }

        return new Workspace
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = workspaceName,
            Directory = directory,
            Entries =
            [
                CreateEntry("Claude Code", "claude", 0),
                CreateEntry("Codex", "codex", 1),
                CreateEntry("OpenCode", "opencode", 2),
            ],
        };
    }

    public static Workspace CloneWorkspace(Workspace workspace) =>
        WorkspaceMapper.CloneWorkspace(workspace);

    public static WorkspaceEntry CreateEntry(string label, string? command, int order) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Label = label,
        Command = command,
        Terminal = "default",
        IsEnabled = true,
        Order = order,
    };

    public static void MoveEntry(Workspace workspace, string entryId, int direction)
    {
        var ordered = workspace.Entries.OrderBy(entry => entry.Order).ToList();
        var index = ordered.FindIndex(entry => entry.Id.Equals(entryId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        var target = index + direction;
        if (target < 0 || target >= ordered.Count)
        {
            return;
        }

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Order = i;
        }

        workspace.Entries = ordered;
    }
}
