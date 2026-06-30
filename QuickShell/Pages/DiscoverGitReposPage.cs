using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Commands;
using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Pages;

internal sealed partial class DiscoverGitReposPage : DynamicListPage
{
    public const string PageId = "com.quickshell.discover-git-repos";

    private readonly Action _onReload;
    private IListItem[] _items = [];
    private string _query = string.Empty;

    public DiscoverGitReposPage(Action onReload)
    {
        _onReload = onReload;
        Id = PageId;
        Icon = new IconInfo("\uE8A5");
        Title = "Discover git repos";
        Name = "Discover";
        PlaceholderText = "Filter discovered repositories...";
        RefreshItems(string.Empty);
    }

    public override IListItem[] GetItems() => _items;

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        var normalized = newSearch ?? string.Empty;
        if (string.Equals(_query, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _query = normalized;
        RefreshItems(normalized);
    }

    private void RefreshItems(string query)
    {
        var extraRoots = GitRepoSearchRoots.FromShortcuts(QuickShellRuntimeServices.Shortcuts.GetShortcuts());
        var discovered = GitRepoIndex.GetAll(extraRoots).ToList();
        var existingDirectories = QuickShellRuntimeServices.Shortcuts.GetShortcuts()
            .Select(shortcut => shortcut.Directory)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(query))
        {
            discovered = discovered
                .Where(candidate =>
                    candidate.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || candidate.Directory.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (candidate.RemoteUrl?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        var items = new List<IListItem>();
        foreach (var candidate in discovered)
        {
            var alreadySaved = existingDirectories.Contains(candidate.Directory);
            var subtitleParts = new List<string> { ShortcutDisplay.ShortenPathForDisplay(candidate.Directory) };
            if (!string.IsNullOrWhiteSpace(candidate.RemoteUrl))
            {
                subtitleParts.Add(candidate.RemoteUrl);
            }

            if (alreadySaved)
            {
                subtitleParts.Add("already saved");
            }

            items.Add(new ListItem(new AddGitRepoWorkspaceCommand(candidate, _onReload))
            {
                Title = candidate.Name,
                Subtitle = string.Join(" · ", subtitleParts),
                Icon = new IconInfo(alreadySaved ? "\uE73E" : "\uE8A5"),
            });
        }

        if (items.Count == 0)
        {
            items.Add(new ListItem(new NoOpCommand())
            {
                Title = string.IsNullOrWhiteSpace(query) ? "No git repositories found" : "No matching repositories",
                Subtitle = "Try searching Projects, dev, or code under your profile folder",
                Icon = new IconInfo("\uE946"),
            });
        }

        _items = items.ToArray();
        RaiseItemsChanged();
    }
}
