using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Models;

namespace QuickShell.Services;

internal static class WorkspaceLayoutDisplay
{
    public const string WorkspacesSectionTitle = "Workspaces";

    public static IEnumerable<IListItem> BuildListItems(
        IReadOnlyList<Workspace> workspaces,
        Func<Workspace, IListItem> buildWorkspaceItem)
    {
        var unpinned = workspaces
            .Where(workspace => !workspace.IsPinned)
            .OrderBy(workspace => workspace.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unpinned.Count == 0)
        {
            yield break;
        }

        yield return new Separator(WorkspacesSectionTitle);
        foreach (var workspace in unpinned)
        {
            yield return buildWorkspaceItem(workspace);
        }
    }
}
