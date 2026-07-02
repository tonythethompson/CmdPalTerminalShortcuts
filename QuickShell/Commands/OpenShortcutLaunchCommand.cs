using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Models;
using QuickShell.Services;
using System.ComponentModel;

namespace QuickShell.Commands;

internal sealed partial class OpenShortcutLaunchCommand : InvokableCommand
{
    private readonly string _shortcutId;
    private readonly string _launchId;
    private readonly QuickShellSettingsManager _settings;
    private readonly bool _runAsAdmin;
    private readonly bool _runAsStandard;

    public OpenShortcutLaunchCommand(
        TerminalShortcut shortcut,
        WorkspaceEntry launch,
        QuickShellSettingsManager settings,
        bool runAsAdmin = false,
        bool runAsStandard = false)
    {
        _shortcutId = shortcut.Id;
        _launchId = launch.Id;
        _settings = settings;
        _runAsAdmin = runAsAdmin;
        _runAsStandard = runAsStandard;
        Id = $"{ShortcutCommandIds.Open(shortcut.Id)}.launch.{launch.Id}";
        var enabledLaunches = ShortcutLaunchNormalization.GetEnabledLaunches(shortcut);
        Name = ShortcutDisplay.GetLaunchContextMenuTitle(launch, enabledLaunches);
        Icon = new IconInfo(
            runAsAdmin || (launch.RunAsAdmin && !runAsStandard)
                ? ShortcutGlyphs.AdminLaunch
                : TerminalLaunchGlyphs.GetForLaunch(launch));
    }

    public override CommandResult Invoke()
    {
        var shortcut = QuickShellRuntimeServices.Shortcuts.GetById(_shortcutId);
        if (shortcut is null)
        {
            return QuickShellNavigation.StayOpen("That workspace was not found.");
        }

        var launch = shortcut.Launches.FirstOrDefault(entry => entry.Id.Equals(_launchId, StringComparison.OrdinalIgnoreCase));
        if (launch is null || !launch.IsEnabled)
        {
            return QuickShellNavigation.StayOpen("That launch entry was not found.");
        }

        if (!ShortcutValidation.DirectoryExists(shortcut.Directory))
        {
            return QuickShellNavigation.StayOpen(
                $"Workspace could not launch: folder not found at {shortcut.Directory}.");
        }

        try
        {
            var launchShortcut = ShortcutLaunchNormalization.ToLaunchShortcut(launch, shortcut);
            TerminalLauncher.Open(
                launchShortcut,
                _settings.TerminalApplicationId,
                _settings.DefaultProfileId,
                _runAsAdmin,
                _runAsStandard);
            QuickShellRuntimeServices.Shortcuts.MarkUsed(shortcut.Id);
            return CommandResult.Dismiss();
        }
        catch (DirectoryNotFoundException)
        {
            return QuickShellNavigation.StayOpen("Failed to open terminal: the folder path could not be found.");
        }
        catch (InvalidOperationException)
        {
            return QuickShellNavigation.StayOpen("Failed to open terminal: check the workspace settings and try again.");
        }
        catch (Win32Exception)
        {
            return QuickShellNavigation.StayOpen("Failed to open terminal: launch was canceled or blocked by the system.");
        }
    }
}
