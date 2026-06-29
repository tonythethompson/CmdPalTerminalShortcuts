using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Services;

namespace QuickShell.Commands;

internal sealed partial class ExportWorkspacesCommand : InvokableCommand
{
    private readonly bool _stayOnSettings;

    public ExportWorkspacesCommand(bool stayOnSettings = true)
    {
        _stayOnSettings = stayOnSettings;
        Name = "Export workspaces";
        Icon = new IconInfo("\uE896");
    }

    public override CommandResult Invoke()
    {
        var path = ShortcutFilePickerService.PickExportFile();
        if (path is null)
        {
            return Finish("Export cancelled.");
        }

        if (!QuickShellRuntimeServices.Workspaces.TryExportToFile(path, out var error))
        {
            return Finish($"Export failed: {error}");
        }

        return Finish($"Exported workspaces to {path}.");
    }

    private CommandResult Finish(string? message) =>
        _stayOnSettings
            ? QuickShellNavigation.StayOnSettings(message)
            : QuickShellNavigation.StayOpen(message);
}

internal sealed partial class ImportWorkspacesMergeCommand : InvokableCommand
{
    private readonly bool _stayOnSettings;

    public ImportWorkspacesMergeCommand(bool stayOnSettings = true)
    {
        _stayOnSettings = stayOnSettings;
        Name = "Import workspaces (merge)";
        Icon = new IconInfo("\uE8B5");
    }

    public override CommandResult Invoke()
    {
        var path = ShortcutFilePickerService.PickImportFile();
        if (path is null)
        {
            return Finish("Import cancelled.");
        }

        var result = QuickShellRuntimeServices.Workspaces.ImportMerge(path);
        return Finish(result.Success ? result.Message : $"Import failed: {result.Message}");
    }

    private CommandResult Finish(string? message) =>
        _stayOnSettings
            ? QuickShellNavigation.StayOnSettings(message)
            : QuickShellNavigation.StayOpen(message);
}

internal sealed partial class ImportWorkspacesReplaceCommand : InvokableCommand
{
    private readonly bool _stayOnSettings;

    public ImportWorkspacesReplaceCommand(bool stayOnSettings = true)
    {
        _stayOnSettings = stayOnSettings;
        Name = "Import workspaces (replace)";
        Icon = new IconInfo("\uE8B5");
    }

    public override CommandResult Invoke()
    {
        var path = ShortcutFilePickerService.PickImportFile();
        if (path is null)
        {
            return Finish("Import cancelled.");
        }

        var result = QuickShellRuntimeServices.Workspaces.ImportReplace(path);
        return Finish(result.Success ? result.Message : $"Import failed: {result.Message}");
    }

    private CommandResult Finish(string? message) =>
        _stayOnSettings
            ? QuickShellNavigation.StayOnSettings(message)
            : QuickShellNavigation.StayOpen(message);
}
