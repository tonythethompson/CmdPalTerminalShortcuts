using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Commands;
using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Pages;

internal sealed partial class WorkspaceEditorPage : DynamicListPage
{
    public const string PageId = "com.quickshell.workspace.editor";

    private readonly Workspace _workspace;
    private readonly string? _originalName;
    private readonly Action _onSaved;
    private IListItem[] _items = [];

    public WorkspaceEditorPage()
    {
        if (!WorkspaceNavigationState.TryTakeEditor(out var workspace, out var originalName, out var onSaved))
        {
            workspace = new Workspace();
            onSaved = () => { };
        }

        _workspace = workspace;
        _originalName = originalName;
        _onSaved = onSaved;

        Id = PageId;
        Icon = new IconInfo(WorkspaceListItems.WorkspaceIcon);
        Title = _originalName is null ? "Create workspace" : $"Edit {_workspace.Name}";
        Name = "Edit";
        RefreshItems();
    }

    public override IListItem[] GetItems() => _items;

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
    }

    private void RefreshItems()
    {
        var folderHint = WorkspaceHealth.BuildListFolderHint(_workspace);
        var items = new List<IListItem>
        {
            new ListItem(new WorkspaceDetailsFormPage(_workspace, RefreshItems, _onSaved, _originalName))
            {
                Title = _workspace.Name,
                Subtitle = folderHint,
                Icon = new IconInfo("\uE70F"),
            },
            new Separator("Launches"),
        };

        var orderedEntries = _workspace.Entries.OrderBy(entry => entry.Order).ToList();
        for (var i = 0; i < orderedEntries.Count; i++)
        {
            var entry = orderedEntries[i];
            var moveUp = i > 0
                ? new MoveWorkspaceEntryCommand(_workspace, entry.Id, -1, ApplyWorkspaceChange)
                : null;
            var moveDown = i < orderedEntries.Count - 1
                ? new MoveWorkspaceEntryCommand(_workspace, entry.Id, 1, ApplyWorkspaceChange)
                : null;

            var moreCommands = new List<CommandContextItem>
            {
                new(new WorkspaceEntryFormPage(_workspace, entry, ApplyWorkspaceChange))
                {
                    Title = "Edit",
                    Icon = new IconInfo("\uE70F"),
                },
            };

            if (moveUp is not null)
            {
                moreCommands.Add(new CommandContextItem(moveUp) { Title = "Move up", Icon = new IconInfo("\uE70E") });
            }

            if (moveDown is not null)
            {
                moreCommands.Add(new CommandContextItem(moveDown) { Title = "Move down", Icon = new IconInfo("\uE70D") });
            }

            moreCommands.Add(new CommandContextItem(new RemoveWorkspaceEntryCommand(_workspace, entry.Id, ApplyWorkspaceChange))
            {
                Title = "Remove",
                Icon = new IconInfo("\uE74D"),
                IsCritical = true,
            });

            items.Add(new ListItem(new WorkspaceEntryFormPage(_workspace, entry, ApplyWorkspaceChange))
            {
                Title = entry.Label,
                Subtitle = WorkspaceDisplay.BuildEntrySubtitle(entry),
                Icon = new IconInfo(entry.IsEnabled ? "\uE756" : "\uE7BA"),
                MoreCommands = moreCommands.ToArray(),
            });
        }

        items.Add(new ListItem(new AddWorkspaceEntryCommand(_workspace, ApplyWorkspaceChange))
        {
            Title = "Add launch",
            Subtitle = "Open a new terminal entry in this workspace",
            Icon = new IconInfo("\uE710"),
        });

        items.Add(new ListItem(new SaveWorkspaceCommand(_workspace, _originalName, _onSaved))
        {
            Title = "Save workspace",
            Subtitle = "Apply changes and return to home",
            Icon = new IconInfo("\uE74E"),
        });

        _items = items.ToArray();
        RaiseItemsChanged();
    }

    private void ApplyWorkspaceChange(Workspace workspace) => RefreshItems();
}

internal sealed partial class AddWorkspaceEntryCommand : InvokableCommand
{
    private readonly Workspace _workspace;
    private readonly Action<Workspace> _onChanged;

    public AddWorkspaceEntryCommand(Workspace workspace, Action<Workspace> onChanged)
    {
        _workspace = workspace;
        _onChanged = onChanged;
        Name = "Add";
        Icon = new IconInfo("\uE710");
    }

    public override CommandResult Invoke()
    {
        var nextOrder = _workspace.Entries.Count == 0 ? 0 : _workspace.Entries.Max(entry => entry.Order) + 1;
        var entry = WorkspaceEditorState.CreateEntry("New launch", null, nextOrder);
        WorkspaceNavigationState.SetEntryForm(_workspace, entry, _onChanged, isNew: true);
        return CommandResult.GoToPage(new GoToPageArgs
        {
            PageId = WorkspaceEntryFormPage.PageId,
        });
    }
}
