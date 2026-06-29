using QuickShell.Models;

namespace QuickShell.Services;

internal static class WorkspaceValidation
{
    public const int MaxWorkspaceCount = 200;
    public const int MaxEntryCount = 50;
    public const int MaxLabelLength = 120;

    public static WorkspaceLoadResult NormalizeForLoad(
        WorkspaceDiskRecord record,
        IShortcutRepository shortcuts,
        HashSet<string> seenWorkspaceIds)
    {
        if (string.IsNullOrWhiteSpace(record.Name))
        {
            return new WorkspaceLoadResult(null, false, false, "Skipped workspace with missing name.");
        }

        if (record.Name.Length > ShortcutValidation.MaxNameLength)
        {
            return new WorkspaceLoadResult(null, false, false, $"Skipped workspace '{record.Name}': name too long.");
        }

        if (record.Entries is null)
        {
            return new WorkspaceLoadResult(null, false, false, $"Skipped workspace '{record.Name}': entries missing.");
        }

        if (!TryNormalizeEntriesStructure(record.Entries, out var normalizedEntries, out var entryError))
        {
            return new WorkspaceLoadResult(null, false, false, $"Skipped workspace '{record.Name}': {entryError}");
        }

        var requiresPersistence = false;
        var workspaceId = record.Id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(workspaceId) || !ShortcutCommandIds.IsStableShortcutId(workspaceId))
        {
            workspaceId = Guid.NewGuid().ToString("N");
            requiresPersistence = true;
        }
        else if (!seenWorkspaceIds.Add(workspaceId))
        {
            return new WorkspaceLoadResult(null, false, false, $"Skipped duplicate workspace ID '{workspaceId}'.");
        }

        var hadLegacyId = !string.IsNullOrWhiteSpace(record.ProjectShortcutId);
        var rawDirectory = record.Directory?.Trim() ?? string.Empty;
        var resolvedDirectory = string.Empty;
        var needsFolderRepair = false;
        string? directoryWarning = null;

        if (!string.IsNullOrWhiteSpace(rawDirectory))
        {
            var originalDirectory = rawDirectory;
            if (WorkspacePath.TryNormalizeLexical(rawDirectory, out var normalizedDirectory, out _))
            {
                resolvedDirectory = normalizedDirectory;
                if (!string.Equals(originalDirectory, normalizedDirectory, StringComparison.Ordinal))
                {
                    requiresPersistence = true;
                }
            }
            else
            {
                resolvedDirectory = rawDirectory;
                needsFolderRepair = true;
                directoryWarning = "Workspace directory could not be normalized.";
            }

            if (hadLegacyId)
            {
                requiresPersistence = true;
            }
        }
        else if (hadLegacyId)
        {
            requiresPersistence = true;
            var shortcut = shortcuts.GetById(record.ProjectShortcutId!);
            if (shortcut is not null && WorkspacePath.TryNormalizeLexical(shortcut.Directory, out var fromShortcut, out _))
            {
                resolvedDirectory = fromShortcut;
            }
            else
            {
                needsFolderRepair = true;
                directoryWarning = shortcut is null
                    ? "Legacy project shortcut was not found."
                    : "Legacy project shortcut directory could not be normalized.";
            }
        }
        else
        {
            needsFolderRepair = true;
        }

        var workspace = new Workspace
        {
            Id = workspaceId,
            Name = record.Name.Trim(),
            Abbreviation = string.IsNullOrWhiteSpace(record.Abbreviation) ? null : record.Abbreviation.Trim(),
            Directory = resolvedDirectory,
            IsPinned = record.IsPinned,
            PinOrder = record.PinOrder,
            Entries = normalizedEntries,
        };

        WorkspaceMapper.NormalizeEntryOrders(workspace);

