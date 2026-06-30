using QuickShell.Models;

namespace QuickShell.Services;

internal static class ShortcutRecents
{
    public const int MaxCount = 8;
    public const string SectionTitle = "Recent";

    public static List<TerminalShortcut> GetRecentWorkspaces(IReadOnlyList<TerminalShortcut> shortcuts) =>
        shortcuts
            .Where(shortcut => shortcut.LastUsedUtc is not null && !shortcut.IsPinned)
            .OrderByDescending(shortcut => shortcut.LastUsedUtc)
            .Take(MaxCount)
            .ToList();
}
