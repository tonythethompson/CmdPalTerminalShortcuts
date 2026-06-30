using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class MoveShortcutLaunchCommand : InvokableCommand
{
    private readonly TerminalShortcut _shortcut;
    private readonly string _launchId;
    private readonly int _direction;
    private readonly Action<TerminalShortcut> _onChanged;

    public MoveShortcutLaunchCommand(
        TerminalShortcut shortcut,
        string launchId,
        int direction,
        Action<TerminalShortcut> onChanged)
    {
        _shortcut = shortcut;
        _launchId = launchId;
        _direction = direction;
        _onChanged = onChanged;
        Name = direction < 0 ? "Move up" : "Move down";
        Icon = new IconInfo(direction < 0 ? "\uE70E" : "\uE70D");
    }

    public override CommandResult Invoke()
    {
        ShortcutEditorState.MoveLaunch(_shortcut, _launchId, _direction);
        _onChanged(_shortcut);
        return QuickShellNavigation.StayOpen();
    }
}

internal sealed partial class RemoveShortcutLaunchCommand : InvokableCommand
{
    private readonly TerminalShortcut _shortcut;
    private readonly string _launchId;
    private readonly Action<TerminalShortcut> _onChanged;

    public RemoveShortcutLaunchCommand(
        TerminalShortcut shortcut,
        string launchId,
        Action<TerminalShortcut> onChanged)
    {
        _shortcut = shortcut;
        _launchId = launchId;
        _onChanged = onChanged;
        Name = "Remove";
        Icon = new IconInfo("\uE74D");
    }

    public override CommandResult Invoke()
    {
        ShortcutLaunchNormalization.EnsureLaunchesFromLegacy(_shortcut);
        _shortcut.Launches.RemoveAll(entry => entry.Id.Equals(_launchId, StringComparison.OrdinalIgnoreCase));
        for (var i = 0; i < _shortcut.Launches.Count; i++)
        {
            _shortcut.Launches[i].Order = i;
        }

        _onChanged(_shortcut);
        return QuickShellNavigation.StayOpen();
    }
}

internal sealed partial class SaveShortcutEditorCommand : InvokableCommand
{
    private readonly TerminalShortcut _shortcut;
    private readonly string? _originalName;
    private readonly Action _onSaved;

    public SaveShortcutEditorCommand(TerminalShortcut shortcut, string? originalName, Action onSaved)
    {
        _shortcut = shortcut;
        _originalName = originalName;
        _onSaved = onSaved;
        Name = "Save workspace";
        Icon = new IconInfo("\uE74E");
    }

    public override CommandResult Invoke()
    {
        ShortcutLaunchNormalization.EnsureLaunchesFromLegacy(_shortcut);
        var launchInputs = _shortcut.Launches
            .OrderBy(entry => entry.Order)
            .Select(entry => new ShortcutFormLaunchInput
            {
                Id = entry.Id,
                Label = entry.Label,
                Command = entry.Command ?? string.Empty,
                LaunchTarget = TerminalCatalog.EncodeLaunchTargetId(new TerminalShortcut
                {
                    Terminal = entry.Terminal,
                    WtProfile = entry.WtProfile,
                }),
                RunAsAdmin = entry.RunAsAdmin,
                IsEnabled = entry.IsEnabled,
            })
            .ToList();

        var result = ShortcutFormSave.TrySave(
            _originalName,
            _shortcut.Name,
            _shortcut.Abbreviation ?? string.Empty,
            _shortcut.Directory,
            launchInputs,
            QuickShellRuntimeServices.Shortcuts,
            _onSaved);

        if (!result.Success)
        {
            return QuickShellNavigation.StayOpen(result.Message);
        }

        QuickShellRuntimeServices.Drafts.Clear();
        return QuickShellNavigation.ReturnHome(result.Message);
    }
}

internal sealed partial class EditShortcutCommand : InvokableCommand
{
    private readonly TerminalShortcut _shortcut;
    private readonly Action _onChanged;

    public EditShortcutCommand(TerminalShortcut shortcut, Action onChanged)
    {
        _shortcut = shortcut;
        _onChanged = onChanged;
        Name = "Edit";
        Icon = new IconInfo("\uE70F");
    }

    public override CommandResult Invoke()
    {
        ShortcutEditorNavigationState.SetEditor(
            ShortcutEditorState.CloneShortcut(_shortcut),
            _shortcut.Name,
            _onChanged);
        return CommandResult.GoToPage(new GoToPageArgs
        {
            PageId = Pages.ShortcutEditorPage.PageId,
        });
    }
}
