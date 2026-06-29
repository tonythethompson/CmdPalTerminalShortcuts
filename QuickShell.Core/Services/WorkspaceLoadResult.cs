using QuickShell.Models;

namespace QuickShell.Services;

internal sealed record WorkspaceLoadResult(
    Workspace? Workspace,
    bool RequiresPersistence,
    bool NeedsFolderRepair,
    string? Warning);
