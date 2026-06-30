using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Models;
using QuickShell.Pages;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class CopyShortcutPathCommand : InvokableCommand
{
    private readonly string _shortcutId;

    public CopyShortcutPathCommand(string shortcutId)
    {
        _shortcutId = shortcutId;
        Name = "Copy path";
        Icon = new IconInfo("\uE8C8");
    }

    public override CommandResult Invoke()
    {
        var shortcut = QuickShellRuntimeServices.Shortcuts.GetById(_shortcutId);
        if (shortcut is null)
        {
            return QuickShellNavigation.StayOpen("That workspace was not found.");
        }

        if (!FolderPathActions.TryCopyPath(shortcut.Directory, out var error))
        {
            return QuickShellNavigation.StayOpen(error);
        }

        return QuickShellNavigation.StayOpen("Path copied to clipboard.");
    }
}

internal sealed partial class OpenShortcutFolderInExplorerCommand : InvokableCommand
{
    private readonly string _shortcutId;

    public OpenShortcutFolderInExplorerCommand(string shortcutId)
    {
        _shortcutId = shortcutId;
        Name = "Open in File Explorer";
        Icon = new IconInfo("\uE838");
    }

    public override CommandResult Invoke()
    {
        var shortcut = QuickShellRuntimeServices.Shortcuts.GetById(_shortcutId);
        if (shortcut is null)
        {
            return QuickShellNavigation.StayOpen("That workspace was not found.");
        }

        if (!FolderPathActions.TryOpenInExplorer(shortcut.Directory, out var error))
        {
            return QuickShellNavigation.StayOpen(error);
        }

        return CommandResult.Dismiss();
    }
}

internal enum WorkspaceLinkKind
{
    DevServer,
    Repo,
}

internal sealed partial class OpenWorkspaceLinkCommand : InvokableCommand
{
    private readonly string _shortcutId;
    private readonly WorkspaceLinkKind _kind;

    public OpenWorkspaceLinkCommand(string shortcutId, WorkspaceLinkKind kind)
    {
        _shortcutId = shortcutId;
        _kind = kind;
        Name = kind switch
        {
            WorkspaceLinkKind.DevServer => "Open dev server",
            WorkspaceLinkKind.Repo => "Open repository",
            _ => "Open link",
        };
        Icon = new IconInfo(kind == WorkspaceLinkKind.Repo ? "\uE737" : "\uE774");
    }

    public override CommandResult Invoke()
    {
        var shortcut = QuickShellRuntimeServices.Shortcuts.GetById(_shortcutId);
        if (shortcut is null)
        {
            return QuickShellNavigation.StayOpen("That workspace was not found.");
        }

        var url = _kind == WorkspaceLinkKind.DevServer ? shortcut.DevServerUrl : shortcut.RepoUrl;
        if (!WorkspaceLinkActions.TryOpenLink(url, out var error))
        {
            return QuickShellNavigation.StayOpen(error);
        }

        return CommandResult.Dismiss();
    }
}

internal sealed partial class OpenCompanionAppCommand : InvokableCommand
{
    private readonly string _shortcutId;

    public OpenCompanionAppCommand(TerminalShortcut shortcut)
    {
        _shortcutId = shortcut.Id;
        Name = $"Open {CompanionAppCatalog.GetDisplayName(shortcut.CompanionAppPath)}";
        Icon = new IconInfo("\uE70F");
    }

    public override CommandResult Invoke()
    {
        var shortcut = QuickShellRuntimeServices.Shortcuts.GetById(_shortcutId);
        if (shortcut is null)
        {
            return QuickShellNavigation.StayOpen("That workspace was not found.");
        }

        if (!CompanionAppLauncher.TryLaunch(shortcut, onDemand: true, out var error))
        {
            return QuickShellNavigation.StayOpen(error ?? "Companion app could not be launched.");
        }

        return CommandResult.Dismiss();
    }
}

internal sealed partial class OpenDiscoverGitReposCommand : InvokableCommand
{
    private readonly Action _onReload;

    public OpenDiscoverGitReposCommand(Action onReload)
    {
        _onReload = onReload;
        Name = "Discover git repos";
        Icon = new IconInfo("\uE8A5");
    }

    public override CommandResult Invoke() =>
        CommandResult.GoToPage(new GoToPageArgs
        {
            PageId = DiscoverGitReposPage.PageId,
        });
}

internal sealed partial class AddGitRepoWorkspaceCommand : InvokableCommand
{
    private readonly GitRepoCandidate _candidate;
    private readonly Action _onReload;

    public AddGitRepoWorkspaceCommand(GitRepoCandidate candidate, Action onReload)
    {
        _candidate = candidate;
        _onReload = onReload;
        Name = "Add";
        Icon = new IconInfo("\uE710");
    }

    public override CommandResult Invoke()
    {
        var existing = QuickShellRuntimeServices.Shortcuts.GetShortcuts()
            .FirstOrDefault(shortcut =>
                string.Equals(shortcut.Directory, _candidate.Directory, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return QuickShellNavigation.StayOpen($"Already saved as '{existing.Name}'.");
        }

        ShortcutCreateNavigationState.SetSeed(WorkspaceSeedFactory.FromGitRepo(_candidate));

        return CommandResult.GoToPage(new GoToPageArgs
        {
            PageId = ShortcutCommandIds.CreateShortcut,
        });
    }
}
