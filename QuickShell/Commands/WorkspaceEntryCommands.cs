using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class MoveWorkspaceEntryCommand : InvokableCommand
{
    private readonly Workspace _workspace;
    private readonly string _entryId;
    private readonly int _direction;
    private readonly Action<Workspace> _onChanged;

    public MoveWorkspaceEntryCommand(
        Workspace workspace,
        string entryId,
        int direction,
        Action<Workspace> onChanged)
    {
        _workspace = workspace;
        _entryId = entryId;
        _direction = direction;
        _onChanged = onChanged;
        Name = direction < 0 ? "Move up" : "Move down";
        Icon = new IconInfo(direction < 0 ? "\uE70E" : "\uE70D");
    }

    public override CommandResult Invoke()
    {
        WorkspaceEditorState.MoveEntry(_workspace, _entryId, _direction);
        _onChanged(_workspace);
        return QuickShellNavigation.StayOpen();
    }
}

internal sealed partial class RemoveWorkspaceEntryCommand : InvokableCommand
{
    private readonly Workspace _workspace;
    private readonly string _entryId;
    private readonly Action<Workspace> _onChanged;

    public RemoveWorkspaceEntryCommand(
        Workspace workspace,
        string entryId,
        Action<Workspace> onChanged)
    {
        _workspace = workspace;
        _entryId = entryId;
        _onChanged = onChanged;
        Name = "Remove";
        Icon = new IconInfo("\uE74D");
    }

    public override CommandResult Invoke()
    {
        _workspace.Entries.RemoveAll(entry => entry.Id.Equals(_entryId, StringComparison.OrdinalIgnoreCase));
        for (var i = 0; i < _workspace.Entries.Count; i++)
        {
            _workspace.Entries[i].Order = i;
        }

        _onChanged(_workspace);
        return QuickShellNavigation.StayOpen();
    }
}

internal sealed partial class SaveWorkspaceCommand : InvokableCommand
{
    private readonly Workspace _workspace;
    private readonly string? _originalName;
    private readonly Action _onSaved;

    public SaveWorkspaceCommand(Workspace workspace, string? originalName, Action onSaved)
    {
        _workspace = workspace;
        _originalName = originalName;
        _onSaved = onSaved;
        Name = "Save workspace";
        Icon = new IconInfo("\uE74E");
    }

    public override CommandResult Invoke()
    {
        try
        {
            QuickShellRuntimeServices.Workspaces.Upsert(_workspace, _originalName);
            _onSaved();
            return QuickShellNavigation.ReturnHome($"Saved workspace '{_workspace.Name}'.");
        }
        catch (InvalidOperationException ex)
        {
            return QuickShellNavigation.StayOpen(ex.Message);
        }
    }
}
