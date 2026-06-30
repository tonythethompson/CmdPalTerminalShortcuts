using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Commands;
using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Pages;

internal sealed partial class ShortcutEditorPage : DynamicListPage
{
    public const string PageId = "com.quickshell.shortcut.editor";

    private readonly TerminalShortcut _shortcut;
    private readonly string? _originalName;
    private readonly Action _onSaved;
    private IListItem[] _items = [];

    public ShortcutEditorPage()
    {
        if (!ShortcutEditorNavigationState.TryTakeEditor(out var shortcut, out var originalName, out var onSaved))
        {
            shortcut = ShortcutEditorState.CreateNew();
            onSaved = () => { };
            originalName = null;
        }

        _shortcut = shortcut;
        _originalName = originalName;
        _onSaved = onSaved;

        Id = PageId;
        Icon = QuickShellBrandIcons.App;
        Title = _originalName is null ? "Create workspace" : $"Edit {_shortcut.Name}";
        Name = _originalName is null ? "Create" : "Edit";
        RefreshItems();
    }

    public ShortcutEditorPage(TerminalShortcut shortcut, string? originalName, Action onSaved)
    {
        _shortcut = ShortcutEditorState.CloneShortcut(shortcut);
        _originalName = originalName;
        _onSaved = onSaved;

        Id = PageId;
        Icon = QuickShellBrandIcons.App;
        Title = _originalName is null ? "Create workspace" : $"Edit {_shortcut.Name}";
        Name = _originalName is null ? "Create" : "Edit";
        RefreshItems();
    }

    public static ShortcutEditorPage ForCreate(Action onSaved)
    {
        var page = new ShortcutEditorPage(ShortcutEditorState.CreateNew(), originalName: null, onSaved);
        page.Id = ShortcutCommandIds.CreateShortcut;
        page.Title = "Create workspace";
        page.Name = "Create";
        return page;
    }

    public override IListItem[] GetItems() => _items;

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
    }

    private void RefreshItems()
    {
        ShortcutLaunchNormalization.EnsureLaunchesFromLegacy(_shortcut);
        var folderHint = string.IsNullOrWhiteSpace(_shortcut.Directory)
            ? "Choose a folder"
            : ShortcutDisplay.ShortenPathForDisplay(_shortcut.Directory);

        var items = new List<IListItem>
        {
            new ListItem(new ShortcutDetailsFormPage(_shortcut, ApplyShortcutChange))
            {
                Title = string.IsNullOrWhiteSpace(_shortcut.Name) ? "Workspace details" : _shortcut.Name,
                Subtitle = folderHint,
                Icon = new IconInfo("\uE70F"),
            },
            new Separator("Terminals"),
        };

        var orderedLaunches = _shortcut.Launches.OrderBy(entry => entry.Order).ToList();
        for (var i = 0; i < orderedLaunches.Count; i++)
        {
            var launch = orderedLaunches[i];
            var moveUp = i > 0
                ? new MoveShortcutLaunchCommand(_shortcut, launch.Id, -1, ApplyShortcutChange)
                : null;
            var moveDown = i < orderedLaunches.Count - 1
                ? new MoveShortcutLaunchCommand(_shortcut, launch.Id, 1, ApplyShortcutChange)
                : null;

            var moreCommands = new List<CommandContextItem>
            {
                new(new ShortcutLaunchFormPage(_shortcut, launch, ApplyShortcutChange))
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

            if (orderedLaunches.Count > 1)
            {
                moreCommands.Add(new CommandContextItem(new RemoveShortcutLaunchCommand(_shortcut, launch.Id, ApplyShortcutChange))
                {
                    Title = "Remove",
                    Icon = new IconInfo("\uE74D"),
                    IsCritical = true,
                });
            }

            items.Add(new ListItem(new ShortcutLaunchFormPage(_shortcut, launch, ApplyShortcutChange))
            {
                Title = launch.Label,
                Subtitle = WorkspaceDisplay.BuildEntrySubtitle(launch),
                Icon = new IconInfo(launch.IsEnabled ? TerminalLaunchGlyphs.GetForLaunch(launch) : "\uE7BA"),
                MoreCommands = moreCommands.ToArray(),
            });
        }

        items.Add(new ListItem(new AddShortcutLaunchCommand(_shortcut, ApplyShortcutChange))
        {
            Title = "+ Add terminal",
            Subtitle = "Open another terminal when this workspace runs",
            Icon = new IconInfo("\uE710"),
        });

        items.Add(new ListItem(new SaveShortcutEditorCommand(_shortcut, _originalName, _onSaved))
        {
            Title = "Save workspace",
            Subtitle = "Apply changes and return to home",
            Icon = new IconInfo("\uE74E"),
        });

        _items = items.ToArray();
        RaiseItemsChanged();
    }

    private void ApplyShortcutChange(TerminalShortcut shortcut) => RefreshItems();
}

internal sealed partial class AddShortcutLaunchCommand : InvokableCommand
{
    private readonly TerminalShortcut _shortcut;
    private readonly Action<TerminalShortcut> _onChanged;

    public AddShortcutLaunchCommand(TerminalShortcut shortcut, Action<TerminalShortcut> onChanged)
    {
        _shortcut = shortcut;
        _onChanged = onChanged;
        Name = "Add";
        Icon = new IconInfo("\uE710");
    }

    public override CommandResult Invoke()
    {
        ShortcutLaunchNormalization.EnsureLaunchesFromLegacy(_shortcut);
        var nextNumber = _shortcut.Launches.Count + 1;
        var nextOrder = _shortcut.Launches.Count == 0
            ? 0
            : _shortcut.Launches.Max(entry => entry.Order) + 1;
        var launch = ShortcutEditorState.CreateLaunch($"Terminal {nextNumber}", null, nextOrder);
        ShortcutEditorNavigationState.SetLaunchForm(_shortcut, launch, _onChanged, isNew: true);
        return CommandResult.GoToPage(new GoToPageArgs
        {
            PageId = ShortcutLaunchFormPage.PageId,
        });
    }
}
