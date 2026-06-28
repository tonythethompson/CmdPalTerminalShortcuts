using QuickShell.Models;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QuickShell.Services;

internal sealed partial class ShortcutDraftStore(IShortcutRepository shortcuts) : IDraftStore, IDisposable
{
    private readonly IShortcutRepository _shortcuts = shortcuts;
    private readonly SemaphoreSlim _sync = new(1, 1);

    private bool _disposed;

    private PersistedShortcutEditDraft? _cached;
    private bool _cacheLoaded;
    private Task _fileIoQueue = Task.CompletedTask;

    public string DraftPath => Path.Combine(_shortcuts.ConfigDirectory, "shortcut-edit-draft.json");

    public bool HasPending =>
        WithLock(() => TryGetPendingLocked(out _));

    public PersistedShortcutEditDraft? Pending =>
        WithLock(() => TryGetPendingLocked(out var draft) ? draft : null);

    public bool TryGetForRestore(string originalName, out PersistedShortcutEditDraft draft)
    {
        draft = null!;

        if (string.IsNullOrWhiteSpace(originalName))
        {
            return false;
        }

        _sync.Wait();
        try
        {
            if (!TryGetPendingLocked(out var pending))
            {
                return false;
            }

            if (pending is null
                || !string.Equals(pending.OriginalName, originalName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            draft = pending;
            return true;
        }
        finally
        {
            _sync.Release();
        }
    }

    public void SaveIfDirty(
        string editKey,
        ShortcutFormDraftData draft,
        ShortcutFormDraftData baseline,
        bool nameCustomized,
        string? autoFilledName)
    {
        if (string.IsNullOrWhiteSpace(editKey))
        {
            return;
        }

        if (DraftEquals(draft, baseline))
        {
            WithLock(() =>
            {
                if (_cached is not null
                    && string.Equals(_cached.OriginalName, editKey, StringComparison.OrdinalIgnoreCase))
                {
                    ClearLocked();
                }
            });

            return;
        }

        var persisted = new PersistedShortcutEditDraft
        {
            OriginalName = editKey,
            Name = draft.Name,
            Abbreviation = draft.Abbreviation,
            Directory = draft.Directory,
            Command = draft.Command,
            LaunchTarget = draft.LaunchTarget,
            NameCustomized = nameCustomized,
            AutoFilledName = autoFilledName,
            RunAsAdmin = draft.RunAsAdmin,
        };

        WithLock(() =>
        {
            _cached = persisted;
            WriteLocked(persisted);
        });
    }

    public void Clear() =>
        WithLock(ClearLocked);

    public ShortcutSaveResult TryCommitPending(Action? onSaved)
    {
        PersistedShortcutEditDraft? pending = null;

        var hasPending = WithLock(() =>
        {
            if (!TryGetPendingLocked(out pending) || pending is null)
            {
                return false;
            }

            return true;
        });

        if (!hasPending)
        {
            return ShortcutSaveResult.Fail("No unsaved shortcut edit is pending.");
        }

        var result = ShortcutFormSave.TrySave(
            pending!.OriginalName,
            pending.Name,
            pending.Abbreviation,
            pending.Directory,
            pending.Command,
            pending.LaunchTarget,
            pending.RunAsAdmin,
            _shortcuts,
            onSaved);

        if (result.Success)
        {
            Clear();
        }

        return result;
    }

    private bool TryGetPendingLocked(out PersistedShortcutEditDraft? draft)
    {
        EnsureLoadedLocked();
        draft = _cached;

        if (draft is null)
        {
            return false;
        }

        if (_shortcuts.GetByName(draft.OriginalName) is not { } saved)
        {
            ClearLocked();
            draft = null;
            return false;
        }

        if (DraftMatchesShortcut(draft, saved))
        {
            ClearLocked();
            draft = null;
            return false;
        }

        return true;
    }

    private void EnsureLoadedLocked()
    {
        if (_cacheLoaded)
        {
            return;
        }

        DrainFileIoQueueLocked();

        _cacheLoaded = true;
        _cached = null;

        try
        {
            if (!File.Exists(DraftPath))
            {
                return;
            }

            using var stream = File.OpenRead(DraftPath);
            _cached = JsonSerializer.Deserialize(stream, ShortcutFormDraftJsonContext.Default.PersistedShortcutEditDraft);
        }
        catch
        {
            _cached = null;
        }
    }

    private void WriteLocked(PersistedShortcutEditDraft draft)
    {
        try
        {
            var json = JsonSerializer.Serialize(draft, ShortcutFormDraftJsonContext.Default.PersistedShortcutEditDraft);
            EnqueueFileIoLocked(() => PersistDraftAsync(json));
        }
        catch
        {
            // Best-effort autosave; ignore serialization failures.
        }
    }

    private void ClearLocked()
    {
        _cached = null;
        EnqueueFileIoLocked(DeleteDraftIfPresentAsync);
    }

    private void DrainFileIoQueueLocked()
    {
        try
        {
            _fileIoQueue.GetAwaiter().GetResult();
        }
        catch
        {
            // Best effort.
        }
    }

    private void EnqueueFileIoLocked(Func<Task> operation)
    {
        _fileIoQueue = _fileIoQueue
            .ContinueWith(_ => operation(), TaskScheduler.Default)
            .Unwrap();
    }

    private async Task PersistDraftAsync(string json)
    {
        try
        {
            Directory.CreateDirectory(_shortcuts.ConfigDirectory);
            await File.WriteAllTextAsync(DraftPath, json).ConfigureAwait(false);
        }
        catch
        {
            // Best effort autosave; ignore IO failures.
        }
    }

    private Task DeleteDraftIfPresentAsync()
    {
        try
        {
            if (File.Exists(DraftPath))
            {
                File.Delete(DraftPath);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }

        return Task.CompletedTask;
    }

    private static bool DraftMatchesShortcut(PersistedShortcutEditDraft draft, TerminalShortcut saved)
    {
        var launchTarget = TerminalCatalog.EncodeLaunchTargetId(saved);
        return string.Equals(Normalize(draft.Name), Normalize(saved.Name), StringComparison.Ordinal)
            && string.Equals(Normalize(draft.Abbreviation), Normalize(saved.Abbreviation), StringComparison.Ordinal)
            && string.Equals(Normalize(draft.Directory), Normalize(saved.Directory), StringComparison.Ordinal)
            && string.Equals(Normalize(draft.Command), Normalize(saved.Command), StringComparison.Ordinal)
            && string.Equals(Normalize(draft.LaunchTarget), Normalize(launchTarget), StringComparison.Ordinal)
            && draft.RunAsAdmin == saved.RunAsAdmin;
    }

    private static bool DraftEquals(ShortcutFormDraftData left, ShortcutFormDraftData right) =>
        string.Equals(Normalize(left.Name), Normalize(right.Name), StringComparison.Ordinal)
        && string.Equals(Normalize(left.Abbreviation), Normalize(right.Abbreviation), StringComparison.Ordinal)
        && string.Equals(Normalize(left.Directory), Normalize(right.Directory), StringComparison.Ordinal)
        && string.Equals(Normalize(left.Command), Normalize(right.Command), StringComparison.Ordinal)
        && string.Equals(Normalize(left.LaunchTarget), Normalize(right.LaunchTarget), StringComparison.Ordinal)
        && left.RunAsAdmin == right.RunAsAdmin;

    private static string Normalize(string? value) => (value ?? string.Empty).Trim();

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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            WithLock(DrainFileIoQueueLocked);
        }
        catch
        {
            // Best effort drain during shutdown.
        }

        _sync.Dispose();
        GC.SuppressFinalize(this);
    }
}
