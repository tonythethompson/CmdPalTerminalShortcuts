using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class ReloadShortcutsCommand : InvokableCommand
{
    private readonly Action _onReload;

    public ReloadShortcutsCommand(Action onReload)
    {
        _onReload = onReload;
        Name = "Refresh terminals";
        Icon = new IconInfo("\uE72C");
    }

    public override CommandResult Invoke()
    {
        ShortcutStore.Reload();
        TerminalCatalog.InvalidateCache();
        _onReload();
        return QuickShellNavigation.StayOpen("Refreshed shortcuts and terminals.");
    }
}
