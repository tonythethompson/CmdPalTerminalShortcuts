using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Pages;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class ImportShortcutsCommand : InvokableCommand
{
    private readonly Action _onReload;

    public ImportShortcutsCommand(Action onReload)
    {
        _onReload = onReload;
        Name = "Import shortcuts";
        Icon = new IconInfo("\uE898");
    }

    public override CommandResult Invoke()
    {
        var path = ShortcutFilePickerService.PickImportFile();
        if (path is null)
        {
            return QuickShellNavigation.StayOpen("Import cancelled.");
        }

        if (!ShortcutStore.TryReadImportFile(path, out var imported, out var error))
        {
            return QuickShellNavigation.StayOpen(error);
        }

        var conflicts = ShortcutStore.CountImportNameConflicts(imported);
        if (conflicts > 0)
        {
            ImportConflictState.Set(path, conflicts, imported.Length, _onReload);
            _onReload();
            return CommandResult.GoToPage(new GoToPageArgs
            {
                PageId = ImportConflictPage.PageId,
            });
        }

        var result = ShortcutStore.ImportMerge(path);
        if (!result.Success)
        {
            return QuickShellNavigation.StayOpen(result.Message);
        }

        _onReload();
        return QuickShellNavigation.StayOpen(result.Message);
    }
}
