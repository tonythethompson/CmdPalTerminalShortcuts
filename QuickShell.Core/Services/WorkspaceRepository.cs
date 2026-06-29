using QuickShell.Models;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuickShell.Services;

internal sealed partial class WorkspaceRepository : IWorkspaceRepository, IDisposable
{
    private const int MaxConfigBytes = 2 * 1024 * 1024;

    private readonly IShortcutRepository _shortcuts;

    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly Mutex _fileMutex = new(false, @"Global\QuickShell_workspaces_json");

    private List<Workspace> _workspaces = [];
    private List<Workspace> _lastGoodWorkspaces = [];
    private DateTime _lastWriteTimeUtc = DateTime.MinValue;
    private bool _configEnsured;
    private bool _persistPending;
    private System.Threading.Timer? _persistTimer;
    private bool _disposed;

    public WorkspaceRepository(IShortcutRepository shortcuts)
    {
        _shortcuts = shortcuts;
    }

    public string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickShell");

    public string ConfigPath => Path.Combine(ConfigDirectory, "workspaces.json");

    public IReadOnlyList<Workspace> GetWorkspaces() =>
        WithLock(() =>
        {
            EnsureLoaded();
            return CloneAll(_workspaces);
        });

    public Workspace? GetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return WithLock(() =>
        {
            EnsureLoaded();
            var workspace = _workspaces.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return workspace is null ? null : WorkspaceMapper.CloneWorkspace(workspace);
        });
    }

    public Workspace? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return WithLock(() =>
        {
            EnsureLoaded();
            var workspace = _workspaces.FirstOrDefault(w => w.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            return workspace is null ? null : WorkspaceMapper.CloneWorkspace(workspace);
        });
    }

    public IReadOnlyList<Workspace> GetByDirectory(string directory)
    {
        if (!WorkspacePath.TryNormalizeLexical(directory, out _, out _))
        {
            return [];
        }

        return WithLock(() =>
        {
            EnsureLoaded();
            return _workspaces
                .Where(workspace => WorkspacePath.PathsEqual(workspace.Directory, directory))
                .Select(WorkspaceMapper.CloneWorkspace)
                .ToList();
        });
    }

    public void Reload() =>
        WithLock(() =>
        {
            CancelPendingPersist();
            _lastWriteTimeUtc = DateTime.MinValue;
            EnsureLoaded(force: true);
        });

    public void FlushPendingWrites() =>
        WithLock(FlushPendingPersistLocked);

    public void Upsert(Workspace workspace, string? originalName = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        if (!WorkspaceValidation.TryValidateForSave(workspace, _shortcuts, this, originalName, out var validationError))
        {
            throw new InvalidOperationException(validationError);
        }

        WithLock(() =>
        {
            EnsureLoaded();
            CancelPendingPersist();
            var workspaces = CloneAll(_workspaces);
            var cloned = WorkspaceMapper.CloneWorkspace(workspace);

            var existingIndex = workspaces.FindIndex(w =>
                w.Name.Equals(cloned.Name, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(originalName) && w.Name.Equals(originalName, StringComparison.OrdinalIgnoreCase)));

            if (existingIndex >= 0)
            {
                cloned.Id = workspaces[existingIndex].Id;
                cloned.IsPinned = workspaces[existingIndex].IsPinned;
                cloned.PinOrder = workspaces[existingIndex].PinOrder;
                workspaces[existingIndex] = cloned;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(cloned.Id) || !ShortcutCommandIds.IsStableShortcutId(cloned.Id))
                {
                    cloned.Id = Guid.NewGuid().ToString("N");
                }

                workspaces.Add(cloned);
            }

            WorkspaceMapper.NormalizeEntryOrders(cloned);
            SaveWorkspacesLocked(workspaces);
        });
    }

    public bool Delete(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return WithLock(() =>
        {
            EnsureLoaded();
            CancelPendingPersist();
            var workspaces = CloneAll(_workspaces);
            var removed = workspaces.RemoveAll(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                SaveWorkspacesLocked(workspaces);
            }

            return removed;
        });
    }

    public bool TogglePinned(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return WithLock(() =>
        {
            EnsureLoaded();
            CancelPendingPersist();
            var workspaces = CloneAll(_workspaces);
            var workspace = workspaces.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (workspace is null)
            {
                return false;
            }

            workspace.IsPinned = !workspace.IsPinned;
            workspace.PinOrder = workspace.IsPinned ? NextPinOrder(workspaces) : null;
            SaveWorkspacesLocked(workspaces);
            return workspace.IsPinned;
        });
    }

    public Workspace? BuildDuplicate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return WithLock(() =>
        {
            EnsureLoaded();
            var source = _workspaces.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (source is null)
            {
                return null;
            }

            var duplicateName = ResolveAvailableName($"{source.Name} copy", null);
            var duplicate = WorkspaceMapper.CloneWorkspace(source);
            duplicate.Id = Guid.NewGuid().ToString("N");
            duplicate.Name = duplicateName;
            duplicate.Abbreviation = null;
            duplicate.IsPinned = false;
            duplicate.PinOrder = null;
            foreach (var entry in duplicate.Entries)
            {
                entry.Id = Guid.NewGuid().ToString("N");
            }

            return duplicate;
        });
    }

    public IEnumerable<Workspace> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return GetWorkspaces();
        }

        var trimmed = query.Trim();
        return WithLock(() =>
        {
            EnsureLoaded();
            return _workspaces
                .Where(workspace => Matches(workspace, trimmed))
                .Select(WorkspaceMapper.CloneWorkspace)
                .OrderBy(workspace => workspace.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        });
    }

    public IEnumerable<Workspace> SearchForRootPalette(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var trimmed = query.Trim();
        return WithLock(() =>
        {
            EnsureLoaded();
            var abbreviationMatches = _workspaces
                .Where(workspace => !string.IsNullOrWhiteSpace(workspace.Abbreviation)
                    && workspace.Abbreviation.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(workspace => workspace.Abbreviation!.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(workspace => workspace.Abbreviation!.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
                .ThenBy(workspace => workspace.Name, StringComparer.OrdinalIgnoreCase)
                .Select(WorkspaceMapper.CloneWorkspace)
                .ToArray();

            if (abbreviationMatches.Length > 0)
            {
                return abbreviationMatches;
            }

            return _workspaces
                .Where(workspace => MatchesForRootPalette(workspace, trimmed))
                .Select(WorkspaceMapper.CloneWorkspace)
                .OrderBy(workspace => workspace.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        });
    }

    public string ResolveAvailableName(string desiredName, string? replacingOriginalName = null)
    {
        var baseName = string.IsNullOrWhiteSpace(desiredName) ? "Workspace" : desiredName.Trim();
        return WithLock(() =>
        {
            EnsureLoaded();
            if (!string.IsNullOrWhiteSpace(replacingOriginalName)
                && baseName.Equals(replacingOriginalName, StringComparison.OrdinalIgnoreCase))
            {
                return baseName;
            }

            if (_workspaces.All(workspace => !workspace.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase)))
            {
                return baseName;
            }

            for (var suffix = 2; suffix < 10_000; suffix++)
            {
                var candidate = $"{baseName} ({suffix})";
                if (_workspaces.All(workspace => !workspace.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }

            return $"{baseName} ({Guid.NewGuid():N})";
        });
    }

    public bool TryExportToFile(string path, out string error)
    {
        var result = TryExportToFileAsync(path).GetAwaiter().GetResult();
        error = result.Error;
        return result.Success;
    }

    public async Task<WorkspaceExportResult> TryExportToFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new WorkspaceExportResult(false, "Export path is required.");
        }

        byte[] payload;
        try
        {
            var prepare = WithLock(() =>
            {
                EnsureLoaded();
                FlushPendingPersistLocked();
                var payload = SerializeWriteRecords(_workspaces);
                return payload.Length > MaxConfigBytes
                    ? (Success: false, Payload: Array.Empty<byte>())
                    : (Success: true, Payload: payload);
            });

            if (!prepare.Success)
            {
                return new WorkspaceExportResult(false, "Workspace data is too large to export.");
            }

            payload = prepare.Payload;
        }
        catch (Exception ex)
        {
            return new WorkspaceExportResult(false, ex.Message);
        }

        try
        {
            await File.WriteAllBytesAsync(path, payload, cancellationToken).ConfigureAwait(false);
            return new WorkspaceExportResult(true, string.Empty);
        }
        catch (Exception ex)
        {
            return new WorkspaceExportResult(false, ex.Message);
        }
    }

    public bool TryReadImportFile(string path, out Workspace[] workspaces, out string error)
    {
        var result = TryReadImportFileAsync(path).GetAwaiter().GetResult();
        workspaces = result.Workspaces;
        error = result.Error;
        return result.Success;
    }

    public async Task<WorkspaceImportReadResult> TryReadImportFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new WorkspaceImportReadResult(false, [], "Import path is required.");
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (bytes.Length > MaxConfigBytes)
            {
                return new WorkspaceImportReadResult(false, [], "Workspace file is too large.");
            }

            if (!TryParseDiskRecords(bytes, out var records, out var error))
            {
                return new WorkspaceImportReadResult(false, [], error);
            }

            var workspaces = LoadFromDiskRecords(records, _shortcuts, out _).ToArray();
            return new WorkspaceImportReadResult(true, workspaces, string.Empty);
        }
        catch (Exception ex)
        {
            return new WorkspaceImportReadResult(false, [], ex.Message);
        }
    }

    public WorkspaceTransferResult ImportMerge(string path) =>
        ImportMergeAsync(path).GetAwaiter().GetResult();

    public Task<WorkspaceTransferResult> ImportMergeAsync(string path, CancellationToken cancellationToken = default) =>
        ImportCoreAsync(path, replace: false, cancellationToken);

    public WorkspaceTransferResult ImportReplace(string path) =>
        ImportReplaceAsync(path).GetAwaiter().GetResult();

    public Task<WorkspaceTransferResult> ImportReplaceAsync(string path, CancellationToken cancellationToken = default) =>
        ImportCoreAsync(path, replace: true, cancellationToken);

    internal byte[] SerializeWriteRecordsForTests(IReadOnlyList<Workspace> workspaces) =>
        SerializeWriteRecords(workspaces);

    internal static List<Workspace> LoadFromDiskRecordsForTests(
        IReadOnlyList<WorkspaceDiskRecord> records,
        IShortcutRepository shortcuts,
        out bool requiresPersistence) =>
        LoadFromDiskRecords(records, shortcuts, out requiresPersistence);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        FlushPendingWrites();
        _persistTimer?.Dispose();
        _fileMutex.Dispose();
        _sync.Dispose();
    }

    private async Task<WorkspaceTransferResult> ImportCoreAsync(string path, bool replace, CancellationToken cancellationToken)
    {
        var read = await TryReadImportFileAsync(path, cancellationToken).ConfigureAwait(false);
        if (!read.Success)
        {
            return new WorkspaceTransferResult
            {
                Success = false,
                Message = read.Error,
            };
        }

        return WithLock(() =>
        {
            EnsureLoaded();
            CancelPendingPersist();
            var workspaces = replace ? new List<Workspace>() : CloneAll(_workspaces);
            var importedCount = 0;
            var renamed = 0;
            var skipped = 0;

            foreach (var workspace in read.Workspaces)
            {
                if (!WorkspaceValidation.TryValidateForImport(workspace, _shortcuts, this, out _))
                {
                    skipped++;
                    continue;
                }

                if (workspaces.Any(existing => existing.Name.Equals(workspace.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    if (replace)
                    {
                        workspaces.RemoveAll(existing => existing.Name.Equals(workspace.Name, StringComparison.OrdinalIgnoreCase));
                    }
                    else
                    {
                        workspace.Name = ResolveAvailableName(workspace.Name, null);
                        renamed++;
                    }
                }

                workspace.Id = Guid.NewGuid().ToString("N");
                foreach (var entry in workspace.Entries)
                {
                    entry.Id = Guid.NewGuid().ToString("N");
                }

                workspaces.Add(workspace);
                importedCount++;
            }

            if (importedCount == 0 && read.Workspaces.Length > 0)
            {
                skipped = read.Workspaces.Length;
            }

            SaveWorkspacesLocked(workspaces);
            return new WorkspaceTransferResult
            {
                Success = true,
                Imported = importedCount,
                Renamed = renamed,
                Skipped = skipped,
                Message = replace
                    ? $"Replaced workspaces with {importedCount} imported."
                    : $"Imported {importedCount} workspace{(importedCount == 1 ? string.Empty : "s")}.",
            };
        });
    }

    private void EnsureLoaded(bool force = false)
    {
        EnsureConfigExists();

        if (!force && _configEnsured && File.Exists(ConfigPath))
        {
            var writeTime = File.GetLastWriteTimeUtc(ConfigPath);
            if (writeTime <= _lastWriteTimeUtc)
            {
                return;
            }
        }

        if (!File.Exists(ConfigPath))
        {
            _workspaces = [];
            _lastGoodWorkspaces = [];
            _lastWriteTimeUtc = DateTime.UtcNow;
            _configEnsured = true;
            return;
        }

        var bytes = File.ReadAllBytes(ConfigPath);
        if (!TryParseDiskRecords(bytes, out var records, out _))
        {
            _workspaces = CloneAll(_lastGoodWorkspaces);
            _lastWriteTimeUtc = File.GetLastWriteTimeUtc(ConfigPath);
            _configEnsured = true;
            return;
        }

        var loaded = LoadFromDiskRecords(records, _shortcuts, out var requiresPersistence);
        _workspaces = loaded;
        _lastGoodWorkspaces = CloneAll(loaded);
        _lastWriteTimeUtc = File.GetLastWriteTimeUtc(ConfigPath);
        _configEnsured = true;

        if (requiresPersistence)
        {
            try
            {
                WriteWorkspacesAtomic(_workspaces);
                _lastWriteTimeUtc = File.GetLastWriteTimeUtc(ConfigPath);
            }
            catch
            {
                // Keep in-memory migrated state; disk remains recoverable legacy JSON.
            }
        }
    }

    private static List<Workspace> LoadFromDiskRecords(
        IReadOnlyList<WorkspaceDiskRecord> records,
        IShortcutRepository shortcuts,
        out bool requiresPersistence)
    {
        requiresPersistence = false;
        var seenWorkspaceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var workspaces = new List<Workspace>();

        foreach (var record in records)
        {
            var result = WorkspaceValidation.NormalizeForLoad(record, shortcuts, seenWorkspaceIds);
            if (result.Workspace is null)
            {
                continue;
            }

            if (result.RequiresPersistence)
            {
                requiresPersistence = true;
            }

            workspaces.Add(result.Workspace);
        }

        return workspaces;
    }

    private void EnsureConfigExists()
    {
        if (_configEnsured)
        {
            return;
        }

        Directory.CreateDirectory(ConfigDirectory);
        if (!File.Exists(ConfigPath))
        {
            WriteWorkspacesAtomic([]);
            _lastWriteTimeUtc = File.GetLastWriteTimeUtc(ConfigPath);
        }

        _configEnsured = true;
    }

    private void SaveWorkspacesLocked(List<Workspace> workspaces)
    {
        if (workspaces.Count > WorkspaceValidation.MaxWorkspaceCount)
        {
            throw new InvalidOperationException($"At most {WorkspaceValidation.MaxWorkspaceCount} workspaces are supported.");
        }

        _workspaces = CloneAll(workspaces);
        _lastGoodWorkspaces = CloneAll(workspaces);
        SchedulePersistLocked();
    }

    private void SchedulePersistLocked()
    {
        _persistPending = true;
        _persistTimer ??= new System.Threading.Timer(_ => WithLock(FlushPendingPersistLocked), null, Timeout.Infinite, Timeout.Infinite);
        _persistTimer.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
    }

    private void CancelPendingPersist()
    {
        _persistPending = false;
        _persistTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void FlushPendingPersistLocked()
    {
        if (!_persistPending)
        {
            return;
        }

        _persistPending = false;
        WriteWorkspacesAtomic(_workspaces);
        _lastGoodWorkspaces = CloneAll(_workspaces);
        _lastWriteTimeUtc = File.GetLastWriteTimeUtc(ConfigPath);
    }

    private void WriteWorkspacesAtomic(IReadOnlyList<Workspace> workspaces)
    {
        var payload = SerializeWriteRecords(workspaces);
        if (payload.Length > MaxConfigBytes)
        {
            throw new InvalidOperationException("Workspace data is too large to save.");
        }

        Directory.CreateDirectory(ConfigDirectory);
        var tempPath = ConfigPath + ".tmp";
        var backupPath = ConfigPath + ".bak";

        if (!_fileMutex.WaitOne(TimeSpan.FromSeconds(5)))
        {
            throw new IOException("Could not acquire the workspace store lock.");
        }

        try
        {
            File.WriteAllBytes(tempPath, payload);
            if (File.Exists(ConfigPath))
            {
                File.Replace(tempPath, ConfigPath, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, ConfigPath);
            }
        }
        finally
        {
            _fileMutex.ReleaseMutex();
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Best effort.
            }
        }
    }

    private static byte[] SerializeWriteRecords(IReadOnlyList<Workspace> workspaces) =>
        JsonSerializer.SerializeToUtf8Bytes(
            WorkspaceMapper.ToWriteRecords(workspaces),
            QuickShellJsonContext.Default.ListWorkspaceWriteRecord);

    private static bool TryParseDiskRecords(byte[] bytes, out List<WorkspaceDiskRecord> records, out string error)
    {
        records = [];
        error = string.Empty;

        try
        {
            var parsed = JsonSerializer.Deserialize(bytes, QuickShellJsonContext.Default.ListWorkspaceDiskRecord);
            records = parsed ?? [];
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Invalid workspace file: {ex.Message}";
            return false;
        }
    }

    private static int NextPinOrder(IReadOnlyList<Workspace> workspaces) =>
        workspaces.Where(workspace => workspace.IsPinned).MaxBy(workspace => workspace.PinOrder ?? 0)?.PinOrder + 1 ?? 1;

    private static bool Matches(Workspace workspace, string query)
    {
        if (workspace.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(workspace.Abbreviation)
            && workspace.Abbreviation.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(workspace.Directory)
            && workspace.Directory.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return workspace.Entries.Any(entry =>
            entry.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(entry.Command)
                && entry.Command.Contains(query, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool MatchesForRootPalette(Workspace workspace, string query)
    {
        if (workspace.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(workspace.Directory)
            && workspace.Directory.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static List<Workspace> CloneAll(IReadOnlyList<Workspace> workspaces) =>
        workspaces.Select(WorkspaceMapper.CloneWorkspace).ToList();

    private void WithLock(Action action)
    {
        _sync.Wait();
        try
        {
            action();
        }
        finally
        {
            _sync.Release();
        }
    }

    private T WithLock<T>(Func<T> action)
    {
        _sync.Wait();
        try
        {
            return action();
        }
        finally
        {
            _sync.Release();
        }
    }
}
