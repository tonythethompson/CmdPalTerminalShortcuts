using QuickShell.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickShell.Services;

internal sealed class ShortcutFormDraftStore
{
    private static readonly object Sync = new();

    private static PersistedShortcutEditDraft? _cached;
    private static bool _cacheLoaded;

    public static string DraftPath => Path.Combine(ShortcutStore.ConfigDirectory, "shortcut-edit-draft.json");

    public static bool HasPending
    {
        get
        {
            lock (Sync)
            {
                return TryGetPendingLocked(out _);
            }
        }
    }

    public static PersistedShortcutEditDraft? Pending
    {
        get
        {
            lock (Sync)
            {
                return TryGetPendingLocked(out var draft) ? draft : null;
            }
        }
    }

    public static bool TryGetForRestore(string originalName, out PersistedShortcutEditDraft draft)
    {
        draft = null!;

        if (string.IsNullOrWhiteSpace(originalName))
        {
            return false;
        }

        lock (Sync)
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
    }

    public static void SaveIfDirty(
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
            lock (Sync)
            {
                if (_cached is not null
                    && string.Equals(_cached.OriginalName, editKey, StringComparison.OrdinalIgnoreCase))
                {
                    ClearLocked();
                }
            }

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

        lock (Sync)
        {
            _cached = persisted;
            WriteLocked(persisted);
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            ClearLocked();
        }
    }

    public static ShortcutSaveResult TryCommitPending(Action? onSaved)
    {
        PersistedShortcutEditDraft? pending;
        lock (Sync)
        {
            if (!TryGetPendingLocked(out pending) || pending is null)
            {
                return ShortcutSaveResult.Fail("No unsaved shortcut edit is pending.");
            }
        }

        var result = ShortcutFormSave.TrySave(
            pending.OriginalName,
            pending.Name,
            pending.Abbreviation,
            pending.Directory,
            pending.Command,
            pending.LaunchTarget,
            pending.RunAsAdmin,
            onSaved);

        if (result.Success)
        {
            Clear();
        }

        return result;
    }

    private static bool TryGetPendingLocked(out PersistedShortcutEditDraft? draft)
    {
        EnsureLoadedLocked();
        draft = _cached;

        if (draft is null)
        {
            return false;
        }

        if (ShortcutStore.GetByName(draft.OriginalName) is not { } saved)
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

    private static void EnsureLoadedLocked()
    {
        if (_cacheLoaded)
        {
            return;
        }

        _cacheLoaded = true;
        _cached = null;

        try
        {
            if (!File.Exists(DraftPath))
            {
                return;
            }

            var json = File.ReadAllText(DraftPath);
            _cached = JsonSerializer.Deserialize(json, ShortcutFormDraftJsonContext.Default.PersistedShortcutEditDraft);
        }
        catch
        {
            _cached = null;
        }
    }

    private static void WriteLocked(PersistedShortcutEditDraft draft)
    {
        try
        {
            Directory.CreateDirectory(ShortcutStore.ConfigDirectory);
            var json = JsonSerializer.Serialize(draft, ShortcutFormDraftJsonContext.Default.PersistedShortcutEditDraft);
            File.WriteAllText(DraftPath, json);
        }
        catch
        {
            // Best-effort autosave; ignore IO failures.
        }
    }

    private static void ClearLocked()
    {
        _cached = null;

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

    internal static bool DraftEquals(ShortcutFormDraftData left, ShortcutFormDraftData right) =>
        string.Equals(Normalize(left.Name), Normalize(right.Name), StringComparison.Ordinal)
        && string.Equals(Normalize(left.Abbreviation), Normalize(right.Abbreviation), StringComparison.Ordinal)
        && string.Equals(Normalize(left.Directory), Normalize(right.Directory), StringComparison.Ordinal)
        && string.Equals(Normalize(left.Command), Normalize(right.Command), StringComparison.Ordinal)
            && string.Equals(Normalize(left.LaunchTarget), Normalize(right.LaunchTarget), StringComparison.Ordinal)
            && left.RunAsAdmin == right.RunAsAdmin;

    private static string Normalize(string? value) => (value ?? string.Empty).Trim();
}

internal sealed class ShortcutFormDraftData
{
    public string OriginalName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;

    public string Directory { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string LaunchTarget { get; set; } = "default";

    public bool RunAsAdmin { get; set; }

    public static ShortcutFormDraftData FromPersisted(PersistedShortcutEditDraft draft) =>
        new()
        {
            OriginalName = draft.OriginalName,
            Name = draft.Name,
            Abbreviation = draft.Abbreviation,
            Directory = draft.Directory,
            Command = draft.Command,
            LaunchTarget = draft.LaunchTarget,
            RunAsAdmin = draft.RunAsAdmin,
        };
}

internal sealed class PersistedShortcutEditDraft
{
    public string OriginalName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Abbreviation { get; set; } = string.Empty;

    public string Directory { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string LaunchTarget { get; set; } = "default";

    public bool NameCustomized { get; set; }

    public string? AutoFilledName { get; set; }

    public bool RunAsAdmin { get; set; }
}

internal sealed class ShortcutSaveResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public static ShortcutSaveResult Ok(string message) =>
        new() { Success = true, Message = message };

    public static ShortcutSaveResult Fail(string message) =>
        new() { Success = false, Message = message };
}

internal static class ShortcutFormSave
{
    public static ShortcutSaveResult TrySave(
        string? originalName,
        string name,
        string abbreviation,
        string directory,
        string command,
        string launchTarget,
        bool runAsAdmin,
        Action? onSaved)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return ShortcutSaveResult.Fail("Folder path is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = DeriveNameFromDirectory(directory);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return ShortcutSaveResult.Fail("Name is required.");
        }

        name = name.Trim();
        var resolvedName = ShortcutStore.ResolveAvailableName(name, originalName);
        var renamedForConflict = !string.Equals(resolvedName, name, StringComparison.OrdinalIgnoreCase);

        var shortcut = new TerminalShortcut
        {
            Name = resolvedName,
            Abbreviation = string.IsNullOrWhiteSpace(abbreviation) ? null : abbreviation.Trim(),
            Directory = directory.Trim(),
            Command = string.IsNullOrWhiteSpace(command) ? null : command,
            RunAsAdmin = runAsAdmin,
        };

        TerminalCatalog.ApplyLaunchTargetId(shortcut, launchTarget);

        if (!ShortcutValidation.TryValidate(shortcut, out var validationError))
        {
            return ShortcutSaveResult.Fail(validationError);
        }

        try
        {
            ShortcutStore.Upsert(shortcut, originalName);
            onSaved?.Invoke();
            var message = renamedForConflict
                ? $"Saved shortcut as '{resolvedName}' (name was already in use)."
                : $"Saved shortcut '{resolvedName}'.";
            return ShortcutSaveResult.Ok(message);
        }
        catch (Exception ex)
        {
            return ShortcutSaveResult.Fail($"Failed to save shortcut: {ex.Message}");
        }
    }

    private static string DeriveNameFromDirectory(string directory)
    {
        var trimmed = directory.Trim().TrimEnd('\\', '/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        var leaf = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(leaf) ? trimmed : leaf;
    }
}

[JsonSerializable(typeof(PersistedShortcutEditDraft))]
internal sealed partial class ShortcutFormDraftJsonContext : JsonSerializerContext;
