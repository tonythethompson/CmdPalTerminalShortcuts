using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Commands;
using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Pages;

internal sealed partial class ProjectShortcutPickerPage : DynamicListPage, IDisposable
{
    public const string PageId = "com.quickshell.workspace.project-picker";

    private readonly SearchDebouncer _searchDebouncer;
    private IListItem[] _items = [];
    private string _query = string.Empty;
    private Action _onSaved = () => { };

    public ProjectShortcutPickerPage()
    {
        _searchDebouncer = new SearchDebouncer(ApplyQueryDebounced);
        Id = PageId;
        Icon = new IconInfo(WorkspaceListItems.WorkspaceIcon);
        Title = "Choose project shortcut";
        Name = "Select";
        PlaceholderText = "Search saved shortcuts...";
        RefreshItems(string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) =>
        _searchDebouncer.Schedule(newSearch ?? string.Empty);

    public override IListItem[] GetItems() => _items;

    internal void Prepare(Action onSaved)
    {
        _onSaved = onSaved;
        RefreshItems(_query);
    }

    private void ApplyQueryDebounced(string normalized)
    {
        _query = normalized;
        RefreshItems(normalized);
    }

    private void RefreshItems(string query)
    {
        var shortcuts = string.IsNullOrWhiteSpace(query)
            ? QuickShellRuntimeServices.Shortcuts.GetShortcuts()
            : QuickShellRuntimeServices.Shortcuts.Search(query).ToArray();

        _items = shortcuts
            .OrderBy(shortcut => shortcut.Name, StringComparer.OrdinalIgnoreCase)
            .Select(shortcut => new ListItem(new SelectProjectShortcutCommand(shortcut.Id, _onSaved))
            {
                Title = shortcut.Name,
                Subtitle = ShortcutDisplay.BuildDirectorySubtitle(shortcut),
                Icon = new IconInfo(ShortcutHealth.GetListGlyph(shortcut)),
            })
            .Cast<IListItem>()
            .ToArray();

        RaiseItemsChanged();
    }

    public void Dispose() => _searchDebouncer.Dispose();
}

internal sealed partial class SelectProjectShortcutCommand : InvokableCommand
{
    private readonly string _shortcutId;

    public SelectProjectShortcutCommand(string shortcutId, Action onSaved)
    {
        _shortcutId = shortcutId;
        Name = "Select";
    }

    public override CommandResult Invoke()
    {
        var shortcut = QuickShellRuntimeServices.Shortcuts.GetById(_shortcutId);
        if (shortcut is null)
        {
            return QuickShellNavigation.StayOpen("That shortcut was not found.");
        }

        WorkspaceNavigationState.PeekPicker(out var onSaved, out _, out var changeDirectory);
        if (changeDirectory)
        {
            WorkspaceNavigationState.PeekEditor(out var workspace, out var originalName, out _);
            workspace.Directory = WorkspacePath.TryNormalizeLexical(shortcut.Directory, out var normalized, out _)
                ? normalized
                : shortcut.Directory.Trim();
            WorkspaceNavigationState.SetEditor(workspace, originalName, onSaved);
            return CommandResult.GoToPage(new GoToPageArgs
            {
                PageId = WorkspaceEditorPage.PageId,
            });
        }

        var newWorkspace = WorkspaceEditorState.CreateNew(shortcut);
        WorkspaceNavigationState.SetEditor(newWorkspace, originalName: null, onSaved);
        return CommandResult.GoToPage(new GoToPageArgs
        {
            PageId = WorkspaceEditorPage.PageId,
        });
    }
}
