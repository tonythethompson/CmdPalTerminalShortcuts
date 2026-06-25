using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class MoveFavoriteShortcutCommand : InvokableCommand
{
    private readonly string _name;
    private readonly int _direction;
    private readonly Action _onChanged;

    public MoveFavoriteShortcutCommand(string name, int direction, Action onChanged)
    {
        _name = name;
        _direction = direction;
        _onChanged = onChanged;

        Name = direction < 0 ? "Move favorite up" : "Move favorite down";
        Icon = new IconInfo(direction < 0 ? "\uE70E" : "\uE70D");
    }

    public override CommandResult Invoke()
    {
        var moved = ShortcutStore.MovePinned(_name, _direction);
        if (!moved)
        {
            return QuickShellNavigation.StayOpen("Favorite cannot be moved further.");
        }

        _onChanged();
        return QuickShellNavigation.StayOpen(
            _direction < 0
                ? $"Moved '{_name}' up in favorites."
                : $"Moved '{_name}' down in favorites.");
    }
}
