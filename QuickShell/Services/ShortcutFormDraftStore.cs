using QuickShell.Models;
using System.Text.Json.Serialization;

namespace QuickShell.Services;

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
        var resolvedName = QuickShellRuntimeServices.Shortcuts.ResolveAvailableName(name, originalName);
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
            QuickShellRuntimeServices.Shortcuts.Upsert(shortcut, originalName);
            onSaved?.Invoke();
            var message = renamedForConflict
                ? $"Saved shortcut as '{resolvedName}' (name was already in use)."
                : $"Saved shortcut '{resolvedName}'.";
            return ShortcutSaveResult.Ok(message);
        }
        catch (IOException)
        {
            return ShortcutSaveResult.Fail("Failed to save shortcut: unable to write shortcut data.");
        }
        catch (UnauthorizedAccessException)
        {
            return ShortcutSaveResult.Fail("Failed to save shortcut: access to shortcut storage was denied.");
        }
        catch (InvalidOperationException)
        {
            return ShortcutSaveResult.Fail("Failed to save shortcut: shortcut data is invalid.");
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
