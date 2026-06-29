using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using QuickShell.Commands;
using QuickShell.Models;
using QuickShell.Services;

namespace QuickShell.Services;

internal static class WorkspaceListItems
{
    public const string WorkspaceIcon = "\uE8A7";

    public static ListItem CreateOpen(
        Workspace workspace,
        QuickShellSettingsManager settings,
        Action? onChanged = null,
        CreateShortcutCommand? createShortcutCommand = null)
    {
        var item = new ListItem(new OpenWorkspaceCommand(workspace, settings))
        {
            Title = workspace.Name,
            Subtitle = BuildRowSubtitle(workspace),
            Icon = new IconInfo(WorkspaceIcon),
        };

        if (workspace.IsPinned)
        {
            item.Tags =
            [
                new Tag
                {
                    Text = "Favorite",
                    Icon = new IconInfo(ShortcutGlyphs.FavoriteFilled),
                },
            ];
        }

        if (onChanged is not null)
        {
            item.MoreCommands = WorkspaceContextCommands.Build(
                workspace,
                onChanged,
                settings,
                createShortcutCommand);
        }

        return item;
    }

    public static ListItem CreateSearchResult(
        Workspace workspace,
        QuickShellSettingsManager settings,
        Action onChanged)
    {
        var item = CreateOpen(workspace, settings, onChanged);
        item.Subtitle = WorkspaceDisplay.BuildSearchSubtitle(workspace);
        return item;
    }

    private static string BuildRowSubtitle(Workspace workspace)
    {
        var entries = WorkspaceDisplay.BuildListSubtitle(workspace);
        var folder = WorkspaceHealth.BuildListFolderHint(workspace);
        return $"{entries} · {folder}";
    }
}
