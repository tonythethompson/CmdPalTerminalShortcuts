namespace QuickShell.Services;

internal interface IDraftStore
{
    string DraftPath { get; }

    bool HasPending { get; }

    PersistedShortcutEditDraft? Pending { get; }

    bool TryGetForRestore(string originalName, out PersistedShortcutEditDraft draft);

    void SaveIfDirty(
        string editKey,
        ShortcutFormDraftData draft,
        ShortcutFormDraftData baseline,
        bool nameCustomized,
        string? autoFilledName);

    void Clear();

    ShortcutSaveResult TryCommitPending(Action? onSaved);
}
