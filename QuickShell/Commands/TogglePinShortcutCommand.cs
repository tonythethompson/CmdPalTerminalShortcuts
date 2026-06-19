using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class TogglePinShortcutCommand : InvokableCommand
{
    private readonly string _name;
    private readonly Action _onChanged;

    public TogglePinShortcutCommand(string name, Action onChanged, bool isPinned)
    {
        _name = name;
        _onChanged = onChanged;
        Name = isPinned ? "Unpin" : "Pin to top";
        Icon = new IconInfo(isPinned ? "\uE718" : "\uE735");
    }

    public override CommandResult Invoke()
    {
        var pinned = ShortcutStore.TogglePinned(_name);
        _onChanged();
        return QuickShellNavigation.StayOpen(pinned ? $"Pinned '{_name}'." : $"Unpinned '{_name}'.");
    }
}