        return new WorkspaceLoadResult(workspace, requiresPersistence, needsFolderRepair, directoryWarning);
    }

    public static bool TryValidateForSave(
        Workspace workspace,
        IShortcutRepository shortcuts,
        IWorkspaceRepository workspaces,
        string? originalName,
        out string error)
    {
        if (!TryValidateStructural(workspace, out error))
        {
            return false;
        }

        if (!TryValidateUniqueName(workspace.Name, originalName, workspaces, out error))
        {
            return false;
        }

        if (!TryValidateAbbreviationForSave(workspace, shortcuts, workspaces, out error))
        {
            return false;
        }

        if (!WorkspacePath.TryNormalizeLexical(workspace.Directory, out var normalizedDirectory, out error))
        {
            return false;
        }

        workspace.Directory = normalizedDirectory;

        if (!WorkspacePath.DirectoryExists(workspace.Directory))
        {
            error = $"Directory not found: {workspace.Directory}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateForImport(
        Workspace workspace,
        IShortcutRepository shortcuts,
        IWorkspaceRepository workspaces,
        out string error)
    {
        if (!TryValidateStructural(workspace, out error))
        {
            return false;
        }

        if (!TryValidateAbbreviationForSave(workspace, shortcuts, workspaces, out error))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(workspace.Directory)
            && !WorkspacePath.TryNormalizeLexical(workspace.Directory, out var normalizedDirectory, out error))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(workspace.Directory))
        {
            workspace.Directory = WorkspacePath.TryNormalizeLexical(workspace.Directory, out var normalized, out _)
                ? normalized
                : workspace.Directory.Trim();
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateEntry(WorkspaceEntry entry, out string error)
    {
        if (string.IsNullOrWhiteSpace(entry.Label))
        {
            error = "Launch label is required.";
            return false;
        }

        if (entry.Label.Length > MaxLabelLength)
        {
            error = $"Launch label must be {MaxLabelLength} characters or fewer.";
            return false;
        }

        if (!ShortcutValidation.TryValidateCommand(entry.Command, out error))
        {
            return false;
        }

        if (!ShortcutValidation.TryValidateWtProfile(entry.WtProfile, out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateUniqueName(
        string name,
        string? originalName,
        IWorkspaceRepository workspaces,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Workspace name is required.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(originalName)
            && name.Equals(originalName, StringComparison.OrdinalIgnoreCase))
        {
            error = string.Empty;
            return true;
        }

        if (workspaces.GetByName(name) is not null)
        {
            error = $"A workspace named '{name}' already exists.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateUniqueAbbreviation(
        string abbreviation,
        string workspaceName,
        IShortcutRepository shortcuts,
        IWorkspaceRepository workspaces,
        out string error)
    {
        if (string.IsNullOrWhiteSpace(abbreviation))
        {
            error = string.Empty;
            return true;
        }

        var shortcutConflict = shortcuts.GetShortcuts()
            .FirstOrDefault(shortcut => !string.IsNullOrWhiteSpace(shortcut.Abbreviation)
                && shortcut.Abbreviation.Equals(abbreviation, StringComparison.OrdinalIgnoreCase));
        if (shortcutConflict is not null)
        {
            error = $"Home keyword '{abbreviation}' is already used by shortcut '{shortcutConflict.Name}'.";
            return false;
        }

        var workspaceConflict = workspaces.GetWorkspaces()
            .FirstOrDefault(workspace => !workspace.Name.Equals(workspaceName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(workspace.Abbreviation)
                && workspace.Abbreviation.Equals(abbreviation, StringComparison.OrdinalIgnoreCase));
        if (workspaceConflict is not null)
        {
            error = $"Home keyword '{abbreviation}' is already used by workspace '{workspaceConflict.Name}'.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateStructural(Workspace workspace, out string error)
    {
        if (string.IsNullOrWhiteSpace(workspace.Name))
        {
            error = "Workspace name is required.";
            return false;
        }

        if (workspace.Name.Length > ShortcutValidation.MaxNameLength)
        {
            error = $"Workspace name must be {ShortcutValidation.MaxNameLength} characters or fewer.";
            return false;
        }

        var enabledEntries = workspace.Entries.Where(entry => entry.IsEnabled).ToList();
        if (enabledEntries.Count == 0)
        {
            error = "At least one enabled launch entry is required.";
            return false;
        }

        if (workspace.Entries.Count > MaxEntryCount)
        {
            error = $"At most {MaxEntryCount} launch entries are supported.";
            return false;
        }

        if (!TryNormalizeEntriesStructure(workspace.Entries, out var normalizedEntries, out error))
        {
            return false;
        }

        workspace.Entries = normalizedEntries;
        WorkspaceMapper.NormalizeEntryOrders(workspace);

        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in workspace.Entries)
        {
            if (!TryValidateEntry(entry, out error))
            {
                return false;
            }

            if (!labels.Add(entry.Label))
            {
                error = $"Duplicate launch label '{entry.Label}'.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateAbbreviationForSave(
        Workspace workspace,
        IShortcutRepository shortcuts,
        IWorkspaceRepository workspaces,
        out string error)
    {
        if (!string.IsNullOrWhiteSpace(workspace.Abbreviation)
            && workspace.Abbreviation.Length > ShortcutValidation.MaxAbbreviationLength)
        {
            error = $"Home keyword must be {ShortcutValidation.MaxAbbreviationLength} characters or fewer.";
            return false;
        }

        return TryValidateUniqueAbbreviation(
            workspace.Abbreviation ?? string.Empty,
            workspace.Name,
            shortcuts,
            workspaces,
            out error);
    }

    private static bool TryNormalizeEntriesStructure(
        List<WorkspaceEntry> entries,
        out List<WorkspaceEntry> normalizedEntries,
        out string error)
    {
        normalizedEntries = [];
        error = string.Empty;

        if (entries.Count == 0)
        {
            error = "At least one launch entry is required.";
            return false;
        }

        var seenEntryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                error = "Encountered null launch entry.";
                return false;
            }

            var entryId = entry.Id?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entryId) || !ShortcutCommandIds.IsStableShortcutId(entryId))
            {
                entryId = Guid.NewGuid().ToString("N");
            }

            if (!seenEntryIds.Add(entryId))
            {
                error = $"Duplicate launch entry ID '{entryId}'.";
                return false;
            }

            normalizedEntries.Add(new WorkspaceEntry
            {
                Id = entryId,
                Label = entry.Label ?? string.Empty,
                Terminal = string.IsNullOrWhiteSpace(entry.Terminal) ? "default" : entry.Terminal,
                WtProfile = entry.WtProfile,
                Command = entry.Command,
                RunAsAdmin = entry.RunAsAdmin,
                IsEnabled = entry.IsEnabled,
                Order = entry.Order,
            });
        }

        return true;
    }
}
