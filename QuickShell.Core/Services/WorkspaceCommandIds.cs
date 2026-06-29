using System.Text;

namespace QuickShell.Services;

internal static class WorkspaceCommandIds
{
    public const string CreateWorkspace = "com.quickshell.workspace-form.create";

    private const string OpenPrefix = "com.quickshell.workspace.open.";

    private const string OpenEntryPrefix = "com.quickshell.workspace.entry.open.";

    public static string Open(string workspaceId) =>
        OpenPrefix + workspaceId;

    public static string OpenEntry(string workspaceId, string entryId) =>
        $"{OpenEntryPrefix}{workspaceId}.{entryId}";

    public static string FavoriteToggle(string workspaceName) =>
        $"com.quickshell.workspace.favorite.{EncodeNameKey(workspaceName)}";

    private static string EncodeNameKey(string name) =>
        Convert.ToHexString(Encoding.UTF8.GetBytes(name)).ToLowerInvariant();

    public static bool TryParseOpen(string commandId, out string workspaceId)
    {
        workspaceId = string.Empty;

        if (!commandId.StartsWith(OpenPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        workspaceId = commandId[OpenPrefix.Length..];
        return !string.IsNullOrWhiteSpace(workspaceId);
    }

    public static bool TryParseOpenEntry(string commandId, out string workspaceId, out string entryId)
    {
        workspaceId = string.Empty;
        entryId = string.Empty;

        if (!commandId.StartsWith(OpenEntryPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remainder = commandId[OpenEntryPrefix.Length..];
        var separatorIndex = remainder.IndexOf('.', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= remainder.Length - 1)
        {
            return false;
        }

        workspaceId = remainder[..separatorIndex];
        entryId = remainder[(separatorIndex + 1)..];
        return !string.IsNullOrWhiteSpace(workspaceId) && !string.IsNullOrWhiteSpace(entryId);
    }
}
