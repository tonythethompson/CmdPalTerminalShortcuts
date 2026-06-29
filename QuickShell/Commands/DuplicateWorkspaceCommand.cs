using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class DuplicateWorkspaceCommand : InvokableCommand
{
    private readonly string _sourceName;
    private readonly Action _onChanged;

    public DuplicateWorkspaceCommand(string sourceName, Action onChanged)
    {
        _sourceName = sourceName;
        _onChanged = onChanged;
        Name = "Duplicate";
        Icon = new IconInfo("\uE8C8");
    }

    public override CommandResult Invoke()
    {
        var duplicate = QuickShellRuntimeServices.Workspaces.BuildDuplicate(_sourceName);
        if (duplicate is null)
        {
            return QuickShellNavigation.StayOpen($"Workspace '{_sourceName}' was not found.");
        }

        QuickShellRuntimeServices.Workspaces.Upsert(duplicate);
        _onChanged();
        return QuickShellNavigation.StayOpen($"Duplicated as '{duplicate.Name}'.");
    }
}
