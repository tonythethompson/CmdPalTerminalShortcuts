namespace QuickShell.Services;

internal static class QuickShellRuntimeServices
{
    public static ShortcutRepository Shortcuts { get; } = new();

    public static ShortcutDraftStore Drafts { get; } = new(Shortcuts);

    public static void Dispose()
    {
        Drafts.Dispose();
        Shortcuts.Dispose();
    }
}
