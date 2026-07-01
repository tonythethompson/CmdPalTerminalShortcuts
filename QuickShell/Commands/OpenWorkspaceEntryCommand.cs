using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Models;
using QuickShell.Services;
using System.ComponentModel;

namespace QuickShell.Commands;

internal sealed partial class OpenWorkspaceEntryCommand : InvokableCommand
{
    private readonly string _workspaceId;
    private readonly string _entryId;
    private readonly QuickShellSettingsManager _settings;

    public OpenWorkspaceEntryCommand(
        Workspace workspace,
        WorkspaceEntry entry,
        QuickShellSettingsManager settings)
    {
        _workspaceId = workspace.Id;
        _entryId = entry.Id;
        _settings = settings;
        Id = WorkspaceCommandIds.OpenEntry(workspace.Id, entry.Id);
        Name = ShortcutDisplay.GetLaunchContextMenuTitle(entry);
        Icon = new IconInfo(TerminalLaunchGlyphs.GetForLaunch(entry));
    }

    public override CommandResult Invoke()
    {
        var workspace = QuickShellRuntimeServices.Workspaces.GetById(_workspaceId);
        if (workspace is null)
        {
            return QuickShellNavigation.StayOpen("That workspace was not found.");
        }

        var entry = workspace.Entries.FirstOrDefault(e => e.Id.Equals(_entryId, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return QuickShellNavigation.StayOpen("That launch entry was not found.");
        }

        if (WorkspaceHealth.NeedsFolder(workspace))
        {
            return QuickShellNavigation.StayOpen("Workspace needs a project folder. Edit the workspace to choose one.");
        }

        if (!WorkspacePath.DirectoryExists(workspace.Directory))
        {
            return QuickShellNavigation.StayOpen($"Folder not found: {workspace.Directory}");
        }

        try
        {
            var launchShortcut = WorkspaceDisplay.ToLaunchShortcut(entry, workspace.Directory);
            TerminalLauncher.Open(
                launchShortcut,
                _settings.TerminalApplicationId,
                _settings.DefaultProfileId,
                entry.RunAsAdmin);
            return CommandResult.Dismiss();
        }
        catch (DirectoryNotFoundException)
        {
            return QuickShellNavigation.StayOpen($"{entry.Label} could not be launched: folder not found.");
        }
        catch (InvalidOperationException)
        {
            return QuickShellNavigation.StayOpen($"{entry.Label} could not be launched.");
        }
        catch (Win32Exception)
        {
            return QuickShellNavigation.StayOpen($"{entry.Label} could not be launched.");
        }
    }
}
