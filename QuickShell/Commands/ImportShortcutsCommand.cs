using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Pages;
using QuickShell.Services;
using System.Threading;

namespace QuickShell.Commands;

internal sealed partial class ImportShortcutsCommand : InvokableCommand
{
    private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(30);

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

        using var readCancellation = new CancellationTokenSource(IoTimeout);
        var readResult = QuickShellRuntimeServices.Shortcuts.TryReadImportFileAsync(path, readCancellation.Token).GetAwaiter().GetResult();
        if (!readResult.Success)
        {
            return QuickShellNavigation.StayOpen(readResult.Error);
        }

        var imported = readResult.Shortcuts;
        var conflicts = QuickShellRuntimeServices.Shortcuts.CountImportNameConflicts(imported);
        if (conflicts > 0)
        {
            ImportConflictState.Set(path, conflicts, imported.Length, _onReload);
            _onReload();
            return CommandResult.GoToPage(new GoToPageArgs
            {
                PageId = ImportConflictPage.PageId,
            });
        }

        using var mergeCancellation = new CancellationTokenSource(IoTimeout);
        var result = QuickShellRuntimeServices.Shortcuts.ImportMergeAsync(path, mergeCancellation.Token).GetAwaiter().GetResult();
        if (!result.Success)
        {
            return QuickShellNavigation.StayOpen(result.Message);
        }

        _onReload();
        return QuickShellNavigation.StayOpen(result.Message);
    }
}
