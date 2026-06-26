using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class DuplicateShortcutCommand : InvokableCommand
{
    private readonly string _sourceName;
    private readonly Action _onChanged;

    public DuplicateShortcutCommand(string sourceName, Action onChanged)
    {
        _sourceName = sourceName;
        _onChanged = onChanged;
        Name = "Duplicate";
        Icon = new IconInfo("\uE8C8");
    }

    public override CommandResult Invoke()
    {
        var duplicate = QuickShellRuntimeServices.Shortcuts.BuildDuplicate(_sourceName);
        if (duplicate is null)
        {
            return QuickShellNavigation.StayOpen($"Shortcut '{_sourceName}' was not found.");
        }

        QuickShellRuntimeServices.Shortcuts.Upsert(duplicate);
        _onChanged();
        return QuickShellNavigation.StayOpen($"Duplicated as '{duplicate.Name}'.");
    }
}
