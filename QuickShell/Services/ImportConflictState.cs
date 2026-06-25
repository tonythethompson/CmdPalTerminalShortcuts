namespace QuickShell.Services;

internal static class ImportConflictState
{
    private static PendingImport? _pending;

    public static bool HasPending => _pending is not null;

    public static PendingImport? Pending => _pending;

    public static void Set(string path, int conflictCount, int importCount, Action onReload) =>
        _pending = new PendingImport(path, conflictCount, importCount, onReload);

    public static void Clear() => _pending = null;

    internal sealed record PendingImport(string Path, int ConflictCount, int ImportCount, Action OnReload);
}
