using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class OpenTerminalShortcutCommand : InvokableCommand
{
    private readonly string _shortcutName;
    private readonly QuickShellSettingsManager _settings;
    private readonly bool _runAsAdmin;

    public OpenTerminalShortcutCommand(TerminalShortcut shortcut, QuickShellSettingsManager settings, bool runAsAdmin = false)
    {
        _shortcutName = shortcut.Name;
        _settings = settings;
        _runAsAdmin = runAsAdmin;
        Name = runAsAdmin ? "Open as administrator" : shortcut.Name;
        Icon = new IconInfo(runAsAdmin || shortcut.RunAsAdmin ? "\uE946" : "\uE756");
    }

    public override CommandResult Invoke()
    {
        var shortcut = ShortcutStore.GetByName(_shortcutName);
        if (shortcut is null)
        {
            return QuickShellNavigation.StayOpen($"Shortcut '{_shortcutName}' was not found.");
        }

        try
        {
            TerminalLauncher.Open(shortcut, _settings.DefaultLaunchTargetId, _runAsAdmin);
            ShortcutStore.MarkUsed(_shortcutName);
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return QuickShellNavigation.StayOpen($"Failed to open terminal: {ex.Message}");
        }
    }
}
