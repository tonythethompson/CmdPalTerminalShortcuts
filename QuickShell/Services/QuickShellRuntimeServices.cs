namespace QuickShell.Services;

internal static class QuickShellRuntimeServices
{
    public static IShortcutRepository Shortcuts { get; } = new ShortcutRepository();

    public static IDraftStore Drafts { get; } = new ShortcutDraftStore(Shortcuts);
}
