using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Models;
using QuickShell.Services;
using System.ComponentModel;

namespace QuickShell.Commands;

internal sealed partial class OpenWorkspaceCommand : InvokableCommand
{
    private readonly string _workspaceId;
    private readonly QuickShellSettingsManager _settings;

    public OpenWorkspaceCommand(Workspace workspace, QuickShellSettingsManager settings)
    {
        _workspaceId = workspace.Id;
        _settings = settings;
        Id = WorkspaceCommandIds.Open(workspace.Id);
        Name = "Open";
        Icon = new IconInfo("\uE8A7");
    }

    public override CommandResult Invoke()
    {
        var workspace = QuickShellRuntimeServices.Workspaces.GetById(_workspaceId);
        if (workspace is null)
        {
            return QuickShellNavigation.StayOpen("That workspace was not found.");
        }

        if (WorkspaceHealth.NeedsFolder(workspace))
        {
            return QuickShellNavigation.StayOpen(
                "Workspace needs a project folder. Edit the workspace to choose one.");
        }

        if (!WorkspacePath.DirectoryExists(workspace.Directory))
        {
            return QuickShellNavigation.StayOpen(
                $"Workspace could not launch: folder not found at {workspace.Directory}.");
        }

        var enabledEntries = workspace.Entries
            .Where(entry => entry.IsEnabled)
            .OrderBy(entry => entry.Order)
            .ToList();

        if (enabledEntries.Count == 0)
        {
            return QuickShellNavigation.StayOpen("Workspace has no enabled launch entries.");
        }

        var opened = 0;
        string? lastFailureLabel = null;

        foreach (var entry in enabledEntries)
        {
            try
            {
                var launchShortcut = WorkspaceDisplay.ToLaunchShortcut(entry, workspace.Directory);
                TerminalLauncher.Open(
                    launchShortcut,
                    _settings.TerminalApplicationId,
                    _settings.DefaultProfileId,
                    entry.RunAsAdmin);
                opened++;
            }
            catch (DirectoryNotFoundException)
            {
                lastFailureLabel = entry.Label;
            }
            catch (InvalidOperationException)
            {
                lastFailureLabel = entry.Label;
            }
            catch (Win32Exception)
            {
                lastFailureLabel = entry.Label;
            }
        }

        if (opened == 0)
        {
            return QuickShellNavigation.StayOpen(
                lastFailureLabel is null
                    ? "Workspace could not launch any terminals."
                    : $"{lastFailureLabel} could not be launched.");
        }

        if (opened == enabledEntries.Count)
        {
            return CommandResult.Dismiss();
        }

        return QuickShellNavigation.StayOpen(
            $"Workspace partially launched: {opened} of {enabledEntries.Count} terminals opened.");
    }
}
