using QuickShell.Models;

namespace QuickShell.Services;

internal interface IWorkspaceRepository
{
    string ConfigPath { get; }

    IReadOnlyList<Workspace> GetWorkspaces();

    Workspace? GetByName(string name);

    Workspace? GetById(string id);

    IReadOnlyList<Workspace> GetByDirectory(string directory);

    void Reload();

    void FlushPendingWrites();

    void Upsert(Workspace workspace, string? originalName = null);

    bool Delete(string name);

    bool TogglePinned(string name);

    Workspace? BuildDuplicate(string name);

    IEnumerable<Workspace> Search(string query);

    IEnumerable<Workspace> SearchForRootPalette(string query);

    string ResolveAvailableName(string desiredName, string? replacingOriginalName = null);

    bool TryExportToFile(string path, out string error);

    Task<WorkspaceExportResult> TryExportToFileAsync(string path, CancellationToken cancellationToken = default);

    bool TryReadImportFile(string path, out Workspace[] workspaces, out string error);

    Task<WorkspaceImportReadResult> TryReadImportFileAsync(string path, CancellationToken cancellationToken = default);

    WorkspaceTransferResult ImportMerge(string path);

    Task<WorkspaceTransferResult> ImportMergeAsync(string path, CancellationToken cancellationToken = default);

    WorkspaceTransferResult ImportReplace(string path);

    Task<WorkspaceTransferResult> ImportReplaceAsync(string path, CancellationToken cancellationToken = default);
}

internal readonly record struct WorkspaceExportResult(bool Success, string Error);

internal readonly record struct WorkspaceImportReadResult(bool Success, Workspace[] Workspaces, string Error);

internal sealed class WorkspaceTransferResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public int Imported { get; init; }

    public int Skipped { get; init; }

    public int Renamed { get; init; }
}
