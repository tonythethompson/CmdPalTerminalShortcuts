using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Pages;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class CreateWorkspaceCommand : InvokableCommand
{
    private readonly Action _onSaved;

    public CreateWorkspaceCommand(Action onSaved)
    {
        _onSaved = onSaved;
        Id = WorkspaceCommandIds.CreateWorkspace;
        Name = "Create workspace";
        Icon = new IconInfo("\uE8A7");
    }

    public override CommandResult Invoke()
    {
        WorkspaceNavigationState.SetPicker(_onSaved, forCreate: true);
        return CommandResult.GoToPage(new GoToPageArgs
        {
            PageId = ProjectShortcutPickerPage.PageId,
        });
    }
}

internal sealed partial class EditWorkspaceCommand : InvokableCommand
{
    private readonly string _workspaceName;
    private readonly Action _onChanged;

    public EditWorkspaceCommand(string workspaceName, Action onChanged)
    {
        _workspaceName = workspaceName;
        _onChanged = onChanged;
        Name = "Edit workspace";
        Icon = new IconInfo("\uE70F");
    }

    public override CommandResult Invoke()
    {
        var workspace = QuickShellRuntimeServices.Workspaces.GetByName(_workspaceName);
        if (workspace is null)
        {
            return QuickShellNavigation.StayOpen("That workspace was not found.");
        }

        WorkspaceNavigationState.SetEditor(
            WorkspaceEditorState.CloneWorkspace(workspace),
            workspace.Name,
            _onChanged);
        return CommandResult.GoToPage(new GoToPageArgs
        {
            PageId = WorkspaceEditorPage.PageId,
        });
    }
}

internal sealed partial class CreateWorkspaceFromShortcutCommand : InvokableCommand
{
    private readonly string _shortcutId;
    private readonly Action _onSaved;

    public CreateWorkspaceFromShortcutCommand(string shortcutId, Action onSaved)
    {
        _shortcutId = shortcutId;
        _onSaved = onSaved;
        Name = "Create workspace from this shortcut";
        Icon = new IconInfo("\uE8A7");
    }

    public override CommandResult Invoke()
    {
        var shortcut = QuickShellRuntimeServices.Shortcuts.GetById(_shortcutId);
        if (shortcut is null)
        {
            return QuickShellNavigation.StayOpen("That shortcut was not found.");
        }

        var workspace = WorkspaceEditorState.CreateNew(shortcut);
        WorkspaceNavigationState.SetEditor(workspace, originalName: null, _onSaved);
        return CommandResult.GoToPage(new GoToPageArgs
        {
            PageId = WorkspaceEditorPage.PageId,
        });
    }
}
