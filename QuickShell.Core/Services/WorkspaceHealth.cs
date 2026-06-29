using QuickShell.Models;

namespace QuickShell.Services;

internal static class WorkspaceHealth
{
    public static bool NeedsFolder(Workspace workspace) =>
        WorkspacePath.IsEmptyOrWhitespace(workspace.Directory);

    public static bool HasConfiguredLexicalDirectory(Workspace workspace) =>
        WorkspacePath.HasConfiguredLexicalDirectory(workspace.Directory);

    public static string BuildListFolderHint(Workspace workspace)
    {
        if (NeedsFolder(workspace))
        {
            return "Choose project folder";
        }

        if (!HasConfiguredLexicalDirectory(workspace))
        {
            return "Invalid folder path · fix in details";
        }

        return ShortcutDisplay.ShortenPathForDisplay(workspace.Directory);
    }
}
