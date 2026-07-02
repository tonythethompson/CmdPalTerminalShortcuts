using Microsoft.CommandPalette.Extensions.Toolkit;

using QuickShell.Models;

using QuickShell.Services;
using System.ComponentModel;

namespace QuickShell.Commands;



internal sealed partial class OpenTerminalShortcutCommand : InvokableCommand

{

    private readonly string _shortcutId;

    private readonly QuickShellSettingsManager _settings;

    private readonly bool _runAsAdmin;

    private readonly bool _runAsStandard;



    public OpenTerminalShortcutCommand(

        TerminalShortcut shortcut,

        QuickShellSettingsManager settings,

        bool runAsAdmin = false,

        bool runAsStandard = false)

    {

        _shortcutId = shortcut.Id;

        _settings = settings;

        _runAsAdmin = runAsAdmin;

        _runAsStandard = runAsStandard;

        Id = runAsAdmin

            ? $"{ShortcutCommandIds.Open(shortcut.Id)}.admin"

            : runAsStandard

                ? $"{ShortcutCommandIds.Open(shortcut.Id)}.standard"

                : ShortcutCommandIds.Open(shortcut.Id);

        Name = runAsAdmin

            ? "Run as Admin"

            : runAsStandard

                ? "Run normally"

                : "Run";

        Icon = new IconInfo(ResolveLaunchIcon(shortcut, runAsAdmin, runAsStandard));

    }

    private static string ResolveLaunchIcon(TerminalShortcut shortcut, bool runAsAdmin, bool runAsStandard)
    {
        if (runAsStandard)
        {
            return ShortcutHealth.NeedsRepair(shortcut)
                ? ShortcutGlyphs.IncidentTriangle
                : TerminalLaunchGlyphs.GetForShortcut(shortcut);
        }

        if (runAsAdmin || shortcut.RunAsAdmin)
        {
            return ShortcutGlyphs.AdminLaunch;
        }

        return ShortcutHealth.GetListGlyph(shortcut);
    }



    public override CommandResult Invoke()

    {

        var shortcut = QuickShellRuntimeServices.Shortcuts.GetById(_shortcutId);

        if (shortcut is null)

        {

            return QuickShellNavigation.StayOpen("That workspace was not found.");

        }



        ShortcutLaunchNormalization.EnsureLaunchesFromLegacy(shortcut);



        if (!ShortcutValidation.DirectoryExists(shortcut.Directory))

        {

            return QuickShellNavigation.StayOpen(

                $"Workspace could not launch: folder not found at {shortcut.Directory}.");

        }



        var enabledLaunches = ShortcutLaunchNormalization.GetEnabledLaunches(shortcut);

        if (enabledLaunches.Count == 0)

        {

            return QuickShellNavigation.StayOpen("Workspace has no enabled launch entries.");

        }



        var companionAttempted = CompanionAppLauncher.ShouldLaunchOnWorkspaceOpen(shortcut);

        string? companionError = null;

        var companionSucceeded = !companionAttempted

            || CompanionAppLauncher.TryLaunch(shortcut, onDemand: false, out companionError);



        if (enabledLaunches.Count == 1)

        {

            return OpenSingleLaunch(shortcut, enabledLaunches[0], companionAttempted, companionSucceeded, companionError);

        }



        return OpenAllLaunches(shortcut, enabledLaunches, companionAttempted, companionSucceeded, companionError);

    }



    private CommandResult OpenSingleLaunch(

        TerminalShortcut shortcut,

        WorkspaceEntry launch,

        bool companionAttempted,

        bool companionSucceeded,

        string? companionError)

    {

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



            if (companionAttempted && !companionSucceeded)

            {

                return QuickShellNavigation.StayOpen(

                    string.IsNullOrWhiteSpace(companionError)

                        ? "Workspace opened, but the companion app could not be launched."

                        : $"Workspace opened, but the companion app failed: {companionError}");

            }



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



    private CommandResult OpenAllLaunches(

        TerminalShortcut shortcut,

        IReadOnlyList<WorkspaceEntry> enabledLaunches,

        bool companionAttempted,

        bool companionSucceeded,

        string? companionError)

    {

        var opened = 0;

        string? lastFailureLabel = null;



        foreach (var launch in enabledLaunches)

        {

            try

            {

                var launchShortcut = ShortcutLaunchNormalization.ToLaunchShortcut(launch, shortcut);

                TerminalLauncher.Open(

                    launchShortcut,

                    _settings.TerminalApplicationId,

                    _settings.DefaultProfileId,

                    launch.RunAsAdmin);

                opened++;

            }

            catch (DirectoryNotFoundException)

            {

                lastFailureLabel = launch.Label;

            }

            catch (InvalidOperationException)

            {

                lastFailureLabel = launch.Label;

            }

            catch (Win32Exception)

            {

                lastFailureLabel = launch.Label;

            }

        }



        if (opened == 0)

        {

            return QuickShellNavigation.StayOpen(

                lastFailureLabel is null

                    ? "Workspace could not launch any terminals."

                    : $"{lastFailureLabel} could not be launched.");

        }



        QuickShellRuntimeServices.Shortcuts.MarkUsed(shortcut.Id);



        if (opened == enabledLaunches.Count && (!companionAttempted || companionSucceeded))

        {

            return CommandResult.Dismiss();

        }



        var message = opened == enabledLaunches.Count

            ? "Workspace launched."

            : $"Workspace partially launched: {opened} of {enabledLaunches.Count} terminals opened.";



        if (companionAttempted && !companionSucceeded)

        {

            message = string.IsNullOrWhiteSpace(companionError)

                ? $"{message} Companion app could not be launched."

                : $"{message} Companion app failed: {companionError}";

        }



        return QuickShellNavigation.StayOpen(message);

    }

}

