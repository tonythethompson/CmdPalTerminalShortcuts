using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class ToggleFavoriteWorkspaceCommand : InvokableCommand
{
    private readonly string _name;
    private readonly Action _onChanged;

    public ToggleFavoriteWorkspaceCommand(string name, Action onChanged, bool isFavorite)
    {
        _name = name;
        _onChanged = onChanged;
        Id = WorkspaceCommandIds.FavoriteToggle(name);
        Name = isFavorite ? "Unfavorite" : "Favorite";
        Icon = new IconInfo(isFavorite ? ShortcutGlyphs.FavoriteFilled : ShortcutGlyphs.FavoriteOutline);
    }

    public override CommandResult Invoke()
    {
        var favorited = QuickShellRuntimeServices.Workspaces.TogglePinned(_name);
        _onChanged();
        return QuickShellNavigation.StayOpen(
            favorited ? $"Favorited '{_name}'." : $"Removed '{_name}' from favorites.");
    }
}
